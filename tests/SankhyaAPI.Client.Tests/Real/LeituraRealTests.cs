using System.Text;
using SankhyaAPI.Client.Exceptions;
using SankhyaAPI.Client.Tests.Support;

namespace SankhyaAPI.Client.Tests.Real;

/// <summary>
/// Testes contra um Sankhya real. Só fazem leitura (usuário SUP, CODUSU 0, que existe em toda base),
/// então são seguros em homologação. Rodar só eles: dotnet test --filter Category=Real
/// </summary>
[Trait("Category", "Real")]
public class LeituraRealTests
{
    private const string QueryUsuarioSup = "SELECT CODUSU, NOMEUSU FROM TSIUSU WHERE CODUSU = 0";

    private static ISankhyaRepository<Usuario> Usuarios() => SankhyaReal.Cliente().Repository<Usuario>("Usuario");
    private static ISankhyaSql Sql() => SankhyaReal.Cliente().Sql;

    [RealFact]
    public async Task Login_e_logout_funcionam()
    {
        await using SankhyaClient cliente = SankhyaReal.Cliente();

        var login = await cliente.Session.LoginAsync();
        Assert.False(string.IsNullOrWhiteSpace(login.SessionId));

        await cliente.Session.LogoutAsync();
    }

    [RealFact]
    public async Task Logout_encerra_a_sessao_no_servidor()
    {
        await using SankhyaClient cliente = SankhyaReal.Cliente();
        string cookie = $"JSESSIONID={(await cliente.Session.LoginAsync()).SessionId}";

        Assert.True(await SessaoAtiva(cookie), "a sessão recém-aberta deveria estar ativa");

        await cliente.Session.LogoutAsync();

        Assert.False(await SessaoAtiva(cookie), "a sessão continua ativa no servidor depois do logout");
    }

    [RealFact]
    public async Task FindAsync_encontra_o_usuario_SUP()
    {
        Usuario sup = (await Usuarios().FindAsync("this.CODUSU = 0")).Single();

        Assert.Equal(0, sup.CodUsu.Value);
        Assert.False(string.IsNullOrWhiteSpace(sup.NomeUsu.Value));
    }

    [RealFact]
    public async Task QueryRawAsync_retorna_linha_tipada()
    {
        UsuarioLinha linha = (await Sql().QueryRawAsync<UsuarioLinha>(QueryUsuarioSup)).Single();

        Assert.Equal(0, linha.CodUsu);
        Assert.False(string.IsNullOrWhiteSpace(linha.NomeUsu));
    }

    [RealFact]
    public async Task QueryAsDictionaryAsync_retorna_as_colunas()
    {
        Dictionary<string, object?> linha = (await Sql().QueryAsDictionaryAsync(QueryUsuarioSup)).Single();

        Assert.Contains("CODUSU", linha.Keys);
        Assert.Contains("NOMEUSU", linha.Keys);
    }

    [RealFact]
    public async Task SQL_invalido_traz_a_mensagem_do_servidor()
    {
        var ex = await Assert.ThrowsAnyAsync<SankhyaException>(() => Sql().QueryAsDictionaryAsync("SELECT * FROM TABELA_QUE_NAO_EXISTE_XYZ"));

        Assert.DoesNotContain("Erro de sintaxe na consulta", ex.Message);
    }

    [RealFact]
    public async Task Senha_errada_lanca_excecao()
    {
        var service = SankhyaReal.Cliente(senha: "senha-errada-" + Guid.NewGuid()).Repository<Usuario>("Usuario");

        await Assert.ThrowsAnyAsync<SankhyaException>(() => service.FindAsync("this.CODUSU = 0"));
    }

    [RealFact]
    public async Task FindAsync_parametrizado_encontra_o_usuario_SUP()
    {
        Usuario sup = (await Usuarios().FindAsync("this.CODUSU = ?", [Parameter.Inteiro(0)])).Single();

        Assert.Equal(0, sup.CodUsu.Value);
    }

    [RealFact]
    public async Task FindAsync_parametrizado_trata_o_valor_como_texto_e_nao_como_SQL()
    {
        List<Usuario> usuarios = await Usuarios().FindAsync("this.NOMEUSU = ?", [Parameter.Texto("X' OR 1=1 --")]);

        Assert.Empty(usuarios);
    }

    [RealFact]
    public async Task FindPageAsync_pagina_sem_repetir_registros()
    {
        ISankhyaRepository<Usuario> service = Usuarios();

        SankhyaPage<Usuario> primeira = await service.FindPageAsync("1 = 1", null, 0, "this.CODUSU");

        Assert.InRange(primeira.Items.Count, 1, 50);
        if (!primeira.HasMoreResult) return;

        SankhyaPage<Usuario> segunda = await service.FindPageAsync("1 = 1", null, 1, "this.CODUSU");
        Assert.NotEmpty(segunda.Items);
        Assert.Empty(primeira.Items.Select(u => u.CodUsu.Value).Intersect(segunda.Items.Select(u => u.CodUsu.Value)));
    }

    /// <summary>
    /// Confere o encoding do corpo XML pelo caminho de leitura: o servidor só acha o SUP se receber 'Ã' como
    /// um caractere. Usa LEN, então depende de SQL Server (o banco da homologação).
    /// </summary>
    [RealFact]
    public async Task Texto_com_acento_chega_intacto_ao_servidor()
    {
        List<Usuario> usuarios = await Usuarios().FindAsync("this.CODUSU = 0 AND LEN(?) = 1", [Parameter.Texto("Ã")]);

        Assert.Single(usuarios);
    }

    [RealFact]
    public async Task Chamadas_concorrentes_na_mesma_instancia_nao_se_atropelam()
    {
        ISankhyaRepository<Usuario> service = Usuarios();

        List<Usuario>[] resultados = await Task.WhenAll(
            Enumerable.Range(0, 12).Select(_ => service.FindAsync("this.CODUSU = 0")));

        Assert.All(resultados, r => Assert.Single(r));
    }

    [RealFact]
    public async Task Sessao_compartilhada_serve_50_chamadas_concorrentes()
    {
        await using SankhyaClient cliente = SankhyaReal.Cliente(SessionMode.Shared);
        var usuarios = cliente.Repository<Usuario>("Usuario");

        List<Usuario>[] resultados = await Task.WhenAll(Enumerable.Range(0, 50).Select(_ => usuarios.FindAsync("this.CODUSU = 0")));

        Assert.All(resultados, r => Assert.Single(r));
    }

    [RealFact]
    public async Task Sessao_do_pool_encerrada_no_servidor_e_renovada_e_a_chamada_repetida_com_a_sessao_nova()
    {
        await using SankhyaClient cliente = SankhyaReal.Cliente(SessionMode.Shared);
        var pool = (SankhyaAPI.Client.Session.SankhyaSession)cliente.Session;
        var usuarios = cliente.Repository<Usuario>();

        await usuarios.FindAsync("this.CODUSU = 0");
        string velha = pool.PooledSessionIds.Single();

        // Encerra a sessão do pool direto no servidor, como se tivesse expirado: o cliente não sabe.
        await EncerrarPorFora($"JSESSIONID={velha}");
        Assert.False(await SessaoAtiva($"JSESSIONID={velha}"), "a sessão deveria estar encerrada no servidor");

        // A próxima chamada recebe status 3, abre outra sessão e repete: tem que dar certo, com um id novo.
        Usuario sup = (await usuarios.FindAsync("this.CODUSU = 0")).Single();

        Assert.Equal(0, sup.CodUsu.Value);
        string nova = pool.PooledSessionIds.Single();
        Assert.NotEqual(velha, nova);
        Assert.True(await SessaoAtiva($"JSESSIONID={nova}"));
    }

    [RealFact]
    public async Task Sessao_compartilhada_se_renova_depois_de_encerrada_por_fora()
    {
        await using SankhyaClient cliente = SankhyaReal.Cliente(SessionMode.Shared);
        var usuarios = cliente.Repository<Usuario>("Usuario");
        await usuarios.FindAsync("this.CODUSU = 0");

        // Encerra a sessão pelo servidor, sem o cliente saber: a próxima chamada recebe status 3 e renova.
        SankhyaAPI.Client.Session.SankhyaSessionInfo sessao = await cliente.Session.LoginAsync();
        await EncerrarPorFora($"JSESSIONID={sessao.SessionId}");
        Assert.False(await SessaoAtiva($"JSESSIONID={sessao.SessionId}"));

        Assert.Single(await usuarios.FindAsync("this.CODUSU = 0"));
    }

    private static async Task EncerrarPorFora(string cookie)
    {
        using var http = new HttpClient();
        using var request = new HttpRequestMessage(
            HttpMethod.Post, $"{SankhyaReal.BaseUrl!.TrimEnd('/')}/service.sbr?serviceName=MobileLoginSP.logout");
        request.Headers.Add("Cookie", cookie);
        (await http.SendAsync(request)).Dispose();
    }

    /// <summary>
    /// Pergunta ao servidor, com o cookie informado, se a sessão ainda vale (executeQuery mínimo).
    /// </summary>
    private static async Task<bool> SessaoAtiva(string cookie)
    {
        using var http = new HttpClient();
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{SankhyaReal.BaseUrl!.TrimEnd('/')}/service.sbr?serviceName=DbExplorerSP.executeQuery&outputType=json");
        request.Headers.Add("Cookie", cookie);
        request.Content = new StringContent(
            $$$"""{"serviceName":"DbExplorerSP.executeQuery","requestBody":{"sql":"{{{QueryUsuarioSup}}}"}}""",
            Encoding.UTF8,
            "application/json");

        using HttpResponseMessage response = await http.SendAsync(request);
        string body = await response.Content.ReadAsStringAsync();
        return response.IsSuccessStatusCode && body.Contains("\"status\":\"1\"");
    }

}
