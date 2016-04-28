using System.Drawing;
using System.IO;
using ASCompletion.Completion;
using PluginCore;
using PluginCore.Controls;
using PluginCore.Helpers;
using PluginCore.Managers;
using PluginCore.Utilities;
using PostfixCodeCompletion.Completion;
using PostfixCodeCompletion.Helpers;
using ProjectManager;
using ScintillaNet;
using TemplateUtils = PostfixCodeCompletion.Helpers.TemplateUtils;

namespace PostfixCodeCompletion
{
    public class PluginMain : IPlugin
    {
        string settingFilename;

        #region Required Properties

        public int Api => 1;

        public string Name => "PostfixCodeCompletion";

        public string Guid => "21d9ab3e-93e4-4460-9298-c62f87eed7ba";

        public string Help => string.Empty;

        public string Author => "SlavaRa";

        public string Description => "Postfix code completion helps reduce backward caret jumps as you write code";

        public object Settings { get; private set; }

        #endregion

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
            Complete.CompletionModeHandler?.Stop();
            SaveSettings();
        }

        /// <summary>
        /// Handles the incoming events
        /// </summary>
        public void HandleEvent(object sender, NotifyEvent e, HandlingPriority priority)
        {
            switch (e.Type)
            {
                case EventType.Command:
                    if (((DataEvent) e).Action == ProjectManagerEvents.Project) Complete.Restart();
                    break;
                case EventType.Keys:
                    e.Handled = Complete.OnShortcut(((KeyEvent) e).Value);
                    break;
            }
        }

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
            EventManager.AddEventHandler(this, EventType.Command);
            EventManager.AddEventHandler(this, EventType.Keys, HandlingPriority.High);
            UITools.Manager.OnCharAdded += OnCharAdded;
        }

        /// <summary>
        /// Saves the plugin settings
        /// </summary>
        void SaveSettings() => ObjectSerializer.Serialize(settingFilename, Settings);

        static void OnCharAdded(ScintillaControl sender, int value) => Complete.OnCharAdded(value);
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