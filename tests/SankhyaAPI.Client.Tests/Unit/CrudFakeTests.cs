using System.Globalization;
using SankhyaAPI.Client.Exceptions;

using SankhyaAPI.Client.Tests.Support;

namespace SankhyaAPI.Client.Tests.Unit;

/// <summary>
/// Testes do pacote contra o servidor fake: rodam offline e no CI.
/// </summary>
public class CrudFakeTests : IDisposable
{
    private const string Login = "MobileLoginSP.login";
    private const string Logout = "MobileLoginSP.logout";
    private const string Load = "CRUDServiceProvider.loadRecords";
    private const string Save = "CRUDServiceProvider.saveRecord";
    private const string Remove = "CRUDServiceProvider.removeRecord";

    private readonly SankhyaFakeServer _fake = new();

    public void Dispose() => _fake.Dispose();

    private ISankhyaRepository<Usuario> Usuarios() => _fake.CreateClient().Repository<Usuario>("Usuario");
    private ISankhyaRepository<Produto> Produtos() => _fake.CreateClient(configure: o => o.VerifyExistenceOnUpdate = false).Repository<Produto>("Produto");

    [Fact]
    public async Task FindAsync_faz_login_com_credenciais_e_usa_cookie_da_sessao()
    {
        _fake.WithEntities(Load, ["0", "SUP"]);

        await Usuarios().FindAsync("this.CODUSU = 0");

        string loginBody = _fake.Calls(Login).Single().RequestMessage!.Body!;
        Assert.Contains("<NOMUSU>usuario</NOMUSU>", loginBody);
        Assert.Contains("<INTERNO>senha</INTERNO>", loginBody);

        var load = _fake.Calls(Load).Single().RequestMessage!;
        Assert.Contains($"JSESSIONID={SankhyaFakeServer.SessionId}", load.Headers!["Cookie"].Single());
        Assert.Contains("this.CODUSU = 0", load.Body);
        Assert.Contains("rootEntity=\"Usuario\"", load.Body);
    }

    [Fact]
    public async Task FindAsync_mapeia_entidades_retornadas()
    {
        _fake.WithEntities(Load, ["0", "SUP"], ["1", "JOÃO"]);

        List<Usuario> usuarios = await Usuarios().FindAsync("1 = 1");

        Assert.Equal(2, usuarios.Count);
        Assert.Equal(0, usuarios[0].CodUsu.Value);
        Assert.Equal("SUP", usuarios[0].NomeUsu.Value);
        Assert.Equal("JOÃO", usuarios[1].NomeUsu.Value);
    }

    [Fact]
    public async Task FindAsync_sem_registros_retorna_lista_vazia()
    {
        _fake.WithRawXml(Load, "<responseBody><entities total=\"0\" /></responseBody>");

        Assert.Empty(await Usuarios().FindAsync("1 = 0"));
    }

    [Fact]
    public async Task UpdateAsync_usa_saveRecord_envia_chave_e_so_campos_alterados()
    {
        _fake.WithEntities(Save, ["10", "NOVO", "3.5", "", "S"]);
        var produto = new Produto { CodProd = 10, DescrProd = "NOVO" };

        await Produtos().UpdateAsync(produto);

        string body = _fake.Calls(Save).Single().RequestMessage!.Body!;
        Assert.Contains("serviceName=\"CRUDServiceProvider.saveRecord\"", body);
        Assert.Contains("<key>", body);
        Assert.Contains("<CODPROD>10</CODPROD>", body);
        Assert.Contains("<DESCRPROD>NOVO</DESCRPROD>", body);
        Assert.DoesNotContain("<PRECO", body);
        Assert.Empty(_fake.Calls(Remove));
    }

    [Fact]
    public async Task UpdateAsync_com_Clear_envia_elemento_vazio_para_apagar_o_campo()
    {
        _fake.WithEntities(Save, ["10", "", "", "", ""]);
        var produto = new Produto { CodProd = 10, DescrProd = NullableState<string>.Clear() };

        await Produtos().UpdateAsync(produto);

        Assert.Contains("<DESCRPROD />", _fake.Calls(Save).Single().RequestMessage!.Body);
    }

    [Fact]
    public async Task CreateAsync_nao_envia_chave_auto_enumerada()
    {
        _fake.WithEntities(Save, ["99", "NOVO", "", "", ""]);

        Produto? criado = await Produtos().CreateAsync(new Produto { DescrProd = "NOVO" });

        string body = _fake.Calls(Save).Single().RequestMessage!.Body!;
        Assert.DoesNotContain("<CODPROD>", body);
        Assert.Equal(99, criado!.CodProd.Value);
    }

    [Fact]
    public async Task CreateAsync_com_chave_auto_enumerada_preenchida_e_rejeitado_sem_chamar_o_servidor()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => Produtos().CreateAsync(new Produto { CodProd = 1, DescrProd = "X" }));

        Assert.Empty(_fake.Calls(Save));
    }

    [Fact]
    public async Task CreateAsync_sem_chave_que_nao_e_auto_enumerada_e_rejeitado_sem_chamar_o_servidor()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => Usuarios().CreateAsync(new Usuario { NomeUsu = "X" }));

        Assert.Equal(nameof(Usuario.CodUsu), ex.ParamName);
        Assert.Empty(_fake.Calls(Save));
    }

    [Fact]
    public async Task CreateAsync_envia_a_chave_que_nao_e_auto_enumerada()
    {
        _fake.WithEntities(Save, ["7", "NOVO"]);

        Usuario? criado = await Usuarios().CreateAsync(new Usuario { CodUsu = 7, NomeUsu = "NOVO" });

        Assert.Contains("<CODUSU>7</CODUSU>", _fake.Calls(Save).Single().RequestMessage!.Body);
        Assert.Equal(7, criado!.CodUsu.Value);
    }

    [Fact]
    public async Task UpdateAsync_sem_chave_e_rejeitado()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => Produtos().UpdateAsync(new Produto { DescrProd = "X" }));
    }

    [Fact]
    public async Task DeleteAsync_usa_removeRecord_com_a_chave()
    {
        _fake.WithRawXml(Remove, "<responseBody />");

        await Produtos().DeleteAsync(new Produto { CodProd = 10 });

        string body = _fake.Calls(Remove).Single().RequestMessage!.Body!;
        Assert.Contains("<id>", body);
        Assert.Contains("<CODPROD>10</CODPROD>", body);
    }

    [Fact]
    public async Task Valores_sao_formatados_sem_depender_da_cultura_da_maquina()
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("pt-BR");
        try
        {
            _fake.WithEntities(Save, ["10", "", "1234.56", "07/11/2026 10:30:00", "S"]);
            var produto = new Produto
            {
                CodProd = 10, Preco = 1234.56m, DtAlter = new DateTime(2026, 11, 7, 10, 30, 0), Ativo = true
            };

            Produto? salvo = await Produtos().UpdateAsync(produto);

            string body = _fake.Calls(Save).Single().RequestMessage!.Body!;
            Assert.Contains("<PRECO>1234.56</PRECO>", body);
            Assert.Contains("<DTALTER>07/11/2026 10:30:00</DTALTER>", body);
            Assert.Contains("<ATIVO>S</ATIVO>", body);
            Assert.Equal(1234.56m, salvo!.Preco.Value);
            Assert.Equal(new DateTime(2026, 11, 7, 10, 30, 0), salvo.DtAlter.Value);
            Assert.True(salvo.Ativo.Value);
        } finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public async Task Erro_do_Sankhya_vira_excecao_com_a_mensagem_decodificada()
    {
        _fake.WithError(Load, "Usuário sem permissão");

        var ex = await Assert.ThrowsAsync<SankhyaException>(() => Usuarios().FindAsync("1 = 1"));

        Assert.Contains("Usuário sem permissão", ex.Message);
        Assert.Equal("0", ex.Status);
        Assert.Equal(Load, ex.ServiceName);
    }

    [Fact]
    public async Task Falha_HTTP_lanca_excecao_em_vez_de_retornar_lista_vazia()
    {
        _fake.WithHttpStatus(Load, 500);

        var ex = await Assert.ThrowsAsync<SankhyaTransportException>(() => Usuarios().FindAsync("1 = 1"));
        Assert.Equal(System.Net.HttpStatusCode.InternalServerError, ex.HttpStatus);
        Assert.Equal(SankhyaErrorKind.Unavailable, ex.Kind);
    }

    [Fact]
    public async Task Login_invalido_lanca_excecao_sem_executar_a_operacao()
    {
        _fake.WithError(Login, "Usuário/Senha inválido.");

        var ex = await Assert.ThrowsAsync<SankhyaAuthenticationException>(() => Usuarios().FindAsync("1 = 1"));

        Assert.Contains("inválido", ex.Message);
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, ex.SuggestedHttpStatus);
        Assert.Empty(_fake.Calls(Load));
    }

    [Fact]
    public async Task Logout_e_tentado_mesmo_quando_a_operacao_falha()
    {
        _fake.WithError(Load, "falhou");

        await Assert.ThrowsAnyAsync<Exception>(() => Usuarios().FindAsync("1 = 1"));

        Assert.Single(_fake.Calls(Logout));
    }

    [Fact]
    public async Task Logout_usa_a_mesma_rota_dos_demais_servicos()
    {
        _fake.WithEntities(Load, ["0", "SUP"]);

        await Usuarios().FindAsync("1 = 1");

        Assert.Equal(SankhyaFakeServer.ServicePath, _fake.Calls(Logout).Single().RequestMessage!.Path);
    }

    [Fact]
    public async Task Logout_envia_o_cookie_JSESSIONID_da_sessao()
    {
        _fake.WithEntities(Load, ["0", "SUP"]);

        await Usuarios().FindAsync("1 = 1");

        string cookie = _fake.Calls(Logout).Single().RequestMessage!.Headers!["Cookie"].Single();
        Assert.StartsWith($"JSESSIONID={SankhyaFakeServer.SessionId}", cookie);
    }
}
