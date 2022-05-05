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

namespace Microsoft.VsSaaS.TunnelService.Generator;

internal class GoContractWriter : ContractWriter
{

    public GoContractWriter(string repoRoot, string csNamespace) : base(repoRoot, csNamespace)
    {
    }

    public override void WriteContract(ITypeSymbol type, ICollection<ITypeSymbol> allTypes)
    {
        var csFilePath = GetRelativePath(type.Locations.Single().GetLineSpan().Path);

        var fileName = ToSnakeCase(type.Name) + ".go";
        var filePath = GetAbsolutePath(Path.Combine("go", fileName));

        var s = new StringBuilder();
        s.AppendLine("// Copyright (c) Microsoft Corporation.");
        s.AppendLine("// Licensed under the MIT license.");
        s.AppendLine($"// Generated from ../../../{csFilePath}");
        s.AppendLine();
        s.AppendLine("package tunnels");
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
            importsString.AppendLine("import (");

            foreach (var import in imports)
            {
                importsString.AppendLine($"\t\"{import}\"");
            }

            importsString.AppendLine(")");
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
        if (type.BaseType?.Name == nameof(Enum) || members.All((m) =>
            (m is IFieldSymbol field &&
             ((field.IsConst && field.Type.Name == nameof(String)) || field.Name == "All")) ||
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
            // Derived type interfaces will be generated along with the base type.
            if (type.BaseType != null && type.BaseType.Name != nameof(Object))
            {
                return false;
            }

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
        s.Append(FormatDocComment(type.GetDocumentationCommentXml(), ""));
        s.Append($"type {type.Name} struct {{");

        var derivedTypes = allTypes.Where(
            (t) => SymbolEqualityComparer.Default.Equals(t.BaseType, type)).ToArray();

        var properties = type.GetMembers()
            .OfType<IPropertySymbol>()
            .Where((p) => !p.IsStatic)
            .ToArray();
        var maxPropertyNameLength = properties.Length == 0 ? 0 :
            properties.Select((p) => p.Name.Length).Max();
        foreach (var property in properties)
        {
            var propertyName = FixPropertyNameCasing(property.Name);

            s.AppendLine();
            s.Append(FormatDocComment(property.GetDocumentationCommentXml(), "\t"));
            var alignment = new string(' ', maxPropertyNameLength - propertyName.Length);
            var propertyType = property.Type.ToDisplayString();
            var goType = GetGoTypeForCSType(propertyType, property, imports);
            var jsonTag = GetJsonTagForProperty(property);
            s.AppendLine($"\t{propertyName}{alignment} {goType} `json:\"{jsonTag}\"`");
        }

        if (derivedTypes.Length > 0)
        {
            s.AppendLine();
            foreach (var derivedType in derivedTypes.OrderBy((t) => t.Name))
            {
                s.AppendLine($"\t{derivedType.Name}");
            }
        }

        s.AppendLine("}");

        foreach (var derivedType in derivedTypes.OrderBy((t) => t.Name))
        {
            s.AppendLine();
            WriteInterfaceContract(s, derivedType, imports, allTypes);
        }

        foreach (var field in type.GetMembers().OfType<IFieldSymbol>()
            .Where((f) => f.IsConst))
        {
            var fieldName = FixPropertyNameCasing(field.Name);
            if (field.DeclaredAccessibility == Accessibility.Internal)
            {
                fieldName = TSContractWriter.ToCamelCase(fieldName);
            }
            else if (field.DeclaredAccessibility != Accessibility.Public)
            {
                continue;
            }

            s.AppendLine();
            s.Append(FormatDocComment(field.GetDocumentationCommentXml(), ""));
            s.AppendLine($"var {fieldName} = \"{field.ConstantValue}\"");
        }

    }

    private void WriteEnumContract(StringBuilder s, ITypeSymbol type)
    {
        s.Append(FormatDocComment(type.GetDocumentationCommentXml(), ""));

        string typeName = type.Name;
        if (type.ContainingType != null)
        {
            typeName = type.ContainingType.Name + typeName;
        }

        if (typeName.EndsWith("s"))
        {
            var pluralTypeName = typeName;
            typeName = typeName.Substring(0, typeName.Length - 1);
            s.AppendLine($"type {pluralTypeName} []{typeName}");
        }

        s.AppendLine($"type {typeName} string");
        s.AppendLine();
        s.Append("const (");

        var fields = type.GetMembers()
            .OfType<IFieldSymbol>()
            .Where((f) => f.HasConstantValue)
            .ToArray();
        var maxFieldNameLength = fields.Length == 0 ? 0 : fields.Select((p) => p.Name.Length).Max();
        foreach (var field in fields)
        {
            s.AppendLine();
            s.Append(FormatDocComment(field.GetDocumentationCommentXml(), "\t"));
            var alignment = new string(' ', maxFieldNameLength - field.Name.Length);
            var value = type.BaseType?.Name == "Enum" ?
                TSContractWriter.ToCamelCase(field.Name) : field.ConstantValue;
            s.AppendLine($"\t{typeName}{field.Name}{alignment} {typeName} = \"{value}\"");
        }

        s.AppendLine(")");

    }

    private void WriteStaticClassContract(
        StringBuilder s,
        ITypeSymbol type,
        SortedSet<string> imports)
    {
        var constLines = new List<string>();
        var varLines = new List<string>();

        foreach (var property in type.GetMembers().OfType<IPropertySymbol>())
        {
            if (!property.IsStatic || !property.IsReadOnly)
            {
                continue;
            }

            var propertyName = FixPropertyNameCasing(property.Name);
            var value = GetPropertyInitializer(type, property);

            if (value != null)
            {
                foreach (var package in new[] { "strings", "strconv", "regexp" })
                {
                    if (value.Contains(package + ".") && !imports.Contains(package))
                    {
                        imports.Add(package);
                    }
                }

                var lines = (value.All((c) => char.IsDigit(c)) ||
                    (value.StartsWith("\"") && value.EndsWith("\""))) ? constLines : varLines;
                lines.Add(FormatDocComment(property.GetDocumentationCommentXml(), "\t")
                    .Replace("// Gets a ", "// A ").TrimEnd());
                lines.Add($"\t{type.Name}{propertyName} = {value}");
                lines.Add(string.Empty);
            }
        }

        if (constLines.Count > 0)
        {
            constLines.RemoveAt(constLines.Count - 1);
            s.AppendLine("const (");
            constLines.ForEach((line) => s.AppendLine(line));
            s.AppendLine(")");
        }

        if (varLines.Count > 0)
        {
            varLines.RemoveAt(varLines.Count - 1);
            s.AppendLine("var (");
            varLines.ForEach((line) => s.AppendLine(line));
            s.AppendLine(")");
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

        return s.ToString();
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

    private static string? GetPropertyInitializer(ITypeSymbol type, IPropertySymbol property)
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

        // Attempt to convert the CS expression to a Go expression. This involes several
        // weak assumptions, and will not work for many kinds of expressions. But it might
        // be good enough.
        var goExpression = csExpression.Replace("new Regex", "regexp.MustCompile");
        goExpression = new Regex("(\\w+)\\.Replace\\(([^,]*), ([^)]*)\\)")
            .Replace(goExpression, "strings.Replace($1, $2, $3, -1)");

        // Assume any PascalCase identifiers are referncing other variables in scope.
        // Contvert integer constants to strings, allowing for integer offsets.
        goExpression = new Regex("([A-Z][a-z]+){2,4}\\b(?!\\()").Replace(
            goExpression, (m) => $"{type.Name}{m.Value}");
        goExpression = new Regex("\\(([A-Z][a-z]+){4,7} - \\d\\)").Replace(
            goExpression, (m) => $"strconv.Itoa{m.Value}");
        goExpression = new Regex("\\b([A-Z][a-z]+){3,6}Length\\b(?! - \\d)").Replace(
            goExpression, (m) => $"strconv.Itoa({m.Value})");
        goExpression = new Regex("\"(([^\"]*\\\\)+[^\"]*)\"").Replace(
            goExpression, (m) => $"`{m.Groups[1].Value.Replace("\\\\", "\\")}`");
        goExpression = FixPropertyNameCasing(goExpression);
        goExpression = goExpression.Replace("        ", "\t\t");

        return goExpression;
    }

    private string GetJsonTagForProperty(IPropertySymbol property)
    {
        var tag = TSContractWriter.ToCamelCase(property.Name);

        var jsonIgnoreAttribute = property.GetAttributes()
            .SingleOrDefault((a) => a.AttributeClass!.Name == "JsonIgnoreAttribute");
        if (jsonIgnoreAttribute != null)
        {
            if (jsonIgnoreAttribute.NamedArguments.Length != 1 ||
                jsonIgnoreAttribute.NamedArguments[0].Key != "Condition")
            {
                // TODO: Diagnostic
                throw new ArgumentException("JsonIgnoreAttribute must have a condition argument.");
            }

            tag += ",omitempty";
        }

        return tag;
    }

    private static string FixPropertyNameCasing(string propertyName)
    {
        propertyName = propertyName.Replace("Id", "ID");
        propertyName = propertyName.Replace("Uri", "URI");
        return propertyName;
    }

    private string GetGoTypeForCSType(string csType, IPropertySymbol property, SortedSet<string> imports)
    {
        var isNullable = csType.EndsWith("?");
        if (isNullable)
        {
            csType = csType.Substring(0, csType.Length - 1);
        }

        var prefix = "";
        if (csType.EndsWith("[]"))
        {
            prefix = "[]";
            csType = csType.Substring(0, csType.Length - 2);
            isNullable = false;
        }

        string goType;
        if (csType.StartsWith(this.csNamespace + "."))
        {
            goType = csType.Substring(csNamespace.Length + 1);
        }
        else
        {
            goType = csType switch
            {
                "bool" => "bool",
                "short" => "int16",
                "ushort" => "uint16",
                "int" => "int32",
                "uint" => "uint32",
                "long" => "int64",
                "ulong" => "uint64",
                "string" => "string",
                "System.DateTime" => "time.Time",
                "System.Text.RegularExpressions.Regex" => "regexp.Regexp",
                "System.Collections.Generic.IDictionary<string, string>"
                    => $"map[{(property.Name == "AccessTokens" ? "TunnelAccessScope" : "string")}]string",
                "System.Collections.Generic.IDictionary<string, string[]>" => "map[string][]string",
                _ => throw new NotSupportedException("Unsupported C# type: " + csType),
            };

            if (!goType.Contains("."))
            {
                // Struct members of type string and other basic types, arrays, and maps are
                // conventionally not represented as pointers in Go. An implication is that partial
                // resource updates may require custom marshalling to omit non-updated fields.
                isNullable = false;
            }
        }

        if (isNullable)
        {
            prefix = "*" + prefix;
        }

        if (goType.Contains('.'))
        {
            var package = goType.Split('.')[0];
            if (!imports.Contains(package))
            {
                imports.Add(package);
            }
        }

        goType = prefix + goType;
        return goType;
    }
}
