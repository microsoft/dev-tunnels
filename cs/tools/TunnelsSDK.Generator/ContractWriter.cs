// <copyright file="ContractWriter.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;

namespace Microsoft.DevTunnels.Generator;

internal abstract class ContractWriter
{
    private static readonly Regex paragraphBreakRegex = new Regex(@" *\<para */\> *");

    protected readonly string repoRoot;
    protected readonly string csNamespace;

    public static string[] SupportedLanguages { get; } = new[]
    {
        "TypeScript",
        "Go",
        "Java",
        "Rust"
    };

    public static ContractWriter Create(string language, string repoRoot, string csNamespace)
    {
        return language switch
        {
            "TypeScript" => new TSContractWriter(repoRoot, csNamespace),
            "Go" => new GoContractWriter(repoRoot, csNamespace),
            "Java" => new JavaContractWriter(repoRoot, csNamespace),
            "Rust" => new RustContractWriter(repoRoot, csNamespace),
            _ => throw new NotSupportedException("Unsupported contract language: " + language),
        };
    }

    protected ContractWriter(string repoRoot, string csNamespace)
    {
        this.repoRoot = repoRoot;
        this.csNamespace = csNamespace;
    }

    public abstract void WriteContract(ITypeSymbol type, ICollection<ITypeSymbol> allTypes);

    public virtual void WriteCompleted()
    {
    }

    protected string GetAbsolutePath(string relativePath)
    {
        return Path.Combine(this.repoRoot, relativePath);
    }

    protected string GetRelativePath(string absolutePath)
    {
        if (absolutePath.StartsWith(this.repoRoot))
        {
            return absolutePath.Substring(this.repoRoot.Length + 1).Replace("\\", "/");
        }

        return absolutePath;
    }

    protected static IEnumerable<string> WrapComment(string comment, int wrapColumn)
    {
        var isFirst = true;
        foreach (var paragraph in paragraphBreakRegex.Split(comment))
        {
            if (isFirst)
            {
                isFirst = false;
            }
            else
            {
                // Insert a blank line between paragraphs.
                yield return string.Empty;
            }

            comment = paragraph;
            while (comment.Length > wrapColumn)
            {
                var i = wrapColumn;
                while (i > 0 && comment[i] != ' ')
                {
                    i--;
                }

                if (i == 0)
                {
                    i = comment.IndexOf(' ');
                }

                yield return comment.Substring(0, i).TrimEnd();
                comment = comment.Substring(i + 1);
            }

            yield return comment.TrimEnd();
        }
    }

    protected static AttributeData? GetAttribute(ISymbol symbol, string attributeName)
    {
        return symbol.GetAttributes().FirstOrDefault((a) => a.AttributeClass?.Name == attributeName);
    }

    protected static AttributeData? GetObsoleteAttribute(ISymbol symbol)
    {
        return GetAttribute(symbol, nameof(ObsoleteAttribute));
    }

    protected static string? GetObsoleteMessage(AttributeData? obsoleteAttribute)
    {
        return obsoleteAttribute?.ConstructorArguments.FirstOrDefault().Value?.ToString();
    }
}
