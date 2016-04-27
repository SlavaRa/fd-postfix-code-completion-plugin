using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using ASCompletion.Completion;
using ASCompletion.Context;
using ASCompletion.Model;
using HaXeContext;
using PluginCore;
using PluginCore.Controls;
using PluginCore.Helpers;
using PluginCore.Managers;
using PluginCore.Utilities;
using PostfixCodeCompletion.Completion;
using PostfixCodeCompletion.Helpers;
using ProjectManager;
using ProjectManager.Projects.Haxe;
using ScintillaNet;
using TemplateUtils = PostfixCodeCompletion.Helpers.TemplateUtils;

namespace PostfixCodeCompletion
{
    public class PluginMain : IPlugin
    {
        string settingFilename;
        static IHaxeCompletionHandler completionModeHandler;
        static int completionListItemCount;

        #region Required Properties

        public int Api => 1;

        public string Name => "PostfixCodeCompletion";

        public string Guid => "21d9ab3e-93e4-4460-9298-c62f87eed7ba";

        public string Help => string.Empty;

        public string Author => "SlavaRa";

        public string Description => "Postfix code completion helps reduce backward caret jumps as you write code";

        public object Settings { get; private set; }

        #endregion

        #region Required Methods

        /// <summary>
        /// Initializes the plugin
        /// </summary>
        public void Initialize()
        {
            InitBasics();
            LoadSettings();
            TemplateUtils.Settings = (Settings)Settings;
            CompletionHelper.Settings = (Settings)Settings;
            AddEventHandlers();
        }

        /// <summary>
        /// Disposes the plugin
        /// </summary>
        public void Dispose()
        {
            completionModeHandler?.Stop();
            SaveSettings();
        }

        /// <summary>
        /// Handles the incoming events
        /// </summary>
        public void HandleEvent(object sender, NotifyEvent e, HandlingPriority priority)
        {
            var completionList = Reflector.CompletionList.CompletionList;
            switch (e.Type)
            {
                case EventType.UIStarted:
                    completionList.VisibleChanged -= OnCompletionListVisibleChanged;
                    completionList.VisibleChanged += OnCompletionListVisibleChanged;
                    break;
                case EventType.Command:
                    if (((DataEvent) e).Action == ProjectManagerEvents.Project)
                    {
                        if (!(PluginBase.CurrentProject is HaxeProject)) return;
                        var context = (Context) ASContext.GetLanguageContext("haxe");
                        if (context == null) return;
                        var settings = (HaXeSettings) context.Settings;
                        settings.CompletionModeChanged -= OnHaxeCompletionModeChanged;
                        settings.CompletionModeChanged += OnHaxeCompletionModeChanged;
                        OnHaxeCompletionModeChanged();
                    }
                    break;
                case EventType.Keys:
                    var keys = ((KeyEvent) e).Value;
                    if (keys == (Keys.Control | Keys.Space))
                    {
                        if (CompletionList.Active) return;
                        var expr = CompletionHelper.GetCurrentCompletionExpr();
                        if (expr == null || expr.IsNull()) return;
                        e.Handled = ASComplete.OnShortcut(keys, PluginBase.MainForm.CurrentDocument.SciControl);
                        completionList.VisibleChanged -= OnCompletionListVisibleChanged;
                        UpdateCompletionList(expr);
                        completionList.VisibleChanged += OnCompletionListVisibleChanged;
                    }
                    break;
            }
        }

        #endregion

        #region Custom Methods

        /// <summary>
        /// Initializes important variables
        /// </summary>
        void InitBasics()
        {
            var path = Path.Combine(PathHelper.DataDir, Name);
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            settingFilename = Path.Combine(path, "Settings.fdb");
        }

        /// <summary>
        /// Loads the plugin settings
        /// </summary>
        void LoadSettings()
        {
            Settings = new Settings();
            if (!File.Exists(settingFilename)) SaveSettings();
            else Settings = (Settings) ObjectSerializer.Deserialize(settingFilename, Settings);
        }

        /// <summary>
        /// Adds the required event handlers
        /// </summary>
        void AddEventHandlers()
        {
            EventManager.AddEventHandler(this, EventType.UIStarted | EventType.Command);
            EventManager.AddEventHandler(this, EventType.Keys, HandlingPriority.High);
            UITools.Manager.OnCharAdded += OnCharAdded;
        }

        /// <summary>
        /// Saves the plugin settings
        /// </summary>
        void SaveSettings() => ObjectSerializer.Serialize(settingFilename, Settings);

        void UpdateCompletionList() => UpdateCompletionList(CompletionHelper.GetCurrentCompletionExpr());

        void UpdateCompletionList(ASResult expr)
        {
            if (expr == null || expr.IsNull()) return;
            var target = CompletionHelper.GetCompletionTarget(expr);
            if (target != null)
            {
                UpdateCompletionList(target, expr);
                return;
            }
            if (expr.Context == null || completionModeHandler == null) return;
            var sci = PluginBase.MainForm.CurrentDocument.SciControl;
            if (sci.ConfigurationLanguage.ToLower() != "haxe" || sci.CharAt(expr.Context.Position) != '.') return;
            var hc = new HaxeComplete(sci, expr, false, completionModeHandler, HaxeCompilerService.Type);
            hc.GetPositionType(OnFunctionTypeResult);
        }

        void UpdateCompletionList(MemberModel target, ASResult expr)
        {
            if (target == null || !TemplateUtils.GetHasTemplates()) return;
            var items = CompletionHelper.GetCompletionItems(target, expr);
            var allItems = Reflector.CompletionList.AllItems;
            if (allItems != null)
            {
                var labels = new HashSet<string>();
                foreach (var item in allItems)
                {
                    if (item is PostfixCompletionItem) labels.Add(item.Label);
                }
                foreach (var item in items)
                {
                    if (!labels.Contains(item.Label)) allItems.Add(item);
                }
                items = allItems;
            }
            var sci = PluginBase.MainForm.CurrentDocument.SciControl;
            var word = sci.GetWordLeft(sci.CurrentPos - 1, false);
            if (!string.IsNullOrEmpty(word))
            {
                items = items.FindAll(it =>
                {
                    var score = CompletionList.SmartMatch(it.Label, word, word.Length);
                    return score > 0 && score < 6;
                });
            }
            CompletionList.Show(items, false, word);
            var list = Reflector.CompletionList.CompletionList;
            completionListItemCount = list.Items.Count;
            list.SelectedValueChanged -= OnCompletionListSelectedValueChanged;
            list.SelectedValueChanged += OnCompletionListSelectedValueChanged;
        }

        static Process CreateHaxeProcess(string args)
        {
            var process = Path.Combine(PluginBase.CurrentProject.CurrentSDK, "haxe.exe");
            if (!File.Exists(process)) return null;
            var result = new Process
            {
                StartInfo =
                {
                    FileName = process,
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                },
                EnableRaisingEvents = true
            };
            return result;
        }

        #endregion

        #region Event Handlers

        void OnCharAdded(ScintillaControl sender, int value)
        {
            try
            {
                if ((char) value != '.' || !TemplateUtils.GetHasTemplates()) return;
                var sci = PluginBase.MainForm.CurrentDocument.SciControl;
                if (sci.PositionIsOnComment(sci.CurrentPos)) return;
                if (ASComplete.OnChar(sci, value, false))
                {
                    if (Reflector.CompletionList.CompletionList.Visible) UpdateCompletionList();
                    return;
                }
                if (!Reflector.ASComplete.HandleDotCompletion(sci, true) || CompletionList.Active) return;
                var expr = CompletionHelper.GetCurrentCompletionExpr();
                if (expr == null || expr.IsNull()) return;
                Reflector.CompletionList.CompletionList.VisibleChanged -= OnCompletionListVisibleChanged;
                UpdateCompletionList(expr);
                Reflector.CompletionList.CompletionList.VisibleChanged += OnCompletionListVisibleChanged;
            }
            catch (Exception e)
            {
                ErrorManager.ShowError(e);
            }
        }

        void OnCompletionListVisibleChanged(object o, EventArgs args)
        {
            var list = Reflector.CompletionList.CompletionList;
            if (list.Visible) UpdateCompletionList();
            else list.SelectedValueChanged -= OnCompletionListSelectedValueChanged;
        }

        void OnCompletionListSelectedValueChanged(object sender, EventArgs args)
        {
            var list = Reflector.CompletionList.CompletionList;
            list.SelectedValueChanged -= OnCompletionListSelectedValueChanged;
            if (completionListItemCount != list.Items.Count) UpdateCompletionList();
        }

        static void OnHaxeCompletionModeChanged()
        {
            if (completionModeHandler != null)
            {
                completionModeHandler.Stop();
                completionModeHandler = null;
            }
            if (!(PluginBase.CurrentProject is HaxeProject)) return;
            var settings = (HaXeSettings)((Context) ASContext.GetLanguageContext("haxe")).Settings;
            var sdk = settings.InstalledSDKs.FirstOrDefault(it => it.Path == PluginBase.CurrentProject.CurrentSDK);
            if (sdk == null || new SemVer(sdk.Version).IsOlderThan(new SemVer("3.2.0"))) return;
            switch (settings.CompletionMode)
            {
                case HaxeCompletionModeEnum.CompletionServer:
                    if (settings.CompletionServerPort < 1024) completionModeHandler = new CompilerCompletionHandler(CreateHaxeProcess(string.Empty));
                    else
                    {
                        completionModeHandler = new CompletionServerCompletionHandler(
                            CreateHaxeProcess($"--wait {settings.CompletionServerPort}"),
                            settings.CompletionServerPort
                        );
                        ((CompletionServerCompletionHandler)completionModeHandler).FallbackNeeded += OnHaxeContextFallbackNeeded;
                    }
                    break;
                default:
                    completionModeHandler = new CompilerCompletionHandler(CreateHaxeProcess(string.Empty));
                    break;
            }
        }

        static void OnHaxeContextFallbackNeeded(bool notSupported)
        {
            TraceManager.AddAsync("PCC: This SDK does not support server mode");
            completionModeHandler?.Stop();
            completionModeHandler = new CompilerCompletionHandler(CreateHaxeProcess(string.Empty));
        }

        void OnFunctionTypeResult(HaxeComplete hc, HaxeCompleteResult result, HaxeCompleteStatus status)
        {
            switch (status)
            {
                case HaxeCompleteStatus.Error:
                    TraceManager.AddAsync(hc.Errors, -3);
                    if (hc.AutoHide) CompletionList.Hide();
                    break;
                case HaxeCompleteStatus.Type:
                    var list = Reflector.CompletionList.CompletionList;
                    list.VisibleChanged -= OnCompletionListVisibleChanged;
                    var expr = hc.Expr;
                    if (result.Type is ClassModel)
                    {
                        expr.Type = (ClassModel)result.Type;
                        expr.Member = null;
                        UpdateCompletionList(expr.Type, expr);
                    }
                    else
                    {
                        expr.Type = ASContext.Context.ResolveType(result.Type.Type, result.Type.InFile);
                        expr.Member = result.Type;
                        UpdateCompletionList(expr.Member, expr);
                    }
                    list.VisibleChanged += OnCompletionListVisibleChanged;
                    break;
            }
        }

        #endregion
    }

    internal class PostfixCompletionItem : ICompletionListItem
    {
        readonly string template;
        readonly ASResult expr;

        public PostfixCompletionItem(string label, string template, ASResult expr)
        {
            Label = label;
            this.template = template;
            this.expr = expr;
        }

        public string Label { get; }

        string pattern;
        public virtual string Pattern
        {
            get { return pattern ?? TemplateUtils.PatternMember; }
            set { pattern = value; }
        }

        public string Value
        {
            get
            {
                TemplateUtils.InsertSnippetText(expr, template, Pattern);
                return null;
            }
        }

        Bitmap icon;
        public Bitmap Icon
        {
            get { return icon ?? (icon = (Bitmap) PluginBase.MainForm.FindImage("341")); }
            set { icon = value; }
        }

        string description;
        public string Description => description ?? (description = TemplateUtils.GetDescription(expr, template, Pattern));

        public new string ToString() => Description;

        /// <summary>
        /// Determines whether the specified <see cref="T:System.Object"/> is equal to the current <see cref="T:System.Object"/>.
        /// </summary>
        /// <returns>
        /// true if the specified <see cref="T:System.Object"/> is equal to the current <see cref="T:System.Object"/>; otherwise, false.
        /// </returns>
        /// <param name="obj">The <see cref="T:System.Object"/> to compare with the current <see cref="T:System.Object"/>. </param><filterpriority>2</filterpriority>
        public override bool Equals(object obj)
        {
            if (!(obj is PostfixCompletionItem)) return false;
            var other = (PostfixCompletionItem)obj;
            return other.Label == Label && other.expr == expr;
        }

        /// <summary>
        /// Serves as a hash function for a particular type. 
        /// </summary>
        /// <returns>
        /// A hash code for the current <see cref="T:System.Object"/>.
        /// </returns>
        /// <filterpriority>2</filterpriority>
        public override int GetHashCode() => Label.GetHashCode() ^ expr.GetHashCode();
    }
}