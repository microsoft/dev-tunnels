﻿// <copyright file="ContractsGenerator.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Microsoft.VsSaaS.TunnelService.Generator;

[Generator]
public class ContractsGenerator : ISourceGenerator
{
    private const string DiagnosticPrefix = "TUN";
    private const string DiagnosticCategory = "Tunnels";
    private const string ContractsNamespace = "Microsoft.VsSaaS.TunnelService.Contracts";
    internal static readonly string[] ExcludedContractTypes = new[]
    {
        "TunnelContracts",
        "ThisAssembly",
        "Converter",
    };

    public void Initialize(GeneratorInitializationContext context)
    {
#if DEBUG
        // Note source generators re not covered by normal debugging,
        // because the generator runs at build time, not at application run-time.
        // Un-comment the line below to enable debugging at build time.

        ////System.Diagnostics.Debugger.Launch();
#endif
    }

    public void Execute(GeneratorExecutionContext context)
    {
        // Path of the ThisAssembly type's location will be like:
        //   cs/bin/obj/[projectname]/Release/net6.0/[assemblyname].Version.cs
        var thisAssemblyType = context.Compilation.GetSymbolsWithName(
            nameof(ThisAssembly), SymbolFilter.Type).Single();
        var thisAssemblyPath = thisAssemblyType.Locations.Single().GetLineSpan().Path;
        var repoRoot = Path.GetFullPath(Path.Combine(
            thisAssemblyPath, "..", "..", "..", "..", "..", "..", ".."));

        var writers = new List<ContractWriter>();
        foreach (var language in ContractWriter.SupportedLanguages)
        {
            writers.Add(ContractWriter.Create(language, repoRoot, ContractsNamespace));
        }

        var typeNames = context.Compilation.Assembly.TypeNames;
        var types = typeNames
            .SelectMany((t) => context.Compilation.GetSymbolsWithName(t, SymbolFilter.Type))
            .OfType<ITypeSymbol>()
            .ToArray();
        foreach (var type in types)
        {
            if (ExcludedContractTypes.Contains(type!.Name))
            {
                continue;
            }
            else if (type.ContainingType != null)
            {
                // Nested types will be written as part of their containing type.
                continue;
            }

            var path = type.Locations.Single().GetLineSpan().Path;

            foreach (var method in type.GetMembers().OfType<IMethodSymbol>()
                .Where((m) => m.MethodKind == MethodKind.Ordinary))
            {
                if (!method.IsStatic &&
                    method.Name != "ToString" &&
                    method.Name != "GetEnumerator")
                {
                    var title = "Tunnel contracts must not have instance methods other than " +
                            "GetEnumerator() or ToString().";
                    var descriptor = new DiagnosticDescriptor(
                        id: DiagnosticPrefix + "1000",
                        title,
                        messageFormat: title + " Generated contract interfaces cannot support " +
                            "instance methods. Consider converting the method to static " +
                            "or other refactoring.",
                        DiagnosticCategory,
                        DiagnosticSeverity.Error,
                        isEnabledByDefault: true);
                    context.ReportDiagnostic(
                        Diagnostic.Create(descriptor, method.Locations.Single()));
                }
            }

            foreach (var writer in writers)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                writer.WriteContract(type, types);
            }
        }
    }
}
