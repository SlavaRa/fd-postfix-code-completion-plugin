using System.Collections.Generic;
using System.Reflection;
using AS3Context;
using ASCompletion;
using ASCompletion.Completion;
using ASCompletion.Context;
using ASCompletion.Model;
using ASCompletion.Settings;
using FlashDevelop;
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
        MainForm mainForm;
        ISettings settings;
        ITabbedDocument doc;

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
            FlashDevelop.Managers.ScintillaManager.LoadConfiguration();
        }

        [TestFixtureTearDown]
        public void FixtureTearDown()
        {
            settings = null;
            doc = null;
            mainForm.Dispose();
            mainForm = null;
        }

        ScintillaControl GetBaseScintillaControl()
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
                Helpers.TemplateUtils.Settings = new Settings();
            }

            protected string Generate(string sourceText, ClassModel type, string template, string pccpattern)
            {
                Sci.Text = sourceText;
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
                ASContext.Context.GetVisibleExternalElements().Returns(_ => context.GetVisibleExternalElements());
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
                var tmp = Helpers.TemplateUtils.GetTemplate(template, new[] {type.Type, pccpattern});
                if (!string.IsNullOrEmpty(tmp)) template = tmp;
                template = template.Replace("$(ItmUniqueVar)", ASComplete.FindFreeIterator(ASContext.Context, ASContext.Context.CurrentClass, new ASResult().Context));
                Helpers.TemplateUtils.InsertSnippetText(expr, template, pccpattern);
                return Sci.Text;
            }

            [TestFixture]
            public class GenerateConstTests : GeneratorJob
            {
                public IEnumerable<TestCaseData> AS3TestCases
                {
                    get
                    {
                        yield return
                            new TestCaseData(
                                    TestFile.ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.BeforeGenerate_fromString.as"),
                                    new ClassModel {InFile = new FileModel(), Name = "String", Type = "String"},
                                    TestFile.ReadAllText(
                                        "PostfixCodeCompletion.Test_Snippets.as3.postfixgenerator.const.fds"),
                                    Helpers.TemplateUtils.PatternMember)
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
                                        "PostfixCodeCompletion.Test_Snippets.as3.postfixgenerator.const.fds"),
                                    Helpers.TemplateUtils.PatternMember)
                                .Returns(
                                    TestFile.ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.AfterGenerateConst_fromUInt.as"))
                                .SetName("Generate const from 1.|");
                        yield return
                            new TestCaseData(
                                    TestFile.ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.BeforeGenerateConst_fromNumber.as"),
                                    new ClassModel {InFile = new FileModel(), Name = "Number", Type = "Number"},
                                    TestFile.ReadAllText(
                                        "PostfixCodeCompletion.Test_Snippets.as3.postfixgenerator.const.fds"),
                                    Helpers.TemplateUtils.PatternMember)
                                .Returns(
                                    TestFile.ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.AfterGenerateConst_fromNumber.as"))
                                .SetName("Generate const from 10.0.|");
                        /*yield return
                            new TestCaseData(
                                    TestFile.ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.BeforeGenerateConst_fromInt.as"),
                                    new ClassModel {InFile = new FileModel(), Name = "Number", Type = "Number"},
                                    TestFile.ReadAllText(
                                        "PostfixCodeCompletion.Test_Snippets.as3.postfixgenerator.const.fds"),
                                    Helpers.TemplateUtils.PatternMember)
                                .Returns(
                                    TestFile.ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.AfterGenerateConst_fromInt.as"))
                                .SetName("Generate const from -1.|")
                                .Ignore();*/
                        yield return
                            new TestCaseData(
                                    TestFile.ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.BeforeGenerateConst_fromArray.as"),
                                    new ClassModel {InFile = new FileModel(), Name = "Array", Type = "Array"},
                                    TestFile.ReadAllText(
                                        "PostfixCodeCompletion.Test_Snippets.as3.postfixgenerator.const.fds"),
                                    Helpers.TemplateUtils.PatternMember)
                                .Returns(
                                    TestFile.ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.AfterGenerateConst_fromArray.as"))
                                .SetName("Generate const from [].|");
                        yield return
                            new TestCaseData(
                                    TestFile.ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.BeforeGenerateConst_fromObject.as"),
                                    new ClassModel {InFile = new FileModel(), Name = "Object", Type = "Object"},
                                    TestFile.ReadAllText(
                                        "PostfixCodeCompletion.Test_Snippets.as3.postfixgenerator.const.fds"),
                                    Helpers.TemplateUtils.PatternMember)
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
                                        "PostfixCodeCompletion.Test_Snippets.as3.postfixgenerator.const.fds"),
                                    Helpers.TemplateUtils.PatternMember)
                                .Returns(
                                    TestFile.ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.AfterGenerateConst_fromNewObject.as"))
                                .SetName("Generate const from new Object().|");
                        yield return
                            new TestCaseData(
                                    TestFile.ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.BeforeGenerateConst_fromNewVectorInt.as"),
                                    new ClassModel
                                    {
                                        InFile = new FileModel(),
                                        Name = "Vector.<int>",
                                        Type = "Vector.<int>"
                                    },
                                    TestFile.ReadAllText(
                                        "PostfixCodeCompletion.Test_Snippets.as3.postfixgenerator.const.fds"),
                                    Helpers.TemplateUtils.PatternMember)
                                .Returns(
                                    TestFile.ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.AfterGenerateConst_fromNewVectorInt.as"))
                                .SetName("Generate const from new Vector.<int>().|");
                        yield return
                            new TestCaseData(
                                    TestFile.ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.BeforeGenerateConst_fromNewVectorInt_short.as"),
                                    new ClassModel
                                    {
                                        InFile = new FileModel(),
                                        Name = "Vector.<int>",
                                        Type = "Vector.<int>"
                                    },
                                    TestFile.ReadAllText(
                                        "PostfixCodeCompletion.Test_Snippets.as3.postfixgenerator.const.fds"),
                                    Helpers.TemplateUtils.PatternMember)
                                .Returns(
                                    TestFile.ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.AfterGenerateConst_fromNewVectorInt_short.as"))
                                .SetName("Generate const from new <int>[].|");
                    }
                }

                [Test, TestCaseSource("AS3TestCases")]
                public string AS3(string sourceText, ClassModel type, string template, string pccpattern)
                {
                    Sci.ConfigurationLanguage = "as3";
                    return Generate(sourceText, type, template, pccpattern);
                }
            }

            [TestFixture]
            public class GenerateVarTests : GeneratorJob
            {
                public IEnumerable<TestCaseData> AS3TestCases
                {
                    get
                    {
                        yield return
                            new TestCaseData(
                                    TestFile.ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.BeforeGenerate_fromString.as"),
                                    new ClassModel {InFile = new FileModel(), Name = "String", Type = "String"},
                                    TestFile.ReadAllText(
                                        "PostfixCodeCompletion.Test_Snippets.as3.postfixgenerator.var.fds"),
                                    Helpers.TemplateUtils.PatternMember)
                                .Returns(
                                    TestFile.ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.AfterGenerateVar_fromString.as"))
                                .SetName("Generate var from \"\".|");
                        yield return
                            new TestCaseData(
                                    TestFile.ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.BeforeGenerateVar_fromUInt.as"),
                                    new ClassModel {InFile = new FileModel(), Name = "Number", Type = "Number"},
                                    TestFile.ReadAllText(
                                        "PostfixCodeCompletion.Test_Snippets.as3.postfixgenerator.var.fds"),
                                    Helpers.TemplateUtils.PatternMember)
                                .Returns(
                                    TestFile.ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.AfterGenerateVar_fromUInt.as"))
                                .SetName("Generate var from 1.|");
                        yield return
                            new TestCaseData(
                                    TestFile.ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.BeforeGenerateVar_fromNumber.as"),
                                    new ClassModel {InFile = new FileModel(), Name = "Number", Type = "Number"},
                                    TestFile.ReadAllText(
                                        "PostfixCodeCompletion.Test_Snippets.as3.postfixgenerator.var.fds"),
                                    Helpers.TemplateUtils.PatternMember)
                                .Returns(
                                    TestFile.ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.AfterGenerateVar_fromNumber.as"))
                                .SetName("Generate var from 10.0.|");
                        /*yield return
                            new TestCaseData(
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.BeforeGenerateVar_fromInt.as"),
                                    new ClassModel {InFile = new FileModel(), Name = "Number", Type = "Number"},
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Snippets.as3.postfixgenerator.var.fds"),
                                    Helpers.TemplateUtils.PatternMember)
                                .Returns(
                                    ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.AfterGenerateVar_fromInt.as"))
                                .SetName("Generate var from -1.|")
                                .Ignore();*/
                        yield return
                            new TestCaseData(
                                    TestFile.ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.BeforeGenerateVar_fromArray.as"),
                                    new ClassModel {InFile = new FileModel(), Name = "Array", Type = "Array"},
                                    TestFile.ReadAllText(
                                        "PostfixCodeCompletion.Test_Snippets.as3.postfixgenerator.var.fds"),
                                    Helpers.TemplateUtils.PatternMember)
                                .Returns(
                                    TestFile.ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.AfterGenerateVar_fromArray.as"))
                                .SetName("Generate var from [].|");
                        yield return
                            new TestCaseData(
                                    TestFile.ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.BeforeGenerateVar_fromObject.as"),
                                    new ClassModel {InFile = new FileModel(), Name = "Object", Type = "Object"},
                                    TestFile.ReadAllText(
                                        "PostfixCodeCompletion.Test_Snippets.as3.postfixgenerator.var.fds"),
                                    Helpers.TemplateUtils.PatternMember)
                                .Returns(
                                    TestFile.ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.AfterGenerateVar_fromObject.as"))
                                .SetName("Generate var from {}.|");
                        yield return
                            new TestCaseData(
                                    TestFile.ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.BeforeGenerateVar_fromNewObject.as"),
                                    new ClassModel {InFile = new FileModel(), Name = "Object", Type = "Object"},
                                    TestFile.ReadAllText(
                                        "PostfixCodeCompletion.Test_Snippets.as3.postfixgenerator.var.fds"),
                                    Helpers.TemplateUtils.PatternMember)
                                .Returns(
                                    TestFile.ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.AfterGenerateVar_fromNewObject.as"))
                                .SetName("Generate var from new Object().|");
                        yield return
                            new TestCaseData(
                                    TestFile.ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.BeforeGenerateVar_fromNewVectorInt.as"),
                                    new ClassModel
                                    {
                                        InFile = new FileModel(),
                                        Name = "Vector.<int>",
                                        Type = "Vector.<int>"
                                    },
                                    TestFile.ReadAllText(
                                        "PostfixCodeCompletion.Test_Snippets.as3.postfixgenerator.var.fds"),
                                    Helpers.TemplateUtils.PatternMember)
                                .Returns(
                                    TestFile.ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.AfterGenerateVar_fromNewVectorInt.as"))
                                .SetName("Generate var from new Vector.<int>().|");
                        yield return
                            new TestCaseData(
                                    TestFile.ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.BeforeGenerateVar_fromNewVectorInt_short.as"),
                                    new ClassModel
                                    {
                                        InFile = new FileModel(),
                                        Name = "Vector.<int>",
                                        Type = "Vector.<int>"
                                    },
                                    TestFile.ReadAllText(
                                        "PostfixCodeCompletion.Test_Snippets.as3.postfixgenerator.var.fds"),
                                    Helpers.TemplateUtils.PatternMember)
                                .Returns(
                                    TestFile.ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.AfterGenerateVar_fromNewVectorInt_short.as"))
                                .SetName("Generate var from new <int>[].|");
                    }
                }

                [Test, TestCaseSource("AS3TestCases")]
                public string AS3(string sourceText, ClassModel type, string template, string pccpattern)
                {
                    Sci.ConfigurationLanguage = "as3";
                    return Generate(sourceText, type, template, pccpattern);
                }
            }

            [TestFixture]
            public class GenerateConstructorTests : GeneratorJob
            {
                public IEnumerable<TestCaseData> AS3TestCases
                {
                    get
                    {
                        yield return
                            new TestCaseData(
                                    TestFile.ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.BeforeGenerate_fromString.as"),
                                    new ClassModel {InFile = new FileModel(), Name = "String", Type = "String"},
                                    TestFile.ReadAllText(
                                        "PostfixCodeCompletion.Test_Snippets.as3.postfixgenerator.constructor.fds"),
                                    Helpers.TemplateUtils.PatternMember)
                                .Returns(
                                    TestFile.ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.AfterGenerateConstructor_fromString.as"))
                                .SetName("Generate constructor from \"\".|");
                    }
                }

                [Test, TestCaseSource("AS3TestCases")]
                public string AS3(string sourceText, ClassModel type, string template, string pccpattern)
                {
                    Sci.ConfigurationLanguage = "as3";
                    return Generate(sourceText, type, template, pccpattern);
                }
            }

            [TestFixture]
            public class GenerateNotNullTests : GeneratorJob
            {
                public IEnumerable<TestCaseData> AS3TestCases
                {
                    get
                    {
                        yield return
                            new TestCaseData(
                                    TestFile.ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.BeforeGenerate_fromString.as"),
                                    new ClassModel {InFile = new FileModel(), Name = "String", Type = "String"},
                                    TestFile.ReadAllText(
                                        "PostfixCodeCompletion.Test_Snippets.as3.postfixgenerator.notnull.fds"),
                                    Helpers.TemplateUtils.PatternNullable)
                                .Returns(
                                    TestFile.ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.AfterGenerateNotNull_fromString.as"))
                                .SetName("Generate notnull from \"\".|");
                    }
                }

                [Test, TestCaseSource("AS3TestCases")]
                public string AS3(string sourceText, ClassModel type, string template, string pccpattern)
                {
                    Sci.ConfigurationLanguage = "as3";
                    return Generate(sourceText, type, template, pccpattern);
                }
            }

            [TestFixture]
            public class GenerateNullTests : GeneratorJob
            {
                public IEnumerable<TestCaseData> AS3TestCases
                {
                    get
                    {
                        yield return
                            new TestCaseData(
                                    TestFile.ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.BeforeGenerate_fromString.as"),
                                    new ClassModel {InFile = new FileModel(), Name = "String", Type = "String"},
                                    TestFile.ReadAllText(
                                        "PostfixCodeCompletion.Test_Snippets.as3.postfixgenerator.null.fds"),
                                    Helpers.TemplateUtils.PatternNullable)
                                .Returns(
                                    TestFile.ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.AfterGenerateNull_fromString.as"))
                                .SetName("Generate null from \"\".|");
                    }
                }

                [Test, TestCaseSource("AS3TestCases")]
                public string AS3(string sourceText, ClassModel type, string template, string pccpattern)
                {
                    Sci.ConfigurationLanguage = "as3";
                    return Generate(sourceText, type, template, pccpattern);
                }
            }

            [TestFixture]
            public class GenerateParTests : GeneratorJob
            {
                public IEnumerable<TestCaseData> AS3TestCases
                {
                    get
                    {
                        yield return
                            new TestCaseData(
                                    TestFile.ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.BeforeGenerate_fromString.as"),
                                    new ClassModel {InFile = new FileModel(), Name = "String", Type = "String"},
                                    TestFile.ReadAllText(
                                        "PostfixCodeCompletion.Test_Snippets.as3.postfixgenerator.par.fds"),
                                    Helpers.TemplateUtils.PatternMember)
                                .Returns(
                                    TestFile.ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.AfterGeneratePar_fromString.as"))
                                .SetName("Generate par from \"\".|");
                    }
                }

                [Test, TestCaseSource("AS3TestCases")]
                public string AS3(string sourceText, ClassModel type, string template, string pccpattern)
                {
                    Sci.ConfigurationLanguage = "as3";
                    return Generate(sourceText, type, template, pccpattern);
                }
            }

            [Ignore]
            [TestFixture]
            public class GenerateSelTests : GeneratorJob
            {
                public IEnumerable<TestCaseData> AS3TestCases
                {
                    get
                    {
                        yield return
                            new TestCaseData(
                                    TestFile.ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.BeforeGenerate_fromString.as"),
                                    new ClassModel {InFile = new FileModel(), Name = "String", Type = "String"},
                                    TestFile.ReadAllText(
                                        "PostfixCodeCompletion.Test_Snippets.as3.postfixgenerator.sel.fds"),
                                    Helpers.TemplateUtils.PatternMember)
                                .Returns(
                                    TestFile.ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.AfterGenerateSel_fromString.as"))
                                .SetName("Generate sel from \"\".|");
                    }
                }

                [Test, TestCaseSource("AS3TestCases")]
                public string AS3(string sourceText, ClassModel type, string template, string pccpattern)
                {
                    Sci.ConfigurationLanguage = "as3";
                    Generate(sourceText, type, template, pccpattern);
                    return Sci.SelText;
                }
            }

            [TestFixture]
            public class GenerateReturnTests : GeneratorJob
            {
                public IEnumerable<TestCaseData> AS3TestCases
                {
                    get
                    {
                        yield return
                            new TestCaseData(
                                    TestFile.ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.BeforeGenerate_fromString.as"),
                                    new ClassModel {InFile = new FileModel(), Name = "String", Type = "String"},
                                    TestFile.ReadAllText(
                                        "PostfixCodeCompletion.Test_Snippets.as3.postfixgenerator.return.fds"),
                                    Helpers.TemplateUtils.PatternMember)
                                .Returns(
                                    TestFile.ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.AfterGenerateReturn_fromString.as"))
                                .SetName("Generate return from \"\".|");
                    }
                }

                [Test, TestCaseSource("AS3TestCases")]
                public string AS3(string sourceText, ClassModel type, string template, string pccpattern)
                {
                    Sci.ConfigurationLanguage = "as3";
                    return Generate(sourceText, type, template, pccpattern);
                }
            }

            [TestFixture]
            public class GenerateIfTests : GeneratorJob
            {
                public IEnumerable<TestCaseData> AS3TestCases
                {
                    get
                    {
                        yield return
                            new TestCaseData(
                                    TestFile.ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.BeforeGenerate_fromBoolean.as"),
                                    new ClassModel {InFile = new FileModel(), Name = "Boolean", Type = "Boolean"},
                                    TestFile.ReadAllText(
                                        "PostfixCodeCompletion.Test_Snippets.as3.postfixgenerator.if.fds"),
                                    Helpers.TemplateUtils.PatternBool)
                                .Returns(
                                    TestFile.ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.AfterGenerateIf_fromBoolean.as"))
                                .SetName("Generate if from true.|");
                    }
                }

                [Test, TestCaseSource("AS3TestCases")]
                public string AS3(string sourceText, ClassModel type, string template, string pccpattern)
                {
                    Sci.ConfigurationLanguage = "as3";
                    return Generate(sourceText, type, template, pccpattern);
                }
            }

            [TestFixture]
            public class GenerateForTests : GeneratorJob
            {
                public IEnumerable<TestCaseData> AS3TestCases
                {
                    get
                    {
                        yield return
                            new TestCaseData(
                                    TestFile.ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.BeforeGenerate_fromArray.as"),
                                    new ClassModel {InFile = new FileModel(), Name = "Array", Type = "Array"},
                                    TestFile.ReadAllText(
                                        "PostfixCodeCompletion.Test_Snippets.as3.postfixgenerator.for.fds"),
                                    Helpers.TemplateUtils.PatternCollection)
                                .Returns(
                                    TestFile.ReadAllText(
                                        "PostfixCodeCompletion.Test_Files.generated.as3.AfterGenerateFor_fromArray.as"))
                                .SetName("Generate for (var i = 0; i < array.length; i++) from array.|");
                    }
                }

                [Test, TestCaseSource("AS3TestCases")]
                public string AS3(string sourceText, ClassModel type, string template, string pccpattern)
                {
                    Sci.ConfigurationLanguage = "as3";
                    return Generate(sourceText, type, template, pccpattern);
                }
            }
        }
    }
}