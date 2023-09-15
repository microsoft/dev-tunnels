// <copyright file="JavaContractWriter.cs" company="Microsoft">
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

internal class JavaContractWriter : ContractWriter
{
    public const string JavaDateTimeType = "java.util.Date";
    public const string PackageName = "com.microsoft.tunnels.contracts";
    public const string RegexPatternType = "java.util.regex.Pattern";
    public const string SerializedNameTagFormat = "@SerializedName(\"{0}\")";
    public const string SerializedNameType = $"com.google.gson.annotations.SerializedName";
    public const string ClassDeclarationHeader = "public class";
    public const string StaticClassDeclarationHeader = "public static class";
    public const string EnumDeclarationHeader = "public enum";
    public const string GsonExposeType = "com.google.gson.annotations.Expose";
    public const string GsonExposeTag = "@Expose";
    public const string DeprecatedTag = "@Deprecated";

    public JavaContractWriter(string repoRoot, string csNamespace) : base(repoRoot, csNamespace)
    {
    }

    public override void WriteContract(ITypeSymbol type, ICollection<ITypeSymbol> allTypes)
    {
        var csFilePath = GetRelativePath(type.Locations.Single().GetLineSpan().Path);

        var fileName = type.Name + ".java";
        var filePath = GetAbsolutePath(Path.Combine("java", "src", "main", "java", "com", "microsoft", "tunnels", "contracts", fileName));

        var s = new StringBuilder();
        s.AppendLine("// Copyright (c) Microsoft Corporation.");
        s.AppendLine("// Licensed under the MIT license.");
        s.AppendLine($"// Generated from ../../../../../../../../{csFilePath}");
        s.AppendLine();
        s.AppendLine($"package {PackageName};");
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
        }
        else
        {
            WriteClassContract(s, indent, type, imports);
        }
    }

    public void WriteNestedTypes(
        StringBuilder s,
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
        var staticClass = type.IsStatic && type.GetMembers().All((m) => m.IsStatic);
        if (!staticClass) {
            imports.Add(GsonExposeType);
        }

        var enumClass = type.IsStatic && type.GetMembers()
            .Where((m) => m.DeclaredAccessibility == Accessibility.Public)
            .All((m) => m is IFieldSymbol);

        s.Append(FormatDocComment(type.GetDocumentationCommentXml(), indent));

        var extends = "";
        if (baseTypeName != null)
        {
            extends = " extends " + baseTypeName;
        }

        // Only inner classes can be declared static in Java.
        var header = type.IsStatic && type.ContainingType != null
          ? StaticClassDeclarationHeader : ClassDeclarationHeader;
        s.Append($"{indent}{header} {type.Name}{extends} {{");

        CopyConstructor(s, indent + "    ", type, imports);

        var serializedNameTagImportAdded = false;
        foreach (var member in type.GetMembers()
            .Where((m) => m is IPropertySymbol || m is IFieldSymbol field))
        {
            if (member.DeclaredAccessibility != Accessibility.Public &&
                (enumClass || member.DeclaredAccessibility != Accessibility.Internal))
            {
                continue;
            }

            var property = member as IPropertySymbol;
            var field = member as IFieldSymbol;

            if (field != null && !field.IsConst)
            {
                continue;
            }

            s.AppendLine();
            s.Append(FormatDocComment(member.GetDocumentationCommentXml(), indent + "    ", GetJavaDoc(member)));
            if (GetObsoleteAttribute(member) != null)
            {
               s.AppendLine($"{indent}    {DeprecatedTag}");
            }

            var memberType = (property?.Type ?? field!.Type).ToDisplayString();
            var isNullable = memberType.EndsWith("?");
            if (isNullable)
            {
                memberType = memberType.Substring(0, memberType.Length - 1);
            }

            var accessMod = member.DeclaredAccessibility == Accessibility.Public ? "public " : "";
            var staticKeyword = member.IsStatic ? "static " : "";
            var finalKeyword = field?.IsConst == true || property?.IsReadOnly == true ? "final " : "";
            var javaName = ToCamelCase(member.Name);
            var javaType = GetJavaTypeForCSType(memberType, javaName, imports);

            // Static properties in a non-static class are linked to the non-generated *Statics.java class.
            var value = field?.IsConst != true && member.IsStatic && !staticClass ?
                $"{type.Name}Statics.{javaName}" : GetMemberInitializer(member);

            if (!member.IsStatic && field?.IsConst != true) {
                if (property.TryGetJsonPropertyName(out var jsonPropertyName))
                {
                    s.AppendLine($"{indent}    {string.Format(SerializedNameTagFormat, jsonPropertyName)}");
                    if (!serializedNameTagImportAdded)
                    {
                        imports.Add(SerializedNameType);
                        serializedNameTagImportAdded = true;
                    }
                }

                s.AppendLine($"{indent}    {GsonExposeTag}");
            }

            if (value != null && !value.Equals("null") && !value.Equals("null!"))
            {
                s.AppendLine($"{indent}    {accessMod}{staticKeyword}{finalKeyword}{javaType} {javaName} = {value};");
            }
            else
            {
                // Uninitialized java fields are null by default.
                s.AppendLine($"{indent}    {accessMod}{staticKeyword}{finalKeyword}{javaType} {javaName};");
            }
        }

        foreach (var method in type.GetMembers().OfType<IMethodSymbol>()) {
            if (method.IsStatic && method.MethodKind == MethodKind.Ordinary && method.DeclaredAccessibility == Accessibility.Public) {
                s.AppendLine();
                s.Append(FormatDocComment(method.GetDocumentationCommentXml(), indent + "    "));
                if (GetObsoleteAttribute(method) != null)
                {
                    s.AppendLine($"{indent}    {DeprecatedTag}");
                }
                var javaName = ToCamelCase(method.Name);
                var javaReturnType = GetJavaTypeForCSType(method.ReturnType.ToDisplayString(), javaName, imports);

                var parameters = new Dictionary<String, String>() { };
                foreach (var parameter in method.Parameters)
                {
                    var parameterType = parameter.Type.ToDisplayString();
                    var javaParameterName = ToCamelCase(parameter.Name);
                    var javaParameterType = GetJavaTypeForCSType(parameterType, javaName, imports);
                    parameters.Add(javaParameterName, javaParameterType);
                }
                var parameterString = String.Join(", ", parameters.Select(p => String.Format("{0} {1}", p.Value, p.Key)));
                var returnKeyword = javaReturnType != "void" ? "return " : "";

                s.AppendLine($"{indent}    public static {javaReturnType} {javaName}({parameterString}) {{");
                s.AppendLine($"{indent}        {returnKeyword}{type.Name}Statics.{javaName}({String.Join(", ", parameters.Keys)});");
                s.AppendLine($"{indent}    }}");
            }
        }

        WriteNestedTypes(s, indent, type, imports);
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
            s.Append(FormatDocComment(field.GetDocumentationCommentXml(), indent + "    ", GetJavaDoc(member)));

            if (member != null && GetObsoleteAttribute(member) != null)
            {
                s.AppendLine($"{indent}    {DeprecatedTag}");
            }

            s.AppendLine($"{indent}    {string.Format(SerializedNameTagFormat, field.Name)}");
            s.AppendLine($"{indent}    {field.Name},");
        }
        s.AppendLine($"{indent}}}");
    }

    private void CopyConstructor(
        StringBuilder s,
        string indent,
        ITypeSymbol type,
        SortedSet<string> imports)
    {
        foreach (var method in type.GetMembers().OfType<IMethodSymbol>())
        {
            if (method.Name == ".ctor")
            {
                // We assume that
                // (1) the constructor only performs property assignments and
                // (2) the property and parameter names match.
                // Then we simply do those assignments.
                var parameters = new Dictionary<String, String>() { };
                foreach (var parameter in method.Parameters)
                {
                    var parameterType = parameter.Type.ToDisplayString();
                    var javaName = ToCamelCase(parameter.Name);
                    var javaType = GetJavaTypeForCSType(parameterType, javaName, imports);
                    parameters.Add(javaName, javaType);
                }
                // No need to write the default constructor.
                if (parameters.Count == 0)
                {
                    return;
                }
                s.AppendLine();
                var parameterString = parameters.Select(p => String.Format("{0} {1}", p.Value, p.Key));
                s.Append($"{indent}{type.Name} ({String.Join(", ", parameterString)}) {{");
                s.AppendLine();
                foreach (String parameter in parameters.Keys)
                {
                    s.AppendLine($"{indent}    this.{parameter} = {parameter};");
                }
                s.AppendLine($"{indent}}}");
            }
        }
    }

    internal static string ToCamelCase(string name)
    {
        return name.Substring(0, 1).ToLowerInvariant() + name.Substring(1);
    }

    private string FormatDocComment(string? comment, string indent, List<string>? javaDoc = null)
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

        if (javaDoc != null)
        {
            foreach (var line in javaDoc)
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

        // Attempt to convert the CS expression to a Java expression. This involes several
        // weak assumptions, and will not work for many kinds of expressions. But it might
        // be good enough.
        var javaExpression = csExpression
            .Replace("new Regex", $"{RegexPatternType}.compile")
            .Replace("Replace", "replace");

        // Assume any PascalCase identifiers are referncing other variables in scope.
        javaExpression = new Regex("(?<= |\\()([A-Z][a-z]+){2,6}\\b(?!\\()").Replace(
            javaExpression, (m) =>
            {
                return (member.ContainingType.MemberNames.Contains(m.Value) ?
                    member.ContainingType.Name + "." : string.Empty) + ToCamelCase(m.Value);
            });

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

        if (csType.EndsWith("?"))
        {
            csType = csType.Substring(0, csType.Length - 1);
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
                "void" => "void",
                "bool" => "boolean",
                "short" => "short",
                "ushort" => "int",
                "int" => "int",
                "uint" => "int",
                "long" => "long",
                "ulong" => "long",
                "string" => "String",
                "System.DateTime" => JavaDateTimeType,
                "System.Text.RegularExpressions.Regex" => RegexPatternType,
                "System.Collections.Generic.IDictionary<string, string>"
                    => $"java.util.Map<String, String>",
                "System.Collections.Generic.IDictionary<string, string[]>"
                    => $"java.util.Map<String, String[]>",
                "System.Uri" => "java.net.URI",
                "System.Collections.Generic.IEnumerable<string>" => "java.util.Collection<String>",
                _ => throw new NotSupportedException("Unsupported C# type: " + csType),
            };
        }

        if (javaType.Contains('.')) {
            imports.Add(javaType.Split('<')[0]);
            javaType = javaType.Split('.').Last();
        }
        javaType += suffix;
        return javaType;
    }

    private static List<string> GetJavaDoc(ISymbol symbol)
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
