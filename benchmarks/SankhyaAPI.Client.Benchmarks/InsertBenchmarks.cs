using System.Xml.Linq;
using BenchmarkDotNet.Attributes;
using SankhyaAPI.Client.Mapping;
using SankhyaAPI.Client.Repositories;

namespace SankhyaAPI.Client.Benchmarks;

/// <summary>
/// Montagem do envelope de insert (<c>EntityXml.Insert</c>) para N registros, nos dois modelos.
/// </summary>
[MemoryDiagnoser]
public class InsertBenchmarks
{
    [Params(1000, 10000)] public int Linhas;

    private EntityMap _mapaAtributos = null!;
    private EntityMap _mapaFluente = null!;
    private IReadOnlyList<object> _atributos = null!;
    private IReadOnlyList<object> _pocos = null!;

    [GlobalSetup]
    public void Preparar()
    {
        Dados.RegistrarMapas();
        _mapaAtributos = SankhyaMaps.For(typeof(ProdutoAtributos));
        _mapaFluente = SankhyaMaps.For(typeof(ProdutoPoco));
        _atributos = Dados.Atributos(Linhas).Cast<object>().ToList();
        _pocos = Dados.Pocos(Linhas).Cast<object>().ToList();
    }

    [Benchmark(Baseline = true, Description = "Atributos")]
    public XElement MontarAtributos() => EntityXml.Insert(_mapaAtributos, "Produto", _atributos).First();

    [Benchmark(Description = "Mapa fluente")]
    public XElement MontarFluente() => EntityXml.Insert(_mapaFluente, "Produto", _pocos).First();
}
