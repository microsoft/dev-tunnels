using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Microsoft.VsSaaS.TunnelService.Generator;

internal class TSContractWriter : ContractWriter
{

    public TSContractWriter(string repoRoot, string csNamespace) : base(repoRoot, csNamespace)
    {
    }

    public override void WriteContract(ITypeSymbol type)
    {
        var csFilePath = GetRelativePath(type.Locations.Single().GetLineSpan().Path);

        var fileName = ToCamelCase(type.Name) + ".ts";
        var filePath = GetAbsolutePath(Path.Combine("ts", "src", "contracts", fileName));

        var s = new StringBuilder();
        s.AppendLine($"// Generated from ../../../{csFilePath}");
        s.AppendLine();

        var members = type.GetMembers();
        if (type.BaseType?.Name == "Enum" || members.All((m) => 
            m is IFieldSymbol field && field.IsConst && field.Type.Name == "String"))
        {
            WriteEnumContract(type, s);
        }
        else
        {
            WriteClassContract(type, s);
        }

        File.WriteAllText(filePath, s.ToString());
    }

    private void WriteClassContract(ITypeSymbol type, StringBuilder s)
    {
        var baseTypeName = type.BaseType?.Name;
        if (baseTypeName == nameof(Object))
        {
            baseTypeName = null;
        }

        var importsOffset = s.Length;
        var imports = new SortedSet<string>();
        if (!string.IsNullOrEmpty(baseTypeName))
        {
            imports.Add(baseTypeName!);
        }

        s.Append(FormatDocComment(type.GetDocumentationCommentXml(), ""));

        var extends = baseTypeName != null ? " extends " + baseTypeName : "";

        s.Append($"export interface {type.Name}{extends} {{");

        foreach (var member in type.GetMembers())
        {
            if (!(member is IPropertySymbol property))
            {
                continue;
            }

            s.AppendLine();
            s.Append(FormatDocComment(property.GetDocumentationCommentXml(), "    "));

            var propertyType = property.Type.ToDisplayString();
            if (propertyType.EndsWith("?"))
            {
                propertyType = propertyType.Substring(0, propertyType.Length - 1);
            }

            var tsName = ToCamelCase(property.Name);
            var tsType = GetTSTypeForCSType(propertyType, tsName);

            if (propertyType.StartsWith(this.csNamespace + ".") && !imports.Contains(tsType))
            {
                var importType = tsType.Replace("?", "").Replace("[]", "");
                if (importType != type.Name)
                {
                    imports.Add(importType);
                }
            }

            s.AppendLine($"    {tsName}?: {tsType};");
        }

        if (imports.Count > 0)
        {
            var importLines = string.Join(Environment.NewLine, imports.Select(
                (type) => $"import {{ {type} }} from './{ToCamelCase(type!)}';")) +
                Environment.NewLine + Environment.NewLine;
            s.Insert(importsOffset, importLines);
        }

        s.AppendLine("}");
    }

    private void WriteEnumContract(ITypeSymbol type, StringBuilder s)
    {
        s.Append(FormatDocComment(type.GetDocumentationCommentXml(), ""));

        s.Append($"export enum {type.Name} {{");

        foreach (var member in type.GetMembers())
        {
            if (!(member is IFieldSymbol field))
            {
                continue;
            }

            s.AppendLine();

            s.Append(FormatDocComment(field.GetDocumentationCommentXml(), "    "));

            var value = type.BaseType?.Name == "Enum" ?
                ToCamelCase(field.Name) : field.ConstantValue;
            s.AppendLine($"    {field.Name} = '{value}',");
        }

        s.AppendLine("}");
    }

    private static string ToCamelCase(string name)
    {
        return name.Substring(0, 1).ToLowerInvariant() + name.Substring(1);
    }

    private string FormatDocComment(string? comment, string prefix)
    {
        if (comment == null)
        {
            return string.Empty;
        }

        comment = comment.Replace("\r", "");
        comment = new Regex("\n *").Replace(comment, " ");
        comment = new Regex($"<see cref=\".:({this.csNamespace}.)?([^\"]+)\" ?/>")
            .Replace(comment, "`$2`");

        var summary = new Regex("<summary>(.*)</summary>").Match(comment).Groups[1].Value.Trim();
        var remarks = new Regex("<remarks>(.*)</remarks>").Match(comment).Groups[1].Value.Trim();

        var s = new StringBuilder();
        s.AppendLine(prefix + "/**");

        foreach (var commentLine in WrapComment(summary, 90 - 3 - prefix.Length))
        {
            s.AppendLine(prefix + " * " + commentLine);
        }

        if (!string.IsNullOrEmpty(remarks))
        {
            s.AppendLine(prefix + " *");
            foreach (var commentLine in WrapComment(remarks, 90 - 3 - prefix.Length))
            {
                s.AppendLine(prefix + " * " + commentLine);
            }
        }

        s.AppendLine(prefix + " */");

        return s.ToString();
    }

    private string GetTSTypeForCSType(string csType, string propertyName)
    {
        var suffix = "";
        if (csType.EndsWith("[]"))
        {
            suffix = "[]";
            csType = csType.Substring(0, csType.Length - 2);
        }

        string tsType;
        if (csType.StartsWith(this.csNamespace + "."))
        {
            tsType = csType.Substring(csNamespace.Length + 1);
        }
        else
        {
            tsType = csType switch
            {
                "uint" => "number",
                "ushort" => "number",
                "string" => "string",
                "System.DateTime" => "Date",
                "System.Collections.Generic.IDictionary<string, string>"
                    => $"{{ [{(propertyName == "accessTokens" ? "scope" : "key")}: string]: string }}",
                "System.Collections.Generic.IDictionary<string, string[]>"
                    => $"{{ [{(propertyName == "errors" ? "property" : "key")}: string]: string[] }}",
                _ => throw new NotSupportedException("Unsupported C# type: " + csType),
            };
        }

        tsType += suffix;
        return tsType;
    }
}
