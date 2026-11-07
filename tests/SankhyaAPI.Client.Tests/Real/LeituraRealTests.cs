using System.Text;
using SankhyaAPI.Client.Envelopes;
using SankhyaAPI.Client.Exceptions;
using SankhyaAPI.Client.Services;
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

    private static UsuarioService Usuarios() => new(SankhyaReal.BaseUrl!, SankhyaReal.Usuario!, SankhyaReal.Senha!);

    [RealFact]
    public async Task Login_e_logout_funcionam()
    {
        var sessao = new SessaoExposta();

        var login = await sessao.LoginSankhya(SankhyaReal.Usuario!, SankhyaReal.Senha!);
        Assert.False(string.IsNullOrWhiteSpace(login.SessionId));

        await sessao.LogoutSankhya();
    }

    [RealFact]
    public async Task Logout_encerra_a_sessao_no_servidor()
    {
        var sessao = new SessaoExposta();
        await sessao.LoginSankhya(SankhyaReal.Usuario!, SankhyaReal.Senha!);
        string cookie = sessao.Cookie;

        Assert.True(await SessaoAtiva(cookie), "a sessão recém-aberta deveria estar ativa");

        await sessao.LogoutSankhya();

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
        UsuarioLinha linha = (await Usuarios().QueryRawAsync<UsuarioLinha>(QueryUsuarioSup)).Single();

        Assert.Equal(0, linha.CodUsu);
        Assert.False(string.IsNullOrWhiteSpace(linha.NomeUsu));
    }

    [RealFact]
    public async Task QueryAsDictionaryAsync_retorna_as_colunas()
    {
        Dictionary<string, object?> linha = (await Usuarios().QueryAsDictionaryAsync(QueryUsuarioSup)).Single();

        Assert.Contains("CODUSU", linha.Keys);
        Assert.Contains("NOMEUSU", linha.Keys);
    }

    [RealFact]
    public async Task SQL_invalido_traz_a_mensagem_do_servidor()
    {
        var ex = await Assert.ThrowsAnyAsync<SankhyaException>(() => Usuarios().QueryAsDictionaryAsync("SELECT * FROM TABELA_QUE_NAO_EXISTE_XYZ"));

        Assert.DoesNotContain("Erro de sintaxe na consulta", ex.Message);
    }

    [RealFact]
    public async Task Senha_errada_lanca_excecao()
    {
        var service = new UsuarioService(SankhyaReal.BaseUrl!, SankhyaReal.Usuario!, "senha-errada-" + Guid.NewGuid());

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
        UsuarioService service = Usuarios();

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
        UsuarioService service = Usuarios();

        List<Usuario>[] resultados = await Task.WhenAll(
            Enumerable.Range(0, 12).Select(_ => service.FindAsync("this.CODUSU = 0")));

        Assert.All(resultados, r => Assert.Single(r));
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

    private sealed class SessaoExposta() : SessionService(SankhyaReal.BaseUrl!, SankhyaReal.Usuario!, SankhyaReal.Senha!)
    {
        public string Cookie => JSessionId;
    }
}
