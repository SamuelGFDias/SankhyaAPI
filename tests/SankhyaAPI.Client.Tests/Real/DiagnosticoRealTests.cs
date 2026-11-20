using SankhyaAPI.Client.Diagnostics;

namespace SankhyaAPI.Client.Tests.Real;

/// <summary>
/// Diagnóstico de índices contra a homologação: lê só o catálogo do SQL Server e o dicionário (TDDINS).
/// </summary>
[Trait("Category", "Real")]
public class DiagnosticoRealTests
{
    [RealFact]
    public async Task FKs_sem_indice_que_referenciam_o_contato_vem_da_maior_filha_para_a_menor()
    {
        await using SankhyaClient cliente = SankhyaReal.Cliente(SessionMode.Shared);

        IReadOnlyList<SankhyaIndexSuggestion> sugestoes = await cliente.Diagnostics.FindMissingForeignKeyIndexesAsync("Contato");

        Assert.NotEmpty(sugestoes);
        Assert.All(sugestoes, s =>
        {
            Assert.Equal("TGFCTT", s.ParentTable);
            Assert.NotEmpty(s.Columns);
            Assert.StartsWith($"CREATE INDEX IX_{s.ChildTable}_", s.CreateIndexSql);
        });
        Assert.Equal(sugestoes.OrderByDescending(s => s.ChildRows).Select(s => s.ChildRows), sugestoes.Select(s => s.ChildRows));
        // A tabela pela entidade e pelo nome dão o mesmo resultado.
        Assert.Equal(sugestoes.Select(s => s.ForeignKey), (await cliente.Diagnostics.FindMissingForeignKeyIndexesAsync("TGFCTT")).Select(s => s.ForeignKey));
    }
}
