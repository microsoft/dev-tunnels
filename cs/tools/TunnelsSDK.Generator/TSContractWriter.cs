// <copyright file="TSContractWriter.cs" company="Microsoft">
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

        var importsOffset = s.Length;
        var imports = new SortedSet<string>();

        var members = type.GetMembers();
        if (type.BaseType?.Name == "Enum" || members.All((m) => 
            (m is IFieldSymbol field &&
             ((field.IsConst && field.Type.Name == "String") || field.Name == "All")) ||
            (m is IMethodSymbol method && method.MethodKind == MethodKind.StaticConstructor)))
        {
            WriteEnumContract(s, type);
        }
        else if (type.IsStatic && members.All((m) => m.IsStatic))
        {
            WriteStaticClassContract(s, type, imports);
        }
        else
        {
            WriteInterfaceContract(s, type, imports);
        }

        imports.Remove(type.Name);
        if (imports.Count > 0)
        {
            var importLines = string.Join(Environment.NewLine, imports.Select(
                (i) => $"import {{ {i} }} from './{ToCamelCase(i!)}';")) +
                Environment.NewLine + Environment.NewLine;
            s.Insert(importsOffset, importLines);
        }

        File.WriteAllText(filePath, s.ToString());
    }

    private void WriteInterfaceContract(
        StringBuilder s,
        ITypeSymbol type,
        SortedSet<string> imports)
    {
        var baseTypeName = type.BaseType?.Name;
        if (baseTypeName == nameof(Object))
        {
            baseTypeName = null;
        }

        if (!string.IsNullOrEmpty(baseTypeName))
        {
            imports.Add(baseTypeName!);
        }

        s.Append(FormatDocComment(type.GetDocumentationCommentXml(), ""));

        var extends = baseTypeName != null ? " extends " + baseTypeName : "";

        s.Append($"export interface {type.Name}{extends} {{");

        foreach (var property in type.GetMembers().OfType<IPropertySymbol>()
            .Where((p) => !p.IsStatic))
        {
            s.AppendLine();
            s.Append(FormatDocComment(property.GetDocumentationCommentXml(), "    "));

            var propertyType = property.Type.ToDisplayString();
            var isNullable = propertyType.EndsWith("?");
            if (isNullable)
            {
                propertyType = propertyType.Substring(0, propertyType.Length - 1);
            }

            // Make booleans always nullable since undefined is falsy anyway.
            isNullable |= propertyType == "bool";

            var tsName = ToCamelCase(property.Name);
            var tsType = GetTSTypeForCSType(propertyType, tsName, imports);

            s.AppendLine($"    {tsName}{(isNullable ? "?" : "")}: {tsType};");
        }

        s.AppendLine("}");

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
            s.Append(FormatDocComment(field.GetDocumentationCommentXml(), ""));

            s.AppendLine($"export const {ToCamelCase(field.Name)} = '{field.ConstantValue}';");

        }

        s.Append(ExportStaticMembers(type, constMemberNames));
    }

    private void WriteEnumContract(StringBuilder s, ITypeSymbol type)
    {
        s.Append(FormatDocComment(type.GetDocumentationCommentXml(), ""));

        s.Append($"export enum {type.Name} {{");

        foreach (var member in type.GetMembers())
        {
            if (!(member is IFieldSymbol field) || !field.HasConstantValue)
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

        s.Append(ExportStaticMembers(type));
    }

    private void WriteStaticClassContract(
        StringBuilder s,
        ITypeSymbol type,
        SortedSet<string> imports)
    {
        s.Append(FormatDocComment(type.GetDocumentationCommentXml(), ""));

        s.Append($"namespace {type.Name} {{");

        foreach (var member in type.GetMembers())
        {
            if (!member.IsStatic || !(member is IPropertySymbol property) || !property.IsReadOnly)
            {
                continue;
            }

            var propertyType = property.Type.ToDisplayString();
            var isNullable = propertyType.EndsWith("?");
            if (isNullable)
            {
                propertyType = propertyType.Substring(0, propertyType.Length - 1);
            }

            // Make booleans always nullable since undefined is falsy anyway.
            isNullable |= propertyType == "bool";

            var tsName = ToCamelCase(property.Name);
            var tsType = GetTSTypeForCSType(propertyType, tsName, imports);
            var value = GetPropertyInitializer(property);
            if (value != null)
            {
                s.AppendLine();
                s.Append(FormatDocComment(property.GetDocumentationCommentXml(), "    "));
                s.AppendLine("    " +
                    $"export const {tsName}: {tsType}{(isNullable ? " | null" : "")} = {value};");
            }
        }

        s.AppendLine("}");
    }

    private static string ExportStaticMembers(
        ITypeSymbol type, ICollection<string>? constMemberNames = null)
    {
        var s = new StringBuilder();
        constMemberNames ??= Array.Empty<string>();

        var staticMemberNames = type.GetMembers()
            .Where((s) => s.IsStatic && s.DeclaredAccessibility == Accessibility.Public &&
                (s is IPropertySymbol p ||
                    (s is IMethodSymbol m && m.MethodKind == MethodKind.Ordinary)))
            .Select((m) => ToCamelCase(m.Name))
            .ToArray();
        if (staticMemberNames.Length > 0)
        {
            s.AppendLine();
            s.AppendLine("// Import static members from a non-generated file,");
            s.AppendLine("// and re-export them as an object with the same name as the interface.");
            s.AppendLine("import {");

            foreach (var memberName in staticMemberNames)
            {
                s.AppendLine($"    {memberName},");
            }

            s.AppendLine($"}} from './{ToCamelCase(type.Name)}Statics';");
        }

        if (constMemberNames.Count > 0 || staticMemberNames.Length > 0)
        {
            s.AppendLine();
            s.AppendLine($"export const {type.Name} = {{");

            foreach (var memberName in constMemberNames.Concat(staticMemberNames))
            {
                s.AppendLine($"    {memberName},");
            }

            s.AppendLine("};");
        }

        return s.ToString();
    }

    internal static string ToCamelCase(string name)
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
        comment = new Regex($"<see cref=\".:({this.csNamespace}\\.)?(\\w+)\\.(\\w+)\" ?/>")
            .Replace(comment, (m) => $"`{m.Groups[2].Value}.{ToCamelCase(m.Groups[3].Value)}`");
        comment = new Regex($"<see cref=\".:({this.csNamespace}\\.)?([^\"]+)\" ?/>")
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

        // Attempt to convert the CS expression to a TS expression. This involes several
        // weak assumptions, and will not work for many kinds of expressions. But it might
        // be good enough.
        var tsExpression = csExpression
            .Replace('"', '\'')
            .Replace("Regex", "RegExp")
            .Replace("Replace", "replace");

        // Assume any PascalCase identifiers are referncing other variables in scope.
        tsExpression = new Regex("([A-Z][a-z]+){2,4}\\b(?!\\()").Replace(
            tsExpression, (m) => ToCamelCase(m.Value));

        return tsExpression;
    }

    private string GetTSTypeForCSType(string csType, string propertyName, SortedSet<string> imports)
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

            if (!imports.Contains(tsType))
            {
                imports.Add(tsType);
            }
        }
        else
        {
            tsType = csType switch
            {
                "bool" => "boolean",
                "int" => "number",
                "uint" => "number",
                "ushort" => "number",
                "string" => "string",
                "System.DateTime" => "Date",
                "System.Text.RegularExpressions.Regex" => "RegExp",
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
