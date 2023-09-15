using System.Linq;
using Microsoft.CodeAnalysis;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.DevTunnels.Generator;

internal static class LanguageExtensions
{
    public static bool TryGetJsonPropertyName(this IPropertySymbol? property, out string? jsonPropertyName)
    {
        if (property?.GetAttributes().FirstOrDefault((a) => a.AttributeClass?.Name == "JsonPropertyNameAttribute") is AttributeData propertyNameAttribute &&
            propertyNameAttribute.ConstructorArguments.FirstOrDefault().Value?.ToString() is string result &&
            !string.IsNullOrEmpty(result))
        {
            jsonPropertyName = result;
            return true;
        }

        jsonPropertyName = null;
        return false;
    }
}
