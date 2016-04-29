using System.Collections.Generic;
using System.Reflection;
using AS3Context;
using ASCompletion;
using ASCompletion.Completion;
using ASCompletion.Context;
using ASCompletion.Model;
using ASCompletion.Settings;
using FlashDevelop;
using FlashDevelop.Managers;
using NSubstitute;
using NUnit.Framework;
using PluginCore;
using PluginCore.Helpers;
using PostfixCodeCompletion.TestUtils;
using ScintillaNet;
using ScintillaNet.Enums;

namespace PostfixCodeCompletion.Completion
{
    [TestFixture]
    internal class PostfixGeneratorTests
    {
        private MainForm mainForm;
        private ISettings settings;
        private ITabbedDocument doc;

        [TestFixtureSetUp]
        public void FixtureSetUp()
        {
            mainForm = new MainForm();
            settings = Substitute.For<ISettings>();
            settings.UseTabs = true;
            settings.IndentSize = 4;
            settings.SmartIndentType = SmartIndent.CPP;
            settings.TabIndents = true;
            settings.TabWidth = 4;
            doc = Substitute.For<ITabbedDocument>();
            mainForm.Settings = settings;
            mainForm.CurrentDocument = doc;
            mainForm.StandaloneMode = false;
            PluginBase.Initialize(mainForm);
            ScintillaManager.LoadConfiguration();
        }

        [TestFixtureTearDown]
        public void FixtureTearDown()
        {
            settings = null;
            doc = null;
            mainForm.Dispose();
            mainForm = null;
        }

        private ScintillaControl GetBaseScintillaControl()
        {
            return new ScintillaControl
            {
                Encoding = System.Text.Encoding.UTF8,
                CodePage = 65001,
                Indent = settings.IndentSize,
                Lexer = 3,
                StyleBits = 7,
                IsTabIndents = settings.TabIndents,
                IsUseTabs = settings.UseTabs,
                TabWidth = settings.TabWidth
            };
        }

        [TestFixture]
        public class GeneratorJob : PostfixGeneratorTests
        {
            protected ScintillaControl Sci;

            [TestFixtureSetUp]
            public void GenerateJobSetup()
            {
                var pluginMain = Substitute.For<ASCompletion.PluginMain>();
                var pluginUI = new PluginUI(pluginMain);
                pluginMain.MenuItems.Returns(new List<System.Windows.Forms.ToolStripItem>());
                pluginMain.Settings.Returns(new GeneralSettings());
                pluginMain.Panel.Returns(pluginUI);
                #region ASContext.GlobalInit(pluginMain);
                var method = typeof(ASContext).GetMethod("GlobalInit", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static);
                method.Invoke(null, new[] {pluginMain});
                #endregion
                ASContext.Context = Substitute.For<IASContext>();
                Sci = GetBaseScintillaControl();
                doc.SciControl.Returns(Sci);
            }

            [TestFixture]
            public class GenerateConstTests : GeneratorJob
            {
                [TestFixtureSetUp]
                public void GenerateTestsSetup()
                {
                    Helpers.TemplateUtils.Settings = new Settings();
                }

                public IEnumerable<TestCaseData> AS3TestCases
                {
                    get
                    {
                        yield return
                            new TestCaseData(
                                TestFile.ReadAllText(
                                    "PostfixCodeCompletion.Test_Files.generated.as3.BeforeGenerateConst_fromString.as"),
                                new ClassModel {InFile = new FileModel(), Name = "String", Type = "String"},
                                TestFile.ReadAllText(
                                    "PostfixCodeCompletion.Test_Snippets.as3.postfixgenerator.const.fds"))
                                .Returns(
                                    TestFile.ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.AfterGenerateConst_fromString.as"))
                                .SetName("Generate const from \"\".|");
                        yield return
                            new TestCaseData(
                                TestFile.ReadAllText(
                                    "PostfixCodeCompletion.Test_Files.generated.as3.BeforeGenerateConst_fromUInt.as"),
                                new ClassModel {InFile = new FileModel(), Name = "Number", Type = "Number"},
                                TestFile.ReadAllText(
                                    "PostfixCodeCompletion.Test_Snippets.as3.postfixgenerator.const.fds"))
                                .Returns(
                                    TestFile.ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.AfterGenerateConst_fromUInt.as"))
                                .SetName("Generate const from 1.|");
                        yield return
                            new TestCaseData(
                                TestFile.ReadAllText(
                                    "PostfixCodeCompletion.Test_Files.generated.as3.BeforeGenerateConst_fromNumber.as"),
                                new ClassModel { InFile = new FileModel(), Name = "Number", Type = "Number" },
                                TestFile.ReadAllText(
                                    "PostfixCodeCompletion.Test_Snippets.as3.postfixgenerator.const.fds"))
                                .Returns(
                                    TestFile.ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.AfterGenerateConst_fromNumber.as"))
                                .SetName("Generate const from 10.0.|");
                        yield return
                            new TestCaseData(
                                TestFile.ReadAllText(
                                    "PostfixCodeCompletion.Test_Files.generated.as3.BeforeGenerateConst_fromInt.as"),
                                new ClassModel { InFile = new FileModel(), Name = "Number", Type = "Number" },
                                TestFile.ReadAllText(
                                    "PostfixCodeCompletion.Test_Snippets.as3.postfixgenerator.const.fds"))
                                .Returns(
                                    TestFile.ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.AfterGenerateConst_fromInt.as"))
                                .SetName("Generate const from -1.|");
                        yield return
                            new TestCaseData(
                                TestFile.ReadAllText(
                                    "PostfixCodeCompletion.Test_Files.generated.as3.BeforeGenerateConst_fromArray.as"),
                                new ClassModel { InFile = new FileModel(), Name = "Array", Type = "Array" },
                                TestFile.ReadAllText(
                                    "PostfixCodeCompletion.Test_Snippets.as3.postfixgenerator.const.fds"))
                                .Returns(
                                    TestFile.ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.AfterGenerateConst_fromArray.as"))
                                .SetName("Generate const from [].|");
                        yield return
                            new TestCaseData(
                                TestFile.ReadAllText(
                                    "PostfixCodeCompletion.Test_Files.generated.as3.BeforeGenerateConst_fromObject.as"),
                                new ClassModel { InFile = new FileModel(), Name = "Object", Type = "Object" },
                                TestFile.ReadAllText(
                                    "PostfixCodeCompletion.Test_Snippets.as3.postfixgenerator.const.fds"))
                                .Returns(
                                    TestFile.ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.AfterGenerateConst_fromObject.as"))
                                .SetName("Generate const from {}.|");
                        yield return
                            new TestCaseData(
                                TestFile.ReadAllText(
                                    "PostfixCodeCompletion.Test_Files.generated.as3.BeforeGenerateConst_fromNewObject.as"),
                                new ClassModel {InFile = new FileModel(), Name = "Object", Type = "Object"},
                                TestFile.ReadAllText(
                                    "PostfixCodeCompletion.Test_Snippets.as3.postfixgenerator.const.fds"))
                                .Returns(
                                    TestFile.ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.AfterGenerateConst_fromNewObject.as"))
                                .SetName("Generate const from new Object().|");
                        yield return
                            new TestCaseData(
                                TestFile.ReadAllText(
                                    "PostfixCodeCompletion.Test_Files.generated.as3.BeforeGenerateConst_fromNewVectorInt.as"),
                                new ClassModel { InFile = new FileModel(), Name = "Vector.<int>", Type = "Vector.<int>" },
                                TestFile.ReadAllText(
                                    "PostfixCodeCompletion.Test_Snippets.as3.postfixgenerator.const.fds"))
                                .Returns(
                                    TestFile.ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.AfterGenerateConst_fromNewVectorInt.as"))
                                .SetName("Generate const from new Vector.<int>().|");
                        yield return
                            new TestCaseData(
                                TestFile.ReadAllText(
                                    "PostfixCodeCompletion.Test_Files.generated.as3.BeforeGenerateConst_fromNewVectorInt_short.as"),
                                new ClassModel { InFile = new FileModel(), Name = "Vector.<int>", Type = "Vector.<int>" },
                                TestFile.ReadAllText(
                                    "PostfixCodeCompletion.Test_Snippets.as3.postfixgenerator.const.fds"))
                                .Returns(
                                    TestFile.ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.AfterGenerateConst_fromNewVectorInt_short.as"))
                                .SetName("Generate const from new <int>[].|");
                    }
                }

                [Test, TestCaseSource("AS3TestCases")]
                public string AS3(string sourceText, ClassModel type, string template)
                {
                    Sci.Text = sourceText;
                    Sci.ConfigurationLanguage = "as3";
                    SnippetHelper.PostProcessSnippets(Sci, 0);
                    var context = new AS3Context.Context(new AS3Settings());
                    ASContext.Context.Features.Returns(context.Features);
                    ASContext.Context.IsFileValid.Returns(true);
                    var currentModel = new FileModel {Context = ASContext.Context};
                    new ASFileParser().ParseSrc(currentModel, Sci.Text);
                    var currentClass = currentModel.Classes[0];
                    ASContext.Context.CurrentClass.Returns(currentClass);
                    ASContext.Context.CurrentModel.Returns(currentModel);
                    ASContext.Context.CurrentMember.Returns(currentClass.Members[0]);
                    ASContext.Context.GetVisibleExternalElements().Returns(x => context.GetVisibleExternalElements());
                    ASContext.Context.GetCodeModel(null).ReturnsForAnyArgs(x =>
                    {
                        var src = x[0] as string;
                        return string.IsNullOrEmpty(src) ? null : context.GetCodeModel(src);
                    });
                    ASContext.Context.ResolveType(null, null).ReturnsForAnyArgs(type);
                    ASContext.Context
                        .When(x => x.ResolveTopLevelElement(Arg.Any<string>(), Arg.Any<ASResult>()))
                        .Do(x =>
                        {
                            var result = x.ArgAt<ASResult>(1);
                            result.Type = type;
                        });
                    var expr = Helpers.CompleteHelper.GetCurrentExpressionType();
                    Helpers.TemplateUtils.InsertSnippetText(expr, template, Helpers.TemplateUtils.PatternMember);
                    return Sci.Text;
                }
            }
        }
    }
}