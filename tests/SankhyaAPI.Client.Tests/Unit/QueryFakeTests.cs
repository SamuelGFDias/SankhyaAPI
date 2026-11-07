using SankhyaAPI.Client.Exceptions;
using SankhyaAPI.Client.Tests.Fake;
using SankhyaAPI.Client.Tests.Support;

namespace SankhyaAPI.Client.Tests.Unit;

public class QueryFakeTests : IDisposable
{
    private const string Metadata =
        """[{"name":"CODUSU","description":"CODUSU","order":1,"userType":"I"},{"name":"NOMEUSU","description":"NOMEUSU","order":2,"userType":"S"}]""";

    private readonly SankhyaFakeServer _fake = new();

    public void Dispose() => _fake.Dispose();

    private UsuarioService Usuarios() => new(_fake.BaseUrl, "usuario", "senha");

    [Fact]
    public async Task QueryRawAsync_mapeia_colunas_pelo_nome()
    {
        _fake.ComQuery(Metadata, """[[0,"SUP"],[1,"JOÃO"]]""");

        List<UsuarioLinha> linhas = await Usuarios().QueryRawAsync<UsuarioLinha>("SELECT CODUSU, NOMEUSU FROM TSIUSU");

        Assert.Equal(2, linhas.Count);
        Assert.Equal(0, linhas[0].CodUsu);
        Assert.Equal("JOÃO", linhas[1].NomeUsu);

        string body = _fake.Chamadas("DbExplorerSP.executeQuery").Single().RequestMessage.Body!;
        Assert.Contains("\"sql\":\"SELECT CODUSU, NOMEUSU FROM TSIUSU\"", body);
    }

    [Fact]
    public async Task QueryRawAsync_respeita_a_ordem_declarada_no_metadata()
    {
        // fieldsMetadata fora de ordem: o nome da coluna tem que acompanhar o seu "order".
        const string desordenado =
            """[{"name":"NOMEUSU","description":"NOMEUSU","order":2,"userType":"S"},{"name":"CODUSU","description":"CODUSU","order":1,"userType":"I"}]""";
        _fake.ComQuery(desordenado, """[[0,"SUP"]]""");

        UsuarioLinha linha = (await Usuarios().QueryRawAsync<UsuarioLinha>("SELECT ...")).Single();

        Assert.Equal(0, linha.CodUsu);
        Assert.Equal("SUP", linha.NomeUsu);
    }

    [Fact]
    public async Task QueryAsDictionaryAsync_respeita_a_ordem_declarada_no_metadata()
    {
        const string desordenado =
            """[{"name":"NOMEUSU","description":"NOMEUSU","order":2,"userType":"S"},{"name":"CODUSU","description":"CODUSU","order":1,"userType":"I"}]""";
        _fake.ComQuery(desordenado, """[[0,"SUP"]]""");

        Dictionary<string, object?> linha = (await Usuarios().QueryAsDictionaryAsync("SELECT ...")).Single();

        Assert.Equal("SUP", linha["NOMEUSU"]);
        Assert.Equal(0L, Convert.ToInt64(linha["CODUSU"]));
    }

    [Fact]
    public async Task Coluna_repetida_e_rejeitada_com_mensagem_clara()
    {
        const string repetido =
            """[{"name":"CODUSU","description":"CODUSU","order":1,"userType":"I"},{"name":"CODUSU","description":"CODUSU","order":2,"userType":"I"}]""";
        _fake.ComQuery(repetido, """[[0,1]]""");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Usuarios().QueryAsDictionaryAsync("SELECT ..."));

        Assert.Contains("CODUSU", ex.Message);
    }

    [Fact]
    public async Task QueryAsDictionaryAsync_retorna_tipos_do_dotnet_e_nao_JsonElement()
    {
        _fake.ComQuery(Metadata, """[[0,"SUP  "]]""");

        Dictionary<string, object?> linha = (await Usuarios().QueryAsDictionaryAsync("SELECT ...")).Single();

        Assert.Equal("SUP", linha["NOMEUSU"]);
        Assert.IsNotType<System.Text.Json.JsonElement>(linha["CODUSU"]);
    }

    [Fact]
    public async Task Erro_de_SQL_mostra_a_mensagem_do_servidor()
    {
        _fake.ComQueryErro("ORA-00942: table or view does not exist");

        var ex = await Assert.ThrowsAsync<SankhyaException>(() => Usuarios().QueryAsDictionaryAsync("SELECT * FROM NAO_EXISTE"));

        Assert.Contains("ORA-00942", ex.Message);
    }
}
