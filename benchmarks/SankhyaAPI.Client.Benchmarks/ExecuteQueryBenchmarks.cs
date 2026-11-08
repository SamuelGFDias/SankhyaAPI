using System.Text.Json;
using BenchmarkDotNet.Attributes;
using SankhyaAPI.Client.Sql;

namespace SankhyaAPI.Client.Benchmarks;

/// <summary>
/// Mapeamento das linhas de um executeQuery (<c>SankhyaSqlClient.Map</c>). O JSON é lido uma vez no GlobalSetup,
/// então a medição é só o mapeamento, sem o parse da resposta.
/// </summary>
[MemoryDiagnoser]
public class ExecuteQueryBenchmarks
{
    [Params(1000, 10000)] public int Linhas;

    private JsonDocument _documento = null!;

    [GlobalSetup]
    public void Preparar()
    {
        Dados.RegistrarMapas();
        _documento = JsonDocument.Parse(Dados.RespostaExecuteQuery(Linhas));
    }

    [GlobalCleanup]
    public void Limpar() => _documento.Dispose();

    [Benchmark(Baseline = true, Description = "Atributos")]
    public int MapearAtributos() => SankhyaSqlClient.Map<ProdutoLinha>(_documento).Count;

    [Benchmark(Description = "Mapa fluente")]
    public int MapearFluente() => SankhyaSqlClient.Map<ProdutoPoco>(_documento).Count;
}
