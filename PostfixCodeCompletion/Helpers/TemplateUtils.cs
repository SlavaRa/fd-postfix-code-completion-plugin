﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ASCompletion.Completion;
using ASCompletion.Context;
using ASCompletion.Model;
using FlashDevelop.Utilities;
using HaXeContext;
using PluginCore;
using PluginCore.Helpers;

namespace PostfixCodeCompletion.Helpers
{
    static class TemplateUtils
    {
        public const string PATTERN_BLOCK = @"\$\([^\)]*{0}.*?\)";
        public const string PATTERN_T_BLOCK = @"[^\$]*?\$\({0}\)";
        internal const string POSTFIX_GENERATORS = "PostfixGenerators";
        internal const string PATTERN_MEMBER = "PCCMember";
        internal const string PATTERN_NULLABLE = "PCCNullable";
        internal const string PATTERN_COLLECTION = "PCCCollection";
        internal const string PATTERN_COLLECTION_KEY_TYPE = "$(CollectionKeyType)";
        internal const string PATTERN_COLLECTION_ITEM_TYPE = "$(CollectionItemType)";
        internal const string PATTERN_HASH = "PCCHash";
        internal const string PATTERN_BOOL = "PCCBoolean";
        internal const string PATTERN_NUMBER = "PCCNumber";
        internal const string PATTERN_STRING = "PCCString";
        internal const string PATTERN_TYPE = "PCCType";
        public static Settings Settings { get; set; }

        static readonly List<string> Templates = new List<string>
        {
            PATTERN_MEMBER,
            PATTERN_NULLABLE,
            PATTERN_COLLECTION,
            PATTERN_HASH,
            PATTERN_BOOL,
            PATTERN_NUMBER,
            PATTERN_STRING,
            PATTERN_TYPE
        };
        
        internal static bool GetHasTemplates() => GetHasTemplates(PluginBase.MainForm.CurrentDocument.SciControl.ConfigurationLanguage.ToLower());

        internal static bool GetHasTemplates(string language)
        {
            return GetHasTemplates(PathHelper.SnippetDir, language)
                || Settings.CustomSnippetDirectories.Any(it => GetHasTemplates(it.Path, language));
        }

        static bool GetHasTemplates(string snippetPath, string language)
        {
            snippetPath = GetTemplatesDir(snippetPath, language);
            return Directory.Exists(snippetPath) && Directory.GetFiles(snippetPath, "*.fds").Length > 0;
        }

        static string GetTemplatesDir(string snippetPath) => GetTemplatesDir(snippetPath, PluginBase.MainForm.CurrentDocument.SciControl.ConfigurationLanguage.ToLower());

        static string GetTemplatesDir(string snippetPath, string language) => Path.Combine(Path.Combine(snippetPath, language), POSTFIX_GENERATORS);

        internal static Dictionary<string, string> GetTemplates(string type)
        {
            var pattern = Templates.Contains(type) ? string.Format(PATTERN_BLOCK, type) : string.Format(PATTERN_T_BLOCK, type);
            var result = new Dictionary<string, string>();
            var paths = Settings.CustomSnippetDirectories.Select(it => GetTemplatesDir(it.Path)).ToList();
            paths.Add(GetTemplatesDir(PathHelper.SnippetDir));
            paths.RemoveAll(s => !Directory.Exists(s));
            foreach (var path in paths)
            {
                foreach (var file in Directory.GetFiles(path, "*.fds"))
                {
                    var content = GetFileContent(file);
                    var marker = $"#pcc:{type}";
                    var startIndex = content.IndexOf(marker, StringComparison.Ordinal);
                    if (startIndex != -1)
                    {
                        startIndex += marker.Length;
                        content = content.Remove(0, startIndex);
                    }
                    startIndex = content.IndexOf("#pcc:", StringComparison.Ordinal);
                    if (startIndex != -1) content = content.Remove(startIndex);
                    if (!Regex.IsMatch(content, pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline)) continue;
                    result.Add(file, content.Replace("\r\n", "\n"));
                }
            }
            return result;
        }

        static string GetFileContent(string file)
        {
            string content;
            using (var reader = new StreamReader(File.OpenRead(file)))
            {
                content = reader.ReadToEnd();
                reader.Close();
            }
            return content;
        }

        internal static KeyValuePair<string, string> GetVarNameToQualifiedName(ASResult expr)
        {
            string type = null;
            var member = expr.Member;
            if (member?.Type != null) type = member.Type;
            else
            {
                var cType = expr.Type;
                if (cType?.Name != null) type = cType.QualifiedName;
            }
            var sci = PluginBase.MainForm.CurrentDocument.SciControl;
            var lineNum = sci.CurrentLine;
            var word = Reflector.ASGenerator.GetStatementReturnType(sci, sci.GetLine(lineNum), sci.PositionFromLine(lineNum))?.Word;
            var varname = string.Empty;
            if (member?.Name != null) varname = Reflector.ASGenerator.GuessVarName(member.Name, type);
            if (!string.IsNullOrEmpty(word) && char.IsDigit(word[0])) word = null;
            if (!string.IsNullOrEmpty(word) && (string.IsNullOrEmpty(type) || Regex.IsMatch(type, "(<[^]]+>)"))) word = null;
            if (!string.IsNullOrEmpty(type) && type == ASContext.Context.Features.voidKey) type = null;
            if (string.IsNullOrEmpty(varname)) varname = Reflector.ASGenerator.GuessVarName(word, type);
            if (!string.IsNullOrEmpty(varname) && varname == word) varname = $"{varname}1";
            return new KeyValuePair<string, string>(varname, type);
        }

        internal static string ProcessMemberTemplate(string template, ASResult expr)
        {
            var varNameToQualifiedName = GetVarNameToQualifiedName(expr);
            var name = varNameToQualifiedName.Key.ToLower();
            var type = varNameToQualifiedName.Value;
            template = ASCompletion.Completion.TemplateUtils.ReplaceTemplateVariable(template, "Name", name);
            if (ASContext.Context is Context && Settings != null && Settings.DisableTypeDeclaration) type = null;
            if (!string.IsNullOrEmpty(type)) type = MemberModel.FormatType(Reflector.ASGenerator.GetShortType(type));
            template = ASCompletion.Completion.TemplateUtils.ReplaceTemplateVariable(template, "Type", type);
            return template;
        }

        internal static string ProcessCollectionTemplate(string template, ASResult expr)
        {
            var type = expr.Member != null ? expr.Member.Type : expr.Type.QualifiedName;
            if (type.Contains("@")) type = $"{type.Replace("@", ".<")}>";
            type = Regex.Match(type, "<([^]]+)>").Groups[1].Value;
            type = Reflector.ASGenerator.GetShortType(type);
            switch (PluginBase.MainForm.CurrentDocument.SciControl.ConfigurationLanguage.ToLower())
            {
                case "as2":
                case "as3":
                    if (string.IsNullOrEmpty(type)) type = ASContext.Context.Features.dynamicKey;
                    template = template.Replace(PATTERN_COLLECTION_KEY_TYPE, "int");
                    break;
            }
            return template.Replace(PATTERN_COLLECTION_ITEM_TYPE, type);
        }

        internal static string ProcessHashTemplate(string template, ASResult expr)
        {
            switch (PluginBase.MainForm.CurrentDocument.SciControl.ConfigurationLanguage.ToLower())
            {
                case "as2":
                case "as3":
                    var type = expr.Member != null ? expr.Member.Type : expr.Type.QualifiedName;
                    type = Reflector.ASGenerator.GetShortType(type);
                    var features = ASContext.Context.Features;
                    var objectKey = features.objectKey;
                    if (type == objectKey || type == "Dictionary")
                    {
                        template = template.Replace(PATTERN_COLLECTION_KEY_TYPE, type == objectKey ? features.stringKey : objectKey);
                        template = template.Replace(PATTERN_COLLECTION_ITEM_TYPE, features.dynamicKey);
                    }
                    break;
            }
            return template;
        }

        internal static string GetDescription(ASResult expr, string template, string pccpattern)
        {
            var sci = PluginBase.MainForm.CurrentDocument.SciControl;
            var position = ScintillaControlHelper.GetDotLeftStartPosition(sci, sci.CurrentPos - 1);
            var exprStartPosition = ScintillaControlHelper.GetExpressionStartPosition(sci, sci.CurrentPos, expr);
            var lineNum = sci.CurrentLine;
            var line = sci.GetLine(lineNum);
            var snippet = line.Substring(exprStartPosition - sci.PositionFromLine(lineNum), position - exprStartPosition);
            var result = template.Replace(SnippetHelper.BOUNDARY, string.Empty);
            result = Regex.Replace(result, string.Format(PATTERN_BLOCK, pccpattern), snippet, RegexOptions.IgnoreCase | RegexOptions.Multiline);
            result = ProcessMemberTemplate(result, expr);
            result = ArgsProcessor.ProcessCodeStyleLineBreaks(result);
            result = result.Replace(SnippetHelper.ENTRYPOINT, "|");
            result = result.Replace(SnippetHelper.EXITPOINT, "|");
            return result;
        }

        internal static void InsertSnippetText(ASResult expr, string template, string pccpattern)
        {
            var sci = PluginBase.MainForm.CurrentDocument.SciControl;
            var position = ScintillaControlHelper.GetDotLeftStartPosition(sci, sci.CurrentPos - 1);
            sci.SetSel(position, sci.CurrentPos);
            sci.ReplaceSel(string.Empty);
            position = ScintillaControlHelper.GetExpressionStartPosition(sci, sci.CurrentPos, expr);
            sci.SetSel(position, sci.CurrentPos);
            var snippet = Regex.Replace(template, string.Format(PATTERN_BLOCK, pccpattern), sci.SelText, RegexOptions.IgnoreCase | RegexOptions.Multiline);
            snippet = ProcessMemberTemplate(snippet, expr);
            snippet = ArgsProcessor.ProcessCodeStyleLineBreaks(snippet);
            sci.ReplaceSel(string.Empty);
            SnippetHelper.InsertSnippetText(sci, position, snippet);
        }
    }
}