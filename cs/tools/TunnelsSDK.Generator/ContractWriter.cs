﻿using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Microsoft.VsSaaS.TunnelService.Generator;

internal abstract class ContractWriter
{
    protected readonly string repoRoot;
    protected readonly string csNamespace;

    public static string[] SupportedLanguages { get; } = new[]
    {
        "ts",
    };

    public static ContractWriter Create(string language, string repoRoot, string csNamespace)
    {
        return language switch
        {
            "ts" => new TSContractWriter(repoRoot, csNamespace),
            _ => throw new NotSupportedException("Unsupported contract language: " + language),
        };
    }

    protected ContractWriter(string repoRoot, string csNamespace)
    {
        this.repoRoot = repoRoot;
        this.csNamespace = csNamespace;
    }

    public abstract void WriteContract(ITypeSymbol type);

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

            yield return comment.Substring(0, i);
            comment = comment.Substring(i + 1);
        }

        yield return comment;
    }
}