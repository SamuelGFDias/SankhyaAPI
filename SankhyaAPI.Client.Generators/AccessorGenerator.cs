using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SankhyaAPI.Client.Generators;

/// <summary>
/// Gera acessores diretos (sem reflexão nem compilação de expressões) para os modelos mapeados: classes usadas
/// em <c>SankhyaEntityMap&lt;T&gt;</c> e classes com <c>[SankhyaEntity]</c>. O código gerado se registra em
/// <c>SankhyaAccessorRegistry</c> num <c>[ModuleInitializer]</c>.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class AccessorGenerator : IIncrementalGenerator
{
    internal const string EntityMapMetadataName = "SankhyaAPI.Client.Mapping.SankhyaEntityMap`1";
    internal const string EntityAttributeMetadataName = "SankhyaAPI.Client.Mapping.SankhyaEntityAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        IncrementalValuesProvider<ModelInfo?> fromMaps = context.SyntaxProvider.CreateSyntaxProvider(
            static (node, _) => node is ClassDeclarationSyntax { BaseList: not null },
            static (ctx, ct) => ModelFromMap((INamedTypeSymbol?)ctx.SemanticModel.GetDeclaredSymbol(ctx.Node, ct)));

        IncrementalValuesProvider<ModelInfo?> fromAttributes = context.SyntaxProvider.ForAttributeWithMetadataName(
            EntityAttributeMetadataName,
            static (node, _) => node is ClassDeclarationSyntax,
            static (ctx, _) => (ModelInfo?)Model((INamedTypeSymbol)ctx.TargetSymbol));

        IncrementalValueProvider<ImmutableArray<ModelInfo>> models = fromMaps.Collect()
            .Combine(fromAttributes.Collect())
            .Select(static (pair, _) => pair.Left.Concat(pair.Right)
                                            .Where(m => m != null)
                                            .Select(m => m!)
                                            .GroupBy(m => m.FullName)
                                            .Select(g => g.First())
                                            .ToImmutableArray());

        context.RegisterSourceOutput(models, static (spc, list) =>
        {
            foreach (ModelInfo model in list)
            {
                if (!model.Accessible) continue;
                spc.AddSource($"SankhyaAccessors.{model.HintName}.g.cs", Render(model));
            }
        });
    }

    private static ModelInfo? ModelFromMap(INamedTypeSymbol? symbol)
    {
        if (symbol is null || symbol.IsAbstract) return null;
        INamedTypeSymbol? model = MappedType(symbol);
        return model is null ? null : Model(model);
    }

    internal static INamedTypeSymbol? MappedType(INamedTypeSymbol symbol)
    {
        for (INamedTypeSymbol? type = symbol.BaseType; type != null; type = type.BaseType)
        {
            if (type.IsGenericType
             && type.ConstructedFrom.ToDisplayString() == "SankhyaAPI.Client.Mapping.SankhyaEntityMap<T>"
             && type.TypeArguments[0] is INamedTypeSymbol model)
                return model;
        }

        return null;
    }

    internal static bool IsAccessible(ITypeSymbol type)
    {
        for (ISymbol? s = type; s is INamedTypeSymbol t; s = t.ContainingType)
        {
            if (t.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Internal or Accessibility.ProtectedOrInternal)) return false;
        }

        return true;
    }

    private static ModelInfo Model(INamedTypeSymbol type)
    {
        bool hasCtor = type.InstanceConstructors.Any(c => c.Parameters.Length == 0 && c.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal);
        var properties = new List<PropertyInfo>();
        var seen = new HashSet<string>();
        for (INamedTypeSymbol? t = type; t != null && t.SpecialType != SpecialType.System_Object; t = t.BaseType)
        {
            foreach (IPropertySymbol p in t.GetMembers().OfType<IPropertySymbol>())
            {
                if (p.IsStatic || p.IsIndexer || p.GetMethod is null || !seen.Add(p.Name)) continue;
                if (p.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Internal)) continue;
                bool settable = p.SetMethod is { IsInitOnly: false } set && set.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal;
                properties.Add(new PropertyInfo(p.Name, p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), settable));
            }
        }

        string fullName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        return new ModelInfo(
            fullName,
            Hint(type),
            IsAccessible(type) && hasCtor && !type.IsAbstract && !type.IsGenericType,
            properties.ToImmutableArray());
    }

    private static string Hint(INamedTypeSymbol type)
    {
        var sb = new StringBuilder();
        foreach (char c in type.ToDisplayString()) sb.Append(char.IsLetterOrDigit(c) ? c : '_');
        return sb.ToString();
    }

    private static string Render(ModelInfo model)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/> SankhyaAPI.Client.Generators");
        sb.AppendLine("#nullable disable");
        sb.AppendLine("namespace SankhyaAPI.Client.Generated");
        sb.AppendLine("{");
        sb.AppendLine($"    internal static class SankhyaAccessors_{model.HintName}");
        sb.AppendLine("    {");
        sb.AppendLine("        [global::System.Runtime.CompilerServices.ModuleInitializer]");
        sb.AppendLine("        internal static void Register()");
        sb.AppendLine("        {");
        sb.AppendLine($"            global::SankhyaAPI.Client.Mapping.SankhyaAccessorRegistry.Register<{model.FullName}>(");
        sb.AppendLine($"                static () => new {model.FullName}(),");
        sb.AppendLine("                new global::SankhyaAPI.Client.Mapping.SankhyaPropertyAccessor[]");
        sb.AppendLine("                {");
        foreach (PropertyInfo p in model.Properties)
        {
            string set = p.Settable ? $"static (o, v) => (({model.FullName})o).{p.Name} = ({p.Type})v" : "null";
            sb.AppendLine($"                    new(\"{p.Name}\", static o => (({model.FullName})o).{p.Name}, {set}),");
        }

        sb.AppendLine("                });");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private sealed record PropertyInfo(string Name, string Type, bool Settable);

    private sealed record ModelInfo(string FullName, string HintName, bool Accessible, ImmutableArray<PropertyInfo> Properties)
    {
        public bool Equals(ModelInfo? other) =>
            other != null && FullName == other.FullName && Accessible == other.Accessible && Properties.SequenceEqual(other.Properties);

        public override int GetHashCode() => FullName.GetHashCode();
    }
}
