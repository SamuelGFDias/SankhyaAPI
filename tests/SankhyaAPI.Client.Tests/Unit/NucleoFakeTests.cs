using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SankhyaAPI.Client.Diagnostics;
using SankhyaAPI.Client.Exceptions;
using SankhyaAPI.Client.Reports;
using SankhyaAPI.Client.Tests.Support;

namespace SankhyaAPI.Client.Tests.Unit;

/// <summary>
/// Núcleo da 6.0: sessão (#9), HTTP sem cookies e com timeout (#8), cancelamento (#10), relatório (#11),
/// opções e DI (#7), resiliência (#32) e observabilidade (#31).
/// </summary>
public class NucleoFakeTests : IDisposable
{
    private const string Login = "MobileLoginSP.login";
    private const string Logout = "MobileLoginSP.logout";
    private const string Load = "CRUDServiceProvider.loadRecords";
    private const string Save = "CRUDServiceProvider.saveRecord";

    private readonly SankhyaFakeServer _fake = new();

    public void Dispose() => _fake.Dispose();

    [Fact]
    public async Task Sessao_compartilhada_faz_um_login_para_varias_operacoes_e_logout_no_dispose()
    {
        _fake.WithEntities(Load, ["0", "SUP"]);

        await using (SankhyaClient cliente = _fake.CreateClient(SessionMode.Shared))
        {
            var usuarios = cliente.Repository<Usuario>("Usuario");
            await usuarios.FindAsync("1 = 1");
            await usuarios.FindAsync("1 = 1");
            await usuarios.FindAsync("1 = 1");

            Assert.Single(_fake.Calls(Login));
            Assert.Empty(_fake.Calls(Logout));
        }

        Assert.Single(_fake.Calls(Logout));
    }

    [Fact]
    public async Task Sessao_compartilhada_com_chamadas_concorrentes_abre_no_maximo_MaxSessions()
    {
        _fake.WithEntities(Load, ["0", "SUP"]);
        await using SankhyaClient cliente = _fake.CreateClient(SessionMode.Shared);
        var usuarios = cliente.Repository<Usuario>("Usuario");

        await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => usuarios.FindAsync("1 = 1")));

        Assert.InRange(_fake.Calls(Login).Count, 1, 4);
        Assert.Equal(20, _fake.Calls(Load).Count);
    }

    [Fact]
    public async Task Sessao_expirada_e_renovada_e_a_operacao_repetida_uma_vez()
    {
        _fake.WithSessionExpiredOnce(Load, ["0", "SUP"]);
        await using SankhyaClient cliente = _fake.CreateClient(SessionMode.Shared);

        Usuario sup = (await cliente.Repository<Usuario>("Usuario").FindAsync("1 = 1")).Single();

        Assert.Equal("SUP", sup.NomeUsu.Value);
        Assert.Equal(2, _fake.Calls(Login).Count);
        Assert.Equal(2, _fake.Calls(Load).Count);
    }

    [Fact]
    public async Task Sessao_compartilhada_expirada_repete_a_chamada_com_o_id_da_sessao_nova()
    {
        _fake.WithLoginSequence("SESSAO-VELHA", "SESSAO-NOVA");
        _fake.WithSessionExpiredOnce(Load, ["0", "SUP"]);
        await using SankhyaClient cliente = _fake.CreateClient(SessionMode.Shared);

        Assert.Single(await cliente.Repository<Usuario>("Usuario").FindAsync("1 = 1"));

        string[] cookies = _fake.Calls(Load).Select(c => c.RequestMessage!.Headers!["Cookie"].Single()).ToArray();
        Assert.Equal(["JSESSIONID=SESSAO-VELHA", "JSESSIONID=SESSAO-NOVA"], cookies);
        Assert.Equal(["SESSAO-NOVA"], ((SankhyaAPI.Client.Session.SankhyaSession)cliente.Session).PooledSessionIds);
    }

    [Fact]
    public async Task Sessao_que_expira_de_novo_na_repeticao_propaga_o_erro()
    {
        _fake.WithStatus(Load, "3", $"<statusMessage><![CDATA[{SankhyaFakeServer.Base64("Não autorizado.")}]]></statusMessage>");
        await using SankhyaClient cliente = _fake.CreateClient(SessionMode.Shared);

        await Assert.ThrowsAsync<SankhyaSessionExpiredException>(() => cliente.Repository<Usuario>("Usuario").FindAsync("1 = 1"));

        Assert.Equal(2, _fake.Calls(Load).Count);
    }

    [Fact]
    public async Task Login_explicito_e_usado_por_todas_as_operacoes_ate_o_logout()
    {
        _fake.WithEntities(Load, ["0", "SUP"]);
        await using SankhyaClient cliente = _fake.CreateClient(SessionMode.PerOperation);

        var sessao = await cliente.Session.LoginAsync();
        await cliente.Repository<Usuario>("Usuario").FindAsync("1 = 1");
        await cliente.Repository<Usuario>("Usuario").FindAsync("1 = 1");
        await cliente.Session.LogoutAsync();

        Assert.Equal(SankhyaFakeServer.SessionId, sessao.SessionId);
        Assert.Equal(0, sessao.UserCode);
        Assert.Single(_fake.Calls(Login));
        Assert.Single(_fake.Calls(Logout));
    }

    [Fact]
    public async Task Nenhuma_chamada_leva_cookie_alem_do_JSESSIONID_da_sessao()
    {
        _fake.WithEntities(Load, ["0", "SUP"]);
        await using SankhyaClient cliente = _fake.CreateClient(SessionMode.PerOperation);

        await cliente.Repository<Usuario>("Usuario").FindAsync("1 = 1");

        Assert.False(_fake.Calls(Login).Single().RequestMessage!.Headers!.ContainsKey("Cookie"));
        Assert.Equal($"JSESSIONID={SankhyaFakeServer.SessionId}", _fake.Calls(Load).Single().RequestMessage!.Headers!["Cookie"].Single());
    }

    [Fact]
    public async Task Falha_transitoria_e_repetida_so_em_operacao_idempotente()
    {
        _fake.WithHttpStatus(Load, 503);
        _fake.WithHttpStatus(Save, 503);
        await using SankhyaClient cliente = _fake.CreateClient(SessionMode.PerOperation, maxRetries: 2, configure: o => o.VerifyExistenceOnUpdate = false);
        var produtos = cliente.Repository<Produto>("Produto");

        await Assert.ThrowsAsync<SankhyaTransportException>(() => produtos.FindAsync("1 = 1"));
        await Assert.ThrowsAsync<SankhyaTransportException>(() => produtos.UpdateAsync(new Produto { CodProd = 1, DescrProd = "X" }));

        Assert.Equal(3, _fake.Calls(Load).Count);
        Assert.Single(_fake.Calls(Save));
    }

    [Fact]
    public async Task Cancelamento_interrompe_a_chamada()
    {
        _fake.WithDelay(Load, TimeSpan.FromSeconds(3));
        await using SankhyaClient cliente = _fake.CreateClient(SessionMode.PerOperation);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));

        var relogio = Stopwatch.StartNew();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cliente.Repository<Usuario>("Usuario").FindAsync("1 = 1", cts.Token));

        Assert.True(relogio.Elapsed < TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task Timeout_da_configuracao_vira_SankhyaTransportException_de_timeout()
    {
        _fake.WithDelay(Load, TimeSpan.FromSeconds(3));
        await using SankhyaClient cliente = SankhyaClient.Create(new SankhyaClientOptions
        {
            BaseUrl = _fake.BaseUrl, Usuario = "u", Senha = "s", AllowHttp = true, SessionMode = SessionMode.PerOperation,
            Timeout = TimeSpan.FromMilliseconds(300), MaxRetries = 0
        });

        var ex = await Assert.ThrowsAsync<SankhyaTransportException>(() => cliente.Repository<Usuario>("Usuario").FindAsync("1 = 1"));
        Assert.Contains("Tempo esgotado", ex.Message);
        Assert.Equal(SankhyaErrorKind.Timeout, ex.Kind);
        Assert.Equal(System.Net.HttpStatusCode.GatewayTimeout, ex.SuggestedHttpStatus);
    }

    // #53: sem resposta (timeout ou cancelamento), a requisição pode seguir rodando no servidor, e o Sankhya recusa
    // outra requisição na mesma sessão. A sessão não volta ao pool e o timeout não é repetido na mesma sessão.
    private static readonly TimeSpan Atraso = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan TimeoutCurto = TimeSpan.FromMilliseconds(400);

    /// <summary>
    /// Cliente de uma sessão só, já aquecido: a primeira chamada de um processo (JIT, WireMock) pode passar do
    /// timeout curto, então o login e um executeQuery rodam com folga antes de apertar o timeout.
    /// </summary>
    private async Task<SankhyaClient> ClienteDeUmaSessaoAsync(int maxRetries = 2)
    {
        SankhyaClientOptions? opcoes = null;
        SankhyaClient cliente = _fake.WithQuery("""[{"name":"N","order":1,"userType":"I"}]""", "[[1]]")
                                     .CreateClient(SessionMode.Shared, maxRetries, o =>
                                     {
                                         o.Timeout = TimeSpan.FromSeconds(30);
                                         o.MaxSessions = 1;
                                         opcoes = o;
                                     });
        await cliente.Sql.QueryRawAsync<UsuarioLinha>("SELECT 1 N");
        opcoes!.Timeout = TimeoutCurto;
        return cliente;
    }

    // O WireMock só registra a requisição depois de responder: espera as respostas atrasadas antes de contar.
    private static Task AguardarRespostasAtrasadas() => Task.Delay(Atraso + TimeSpan.FromSeconds(1));

    private string[] CookiesDe(string servico) =>
        _fake.Calls(servico).Select(c => c.RequestMessage!.Headers!["Cookie"].Single()).ToArray();

    [Fact]
    public async Task Sessao_com_timeout_nao_volta_ao_pool()
    {
        _fake.WithLoginSequence("S1", "S2").WithDelay(Load, Atraso);
        await using SankhyaClient cliente = await ClienteDeUmaSessaoAsync();
        var usuarios = cliente.Repository<Usuario>("Usuario");

        var ex = await Assert.ThrowsAsync<SankhyaTransportException>(() => usuarios.FindAsync("this.CODUSU = 1"));
        Assert.Equal(SankhyaErrorKind.Timeout, ex.Kind);
        await Assert.ThrowsAsync<SankhyaTransportException>(() => usuarios.FindAsync("this.CODUSU = 2"));
        await AguardarRespostasAtrasadas();

        Assert.Equal(["JSESSIONID=S1", "JSESSIONID=S2"], CookiesDe(Load));
        Assert.Equal(2, _fake.Calls(Login).Count);
        Assert.Empty(((SankhyaAPI.Client.Session.SankhyaSession)cliente.Session).PooledSessionIds);
    }

    [Fact]
    public async Task Timeout_nao_e_repetido_na_operacao()
    {
        _fake.WithDelay(Load, Atraso);
        await using SankhyaClient cliente = await ClienteDeUmaSessaoAsync(maxRetries: 2);

        await Assert.ThrowsAsync<SankhyaTransportException>(() => cliente.Repository<Usuario>("Usuario").FindAsync("1 = 1"));
        await AguardarRespostasAtrasadas();

        Assert.Single(_fake.Calls(Load));
    }

    [Fact]
    public async Task Timeout_nao_e_repetido_no_login()
    {
        // Aquece o processo e o servidor antes do login lento.
        await using (SankhyaClient aquecimento = await ClienteDeUmaSessaoAsync()) { }
        using var lento = new SankhyaFakeServer();
        using (var http = new HttpClient()) await http.GetAsync(lento.BaseUrl);
        lento.WithDelay(Login, Atraso);
        await using SankhyaClient cliente = lento.CreateClient(SessionMode.Shared, 2, o => o.Timeout = TimeoutCurto);

        var ex = await Assert.ThrowsAsync<SankhyaTransportException>(() => cliente.Repository<Usuario>("Usuario").FindAsync("1 = 1"));
        Assert.Equal(SankhyaErrorKind.Timeout, ex.Kind);
        await AguardarRespostasAtrasadas();

        Assert.Single(lento.Calls(Login));
    }

    [Fact]
    public async Task Cancelamento_pelo_chamador_descarta_a_sessao()
    {
        _fake.WithLoginSequence("S1", "S2").WithDelay(Load, Atraso);
        await using SankhyaClient cliente = await ClienteDeUmaSessaoAsync();
        var usuarios = cliente.Repository<Usuario>("Usuario");

        using (var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200)))
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => usuarios.FindAsync("1 = 1", cts.Token));
        using (var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200)))
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => usuarios.FindAsync("1 = 1", cts.Token));
        await AguardarRespostasAtrasadas();

        Assert.Equal(["JSESSIONID=S1", "JSESSIONID=S2"], CookiesDe(Load));
    }

    [Fact]
    public async Task Gateway_timeout_de_proxy_nao_se_repete_e_descarta_a_sessao()
    {
        _fake.WithLoginSequence("S1", "S2").WithHttpStatus(Load, 504);
        await using SankhyaClient cliente = await ClienteDeUmaSessaoAsync(maxRetries: 2);
        var usuarios = cliente.Repository<Usuario>("Usuario");

        await Assert.ThrowsAsync<SankhyaTransportException>(() => usuarios.FindAsync("1 = 1"));
        await Assert.ThrowsAsync<SankhyaTransportException>(() => usuarios.FindAsync("1 = 1"));

        Assert.Equal(["JSESSIONID=S1", "JSESSIONID=S2"], CookiesDe(Load));
    }

    [Fact]
    public async Task Recusa_por_concorrencia_descarta_a_sessao()
    {
        _fake.WithLoginSequence("S1", "S2")
             .WithError(Load, "O serviço foi cancelado por situação de concorrência. Essa mesma sessão HTTP fez a requisição duas vezes simultaneamente.");
        await using SankhyaClient cliente = await ClienteDeUmaSessaoAsync();
        var usuarios = cliente.Repository<Usuario>("Usuario");

        var ex = await Assert.ThrowsAsync<SankhyaException>(() => usuarios.FindAsync("1 = 1"));
        Assert.Equal(SankhyaErrorKind.Concurrency, ex.Kind);
        await Assert.ThrowsAsync<SankhyaException>(() => usuarios.FindAsync("1 = 1"));

        Assert.Equal(["JSESSIONID=S1", "JSESSIONID=S2"], CookiesDe(Load));
    }

    [Fact]
    public async Task Timeout_embrulhado_pela_operacao_do_consumidor_tambem_descarta_a_sessao()
    {
        _fake.WithLoginSequence("S1", "S2").WithDelay(Load, Atraso);
        await using SankhyaClient cliente = await ClienteDeUmaSessaoAsync();
        var ids = new List<string>();

        // A operação usa o transporte com a sessão recebida (chamar o repositório aqui dentro esperaria a única vaga do pool).
        for (int i = 0; i < 2; i++)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => cliente.Session.ExecuteAsync<int>(async (sessao, ct) =>
            {
                ids.Add(sessao);
                try { await cliente.Transport.SendXmlAsync(Load, [], sessao, idempotent: true, ct); }
                catch (SankhyaTransportException e) { throw new InvalidOperationException("embrulho do consumidor", e); }
                return 0;
            }));
        }

        Assert.Equal(["S1", "S2"], ids);
    }

    [Fact]
    public async Task Falha_HTTP_com_resposta_completa_devolve_a_sessao_ao_pool()
    {
        _fake.WithLoginSequence("S1", "S2").WithHttpStatus(Load, 503);
        await using SankhyaClient cliente = await ClienteDeUmaSessaoAsync(maxRetries: 0);
        var usuarios = cliente.Repository<Usuario>("Usuario");

        await Assert.ThrowsAsync<SankhyaTransportException>(() => usuarios.FindAsync("1 = 1"));
        await Assert.ThrowsAsync<SankhyaTransportException>(() => usuarios.FindAsync("1 = 1"));

        Assert.Equal(["JSESSIONID=S1", "JSESSIONID=S1"], CookiesDe(Load));
        Assert.Single(_fake.Calls(Login));
    }

    [Theory]
    [InlineData("http://erp.exemplo.com/mge", false, "https")]
    [InlineData("https://erp.exemplo.com", false, "/mge")]
    [InlineData("nao-e-url", false, "absoluta")]
    public void Opcoes_invalidas_sao_rejeitadas(string url, bool allowHttp, string trecho)
    {
        var ex = Assert.Throws<OptionsValidationException>(() => SankhyaClient.Create(new SankhyaClientOptions
        {
            BaseUrl = url, Usuario = "u", Senha = "s", AllowHttp = allowHttp
        }));

        Assert.Contains(trecho, ex.Message);
    }

    [Fact]
    public async Task Injecao_de_dependencia_resolve_e_valida_as_opcoes()
    {
        _fake.WithEntities(Load, ["0", "SUP"]);
        var services = new ServiceCollection();
        services.AddSankhya(o =>
                {
                    o.BaseUrl = _fake.BaseUrl;
                    o.Usuario = "usuario";
                    o.Senha = "senha";
                    o.AllowHttp = true;
                })
                .AddRepository<Usuario>("Usuario");

        await using ServiceProvider provider = services.BuildServiceProvider();
        var usuarios = provider.GetRequiredService<ISankhyaRepository<Usuario>>();

        Assert.Single(await usuarios.FindAsync("1 = 1"));
        Assert.NotNull(provider.GetRequiredService<ISankhyaSql>());
        Assert.NotNull(provider.GetRequiredService<ISankhyaReports>());

        var invalido = new ServiceCollection();
        invalido.AddSankhya(o => o.BaseUrl = "http://sem-mge");
        await using ServiceProvider providerInvalido = invalido.BuildServiceProvider();
        Assert.Throws<OptionsValidationException>(() => providerInvalido.GetRequiredService<IOptions<SankhyaClientOptions>>().Value);
    }

    [Fact]
    public async Task Relatorio_gera_e_baixa_na_mesma_sessao_com_os_metadados()
    {
        byte[] pdf = Encoding.ASCII.GetBytes("%PDF-1.4 fake");
        _fake.WithReport("CHAVE123", pdf);
        await using SankhyaClient cliente = _fake.CreateClient(SessionMode.PerOperation);

        await using SankhyaReportFile arquivo = await cliente.Reports.GenerateAsync(
            42,
            [new ReportParameter("CODEMP", "java.math.BigDecimal", "1")]);

        using var copia = new MemoryStream();
        await arquivo.Content.CopyToAsync(copia);
        Assert.Equal(pdf, copia.ToArray());
        Assert.Equal("application/pdf", arquivo.ContentType);
        Assert.True(arquivo.CanExport);
        Assert.False(arquivo.CanSendByEmail);

        string body = _fake.Calls("VisualizadorRelatorios.visualizarRelatorio").Single().RequestMessage!.Body!;
        Assert.Contains("nuRfe=\"42\"", body);
        Assert.Contains("nome=\"CODEMP\"", body);
        Assert.Single(_fake.Calls(Login));
        Assert.Single(_fake.Calls(Logout));
    }

    [Fact]
    public async Task Logs_e_tracing_nao_carregam_valores_de_campo_nem_parametros()
    {
        const string segredo = "CPF-123.456.789-00";
        _fake.WithEntities(Load, ["0", "SUP"]);
        var logs = new ListaDeLogs();
        using var listener = new ActivityListener
        {
            ShouldListenTo = s => s.Name == SankhyaTelemetry.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        };
        var atividades = new List<Activity>();
        listener.ActivityStopped = a => { lock (atividades) atividades.Add(a); };
        ActivitySource.AddActivityListener(listener);

        await using (SankhyaClient cliente = SankhyaClient.Create(
                         new SankhyaClientOptions { BaseUrl = _fake.BaseUrl, Usuario = "usuario", Senha = "senha", AllowHttp = true },
                         LoggerFactory.Create(b => b.AddProvider(logs).SetMinimumLevel(LogLevel.Trace))))
        {
            await cliente.Repository<Usuario>("Usuario").FindAsync("this.NOMEUSU = ?", [Parameter.Texto(segredo)]);
        }

        Assert.NotEmpty(logs.Mensagens);
        Assert.DoesNotContain(logs.Mensagens, m => m.Contains(segredo) || m.Contains("senha"));
        Assert.Contains(atividades, a => a.DisplayName == Load);
        Assert.DoesNotContain(atividades.SelectMany(a => a.Tags), t => t.Value?.Contains(segredo) == true);
    }

    private sealed class ListaDeLogs : ILoggerProvider, ILogger
    {
        public List<string> Mensagens { get; } = [];
        public ILogger CreateLogger(string categoryName) => this;
        public void Dispose() { }
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            lock (Mensagens) Mensagens.Add(formatter(state, exception));
        }
    }
}
