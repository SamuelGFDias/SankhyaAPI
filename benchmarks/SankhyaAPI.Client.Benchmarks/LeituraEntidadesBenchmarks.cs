using System.Xml.Linq;
using BenchmarkDotNet.Attributes;
using SankhyaAPI.Client.Mapping;
using SankhyaAPI.Client.Repositories;

namespace SankhyaAPI.Client.Benchmarks;

/// <summary>
/// Leitura de uma resposta de loadRecords (<c>EntityXml.ReadEntities</c>): o mesmo XML lido pelo modelo por
/// atributos e pelo POCO com mapa fluente.
/// </summary>
[MemoryDiagnoser]
public class LeituraEntidadesBenchmarks
{
    [Params(1000, 10000)] public int Linhas;

    private XElement _resposta = null!;
    private EntityMap _mapaAtributos = null!;
    private EntityMap _mapaFluente = null!;

    [GlobalSetup]
    public void Preparar()
    {
        Dados.RegistrarMapas();
        _mapaAtributos = SankhyaMaps.For(typeof(ProdutoAtributos));
        _mapaFluente = SankhyaMaps.For(typeof(ProdutoPoco));
        _resposta = Dados.RespostaEntities(Linhas);
    }

    [Benchmark(Baseline = true, Description = "Atributos")]
    public int LerAtributos() => EntityXml.ReadEntities<ProdutoAtributos>(_mapaAtributos, _resposta).Items.Count;

    [Benchmark(Description = "Mapa fluente")]
    public int LerFluente() => EntityXml.ReadEntities<ProdutoPoco>(_mapaFluente, _resposta).Items.Count;
}
