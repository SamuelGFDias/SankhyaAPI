using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace SankhyaAPI.Client.Generators;

/// <summary>
/// Diagnósticos de compilação para mapas e uso do pacote (erros que, sem eles, só apareceriam em tempo de execução).
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SankhyaAnalyzer : DiagnosticAnalyzer
{
    private const string Category = "SankhyaAPI";

    public static readonly DiagnosticDescriptor MoreThanOneAutoNumbered = new(
        "SNK001", "Mais de um campo auto-numerado na chave",
        "O mapa de '{0}' tem mais de um campo auto-numerado na chave; só um pode ser gerado pelo servidor",
        Category, DiagnosticSeverity.Error, true);

    public static readonly DiagnosticDescriptor DuplicateColumn = new(
        "SNK002", "Coluna repetida no mapa",
        "A coluna '{0}' é escrita mais de uma vez no mapa de '{1}'",
        Category, DiagnosticSeverity.Error, true);

    public static readonly DiagnosticDescriptor UnknownConversion = new(
        "SNK003", "Tipo sem conversão conhecida",
        "A propriedade '{0}' é do tipo '{1}', sem conversão conhecida para o Sankhya; informe um SankhyaType ou um ISankhyaConverter",
        Category, DiagnosticSeverity.Warning, true);

    public static readonly DiagnosticDescriptor ComputedOneWay = new(
        "SNK004", "Campo calculado sem leitura",
        "O campo calculado '{0}' escreve no Sankhya mas não tem Reads(...): ele nunca será preenchido nas consultas",
        Category, DiagnosticSeverity.Warning, true);

    public static readonly DiagnosticDescriptor ConcatenatedSql = new(
        "SNK005", "SQL montado por concatenação",
        "SQL passado como string não constante; use uma string interpolada com os valores entre chaves para eles serem escapados, ou SankhyaSql.Raw para trechos fixos",
        Category, DiagnosticSeverity.Warning, true);

    public static readonly DiagnosticDescriptor NullToNullableState = new(
        "SNK006", "null atribuído a NullableState",
        "Atribuir null a NullableState<{0}> apaga o campo no update; use NullableState<{0}>.Clear() para apagar ou não atribua para manter",
        Category, DiagnosticSeverity.Warning, true);

    public static readonly DiagnosticDescriptor NotGenerated = new(
        "SNK010", "Mapa sem acessores gerados",
        "O modelo '{0}' não é acessível ao código gerado (privado, abstrato, genérico ou sem construtor público sem parâmetros); o mapa usa compilação em tempo de execução, que não é compatível com AOT",
        Category, DiagnosticSeverity.Info, true);

    private static readonly HashSet<string> KnownTypes =
    [
        "string", "char", "System.Guid", "bool", "System.DateOnly", "System.DateTime", "System.DateTimeOffset", "System.TimeOnly",
        "System.TimeSpan", "decimal", "double", "float", "int", "long", "short", "byte", "uint", "ulong", "ushort", "sbyte"
    ];

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(MoreThanOneAutoNumbered, DuplicateColumn, UnknownConversion, ComputedOneWay, ConcatenatedSql, NullToNullableState, NotGenerated);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolStartAction(MapStart, SymbolKind.NamedType);
        context.RegisterSymbolAction(AttributeModel, SymbolKind.NamedType);
        context.RegisterOperationAction(SqlArgument, OperationKind.Argument);
        context.RegisterOperationAction(NullAssignment, OperationKind.Conversion);
    }

    private static void MapStart(SymbolStartAnalysisContext context)
    {
        var map = (INamedTypeSymbol)context.Symbol;
        INamedTypeSymbol? model = AccessorGenerator.MappedType(map);
        if (model is null || map.IsAbstract) return;

        var autoNumbered = new List<Location>();
        var written = new Dictionary<string, int>();
        var computedWithoutReads = new List<(string Name, Location Location)>();

        context.RegisterOperationAction(op =>
        {
            var invocation = (IInvocationOperation)op.Operation;
            string name = invocation.TargetMethod.Name;
            if (!FromMapApi(invocation.TargetMethod)) return;

            switch (name)
            {
                case "AutoNumbered":
                    lock (autoNumbered) autoNumbered.Add(invocation.Syntax.GetLocation());
                    break;
                case "Key" or "Property" when ColumnArgument(invocation, 1) is { } column:
                    lock (written) written[column] = written.TryGetValue(column, out int n) ? n + 1 : 1;
                    CheckConversion(op, invocation);
                    break;
                case "Writes" when ColumnArgument(invocation, 0) is { } column:
                    lock (written) written[column] = written.TryGetValue(column, out int n) ? n + 1 : 1;
                    break;
                case "Computed" when !ChainCalls(invocation).Contains("Reads") && !ChainCalls(invocation).Contains("ReadsRaw"):
                    lock (computedWithoutReads) computedWithoutReads.Add((PropertyName(invocation) ?? "?", invocation.Syntax.GetLocation()));
                    break;
            }
        }, OperationKind.Invocation);

        context.RegisterSymbolEndAction(end =>
        {
            if (autoNumbered.Count > 1)
                end.ReportDiagnostic(Diagnostic.Create(MoreThanOneAutoNumbered, autoNumbered[1], model.Name));
            foreach (KeyValuePair<string, int> column in written.Where(c => c.Value > 1))
                end.ReportDiagnostic(Diagnostic.Create(DuplicateColumn, map.Locations.FirstOrDefault(), column.Key, model.Name));
            foreach ((string name, Location location) in computedWithoutReads)
                end.ReportDiagnostic(Diagnostic.Create(ComputedOneWay, location, name));
            if (!AccessorGenerator.IsAccessible(model) || model.IsAbstract || model.IsGenericType
             || !model.InstanceConstructors.Any(c => c.Parameters.Length == 0 && c.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal))
                end.ReportDiagnostic(Diagnostic.Create(NotGenerated, map.Locations.FirstOrDefault(), model.Name));
        });
    }

    /// <summary>Método de <c>SankhyaEntityMap&lt;T&gt;</c> ou de um dos seus builders aninhados.</summary>
    private static bool FromMapApi(IMethodSymbol method)
    {
        for (INamedTypeSymbol? t = method.ContainingType; t != null; t = t.ContainingType)
            if (t.OriginalDefinition.ToDisplayString() == "SankhyaAPI.Client.Mapping.SankhyaEntityMap<T>")
                return true;
        return false;
    }

    private static void AttributeModel(SymbolAnalysisContext context)
    {
        var type = (INamedTypeSymbol)context.Symbol;
        if (!type.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == "SankhyaAPI.Client.Mapping.SankhyaEntityAttribute")) return;

        var autoKeys = new List<IPropertySymbol>();
        var columns = new Dictionary<string, int>();
        foreach (IPropertySymbol p in type.GetMembers().OfType<IPropertySymbol>())
        {
            foreach (AttributeData a in p.GetAttributes())
            {
                string? attr = a.AttributeClass?.ToDisplayString();
                if (attr == "SankhyaAPI.Client.Mapping.KeyAttribute")
                {
                    if (a.ConstructorArguments.Length > 1 && a.ConstructorArguments[1].Value is true) autoKeys.Add(p);
                    if (a.ConstructorArguments.Length > 0 && a.ConstructorArguments[0].Value is string k) columns[k] = columns.TryGetValue(k, out int n) ? n + 1 : 1;
                }
                else if (attr == "System.Xml.Serialization.XmlElementAttribute" && a.ConstructorArguments.Length > 0 && a.ConstructorArguments[0].Value is string c)
                {
                    columns[c] = columns.TryGetValue(c, out int n) ? n + 1 : 1;
                }
            }
        }

        if (autoKeys.Count > 1)
            context.ReportDiagnostic(Diagnostic.Create(MoreThanOneAutoNumbered, autoKeys[1].Locations.FirstOrDefault(), type.Name));
        foreach (KeyValuePair<string, int> column in columns.Where(c => c.Value > 1))
            context.ReportDiagnostic(Diagnostic.Create(DuplicateColumn, type.Locations.FirstOrDefault(), column.Key, type.Name));
    }

    private static void SqlArgument(OperationAnalysisContext context)
    {
        var argument = (IArgumentOperation)context.Operation;
        if (argument.Parameter?.Type.ToDisplayString() != "SankhyaAPI.Client.Sql.SankhyaSql") return;
        if (argument.Value is IConversionOperation { Operand: { } operand } conversion
         && conversion.OperatorMethod?.Name == "op_Implicit"
         && operand.Type?.SpecialType == SpecialType.System_String
         && !operand.ConstantValue.HasValue)
            context.ReportDiagnostic(Diagnostic.Create(ConcatenatedSql, operand.Syntax.GetLocation()));
    }

    private static void NullAssignment(OperationAnalysisContext context)
    {
        var conversion = (IConversionOperation)context.Operation;
        if (conversion.Type is not INamedTypeSymbol { IsGenericType: true } target
         || target.ConstructedFrom.ToDisplayString() != "SankhyaAPI.Client.Mapping.NullableState<T>") return;
        if (conversion.Operand.ConstantValue is { HasValue: true, Value: null }
         || conversion.Operand is IConversionOperation { Operand.ConstantValue: { HasValue: true, Value: null } }
         || conversion.Operand is IDefaultValueOperation { Type.IsReferenceType: true })
            context.ReportDiagnostic(Diagnostic.Create(NullToNullableState, conversion.Syntax.GetLocation(), target.TypeArguments[0].ToDisplayString()));
    }

    private static void CheckConversion(OperationAnalysisContext context, IInvocationOperation invocation)
    {
        if (invocation.Arguments.Length > 2 && !invocation.Arguments[2].IsImplicit) return; // SankhyaType ou conversor explícito
        if (invocation.TargetMethod.TypeArguments.FirstOrDefault() is not { } type) return;
        if (IsKnown(type)) return;
        context.ReportDiagnostic(Diagnostic.Create(UnknownConversion, invocation.Syntax.GetLocation(), PropertyName(invocation) ?? "?", type.ToDisplayString()));
    }

    private static bool IsKnown(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol { IsGenericType: true } named
         && (named.ConstructedFrom.SpecialType == SpecialType.System_Nullable_T
          || named.ConstructedFrom.ToDisplayString() == "SankhyaAPI.Client.Mapping.NullableState<T>"))
            return IsKnown(named.TypeArguments[0]);
        if (type.TypeKind == TypeKind.Enum) return true;
        return KnownTypes.Contains(type.WithNullableAnnotation(NullableAnnotation.NotAnnotated).ToDisplayString());
    }

    private static string? ColumnArgument(IInvocationOperation invocation, int index) =>
        invocation.Arguments.FirstOrDefault(a => a.Parameter?.Ordinal == index)?.Value.ConstantValue.Value as string;

    private static string? PropertyName(IInvocationOperation invocation) =>
        invocation.Arguments.FirstOrDefault()?.Value is IDelegateCreationOperation { Target: IAnonymousFunctionOperation f }
         && f.Body.Operations.FirstOrDefault() is IReturnOperation { ReturnedValue: IPropertyReferenceOperation p }
            ? p.Property.Name
            : null;

    /// <summary>Nomes dos métodos encadeados depois da chamada (<c>Computed(...).Writes(...).Reads(...)</c>).</summary>
    private static HashSet<string> ChainCalls(IInvocationOperation invocation)
    {
        var names = new HashSet<string>();
        IOperation current = invocation;
        while (current.Parent is IInvocationOperation parent && ReferenceEquals(parent.Instance, current))
        {
            names.Add(parent.TargetMethod.Name);
            current = parent;
        }

        return names;
    }
}
