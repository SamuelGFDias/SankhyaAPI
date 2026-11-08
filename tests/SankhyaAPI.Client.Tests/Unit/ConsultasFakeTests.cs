using SankhyaAPI.Client.Tests.Fake;
using SankhyaAPI.Client.Tests.Support;

namespace SankhyaAPI.Client.Tests.Unit;

/// <summary>
/// Consulta fluente e StreamAsync (#27), atributos do loadRecords (#28) e SQL cru seguro e paginado (#29).
/// </summary>
public class ConsultasFakeTests : IDisposable
{
    private const string Load = "CRUDServiceProvider.loadRecords";
    private const string Query = "DbExplorerSP.executeQuery";
    private const string Metadata = """[{"name":"CODUSU","description":"CODUSU","order":1,"userType":"I"}]""";

    private readonly SankhyaFakeServer _fake = new();

    public void Dispose() => _fake.Dispose();

    private ISankhyaRepository<Usuario> Usuarios() => _fake.Cliente().Repository<Usuario>();

    private string Corpo(string servico) => _fake.Chamadas(servico).Single().RequestMessage!.Body!;

    [Fact]
    public async Task Query_combina_filtros_infere_tipos_e_ordena_pela_coluna_mapeada()
    {
        _fake.ComEntidades(Load, ["0", "SUP"]);

        await Usuarios().Query()
                        .Where("this.NOMEUSU LIKE ?", "S%")
                        .Where("this.CODUSU BETWEEN ? AND ? AND this.DTCAD > ?", 0, 10L, new DateOnly(2026, 1, 2))
                        .OrderByDescending(x => x.CodUsu)
                        .ToListAsync();

        string body = Corpo(Load);
        Assert.Contains("<expression>(this.NOMEUSU LIKE ?) AND (this.CODUSU BETWEEN ? AND ? AND this.DTCAD &gt; ?)</expression>", body);
        Assert.Contains("<parameter type=\"S\">S%</parameter><parameter type=\"I\">0</parameter><parameter type=\"I\">10</parameter><parameter type=\"D\">02/01/2026</parameter>", body);
        Assert.Contains("orderByExpression=\"this.CODUSU DESC\"", body);
    }

    [Fact]
    public void Query_recusa_quantidade_de_valores_diferente_dos_pontos_de_interrogacao()
    {
        Assert.Throws<ArgumentException>(() => Usuarios().Query().Where("this.CODUSU = ? AND this.NOMEUSU = ?", 1));
    }

    [Fact]
    public async Task Query_envia_os_atributos_do_loadRecords()
    {
        _fake.ComEntidades(Load, ["0", "SUP"]);

        await Usuarios().Query().ModifiedSince(new DateTime(2026, 4, 16, 12, 59, 59)).IgnoreCalculatedFields().FileBasedPagination().ToListAsync();

        string body = Corpo(Load);
        Assert.Contains("modifiedSince=\"2026-04-16T12:59:59\"", body);
        Assert.Contains("ignoreCalculatedFields=\"true\"", body);
        Assert.Contains("useFileBasedPagination=\"true\"", body);
    }

    [Fact]
    public async Task StreamAsync_percorre_as_paginas_ate_hasMoreResult_falso()
    {
        _fake.ComPagina(0, true, ["1", "A"], ["2", "B"]);
        _fake.ComPagina(1, true, ["3", "C"]);
        _fake.ComPagina(2, false, ["4", "D"]);

        var codigos = new List<int>();
        await foreach (Usuario u in Usuarios().Query().OrderBy(x => x.CodUsu).StreamAsync())
            codigos.Add(u.CodUsu.Value);

        Assert.Equal([1, 2, 3, 4], codigos);
        Assert.Equal(3, _fake.Chamadas(Load).Count);
    }

    [Fact]
    public async Task GetAsync_busca_pela_chave_composta()
    {
        _fake.ComEntidades(Load, ["500", "3", "2"]);

        ItemNota? item = await _fake.Cliente().Repository<ItemNota>().GetAsync([500L, 3]);

        Assert.Equal(3, item!.Sequencia.Value);
        string body = Corpo(Load);
        Assert.Contains("<expression>this.NUNOTA = ? AND this.SEQUENCIA = ?</expression>", body);
        Assert.Contains("<parameter type=\"I\">500</parameter><parameter type=\"I\">3</parameter>", body);
    }

    [Fact]
    public void SankhyaSql_escapa_e_formata_cada_valor_pelo_tipo()
    {
        string nome = "x' OR '1'='1'; DROP TABLE TGFPAR; --";
        SankhyaSql sql = $"SELECT * FROM TGFPAR WHERE NOMEPARC = {nome} AND CODCID IN {new[] { 1, 2 }} AND DTCAD >= {new DateOnly(2026, 1, 2)} AND ATIVO = {true} AND VALOR > {1234.5m} AND OBS = {(string?)null}";

        Assert.Equal(
            "SELECT * FROM TGFPAR WHERE NOMEPARC = 'x'' OR ''1''=''1''; DROP TABLE TGFPAR; --' AND CODCID IN (1, 2) AND DTCAD >= CONVERT(DATE, '2026-01-02', 23) AND ATIVO = 'S' AND VALOR > 1234.5 AND OBS = NULL",
            sql.ToString());
        Assert.Equal("TO_DATE('2026-01-02 10:30:00', 'YYYY-MM-DD HH24:MI:SS')", SankhyaSql.Format(new DateTime(2026, 1, 2, 10, 30, 0), SqlDialect.Oracle));
        Assert.Throws<ArgumentException>(() => SankhyaSql.Format(new object(), SqlDialect.SqlServer));
        Assert.Throws<ArgumentException>(() => SankhyaSql.Format("a\0b", SqlDialect.SqlServer));
    }

    [Fact]
    public async Task QueryAsync_envia_o_SQL_renderizado()
    {
        _fake.ComQuery(Metadata, "[[0]]");
        string nome = "O'Brien";

        await _fake.Cliente().Sql.QueryRawAsync<UsuarioLinha>($"SELECT CODUSU FROM TSIUSU WHERE NOMEUSU = {nome}");

        string sql = System.Text.Json.JsonDocument.Parse(Corpo(Query)).RootElement.GetProperty("requestBody").GetProperty("sql").GetString()!;
        Assert.Equal("SELECT CODUSU FROM TSIUSU WHERE NOMEUSU = 'O''Brien'", sql);
    }

    [Fact]
    public async Task QueryTopAsync_pede_uma_linha_a_mais_para_saber_se_ha_outras()
    {
        _fake.ComQuery(Metadata, "[[1],[2],[3]]");

        SankhyaRows<UsuarioLinha> rows = await _fake.Cliente().Sql.QueryTopAsync<UsuarioLinha>("SELECT CODUSU FROM TSIUSU", 2);

        Assert.Contains("\"maxRows\":\"3\"", Corpo(Query));
        Assert.Equal(2, rows.Items.Count);
        Assert.True(rows.HasMore);
    }

    [Fact]
    public async Task QueryPagedAsync_pagina_com_OFFSET_FETCH_ate_vir_menos_que_a_pagina()
    {
        _fake.ComQuerySe("OFFSET 0 ROWS FETCH NEXT 2 ROWS ONLY", Metadata, "[[1],[2]]");
        _fake.ComQuerySe("OFFSET 2 ROWS FETCH NEXT 2 ROWS ONLY", Metadata, "[[3]]");

        var codigos = new List<int?>();
        await foreach (UsuarioLinha l in _fake.Cliente().Sql.QueryPagedAsync<UsuarioLinha>("SELECT CODUSU FROM TSIUSU", "CODUSU", 2))
            codigos.Add(l.CodUsu);

        Assert.Equal([1, 2, 3], codigos);
        Assert.Contains("SELECT * FROM (SELECT CODUSU FROM TSIUSU) q ORDER BY CODUSU OFFSET 0 ROWS", _fake.Chamadas(Query)[0].RequestMessage!.Body);
    }

    [Theory]
    [InlineData("SELECT CODUSU FROM TSIUSU ORDER BY CODUSU", true)]
    [InlineData("SELECT * FROM (SELECT TOP 5 CODUSU FROM TSIUSU ORDER BY CODUSU) x", false)]
    [InlineData("SELECT 'ORDER BY' AS X FROM TSIUSU", false)]
    public void Detecta_ORDER_BY_fora_de_parenteses_e_literais(string sql, bool esperado)
    {
        Assert.Equal(esperado, SankhyaSqlClient.HasTopLevelOrderBy(sql));
    }

    [Fact]
    public async Task QueryPagedAsync_recusa_SQL_com_ORDER_BY_proprio()
    {
        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await foreach (UsuarioLinha _ in _fake.Cliente().Sql.QueryPagedAsync<UsuarioLinha>("SELECT CODUSU FROM TSIUSU ORDER BY CODUSU", "CODUSU")) { }
        });
        Assert.Empty(_fake.Chamadas(Query));
    }
}
