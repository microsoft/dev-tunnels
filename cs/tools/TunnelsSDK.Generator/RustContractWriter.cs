// <copyright file="GoContractWriter.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using Microsoft.CodeAnalysis;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Microsoft.VsSaaS.TunnelService.Generator;

internal class RustContractWriter : ContractWriter
{

    public RustContractWriter(string repoRoot, string csNamespace) : base(repoRoot, csNamespace)
    {
    }

    public override void WriteContract(ITypeSymbol type, ICollection<ITypeSymbol> allTypes)
    {
        var csFilePath = GetRelativePath(type.Locations.Single().GetLineSpan().Path);

        var fileName = ToSnakeCase(type.Name) + ".rs";
        var filePath = GetAbsolutePath(Path.Combine("rs/src/contracts", fileName));

        var s = new StringBuilder();
        s.AppendLine("// Copyright (c) Microsoft Corporation.");
        s.AppendLine("// Licensed under the MIT license.");
        s.AppendLine($"// Generated from ../../../{csFilePath}");
        s.AppendLine();

        var importsOffset = s.Length;
        var imports = new SortedSet<string>();

        if (!WriteContractType(s, type, imports, allTypes))
        {
            return;
        }

        imports.Remove(type.Name);
        if (imports.Count > 0)
        {
            var importsString = new StringBuilder();

            foreach (var import in imports)
            {
                importsString.AppendLine($"use {import};");
            }

            importsString.AppendLine();
            s.Insert(importsOffset, importsString.ToString());
        }

        if (!Directory.Exists(Path.GetDirectoryName(filePath)))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath));
        }

        File.WriteAllText(filePath, s.ToString());
    }

    private bool WriteContractType(
        StringBuilder s,
        ITypeSymbol type,
        SortedSet<string> imports,
        ICollection<ITypeSymbol> allTypes)
    {
        var members = type.GetMembers();
        if (type.BaseType?.Name == nameof(Enum))
        {
            WriteEnumContract(s, type, imports);
        }
        else if (type.IsStatic && members.All((m) => m.IsStatic))
        {
            WriteStaticClassContract(s, type, imports);
        }
        else
        {
            WriteInterfaceContract(s, type, imports, allTypes);
        }

        var nestedTypes = type.GetTypeMembers()
            .Where((t) => !ContractsGenerator.ExcludedContractTypes.Contains(t.Name))
            .ToArray();
        foreach (var nestedType in nestedTypes.Where(
            (t) => !ContractsGenerator.ExcludedContractTypes.Contains(t.Name)))
        {
            s.AppendLine();
            WriteContractType(s, nestedType, imports, allTypes);
        }

        return true;
    }

    private void WriteInterfaceContract(
        StringBuilder s,
        ITypeSymbol type,
        SortedSet<string> imports,
        ICollection<ITypeSymbol> allTypes)
    {
        imports.Add("serde::{Serialize, Deserialize}");

        s.Append(FormatDocComment(type.GetDocumentationCommentXml(), ""));
        s.AppendLine("#[derive(Serialize, Deserialize)]");
        s.AppendLine("#[serde(rename_all(serialize = \"camelCase\", deserialize = \"camelCase\"))]");
        s.Append($"pub struct {type.Name} {{");

        var properties = type.GetMembers()
            .OfType<IPropertySymbol>()
            .Where((p) => !p.IsStatic)
            .ToArray();
        var maxPropertyNameLength = properties.Length == 0 ? 0 :
            properties.Select((p) => ToSnakeCase(p.Name).Length).Max();
        foreach (var property in properties)
        {
            s.AppendLine();
            s.Append(FormatDocComment(property.GetDocumentationCommentXml(), "    "));

            var propertyName = ToSnakeCase(property.Name);
            if (propertyName == "type" && type.Name.EndsWith("Entry"))
            {
                propertyName = "entry_type";
                s.AppendLine("    #[serde(rename = \"type\")]");
            }

            var alignment = new string(' ', maxPropertyNameLength - propertyName.Length);
            var propertyType = property.Type.ToDisplayString();
            var rsType = GetRustTypeForCSType(propertyType, property, imports);
            s.AppendLine($"    {propertyName}: {rsType},");
        }

        s.AppendLine("}");

        foreach (var field in type.GetMembers().OfType<IFieldSymbol>()
            .Where((f) => f.IsConst))
        {
            if (field.DeclaredAccessibility != Accessibility.Public &&
                field.DeclaredAccessibility != Accessibility.Internal)
            {
                continue;
            }

            if (field.ConstantValue is string value)
            {
                s.AppendLine();
                s.Append(FormatDocComment(field.GetDocumentationCommentXml(), ""));
                var rsName = ToSnakeCase(field.Name).ToUpperInvariant();
                s.AppendLine($"const {rsName}: &str = \"{value}\";");
            }
        }

    }

    private void WriteEnumContract(
        StringBuilder s,
        ITypeSymbol type,
        SortedSet<string> imports)
    {
        imports.Add("serde::{Serialize, Deserialize}");

        s.Append(FormatDocComment(type.GetDocumentationCommentXml(), ""));

        s.AppendLine("#[derive(Serialize, Deserialize)]");
        s.Append($"pub enum {type.Name} {{");

        var fields = type.GetMembers()
            .OfType<IFieldSymbol>()
            .Where((f) => f.HasConstantValue)
            .ToArray();
        var maxFieldNameLength = fields.Length == 0 ? 0 : fields.Select((p) => p.Name.Length).Max();
        foreach (var field in fields)
        {
            s.AppendLine();
            s.Append(FormatDocComment(field.GetDocumentationCommentXml(), "    "));
            var alignment = new string(' ', maxFieldNameLength - field.Name.Length);
            var value = type.BaseType?.Name == "Enum" ? field.Name : field.ConstantValue;
            s.AppendLine($"    {field.Name},");
        }

        s.AppendLine("}");

    }

    private void WriteStaticClassContract(
        StringBuilder s,
        ITypeSymbol type,
        SortedSet<string> imports)
    {
        s.Append(FormatDocComment(type.GetDocumentationCommentXml(), ""));

        var fields = type.GetMembers()
            .OfType<IFieldSymbol>()
            .Where((f) => f.HasConstantValue)
            .ToArray();
        foreach (var field in fields)
        {
            if (field.IsConst && field.ConstantValue is string value)
            {
                s.AppendLine();
                s.Append(FormatDocComment(field.GetDocumentationCommentXml(), ""));
                var rsName = ToSnakeCase(field.Name).ToUpperInvariant();
                s.AppendLine($"const {rsName}: &str = \"{value}\";");
            }
        }
    }

    private static string ToSnakeCase(string name)
    {
        var s = new StringBuilder(name);

        for (int i = 0; i < s.Length; i++)
        {
            if (char.IsUpper(s[i]))
            {
                if (i > 0)
                {
                    s.Insert(i, '_');
                    i++;
                }

                s[i] = char.ToLowerInvariant(s[i]);
            }
        }

        return s.ToString().Replace("i_p", "ip").Replace("i_d", "id").Replace("git_hub", "github");
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
            .Replace(comment, (m) => $"`{m.Groups[2].Value}.{m.Groups[3].Value}`");
        comment = new Regex($"<see cref=\".:({this.csNamespace}\\.)?([^\"]+)\" ?/>")
            .Replace(comment, "`$2`");

        var summary = new Regex("<summary>(.*)</summary>").Match(comment).Groups[1].Value.Trim();
        var remarks = new Regex("<remarks>(.*)</remarks>").Match(comment).Groups[1].Value.Trim();

        var s = new StringBuilder();

        foreach (var commentLine in WrapComment(summary, 90 - 3 - prefix.Length))
        {
            s.AppendLine(prefix + "// " + commentLine);
        }

        if (!string.IsNullOrEmpty(remarks))
        {
            s.AppendLine(prefix + "//");
            foreach (var commentLine in WrapComment(remarks, 90 - 3 - prefix.Length))
            {
                s.AppendLine(prefix + "// " + commentLine);
            }
        }

        return s.ToString();
    }

    private string GetRustTypeForCSType(string csType, IPropertySymbol property, SortedSet<string> imports)
    {
        var isNullable = csType.EndsWith("?");
        if (isNullable)
        {
            csType = csType.Substring(0, csType.Length - 1);
        }

        var isArray = csType.EndsWith("[]");
        if (isArray)
        {
            csType = csType.Substring(0, csType.Length - 2);
            isNullable = false;
        }

        string rsType;
        if (csType.StartsWith(this.csNamespace + "."))
        {
            rsType = csType.Substring(csNamespace.Length + 1);
            imports.Add($"crate::contracts::{rsType}");
        }
        else
        {
            rsType = csType switch
            {
                "bool" => "bool",
                "short" => "i16",
                "ushort" => "u16",
                "int" => "i32",
                "uint" => "u32",
                "long" => "i64",
                "ulong" => "u64",
                "string" => "String",
                "System.DateTime" => "DateTime<Utc>",
                "System.Text.RegularExpressions.Regex" => "regexp.Regexp",
                "System.Collections.Generic.IDictionary<string, string>"
                    => "HashMap<String, String>",
                "System.Collections.Generic.IDictionary<string, string[]>"
                    => "HashMap<String, Vec<String>>",
                _ => throw new NotSupportedException("Unsupported C# type: " + csType),
            };
        }

        if (rsType.Length == 3 && rsType.StartsWith("i") || rsType.StartsWith("u"))
        {
            imports.Add($"std::{rsType}");
        }

        if (isArray)
        {
            rsType = $"Vec<{rsType}>";
        }

        if (isNullable)
        {
            rsType = $"Option<{rsType}>";
        }

        if (csType == "System.DateTime")
        {
            imports.Add("chrono::{DateTime, Utc}");
        }
        else if (csType.Contains("IDictionary<"))
        {
            imports.Add("std::collections::HashMap");
        }

        return rsType;
    }
}
