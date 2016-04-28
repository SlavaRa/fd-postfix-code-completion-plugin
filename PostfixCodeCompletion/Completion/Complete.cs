using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using ASCompletion.Completion;
using ASCompletion.Context;
using ASCompletion.Model;
using HaXeContext;
using PluginCore;
using PluginCore.Controls;
using PluginCore.Managers;
using PluginCore.Utilities;
using PostfixCodeCompletion.Helpers;
using ProjectManager.Projects.Haxe;
using TemplateUtils = PostfixCodeCompletion.Helpers.TemplateUtils;

namespace PostfixCodeCompletion.Completion
{
    internal class Complete
    {
        static IHaxeCompletionHandler completionModeHandler;
        static int completionListItemCount;

        public static void Start()
        {
            var project = PluginBase.CurrentProject;
            if (project == null) return;
            var completionList = Reflector.CompletionList.CompletionList;
            completionList.VisibleChanged -= OnCompletionListVisibleChanged;
            completionList.VisibleChanged += OnCompletionListVisibleChanged;
            if (!(project is HaxeProject)) return;
            var context = (Context) ASContext.GetLanguageContext("haxe");
            if (context == null) return;
            var settings = (HaXeSettings) context.Settings;
            settings.CompletionModeChanged -= OnHaxeCompletionModeChanged;
            settings.CompletionModeChanged += OnHaxeCompletionModeChanged;
            OnHaxeCompletionModeChanged();
        }

        public static void Stop()
        {
            completionModeHandler?.Stop();
            completionModeHandler = null;
            var completionList = Reflector.CompletionList.CompletionList;
            completionList.VisibleChanged -= OnCompletionListVisibleChanged;
            completionList.SelectedValueChanged -= OnCompletionListSelectedValueChanged;
        }

        internal static bool OnShortcut(Keys keys)
        {
            if (keys != (Keys.Control | Keys.Space)) return false;
            var completionList = Reflector.CompletionList.CompletionList;
            if (CompletionList.Active) return false;
            var expr = CompletionHelper.GetCurrentCompletionExpr();
            if (expr == null || expr.IsNull()) return false;
            var result = ASComplete.OnShortcut(keys, PluginBase.MainForm.CurrentDocument.SciControl);
            completionList.VisibleChanged -= OnCompletionListVisibleChanged;
            UpdateCompletionList(expr);
            completionList.VisibleChanged += OnCompletionListVisibleChanged;
            return result;
        }

        internal static void OnCharAdded(int value)
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

        internal static void UpdateCompletionList() => UpdateCompletionList(CompletionHelper.GetCurrentCompletionExpr());

        internal static void UpdateCompletionList(ASResult expr)
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

        internal static void UpdateCompletionList(MemberModel target, ASResult expr)
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

        static void OnCompletionListVisibleChanged(object o, EventArgs args)
        {
            var list = Reflector.CompletionList.CompletionList;
            if (list.Visible) UpdateCompletionList();
            else list.SelectedValueChanged -= OnCompletionListSelectedValueChanged;
        }

        static void OnCompletionListSelectedValueChanged(object sender, EventArgs args)
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
            var settings = (HaXeSettings)((Context)ASContext.GetLanguageContext("haxe")).Settings;
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

        static void OnFunctionTypeResult(HaxeComplete hc, HaxeCompleteResult result, HaxeCompleteStatus status)
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
    }
}