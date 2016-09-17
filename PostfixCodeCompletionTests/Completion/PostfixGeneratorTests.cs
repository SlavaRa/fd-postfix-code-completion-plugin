using System.Collections.Generic;
using System.Reflection;
using AS3Context;
using ASCompletion;
using ASCompletion.Completion;
using ASCompletion.Context;
using ASCompletion.Model;
using ASCompletion.Settings;
using NSubstitute;
using NUnit.Framework;
using PluginCore.Helpers;
using PostfixCodeCompletion.TestUtils;

namespace PostfixCodeCompletion.Completion
{
    [TestFixture]
    internal class PostfixGeneratorTests : TestBase
    {
        [TestFixture]
        public class GeneratorJob : PostfixGeneratorTests
        {
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
                CurrentDocument.SciControl.Returns(Sci);
                Helpers.TemplateUtils.Settings = new Settings();
            }

            static string ConvertWinNewlineToUnix(string s) => s.Replace("\r\n", "\n");

            static string ReadCode(string fileName) => ConvertWinNewlineToUnix(TestFile.ReadAllText($"PostfixCodeCompletion.Test_Files.generated.as3.{fileName}.as"));
            static string ReadSnippet(string fileName) => ConvertWinNewlineToUnix(TestFile.ReadAllText($"PostfixCodeCompletion.Test_Snippets.as3.postfixgenerator.{fileName}.fds"));

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
                template = Helpers.TemplateUtils.ProcessTemplate(pccpattern, template, expr);
                Helpers.TemplateUtils.InsertSnippetText(expr, template, pccpattern);
                return ConvertWinNewlineToUnix(Sci.Text);
            }

            [TestFixture]
            public class AS3GeneratorTests : GeneratorJob
            {
                [TestFixtureSetUp]
                public void AS3GeneratorTestsSetup() => Sci.ConfigurationLanguage = "as3";

                public TestCaseData GetTestCaseFromArray(string patternPath) => new TestCaseData(
                    ReadCode("BeforeGenerate_fromArray"),
                    new ClassModel {InFile = new FileModel(), Name = "Array", Type = "Array"},
                    ReadSnippet(patternPath),
                    Helpers.TemplateUtils.PatternCollection);

                public TestCaseData GetTestCasefromArrayInitializer(string patternPath) => GetTestCasefromArrayInitializer(patternPath, Helpers.TemplateUtils.PatternCollection);
                public TestCaseData GetTestCasefromArrayInitializer(string patternPath, string pccpattern) => new TestCaseData(
                    ReadCode("BeforeGenerate_fromArrayInitializer"),
                    new ClassModel {InFile = new FileModel(), Name = "Array", Type = "Array"},
                    ReadSnippet(patternPath),
                    pccpattern);

                public TestCaseData GetTestCasefromMultilineArrayInitializer(string patternPath) => GetTestCasefromMultilineArrayInitializer(patternPath, Helpers.TemplateUtils.PatternCollection);
                public TestCaseData GetTestCasefromMultilineArrayInitializer(string patternPath, string pccpattern) => new TestCaseData(
                    ReadCode("BeforeGenerate_fromMultilineArrayInitializer"),
                    new ClassModel {InFile = new FileModel(), Name = "Array", Type = "Array"},
                    ReadSnippet(patternPath),
                    pccpattern);

                public TestCaseData GetTestCaseFromBoolean(string patternPath) => new TestCaseData(
                    ReadCode("BeforeGenerate_fromBoolean"),
                    new ClassModel {InFile = new FileModel(), Name = "Boolean", Type = "Boolean"},
                    ReadSnippet(patternPath),
                    Helpers.TemplateUtils.PatternBool);

                public TestCaseData GetTestCaseFromDictionary(string patternPath) => new TestCaseData(
                    ReadCode("BeforeGenerate_fromDictionary"),
                    new ClassModel {InFile = new FileModel(), Name = "Dictionary", Type = "flash.utils.Dictionary"},
                    ReadSnippet(patternPath),
                    Helpers.TemplateUtils.PatternHash);

                public TestCaseData GetTestCaseFromUInt(string patternPath) => GetTestCaseFromUInt(patternPath, Helpers.TemplateUtils.PatternHash);
                public TestCaseData GetTestCaseFromUInt(string patternPath, string pccpattern) => new TestCaseData(
                    ReadCode("BeforeGenerate_fromUInt"),
                    new ClassModel {InFile = new FileModel(), Name = "Number", Type = "Number"},
                    ReadSnippet(patternPath),
                    pccpattern);

                public TestCaseData GetTestCaseFromNumber(string patternPath) => GetTestCaseFromNumber(patternPath, Helpers.TemplateUtils.PatternHash);
                public TestCaseData GetTestCaseFromNumber(string patternPath, string pccpattern) => new TestCaseData(
                    ReadCode("BeforeGenerate_fromNumber"),
                    new ClassModel {InFile = new FileModel(), Name = "Number", Type = "Number"},
                    ReadSnippet(patternPath),
                    pccpattern);

                public TestCaseData GetTestCaseFromObject(string patternPath) => new TestCaseData(
                    ReadCode("BeforeGenerate_fromObject"),
                    new ClassModel {InFile = new FileModel(), Name = "Object", Type = "Object"},
                    ReadSnippet(patternPath),
                    Helpers.TemplateUtils.PatternHash);

                public TestCaseData GetTestCaseFromObjectInitializer(string patternPath) => GetTestCaseFromObjectInitializer(patternPath, Helpers.TemplateUtils.PatternHash);
                public TestCaseData GetTestCaseFromObjectInitializer(string patternPath, string pccpattern) => new TestCaseData(
                    ReadCode("BeforeGenerate_fromObjectInitializer"),
                    new ClassModel {InFile = new FileModel(), Name = "Object", Type = "Object"},
                    ReadSnippet(patternPath),
                    pccpattern);

                public TestCaseData GetTestCaseFromString(string patternPath) => GetTestCaseFromString(patternPath, Helpers.TemplateUtils.PatternString);
                public TestCaseData GetTestCaseFromString(string patternPath, string pccpattern) => new TestCaseData(
                    ReadCode("BeforeGenerate_fromString"),
                    new ClassModel {InFile = new FileModel(), Name = "String", Type = "String"},
                    ReadSnippet(patternPath),
                    pccpattern);

                public IEnumerable<TestCaseData> Constructor
                {
                    get
                    {
                        yield return
                            GetTestCaseFromString("constructor", Helpers.TemplateUtils.PatternMember)
                                .Returns(ReadCode("AfterGenerateConstructor_fromString"))
                                .SetName("constructor from \"\".|");
                    }
                }

                public IEnumerable<TestCaseData> If
                {
                    get
                    {
                        yield return
                            GetTestCaseFromBoolean("if")
                                .Returns(ReadCode("AfterGenerateIf_fromBoolean"))
                                .SetName("if from true.|");
                    }
                }

                public IEnumerable<TestCaseData> Else
                {
                    get
                    {
                        yield return
                            GetTestCaseFromBoolean("else")
                                .Returns(ReadCode("AfterGenerateElse_fromBoolean"))
                                .SetName("else from true.|");
                    }
                }

                public IEnumerable<TestCaseData> Null
                {
                    get
                    {
                        yield return
                            GetTestCaseFromString("null", Helpers.TemplateUtils.PatternNullable)
                                .Returns(ReadCode("AfterGenerateNull_fromString"))
                                .SetName("null from \"\".|");
                    }
                }

                public IEnumerable<TestCaseData> Notnull
                {
                    get
                    {
                        yield return
                            GetTestCaseFromString("notnull", Helpers.TemplateUtils.PatternNullable)
                                .Returns(ReadCode("AfterGenerateNotNull_fromString"))
                                .SetName("notnull from \"\".|");
                    }
                }

                public IEnumerable<TestCaseData> Not
                {
                    get
                    {
                        yield return
                            GetTestCaseFromBoolean("not")
                                .Returns(ReadCode("AfterGenerateNot_fromBoolean"))
                                .SetName("not from true.|");
                    }
                }

                public IEnumerable<TestCaseData> Foreach
                {
                    get
                    {
                        yield return
                            GetTestCaseFromArray("foreach")
                                .Returns(ReadCode("AfterGenerateForeach_fromArray"))
                                .SetName("foreach from array.|");
                        yield return
                            GetTestCasefromArrayInitializer("foreach")
                                .Returns(ReadCode("AfterGenerateForeach_fromArrayInitializer"))
                                .SetName("foreach from [].|");
                        yield return
                            GetTestCaseFromObject("foreach")
                                .Returns(ReadCode("AfterGenerateForeach_fromObject"))
                                .SetName("foreach from object.|");
                        yield return
                            GetTestCaseFromObjectInitializer("foreach")
                                .Returns(ReadCode("AfterGenerateForeach_fromObjectInitializer"))
                                .SetName("foreach from {}.|");
                        yield return
                            GetTestCaseFromDictionary("foreach")
                                .Returns(ReadCode("AfterGenerateForeach_fromDictionary"))
                                .SetName("foreach from dictionary.|");
                    }
                }

                public IEnumerable<TestCaseData> Forin
                {
                    get
                    {
                        yield return
                            GetTestCaseFromObject("forin")
                                .Returns(ReadCode("AfterGenerateForin_fromObject"))
                                .SetName("forin from object.|");
                        yield return
                            GetTestCaseFromObjectInitializer("forin")
                                .Returns(ReadCode("AfterGenerateForin_fromObjectInitializer"))
                                .SetName("forin from {}.|");
                        yield return
                            GetTestCaseFromDictionary("forin")
                                .Returns(ReadCode("AfterGenerateForin_fromDictionary"))
                                .SetName("forin from dictionary.|");
                    }
                }

                public IEnumerable<TestCaseData> For
                {
                    get
                    {
                        yield return
                            GetTestCaseFromArray("for")
                                .Returns(ReadCode("AfterGenerateFor_fromArray"))
                                .SetName("for from array.|");
                        yield return
                            GetTestCasefromArrayInitializer("for")
                                .Returns(ReadCode("AfterGenerateFor_fromArrayInitializer"))
                                .SetName("for from [].|");
                        yield return
                            GetTestCaseFromNumber("for", Helpers.TemplateUtils.PatternNumber)
                                .Returns(ReadCode("AfterGenerateFor_fromNumber"))
                                .SetName("for from 10.0.|");
                    }
                }

                public IEnumerable<TestCaseData> Forr
                {
                    get
                    {
                        yield return
                            GetTestCaseFromArray("forr")
                                .Returns(ReadCode("AfterGenerateForr_fromArray"))
                                .SetName("forr from array.|");
                        yield return
                            GetTestCasefromArrayInitializer("forr")
                                .Returns(ReadCode("AfterGenerateForr_fromArrayInitializer"))
                                .SetName("forr from [].|");
                        yield return
                            GetTestCaseFromNumber("forr", Helpers.TemplateUtils.PatternNumber)
                                .Returns(ReadCode("AfterGenerateForr_fromNumber"))
                                .SetName("forr from 10.0.|");
                    }
                }

                public IEnumerable<TestCaseData> Var
                {
                    get
                    {
                        yield return
                            GetTestCaseFromString("var", Helpers.TemplateUtils.PatternMember)
                                .Returns(ReadCode("AfterGenerateVar_fromString"))
                                .SetName("var from \"\".|");
                        yield return
                            GetTestCaseFromUInt("var", Helpers.TemplateUtils.PatternMember)
                                .Returns(ReadCode("AfterGenerateVar_fromUInt"))
                                .SetName("var from 1.|");
                        yield return
                            GetTestCaseFromNumber("var", Helpers.TemplateUtils.PatternMember)
                                .Returns(ReadCode("AfterGenerateVar_fromNumber"))
                                .SetName("var from 10.0.|");
                        /*yield return
                            new TestCaseData(
                                    ReadCode("BeforeGenerateVar_fromInt"),
                                    new ClassModel {InFile = new FileModel(), Name = "Number", Type = "Number"},
                                    ReadSnippet("var"),
                                    Helpers.TemplateUtils.PatternMember)
                                .Returns(
                                    ReadCode(
                                        "AfterGenerateVar_fromInt"))
                                .SetName("var from -1.|")
                                .Ignore();*/
                        yield return
                            GetTestCasefromArrayInitializer("var", Helpers.TemplateUtils.PatternMember)
                                .Returns(ReadCode("AfterGenerateVar_fromArray"))
                                .SetName("var from [].|");
                        yield return
                            GetTestCaseFromObjectInitializer("var", Helpers.TemplateUtils.PatternMember)
                                .Returns(ReadCode("AfterGenerateVar_fromObject"))
                                .SetName("var from {}.|");
                        yield return
                            new TestCaseData(
                                    ReadCode("BeforeGenerateVar_fromNewObject"),
                                    new ClassModel { InFile = new FileModel(), Name = "Object", Type = "Object" },
                                    ReadSnippet("var"),
                                    Helpers.TemplateUtils.PatternMember)
                                .Returns(ReadCode("AfterGenerateVar_fromNewObject"))
                                .SetName("var from new Object().|");
                        yield return
                            new TestCaseData(
                                ReadCode("BeforeGenerateVar_fromNewVectorInt"),
                                    new ClassModel
                                    {
                                        InFile = new FileModel(),
                                        Name = "Vector.<int>",
                                        Type = "Vector.<int>"
                                    },
                                    ReadSnippet("var"),
                                    Helpers.TemplateUtils.PatternMember)
                                .Returns(ReadCode("AfterGenerateVar_fromNewVectorInt"))
                                .SetName("var from new Vector.<int>().|");
                        yield return
                            new TestCaseData(
                                    ReadCode("BeforeGenerateVar_fromNewVectorInt_short"),
                                    new ClassModel
                                    {
                                        InFile = new FileModel(),
                                        Name = "Vector.<int>",
                                        Type = "Vector.<int>"
                                    },
                                    ReadSnippet("var"),
                                    Helpers.TemplateUtils.PatternMember)
                                .Returns(ReadCode("AfterGenerateVar_fromNewVectorInt_short"))
                                .SetName("var from new <int>[].|");
                    }
                }

                public IEnumerable<TestCaseData> Const
                {
                    get
                    {
                        yield return
                            GetTestCaseFromString("const", Helpers.TemplateUtils.PatternMember)
                                .Returns(ReadCode("AfterGenerateConst_fromString"))
                                .SetName("const from \"\".|");
                        yield return
                            GetTestCaseFromUInt("const", Helpers.TemplateUtils.PatternMember)
                                .Returns(ReadCode("AfterGenerateConst_fromUInt"))
                                .SetName("const from 1.|");
                        yield return
                            GetTestCaseFromNumber("const", Helpers.TemplateUtils.PatternMember)
                                .Returns(ReadCode("AfterGenerateConst_fromNumber"))
                                .SetName("const from 10.0.|");
                        /*yield return
                            new TestCaseData(
                                    ReadCode(
                                        "BeforeGenerateConst_fromInt"),
                                    new ClassModel {InFile = new FileModel(), Name = "Number", Type = "Number"},
                                    ReadSnippet("const"),
                                    Helpers.TemplateUtils.PatternMember)
                                .Returns(
                                    ReadCode(
                                        "AfterGenerateConst_fromInt"))
                                .SetName("const from -1.|")
                                .Ignore();*/
                        yield return
                            GetTestCasefromArrayInitializer("const", Helpers.TemplateUtils.PatternMember)
                                .Returns(ReadCode("AfterGenerateConst_fromArray"))
                                .SetName("const from [].|");
                        yield return
                            GetTestCaseFromObjectInitializer("const", Helpers.TemplateUtils.PatternMember)
                                .Returns(ReadCode("AfterGenerateConst_fromObject"))
                                .SetName("const from {}.|");
                        yield return
                            new TestCaseData(
                                    ReadCode("BeforeGenerateConst_fromNewObject"),
                                    new ClassModel {InFile = new FileModel(), Name = "Object", Type = "Object"},
                                    ReadSnippet("const"),
                                    Helpers.TemplateUtils.PatternMember)
                                .Returns(ReadCode("AfterGenerateConst_fromNewObject"))
                                .SetName("const from new Object().|");
                        yield return
                            new TestCaseData(
                                    ReadCode("BeforeGenerateConst_fromNewVectorInt"),
                                    new ClassModel
                                    {
                                        InFile = new FileModel(),
                                        Name = "Vector.<int>",
                                        Type = "Vector.<int>"
                                    },
                                    ReadSnippet("const"),
                                    Helpers.TemplateUtils.PatternMember)
                                .Returns(ReadCode("AfterGenerateConst_fromNewVectorInt"))
                                .SetName("const from new Vector.<int>().|");
                        yield return
                            new TestCaseData(
                                    ReadCode("BeforeGenerateConst_fromNewVectorInt_short"),
                                    new ClassModel
                                    {
                                        InFile = new FileModel(),
                                        Name = "Vector.<int>",
                                        Type = "Vector.<int>"
                                    },
                                    ReadSnippet("const"),
                                    Helpers.TemplateUtils.PatternMember)
                                .Returns(ReadCode("AfterGenerateConst_fromNewVectorInt_short"))
                                .SetName("const from new <int>[].|");
                    }
                }

                public IEnumerable<TestCaseData> New
                {
                    get
                    {
                        yield return
                            new TestCaseData(
                                    ReadCode("BeforeGenerate_fromType"),
                                    new ClassModel { InFile = new FileModel(), Name = "Type", Type = "Type" },
                                    ReadSnippet("new"),
                                    Helpers.TemplateUtils.PatternType)
                                .Returns(ReadCode("AfterGenerateNew_fromType"))
                                .SetName("new from Type.|");
                    }
                }

                public IEnumerable<TestCaseData> Par
                {
                    get
                    {
                        yield return
                            GetTestCaseFromString("par", Helpers.TemplateUtils.PatternMember)
                                .Returns(ReadCode("AfterGeneratePar_fromString"))
                                .SetName("par from \"\".|");
                    }
                }

                public IEnumerable<TestCaseData> Return
                {
                    get
                    {
                        yield return
                            GetTestCaseFromString("return", Helpers.TemplateUtils.PatternMember)
                                .Returns(ReadCode("AfterGenerateReturn_fromString"))
                                .SetName("return from \"\".|");
                        yield return
                            GetTestCasefromMultilineArrayInitializer("return", Helpers.TemplateUtils.PatternMember)
                                .Returns(ReadCode("AfterGenerateReturn_fromMultilineArrayInitializer"))
                                .SetName("return from [].|");
                    }
                }

                public IEnumerable<TestCaseData> While
                {
                    get
                    {
                        yield return
                            GetTestCaseFromBoolean("while")
                                .Returns(ReadCode("AfterGenerateWhile_fromBoolean"))
                                .SetName("while from true.|");
                    }
                }

                public IEnumerable<TestCaseData> Dowhile
                {
                    get
                    {
                        yield return
                            GetTestCaseFromBoolean("dowhile")
                                .Returns(ReadCode("AfterGenerateDowhile_fromBoolean"))
                                .SetName("dowhile from true.|");
                    }
                }

                public IEnumerable<TestCaseData> Sel
                {
                    get
                    {
                        yield return
                            GetTestCaseFromString("sel", Helpers.TemplateUtils.PatternMember)
                                .Returns(ReadCode("AfterGenerateSel_fromString"))
                                .SetName("sel from \"\".|");
                    }
                }

                public IEnumerable<TestCaseData> Trace
                {
                    get
                    {
                        yield return
                            GetTestCaseFromString("trace", Helpers.TemplateUtils.PatternMember)
                                .Returns(ReadCode("AfterGenerateTrace_fromString"))
                                .SetName("trace from \"\".|");
                    }
                }

                [Test, TestCaseSource("Const"), TestCaseSource("Var"), TestCaseSource("Constructor"), TestCaseSource("Par"), TestCaseSource("Return"),
                       TestCaseSource("If"), TestCaseSource("Else"), TestCaseSource("Not"), TestCaseSource("Notnull"), TestCaseSource("Null"),
                       TestCaseSource("Foreach"), TestCaseSource("Forin"), TestCaseSource("For"), TestCaseSource("Forr"),
                       TestCaseSource("New"),
                       TestCaseSource("While"), TestCaseSource("Dowhile"),
                       TestCaseSource("Trace")]
                public string AS3(string sourceText, ClassModel type, string template, string pccpattern) => Generate(sourceText, type, template, pccpattern);
            }
        }
    }
}