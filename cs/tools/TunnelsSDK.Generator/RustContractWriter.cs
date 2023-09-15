// <copyright file="GoContractWriter.cs" company="Microsoft">
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

internal class RustContractWriter : ContractWriter
{
    // extra, non-generated modules that should be imported but not exported:
    private readonly List<string> ImportModules = new List<string>()
    {
    };

    // extra, non-generated modules that should be exported:
    private readonly List<string> ExportModules = new List<string>()
    {
        "tunnel_environments",
    };

    private static readonly ISet<string> DefaultDerivers = new HashSet<string>()
    {
        "Tunnel",
        "TunnelPort",
    };

    public RustContractWriter(string repoRoot, string csNamespace) : base(repoRoot, csNamespace)
    {
    }

    public override void WriteContract(ITypeSymbol type, ICollection<ITypeSymbol> allTypes)
    {
        var csFilePath = GetRelativePath(type.Locations.Single().GetLineSpan().Path);

        var moduleName = ToSnakeCase(type.Name);
        this.ExportModules.Add(moduleName);

        var fileName = ToSnakeCase(type.Name) + ".rs";
        var filePath = GetAbsolutePath(Path.Combine("rs/src/contracts", fileName));

        var s = new StringBuilder();
        this.AppendFileHeader(s, $"../../../{csFilePath}");

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

    public override void WriteCompleted()
    {
        this.WriteModRs();
    }

    private void WriteModRs()
    {
        var filePath = GetAbsolutePath("rs/src/contracts/mod.rs");
        var s = new StringBuilder();
        this.AppendFileHeader(s, "RustContractWriter.cs");

        this.ExportModules.Sort();
        this.ImportModules.Sort();

        foreach (var mod in this.ExportModules)
        {
            s.AppendLine($"mod {mod};");
        }
        foreach (var mod in this.ImportModules)
        {
            s.AppendLine($"mod {mod};");
        }

        s.AppendLine();

        foreach (var mod in this.ExportModules)
        {
            s.AppendLine($"pub use {mod}::*;");
        }

        File.WriteAllText(filePath, s.ToString());
    }

    private void AppendFileHeader(StringBuilder s, string generatedFrom)
    {
        s.AppendLine("// Copyright (c) Microsoft Corporation.");
        s.AppendLine("// Licensed under the MIT license.");
        s.AppendLine($"// Generated from {generatedFrom}");
        s.AppendLine();
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

    private void WriteResourceStatusSerializer(StringBuilder s)
    {
        s.AppendLine("#[derive(Clone, Debug, Deserialize, Serialize)]");
        s.AppendLine("#[serde(untagged)]");
        s.AppendLine("pub enum ResourceStatus {");
        s.AppendLine("    Detailed(DetailedResourceStatus),");
        s.AppendLine("    Count(u32),");
        s.AppendLine("}");
        s.AppendLine("impl ResourceStatus {");
        s.AppendLine("    pub fn get_count(&self) -> u64 {");
        s.AppendLine("        match self {");
        s.AppendLine("            ResourceStatus::Detailed(d) => d.current,");
        s.AppendLine("            ResourceStatus::Count(c) => (*c).into(),");
        s.AppendLine("        }");
        s.AppendLine("    }");
        s.AppendLine("}");
    }

    private void WriteInterfaceContract(
        StringBuilder s,
        ITypeSymbol type,
        SortedSet<string> imports,
        ICollection<ITypeSymbol> allTypes)
    {
        imports.Add("serde::{Deserialize, Serialize}");

        var rsName = type.Name;
        if (rsName == "ResourceStatus")
        {
            WriteResourceStatusSerializer(s);
            rsName = "DetailedResourceStatus";
        }

        s.Append(FormatDocComment(type.GetDocumentationCommentXml(), ""));
        s.Append("#[derive(Clone, Debug, Deserialize, Serialize");
        if (DefaultDerivers.Contains(rsName))
        {
            s.Append(", Default");
        }
        s.AppendLine(")]");
        s.AppendLine("#[serde(rename_all(serialize = \"camelCase\", deserialize = \"camelCase\"))]");
        s.Append($"pub struct {rsName} {{");

        var fullBaseType = type.BaseType?.ToString();
        if (fullBaseType != null && fullBaseType.StartsWith(this.csNamespace))
        {
            var rsBaseType = fullBaseType.Substring(this.csNamespace.Length + 1);
            s.AppendLine();
            s.AppendLine("    #[serde(flatten)]");
            s.AppendLine($"    pub base: {rsBaseType},");
            imports.Add($"crate::contracts::{rsBaseType}");
        }

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
            AppendStructProperty(type, property, imports, s);
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
                var fieldRsName = ToSnakeCase(field.Name).ToUpperInvariant();
                s.AppendLine($"pub const {fieldRsName}: &str = \"{value}\";");
            }
        }

    }

    private void WriteEnumContract(
        StringBuilder s,
        ITypeSymbol type,
        SortedSet<string> imports)
    {
        imports.Add("serde::{Deserialize, Serialize}");
        imports.Add("std::fmt");

        s.Append(FormatDocComment(type.GetDocumentationCommentXml(), ""));

        s.AppendLine("#[derive(Clone, Debug, Deserialize, Serialize)]");
        s.Append($"pub enum {type.Name} {{");

        var display = new StringBuilder();
        display.AppendLine($"impl fmt::Display for {type.Name} {{");
        display.AppendLine("    fn fmt(&self, f: &mut fmt::Formatter) -> fmt::Result {");
        display.AppendLine("        match *self {");

        var fields = type.GetMembers()
            .OfType<IFieldSymbol>()
            .Where((f) => f.HasConstantValue)
            .ToArray();
        var maxFieldNameLength = fields.Length == 0 ? 0 : fields.Select((p) => p.Name.Length).Max();
        foreach (var field in fields)
        {
            s.AppendLine();
            s.Append(FormatDocComment(field.GetDocumentationCommentXml(), "    "));

            if (GetObsoleteAttribute(field) != null)
            {
                s.AppendLine($"    {GetRustDeprecatedAttribute(field)}");
            }

            var alignment = new string(' ', maxFieldNameLength - field.Name.Length);
            var value = type.BaseType?.Name == "Enum" ? field.Name : field.ConstantValue;
            s.AppendLine($"    {field.Name},");
            display.AppendLine($"            {type.Name}::{field.Name} => write!(f, \"{field.Name}\"),");
        }

        s.AppendLine("}");

        display.AppendLine("        }");
        display.AppendLine("    }");
        display.AppendLine("}");

        s.AppendLine();
        s.Append(display);
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
        foreach (var field in fields
            .Where((f) => f.IsConst && f.DeclaredAccessibility == Accessibility.Public))
        {
            string? memberExpression = null;
            if (field.ConstantValue is string stringValue)
            {
                memberExpression = $"&str = r#\"{stringValue}\"#";
            }
            else if (field.ConstantValue is int)
            {
                memberExpression = $"i32 = {field.ConstantValue}";
            }

            if (memberExpression != null)
            {
                // The type name prefix can be long and redundant. Skip it for certain classes
                // that have sufficiently disticnt member names.
                var prefix = type.Name switch
                {
                    "TunnelConstraints" or "TunnelHeaderNames" => string.Empty,
                    _ => type.Name,
                };

                s.AppendLine();
                s.Append(FormatDocComment(field.GetDocumentationCommentXml(), ""));

                if (GetObsoleteAttribute(field) != null)
                {
                    s.AppendLine(GetRustDeprecatedAttribute(field));
                }

                var rsName = ToSnakeCase($"{prefix}{field.Name}").ToUpperInvariant();
                s.AppendLine($"pub const {rsName}: {memberExpression};");
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
    private void AppendStructProperty(ITypeSymbol parentType, IPropertySymbol property, SortedSet<string> imports, StringBuilder s)
    {
        var csType = property.Type.ToString();
        var isNullable = csType.EndsWith("?");
        if (isNullable)
        {
            csType = csType.Substring(0, csType.Length - 1);
        }

        // Detect JsonIgnoreCondition.WhenWritingDefault
        var ignoreWhenDefault = property.GetAttributes().Any(ad =>
            ad.AttributeClass?.Name == "JsonIgnoreAttribute" && ad.NamedArguments.Any(arg => arg.Value.Value is int i && i == 2));

        var serdeDeclarations = new List<string>();

        if (property.TryGetJsonPropertyName(out var jsonPropertyName))
        {
            serdeDeclarations.Add($"rename = \"{jsonPropertyName}\"");
        }

        var isArray = csType.EndsWith("[]");
        if (isArray)
        {
            csType = csType.Substring(0, csType.Length - 2);
            if (isNullable || ignoreWhenDefault)
            {
                serdeDeclarations.Add("skip_serializing_if = \"Vec::is_empty\"");
                serdeDeclarations.Add("default");
                isNullable = false;
                ignoreWhenDefault = false;
            }
        }

        if (ignoreWhenDefault)
        {
            serdeDeclarations.Add("default");
        }

        if (serdeDeclarations.Count > 0)
        {
            s.AppendLine($"    #[serde({string.Join(", ", serdeDeclarations)})]");
        }

        // todo@connor4312: the service currently returns a non-standard format
        // for these fields, serialize them as strings until that's fixed.
        if (property.Name == "LastClientConnectionTime" || property.Name == "LastHostConnectionTime")
        {
            csType = "string";
        }

        string rsType;
        if (csType.StartsWith(this.csNamespace + "."))
        {
            rsType = csType.Substring(csNamespace.Length + 1);
            if (rsType != parentType.Name)
            {
                imports.Add($"crate::contracts::{rsType}");
            }
            else if (!isArray)
            {
                // Use box for a recursive type
                // https://doc.rust-lang.org/book/ch15-01-box.html#enabling-recursive-types-with-boxes
                // Serde supports boxes, fixed in https://github.com/serde-rs/serde/issues/45
                rsType = $"Box<{rsType}>";
            }
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


        var propertyName = ToSnakeCase(property.Name);
        if (propertyName == "type")
        {
            propertyName = "kind";
            s.AppendLine("    #[serde(rename = \"type\")]");
        }

        s.AppendLine($"    pub {propertyName}: {rsType},");
    }

    private static string? GetRustDeprecatedAttribute(ISymbol symbol)
    {
        var obsoleteAttribute = GetObsoleteAttribute(symbol);
        if (obsoleteAttribute != null)
        {
            var message = GetObsoleteMessage(obsoleteAttribute);
            return $"[deprecated({message})]";
        }

        return null;
    }
}
