using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using SankhyaAPI.Client.Generators;

namespace SankhyaAPI.Client.Generators.Tests;

/// <summary>
/// Source generator de acessores e diagnósticos SNK (#17).
/// </summary>
public class GeradorTests
{
    private const string Usings = """
        using System;
        using System.Xml.Serialization;
        using SankhyaAPI.Client;
        using SankhyaAPI.Client.Mapping;
        using SankhyaAPI.Client.Sql;
        """;

    private static CSharpCompilation Compilar(string codigo)
    {
        IEnumerable<MetadataReference> referencias = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(p => MetadataReference.CreateFromFile(p))
            .Append(MetadataReference.CreateFromFile(typeof(SankhyaClient).Assembly.Location));

        return CSharpCompilation.Create(
            "Teste",
            [CSharpSyntaxTree.ParseText(Usings + codigo, new CSharpParseOptions(LanguageVersion.Latest))],
            referencias,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
    }

    private static (Compilation Saida, ImmutableArray<SyntaxTree> Gerados) Gerar(string codigo)
    {
        CSharpCompilation compilacao = Compilar(codigo);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new AccessorGenerator()).RunGeneratorsAndUpdateCompilation(compilacao, out Compilation saida, out _);
        return (saida, driver.GetRunResult().GeneratedTrees);
    }

    private static async Task<ImmutableArray<Diagnostic>> Analisar(string codigo) =>
        (await Compilar(codigo).WithAnalyzers([new SankhyaAnalyzer()]).GetAnalyzerDiagnosticsAsync())
        .Where(d => d.Id.StartsWith("SNK"))
        .ToImmutableArray();

    private const string ModeloComMapa = """
        public class Pedido { public int Numero { get; set; } public string? Obs { get; set; } public DateTime? Quando { get; set; } public int SoLeitura { get; } }
        public sealed class PedidoMap : SankhyaEntityMap<Pedido>
        {
            public PedidoMap()
            {
                Entity("Pedido");
                Key(x => x.Numero, "NUM").AutoNumbered();
                Property(x => x.Obs, "OBS");
                Computed(x => x.Quando).Writes("DT", SankhyaType.Date).Reads<DateOnly?>("DT", d => d?.ToDateTime(TimeOnly.MinValue));
            }
        }
        """;

    [Fact]
    public void Gera_acessores_para_modelo_de_mapa_fluente_e_para_modelo_com_atributos()
    {
        (Compilation saida, ImmutableArray<SyntaxTree> gerados) = Gerar(ModeloComMapa + """
            [SankhyaEntity("Usuario")] public class Usuario { [Key("CODUSU")] public NullableState<int> CodUsu { get; set; } }
            """);

        string codigo = string.Join("\n", gerados.Select(t => t.ToString()));
        Assert.Equal(2, gerados.Length);
        Assert.Contains("SankhyaAccessorRegistry.Register<global::Pedido>", codigo);
        Assert.Contains("new(\"Numero\", static o => ((global::Pedido)o).Numero, static (o, v) => ((global::Pedido)o).Numero = (int)v)", codigo);
        Assert.Contains("new(\"SoLeitura\", static o => ((global::Pedido)o).SoLeitura, null)", codigo);
        Assert.Contains("SankhyaAccessorRegistry.Register<global::Usuario>", codigo);
        Assert.Contains("[global::System.Runtime.CompilerServices.ModuleInitializer]", codigo);
        Assert.Empty(saida.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));
    }

    [Fact]
    public async Task Modelo_inacessivel_nao_gera_e_recebe_SNK010()
    {
        const string codigo = """
            public class Externo
            {
                private class Privado { public int Id { get; set; } }
                private sealed class PrivadoMap : SankhyaEntityMap<Privado> { public PrivadoMap() { Entity("X"); Key(x => x.Id, "ID"); } }
            }
            """;

        Assert.Empty(Gerar(codigo).Gerados);
        Assert.Contains(await Analisar(codigo), d => d.Id == "SNK010");
    }

    [Theory]
    [InlineData("SNK001", """
        public class M { public int A { get; set; } public int B { get; set; } }
        public sealed class MMap : SankhyaEntityMap<M> { public MMap() { Entity("M"); Key(x => x.A, "A").AutoNumbered(); Key(x => x.B, "B").AutoNumbered(); } }
        """, true)]
    [InlineData("SNK001", """
        public class M { public int A { get; set; } public int B { get; set; } }
        public sealed class MMap : SankhyaEntityMap<M> { public MMap() { Entity("M"); Key(x => x.A, "A"); Key(x => x.B, "B").AutoNumbered(); } }
        """, false)]
    [InlineData("SNK001", """
        [SankhyaEntity("M")] public class M { [Key("A", true)] public int A { get; set; } [Key("B", true)] public int B { get; set; } }
        """, true)]
    [InlineData("SNK002", """
        public class M { public int A { get; set; } public int B { get; set; } }
        public sealed class MMap : SankhyaEntityMap<M> { public MMap() { Entity("M"); Key(x => x.A, "A"); Property(x => x.B, "A"); } }
        """, true)]
    [InlineData("SNK002", """
        public class M { public int A { get; set; } public int B { get; set; } }
        public sealed class MMap : SankhyaEntityMap<M> { public MMap() { Entity("M"); Key(x => x.A, "A"); Property(x => x.B, "B"); } }
        """, false)]
    [InlineData("SNK002", """
        [SankhyaEntity("M")] public class M { [Key("A")] public int A { get; set; } [XmlElement("A")] public int B { get; set; } }
        """, true)]
    [InlineData("SNK003", """
        public class M { public int A { get; set; } public Uri? B { get; set; } }
        public sealed class MMap : SankhyaEntityMap<M> { public MMap() { Entity("M"); Key(x => x.A, "A"); Property(x => x.B, "B"); } }
        """, true)]
    [InlineData("SNK003", """
        public class M { public int A { get; set; } public Uri? B { get; set; } }
        public sealed class MMap : SankhyaEntityMap<M> { public MMap() { Entity("M"); Key(x => x.A, "A"); Property(x => x.B, "B", SankhyaType.Text); } }
        """, false)]
    [InlineData("SNK004", """
        public class M { public int A { get; set; } public DateTime? B { get; set; } }
        public sealed class MMap : SankhyaEntityMap<M> { public MMap() { Entity("M"); Key(x => x.A, "A"); Computed(x => x.B).Writes("D", SankhyaType.Date); } }
        """, true)]
    [InlineData("SNK004", """
        public class M { public int A { get; set; } public DateTime? B { get; set; } }
        public sealed class MMap : SankhyaEntityMap<M> { public MMap() { Entity("M"); Key(x => x.A, "A"); Computed(x => x.B).Writes("D", SankhyaType.Date).Reads<DateTime?>("D", d => d); } }
        """, false)]
    [InlineData("SNK005", """
        public class C { public void F(ISankhyaSql sql, string nome) { sql.QueryAsDictionaryAsync("SELECT * FROM T WHERE N = '" + nome + "'"); } }
        """, true)]
    [InlineData("SNK005", """
        public class C { public void F(ISankhyaSql sql, string nome) { sql.QueryAsDictionaryAsync($"SELECT * FROM T WHERE N = {nome}"); sql.QueryAsDictionaryAsync("SELECT 1"); } }
        """, false)]
    [InlineData("SNK006", """
        public class M { public NullableState<string> A { get; set; } }
        public class C { public void F(M m) { m.A = null; } }
        """, true)]
    [InlineData("SNK006", """
        public class M { public NullableState<string> A { get; set; } }
        public class C { public void F(M m) { m.A = NullableState<string>.Clear(); m.A = "x"; } }
        """, false)]
    public async Task Diagnostico_dispara_so_quando_deve(string id, string codigo, bool dispara)
    {
        ImmutableArray<Diagnostic> diagnosticos = await Analisar(codigo);

        Assert.Equal(dispara, diagnosticos.Any(d => d.Id == id));
    }
}
