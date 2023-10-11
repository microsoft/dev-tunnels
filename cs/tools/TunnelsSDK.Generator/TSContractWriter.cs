// <copyright file="TSContractWriter.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Microsoft.DevTunnels.Generator;

internal class TSContractWriter : ContractWriter
{

    public TSContractWriter(string repoRoot, string csNamespace) : base(repoRoot, csNamespace)
    {
    }

    public override void WriteContract(ITypeSymbol type, ICollection<ITypeSymbol> allTypes)
    {
        var csFilePath = GetRelativePath(type.Locations.Single().GetLineSpan().Path);

        var fileName = ToCamelCase(type.Name) + ".ts";
        var filePath = GetAbsolutePath(Path.Combine("ts", "src", "contracts", fileName));

        var s = new StringBuilder();
        s.AppendLine("// Copyright (c) Microsoft Corporation.");
        s.AppendLine("// Licensed under the MIT license.");
        s.AppendLine($"// Generated from ../../../{csFilePath}");
        s.AppendLine("/* eslint-disable */");
        s.AppendLine();

        var importsOffset = s.Length;
        var imports = new SortedSet<string>();

        WriteContractType(s, "", type, imports);

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

    private void WriteContractType(
        StringBuilder s,
        string indent,
        ITypeSymbol type,
        SortedSet<string> imports)
    {
        var members = type.GetMembers();
        if (type.BaseType?.Name == nameof(Enum) || members.All((m) =>
            m.DeclaredAccessibility != Accessibility.Public ||
            (m is IFieldSymbol field &&
             ((field.IsConst && field.Type.Name == nameof(String)) || field.Name == "All")) ||
            (m is IMethodSymbol method && method.MethodKind == MethodKind.StaticConstructor)))
        {
            WriteEnumContract(s, indent, type);
        }
        else if (type.IsStatic && members.All((m) => m.IsStatic))
        {
            WriteStaticClassContract(s, indent, type, imports);
        }
        else
        {
            WriteInterfaceContract(s, indent, type, imports);
        }

        var nestedTypes = type.GetTypeMembers()
            .Where((t) => !ContractsGenerator.ExcludedContractTypes.Contains(t.Name))
            .ToArray();
        if (nestedTypes.Length > 0)
        {
            s.AppendLine();
            s.Append($"{indent}export namespace {type.Name} {{");

            foreach (var nestedType in nestedTypes.Where(
                (t) => !ContractsGenerator.ExcludedContractTypes.Contains(t.Name)))
            {
                s.AppendLine();
                WriteContractType(s, indent + "    ", nestedType, imports);
            }

            s.AppendLine($"{indent}}}");
        }
    }

    private void WriteInterfaceContract(
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

        if (!string.IsNullOrEmpty(baseTypeName))
        {
            imports.Add(baseTypeName!);
        }

        s.Append(FormatDocComment(type.GetDocumentationCommentXml(), indent));

        var extends = baseTypeName != null ? " extends " + baseTypeName : "";

        s.Append($"{indent}export interface {type.Name}{extends} {{");

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

            // Make booleans always nullable since undefined is falsy anyway.
            isNullable |= propertyType == "bool";

            var tsName = ToCamelCase(property.Name);
            if (property.TryGetJsonPropertyName(out var jsonPropertyName))
            {
                tsName = jsonPropertyName!;
            }

            var tsType = GetTSTypeForCSType(propertyType, tsName, imports);

            s.AppendLine($"{indent}    {tsName}{(isNullable ? "?" : "")}: {tsType};");
        }

        s.AppendLine($"{indent}}}");

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
            s.Append(FormatDocComment(field.GetDocumentationCommentXml(), indent, GetJsDoc(field)));
            s.AppendLine($"{indent}export const {ToCamelCase(field.Name)} = '{field.ConstantValue}';");
        }

        s.Append(ExportStaticMembers(type, constMemberNames));
    }

    private void WriteEnumContract(
        StringBuilder s,
        string indent,
        ITypeSymbol type)
    {
        s.Append(FormatDocComment(type.GetDocumentationCommentXml(), indent));

        s.Append($"{indent}export enum {type.Name} {{");

        foreach (var member in type.GetMembers())
        {
            if (!(member is IFieldSymbol field) || !field.HasConstantValue ||
                field.DeclaredAccessibility != Accessibility.Public)
            {
                continue;
            }

            s.AppendLine();
            s.Append(FormatDocComment(field.GetDocumentationCommentXml(), indent + "    ", GetJsDoc(field)));

            var value = type.BaseType?.Name == "Enum" ? field.Name : field.ConstantValue;
            s.AppendLine($"{indent}    {field.Name} = '{value}',");
        }

        s.AppendLine($"{indent}}}");

        s.Append(ExportStaticMembers(type));
    }

    private void WriteStaticClassContract(
        StringBuilder s,
        string indent,
        ITypeSymbol type,
        SortedSet<string> imports)
    {
        s.Append(FormatDocComment(type.GetDocumentationCommentXml(), indent));

        s.Append($"export namespace {type.Name} {{");

        foreach (var member in type.GetMembers())
        {
            var property = member as IPropertySymbol;
            var field = member as IFieldSymbol;
            if (!member.IsStatic || !(property?.IsReadOnly == true || field?.IsConst == true))
            {
                continue;
            }

            var memberType = (property?.Type ?? field!.Type).ToDisplayString();
            var isNullable = memberType.EndsWith("?");
            if (isNullable)
            {
                memberType = memberType.Substring(0, memberType.Length - 1);
            }

            // Make booleans always nullable since undefined is falsy anyway.
            isNullable |= memberType == "bool";

            var tsName = ToCamelCase(member.Name);
            var tsType = GetTSTypeForCSType(memberType, tsName, imports);
            var value = GetMemberInitializer(member);
            if (value != null)
            {
                s.AppendLine();
                s.Append(FormatDocComment(member.GetDocumentationCommentXml(), indent + "    ", GetJsDoc(member)));
                s.AppendLine($"{indent}    " +
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

    private string FormatDocComment(string? comment, string indent, List<string>? jsdoc = null)
    {
        if (comment == null)
        {
            return string.Empty;
        }

        comment = comment.Replace("\r", "");
        comment = new Regex("\n *").Replace(comment, " ");
        comment = new Regex($"<see cref=\".:({this.csNamespace}\\.)?(\\w+)\\.(\\w+)\" ?/>")
            .Replace(comment, (m) => $"{{@link {m.Groups[2].Value}.{ToCamelCase(m.Groups[3].Value)}}}");
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

        if (jsdoc != null)
        {
            foreach (var line in jsdoc)
            {
                s.AppendLine(indent + " * " + line);
            }
        }

        s.AppendLine(indent + " */");

        return s.ToString();
    }

    private static string? GetMemberInitializer(ISymbol member)
    {
        var location = member.Locations.Single();
        var sourceSpan = location.SourceSpan;
        var sourceText = location.SourceTree!.ToString();
        var eolIndex = sourceText.IndexOf('\n', sourceSpan.End);
        var equalsIndex = sourceText.IndexOf('=', sourceSpan.End);

        if (equalsIndex < 0 || equalsIndex > eolIndex)
        {
            // The member does not have an initializer.
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

        // Attempt to convert the CS expression to a TS expression. This involves several
        // weak assumptions, and will not work for many kinds of expressions. But it might
        // be good enough.
        var tsExpression = csExpression
            .Replace("'", "^^^").Replace("\\\"", "$$$").Replace('"', '\'').Replace("$$$", "\"").Replace("^^^", "\\'")
            .Replace("Regex", "RegExp")
            .Replace("Replace", "replace");

        // Assume any PascalCase identifiers are referncing other variables in scope.
        tsExpression = new Regex("([A-Z][a-z]+){2,6}\\b(?!\\()").Replace(
            tsExpression, (m) =>
            {
                return (member.ContainingType.MemberNames.Contains(m.Value) ?
                    member.ContainingType.Name + "." : string.Empty) + ToCamelCase(m.Value);
            });

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

            if (tsType == "ResourceStatus")
            {
                tsType = "number | ResourceStatus";
            }
        }
        else
        {
            tsType = csType switch
            {
                "bool" => "boolean",
                "short" => "number",
                "ushort" => "number",
                "int" => "number",
                "uint" => "number",
                "long" => "number",
                "ulong" => "number",
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

    private static List<string> GetJsDoc(ISymbol symbol)
    {
        var doc = new List<string>();
        var obsoleteAttribute = GetObsoleteAttribute(symbol);
        if (obsoleteAttribute != null)
        {
            var message = GetObsoleteMessage(obsoleteAttribute);
            doc.Add($"@deprecated {message}");
        }

        return doc;
    }
}
