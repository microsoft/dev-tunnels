using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace Microsoft.VsSaaS.TunnelService.Generator;

[Generator]
public class ContractsGenerator : ISourceGenerator
{
    public void Execute(GeneratorExecutionContext context)
    {
        var test = new StringBuilder();

        var thisAssemblyType = context.Compilation.GetSymbolsWithName(
            nameof(ThisAssembly), SymbolFilter.Type).Single();

        // This assembly path will be like:
        //   cs/bin/obj/[projectname]/Release/net6.0/[assemblyname].Version.cs
        var thisAssemblyPath = thisAssemblyType.Locations.Single().GetLineSpan().Path;
        var repoRoot = Path.GetFullPath(Path.Combine(
            thisAssemblyPath, "..", "..", "..", "..", "..", "..", ".."));
        test.Append(repoRoot + " ");

        var writers = new List<ContractWriter>();
        foreach (var language in ContractWriter.SupportedLanguages)
        {
            writers.Add(ContractWriter.Create(
                language, repoRoot, "Microsoft.VsSaaS.TunnelService.Contracts"));
        }

        var typeNames = context.Compilation.Assembly.TypeNames;
        foreach (var typeName in typeNames)
        {
            if (typeName == nameof(ThisAssembly) || typeName == "Converter")
            {
                continue;
            }

            var types = context.Compilation.GetSymbolsWithName(typeName, SymbolFilter.Type);
            if (types.Count() != 1)
            {
                throw new Exception($"Found {types.Count()} instances of type named '{typeName}'.");
            }

            var type = (ITypeSymbol)types.Single();
            var path = type.Locations.Single().GetLineSpan().Path;

            if (type.ContainingType != null)
            {
                // Contained types will be processed via their containing type.
                continue;
            }

            if (type.GetMembers().OfType<IMethodSymbol>().Any((m) =>
                m.MethodKind == MethodKind.Ordinary && m.Name != "ToString"))
            {
                // Don't try to generate types with ordinary non-ToString() methods.
                continue;
            }

            foreach (var writer in writers)
            {
                writer.WriteContract(type);
            }
        }
    }

    public void Initialize(GeneratorInitializationContext context)
    {
    }
}
