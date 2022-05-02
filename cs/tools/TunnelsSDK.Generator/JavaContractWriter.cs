// <copyright file="JavaContractWriter.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Microsoft.VsSaaS.TunnelService.Generator;

internal class JavaContractWriter : ContractWriter
{
    public const String JavaDateTimeType = "java.util.Date";
    public const String PackageName = "package com.microsoft.tunnels.contracts";
    public const String RegexPatternType = "java.util.regex.Pattern";
    public const String SerializedNameTag = "@SerializedName";
    public const String SerializedNameType = $"com.google.gson.annotations.SerializedName";
    public const String ClassDeclarationHeader = "public class";
    public const String StaticClassDeclarationHeader = "public static class";
    public const String EnumDeclarationHeader = "public enum";
    public const String GsonExposeType = "com.google.gson.annotations.Expose";
    public const String GsonExposeTag = "@Expose";

    public JavaContractWriter(string repoRoot, string csNamespace) : base(repoRoot, csNamespace)
    {
    }

    public override void WriteContract(ITypeSymbol type, ICollection<ITypeSymbol> allTypes)
    {
        var csFilePath = GetRelativePath(type.Locations.Single().GetLineSpan().Path);

        var fileName = type.Name + ".java";
        // TODO - temporarily writing files to a new location.
        var filePath = GetAbsolutePath(Path.Combine("java", "src", "main", "java", "com", "microsoft", "tunnels", "contracts", fileName));

        var s = new StringBuilder();
        s.AppendLine($"// Generated from ../../../../../../../../{csFilePath}");
        s.AppendLine();
        s.AppendLine($"{PackageName};");
        s.AppendLine();

        var importsOffset = s.Length;
        var imports = new SortedSet<string>();

        WriteContractType(s, "", type, imports);

        imports.Remove(type.Name);
        if (imports.Count > 0)
        {
            var importLines = string.Join(Environment.NewLine, imports.Select(
                (i) => $"import {i};")) +
                Environment.NewLine + Environment.NewLine;
            s.Insert(importsOffset, importLines);
        }


        if (!Directory.Exists(Path.GetDirectoryName(filePath)))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath));
        }

        File.WriteAllText(filePath, s.ToString());
    }

    private void WriteContractType(
        StringBuilder s,
        string indent,
        ITypeSymbol type,
        SortedSet<string> imports)
    {
        var members = type.GetMembers();
        if (type.BaseType?.Name == nameof(Enum))
        {
            WriteEnumContract(s, indent, type);
            imports.Add(SerializedNameType);
            imports.Add(GsonExposeType);
        }
        else
        {
            WriteClassContract(s, indent, type, imports);
            imports.Add(GsonExposeType);
        }
    }

    public void writeNestedTypes(StringBuilder s,
        string indent,
        ITypeSymbol type,
        SortedSet<string> imports)
    {
        var nestedTypes = type.GetTypeMembers()
            .Where((t) => !ContractsGenerator.ExcludedContractTypes.Contains(t.Name))
            .ToArray();
        if (nestedTypes.Length > 0)
        {
            foreach (var nestedType in nestedTypes.Where(
                (t) => !ContractsGenerator.ExcludedContractTypes.Contains(t.Name)))
            {
                s.AppendLine();
                WriteContractType(s, indent + "    ", nestedType, imports);
            }
        }
    }

    private void WriteClassContract(
        StringBuilder s,
        string indent,
        ITypeSymbol type,
        SortedSet<string> imports)
    {
        var baseTypeName = type.BaseType?.Name;
        if (baseTypeName == nameof(Object))
        {
            baseTypeName = null;
        }

        s.Append(FormatDocComment(type.GetDocumentationCommentXml(), indent));

        var extends = baseTypeName != null ? " extends " + baseTypeName : "";

        // Only inner classes can be declared static in Java.
        var header = type.IsStatic && type.ContainingType != null
          ? StaticClassDeclarationHeader : ClassDeclarationHeader;
        s.Append($"{indent}{header} {type.Name}{extends} {{");

        foreach (var property in type.GetMembers().OfType<IPropertySymbol>()
        .Where((p) => !p.IsStatic))
        {
            s.AppendLine();
            s.Append(FormatDocComment(property.GetDocumentationCommentXml(), indent + "    "));

            var propertyType = property.Type.ToDisplayString();
            var isNullable = propertyType.EndsWith("?");
            if (isNullable)
            {
                propertyType = propertyType.Substring(0, propertyType.Length - 1);
            }

            var javaName = ToCamelCase(property.Name);
            var javaType = GetJavaTypeForCSType(propertyType, javaName, imports);
            var value = GetPropertyInitializer(property);
            s.AppendLine($"{indent}    {GsonExposeTag}");
            if (value != null && !value.Equals("null") && !value.Equals("null!")) {
              s.AppendLine($"{indent}    public {javaType} {javaName} = {value};");
            } else {
              s.AppendLine($"{indent}    public {javaType} {javaName};");
            }
        }
        var constMemberNames = new List<string>();
        foreach (var field in type.GetMembers().OfType<IFieldSymbol>()
            .Where((f) => f.IsConst))
        {
            if (field.DeclaredAccessibility == Accessibility.Public)
            {
                constMemberNames.Add(ToCamelCase(field.Name));
            }
            else if (field.DeclaredAccessibility != Accessibility.Internal)
            {
                continue;
            }

            s.AppendLine();
            s.Append(FormatDocComment(field.GetDocumentationCommentXml(), indent + "    "));
            var javaName = ToCamelCase(field.Name);
            var javaType = GetJavaTypeForCSType(field.Type.ToDisplayString(), javaName, imports);
            s.AppendLine($"{indent}    {GsonExposeTag}");
            s.AppendLine($"{indent}    public static {javaType} {javaName} = \"{field.ConstantValue}\";");
        }
        writeNestedTypes(s, indent, type, imports);
        s.AppendLine($"{indent}}}");
    }

    private void WriteEnumContract(
        StringBuilder s,
        string indent,
        ITypeSymbol type)
    {
        s.Append(FormatDocComment(type.GetDocumentationCommentXml(), indent));

        s.Append($"{indent}{EnumDeclarationHeader} {type.Name} {{");

        foreach (var member in type.GetMembers())
        {
            if (!(member is IFieldSymbol field) || !field.HasConstantValue)
            {
                continue;
            }

            s.AppendLine();
            s.Append(FormatDocComment(field.GetDocumentationCommentXml(), indent + "    "));
            s.AppendLine($"{indent}    {SerializedNameTag}(\"{field.Name}\")");
            s.AppendLine($"{indent}    {field.Name},");
        }
        s.AppendLine($"{indent}}}");
    }

    internal static string ToCamelCase(string name)
    {
        return name.Substring(0, 1).ToLowerInvariant() + name.Substring(1);
    }

    private string FormatDocComment(string? comment, string indent)
    {
        if (comment == null)
        {
            return string.Empty;
        }

        comment = comment.Replace("\r", "");
        comment = new Regex("\n *").Replace(comment, " ");
        comment = new Regex($"<see cref=\".:({this.csNamespace}\\.)?(\\w+)\\.(\\w+)\" ?/>")
            .Replace(comment, (m) => $"{{@link {m.Groups[2].Value}#{ToCamelCase(m.Groups[3].Value)}}}");
        comment = new Regex($"<see cref=\".:({this.csNamespace}\\.)?([^\"]+)\" ?/>")
            .Replace(comment, "{@link $2}");

        var summary = new Regex("<summary>(.*)</summary>").Match(comment).Groups[1].Value.Trim();
        var remarks = new Regex("<remarks>(.*)</remarks>").Match(comment).Groups[1].Value.Trim();

        var s = new StringBuilder();
        s.AppendLine(indent + "/**");

        foreach (var commentLine in WrapComment(summary, 90 - 3 - indent.Length))
        {
            s.AppendLine(indent + " * " + commentLine);
        }

        if (!string.IsNullOrEmpty(remarks))
        {
            s.AppendLine(indent + " *");
            foreach (var commentLine in WrapComment(remarks, 90 - 3 - indent.Length))
            {
                s.AppendLine(indent + " * " + commentLine);
            }
        }

        s.AppendLine(indent + " */");

        return s.ToString();
    }

    private static string? GetPropertyInitializer(IPropertySymbol property)
    {
        var location = property.Locations.Single();
        var sourceSpan = location.SourceSpan;
        var sourceText = location.SourceTree!.ToString();
        var eolIndex = sourceText.IndexOf('\n', sourceSpan.End);
        var equalsIndex = sourceText.IndexOf('=', sourceSpan.End);

        if (equalsIndex < 0 || equalsIndex > eolIndex)
        {
            // The property does not have an initializer.
            return null;
        }

        var semicolonIndex = sourceText.IndexOf(';', equalsIndex);
        if (semicolonIndex < 0)
        {
            // Invalid syntax??
            return null;
        }

        var csExpression = sourceText.Substring(
            equalsIndex + 1, semicolonIndex - equalsIndex - 1).Trim();

        // Attempt to convert the CS expression to a Java expression. This involes several
        // weak assumptions, and will not work for many kinds of expressions. But it might
        // be good enough.
        var javaExpression = csExpression
            //.Replace('"', '\'')
            .Replace("new Regex", $"{RegexPatternType}.compile")
            .Replace("Replace", "replace");

        // Assume any PascalCase identifiers are referncing other variables in scope.
        javaExpression = new Regex("([A-Z][a-z]+){2,4}\\b(?!\\()").Replace(
            javaExpression, (m) => ToCamelCase(m.Value));

        return javaExpression;
    }

    private string GetJavaTypeForCSType(string csType, string propertyName, SortedSet<string> imports)
    {
        var suffix = "";
        if (csType.EndsWith("[]"))
        {
            suffix = "[]";
            csType = csType.Substring(0, csType.Length - 2);
        }

        string javaType;
        if (csType.StartsWith(this.csNamespace + "."))
        {
            javaType = csType.Substring(csNamespace.Length + 1);
        }
        else
        {
            javaType = csType switch
            {
                "bool" => "boolean",
                "int" => "int",
                "uint" => "int",
                "ushort" => "int",
                "string" => "String",
                "System.DateTime" => JavaDateTimeType,
                "System.Text.RegularExpressions.Regex" => RegexPatternType,
                "System.Collections.Generic.IDictionary<string, string>"
                    => $"java.util.HashMap<String, String>",
                "System.Collections.Generic.IDictionary<string, string[]>"
                    => $"java.util.HashMap<String, String[]>",
                _ => throw new NotSupportedException("Unsupported C# type: " + csType),
            };
        }

        javaType += suffix;
        return javaType;
    }
}
