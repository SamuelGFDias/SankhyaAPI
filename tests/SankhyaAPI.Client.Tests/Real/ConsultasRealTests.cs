using SankhyaAPI.Client.Exceptions;
using SankhyaAPI.Client.Tests.Support;

namespace SankhyaAPI.Client.Tests.Real;

/// <summary>
/// Consulta fluente, StreamAsync, atributos do loadRecords e SQL cru seguro e paginado, contra a homologação (só leitura).
/// </summary>
[Trait("Category", "Real")]
public class ConsultasRealTests
{
    [RealFact]
    public async Task StreamAsync_percorre_a_tabela_inteira_sem_repetir()
    {
        await using SankhyaClient cliente = SankhyaReal.Cliente(SessionMode.Shared);
        var cidades = cliente.Repository<Cidade>();

        var vistos = new HashSet<int>();
        int total = 0;
        await foreach (Cidade c in cidades.Query().OrderBy(x => x.CodCid).StreamAsync())
        {
            total++;
            vistos.Add(c.CodCid.Value);
        }

        Assert.Equal((await cidades.FindAsync("1 = 1")).Count, total);
        Assert.Equal(total, vistos.Count);
        Assert.True(total > 5000);
    }

    [RealFact]
    public async Task GetAsync_e_FirstOrDefault_encontram_o_SUP()
    {
        await using SankhyaClient cliente = SankhyaReal.Cliente(SessionMode.Shared);
        var usuarios = cliente.Repository<Usuario>();

        Assert.Equal(0, (await usuarios.GetAsync([0]))!.CodUsu.Value);
        Assert.Null(await usuarios.GetAsync([-987654]));
        Assert.Equal(0, (await usuarios.Query().Where("this.CODUSU >= ?", 0).OrderBy(x => x.CodUsu).FirstOrDefaultAsync())!.CodUsu.Value);
    }

    [RealFact]
    public async Task ModifiedSince_sem_LOGTABOPER_traz_o_erro_do_servidor()
    {
        var ex = await Assert.ThrowsAsync<SankhyaException>(
            () => SankhyaReal.Cliente().Repository<Usuario>().Query().ModifiedSince(new DateTime(2026, 1, 1)).ToListAsync());

        Assert.Equal("CORE_E07537", ex.TsErrorCode);
    }

    [RealFact]
    public async Task Atributos_aceitos_sem_efeito_nao_mudam_o_resultado()
    {
        var usuarios = SankhyaReal.Cliente().Repository<Usuario>();

        List<Usuario> normal = await usuarios.Query().Where("this.CODUSU < ?", 5).ToListAsync();
        List<Usuario> comAtributos = await usuarios.Query().Where("this.CODUSU < ?", 5).IgnoreCalculatedFields().FileBasedPagination().ToListAsync();

        Assert.Equal(normal.Select(u => u.CodUsu.Value), comAtributos.Select(u => u.CodUsu.Value));
    }

    [RealFact]
    public async Task SankhyaSql_trata_tentativa_de_injecao_como_texto()
    {
        string malicioso = "x' OR '1'='1";
        Dictionary<string, object?> linha = (await SankhyaReal.Cliente().Sql.QueryAsDictionaryAsync(
            $"SELECT COUNT(*) AS N FROM TSIUSU WHERE NOMEUSU = {malicioso}")).Single();

        Assert.Equal(0L, linha["N"]);
    }

    [RealFact]
    public async Task SankhyaSql_formata_data_aceita_pelo_SQL_Server()
    {
        Dictionary<string, object?> linha = (await SankhyaReal.Cliente().Sql.QueryAsDictionaryAsync(
            $"SELECT CASE WHEN {new DateTime(2026, 11, 7, 14, 30, 0)} > {new DateOnly(2026, 11, 7)} THEN 1 ELSE 0 END AS OK")).Single();

        Assert.Equal(1L, linha["OK"]);
    }

    [RealFact]
    public async Task QueryTopAsync_limita_e_avisa_que_ha_mais()
    {
        SankhyaRows<CidadeLinha> rows = await SankhyaReal.Cliente().Sql.QueryTopAsync<CidadeLinha>("SELECT CODCID FROM TSICID", 10);

        Assert.Equal(10, rows.Items.Count);
        Assert.True(rows.HasMore);
    }

    [RealFact]
    public async Task QueryPagedAsync_traz_mais_de_5000_linhas_sem_repetir()
    {
        await using SankhyaClient cliente = SankhyaReal.Cliente(SessionMode.Shared);
        long esperado = (long)(await cliente.Sql.QueryAsDictionaryAsync("SELECT COUNT(*) AS N FROM TSICID")).Single()["N"]!;

        var vistos = new HashSet<int?>();
        await foreach (CidadeLinha c in cliente.Sql.QueryPagedAsync<CidadeLinha>("SELECT CODCID FROM TSICID", "CODCID", 5000))
            vistos.Add(c.CodCid);

        Assert.Equal(esperado, vistos.Count);
    }
}
