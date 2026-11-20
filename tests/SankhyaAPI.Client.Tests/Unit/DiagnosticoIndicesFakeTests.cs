using Microsoft.Extensions.Logging;
using SankhyaAPI.Client.Diagnostics;
using SankhyaAPI.Client.Exceptions;
using SankhyaAPI.Client.Sql;
using SankhyaAPI.Client.Tests.Support;

namespace SankhyaAPI.Client.Tests.Unit;

/// <summary>
/// Aviso de índice faltando: a exclusão de uma linha-pai confere cada FK que a referencia, e sem índice na coluna da
/// tabela filha cada conferência varre a filha inteira (docs/sankhya-protocolo.md, seção 10).
/// </summary>
public class DiagnosticoIndicesFakeTests : IDisposable
{
    private const string Remove = "CRUDServiceProvider.removeRecord";
    private const string Query = "DbExplorerSP.executeQuery";
    private const string MetaTabela = """[{"name":"NOMETAB","order":1,"userType":"S"}]""";

    private const string MetaFks =
        """[{"name":"FK","order":1,"userType":"S"},{"name":"FILHA","order":2,"userType":"S"},{"name":"COLUNA","order":3,"userType":"S"},{"name":"ORDEM","order":4,"userType":"I"},{"name":"LINHAS","order":5,"userType":"I"}]""";

    private const string FksSemIndice =
        """[["FK_TADOSE_CTT","AD_TADOSE","CODPARC",1,711245],["FK_TADOSE_CTT","AD_TADOSE","CODCONTATO",2,711245],["FK_FIN_CTT","TGFFIN","CODPARC",1,135157],["FK_FIN_CTT","TGFFIN","CODCONTATO",2,135157],["FK_PEQUENA","TGFAGD","CODPARC",1,12]]""";

    private readonly SankhyaFakeServer _fake = new();
    private readonly ListaDeLogs _logs = new();

    public void Dispose() => _fake.Dispose();

    private SankhyaClient Cliente(Action<SankhyaClientOptions>? configure = null)
    {
        var opcoes = new SankhyaClientOptions
        {
            BaseUrl = _fake.BaseUrl, Usuario = "usuario", Senha = "senha", AllowHttp = true, SessionMode = SessionMode.Shared,
            SlowDeleteThreshold = TimeSpan.FromMilliseconds(300)
        };
        configure?.Invoke(opcoes);
        return SankhyaClient.Create(opcoes, LoggerFactory.Create(b => b.AddProvider(_logs).SetMinimumLevel(LogLevel.Trace)));
    }

    private SankhyaFakeServer ComCatalogo(string tabela = "TGFPRO") =>
        _fake.WithQueryWhen("TDDINS", MetaTabela, $"""[["{tabela}"]]""").WithQueryWhen("sys.foreign_keys", MetaFks, FksSemIndice);

    private string[] SqlEnviados() =>
        _fake.Calls(Query)
             .Select(c => System.Text.Json.JsonDocument.Parse(c.RequestMessage!.Body!).RootElement.GetProperty("requestBody").GetProperty("sql").GetString()!)
             .ToArray();

    private async Task<string[]> AguardarAvisosAsync(int quantidade)
    {
        for (int i = 0; i < 100; i++)
        {
            string[] avisos = _logs.Avisos();
            if (avisos.Length >= quantidade) return avisos;
            await Task.Delay(50);
        }

        return _logs.Avisos();
    }

    [Fact]
    public async Task Metodo_resolve_a_tabela_pela_entidade_e_agrupa_as_colunas_da_FK()
    {
        ComCatalogo("TGFCTT");
        await using SankhyaClient cliente = Cliente();

        IReadOnlyList<SankhyaIndexSuggestion> sugestoes = await cliente.Diagnostics.FindMissingForeignKeyIndexesAsync("Contato");

        Assert.Equal(["AD_TADOSE", "TGFFIN", "TGFAGD"], sugestoes.Select(s => s.ChildTable));
        SankhyaIndexSuggestion maior = sugestoes[0];
        Assert.Equal("TGFCTT", maior.ParentTable);
        Assert.Equal("FK_TADOSE_CTT", maior.ForeignKey);
        Assert.Equal(["CODPARC", "CODCONTATO"], maior.Columns);
        Assert.Equal(711245, maior.ChildRows);
        Assert.Equal("CREATE INDEX IX_AD_TADOSE_CODPARC_CODCONTATO ON AD_TADOSE (CODPARC, CODCONTATO)", maior.CreateIndexSql);

        string[] sql = SqlEnviados();
        Assert.Contains("NOMEINSTANCIA = 'Contato'", sql[0]);
        Assert.Contains("OBJECT_ID('TGFCTT')", sql[1]);
    }

    [Fact]
    public async Task Sem_entrada_no_dicionario_o_texto_e_tratado_como_tabela()
    {
        _fake.WithQueryWhen("TDDINS", MetaTabela, "[]").WithQueryWhen("sys.foreign_keys", MetaFks, "[]");
        await using SankhyaClient cliente = Cliente();

        Assert.Empty(await cliente.Diagnostics.FindMissingForeignKeyIndexesAsync("AD_MINHATABELA"));

        Assert.Contains("OBJECT_ID('AD_MINHATABELA')", SqlEnviados()[1]);
    }

    [Fact]
    public async Task Exclusao_lenta_loga_o_indice_sugerido_uma_vez_por_entidade()
    {
        ComCatalogo().WithDelay(Remove, TimeSpan.FromMilliseconds(600));
        await using SankhyaClient cliente = Cliente();
        var produtos = cliente.Repository<Produto>();

        await produtos.DeleteAsync(new Produto { CodProd = 1 });
        string[] avisos = await AguardarAvisosAsync(2);
        await produtos.DeleteAsync(new Produto { CodProd = 2 });
        await Task.Delay(500);

        // Só as filhas com volume relevante viram aviso (a TGFAGD tem 12 linhas).
        Assert.Equal(2, avisos.Length);
        Assert.Contains(avisos, a => a.Contains("CREATE INDEX IX_AD_TADOSE_CODPARC_CODCONTATO ON AD_TADOSE (CODPARC, CODCONTATO)") && a.Contains("711245"));
        Assert.Contains(avisos, a => a.Contains("TGFFIN"));
        Assert.Single(SqlEnviados(), s => s.Contains("sys.foreign_keys"));
        Assert.Equal(2, _logs.Avisos().Length);
    }

    [Fact]
    public async Task Timeout_na_exclusao_tambem_dispara_o_diagnostico()
    {
        ComCatalogo().WithDelay(Remove, TimeSpan.FromSeconds(2));
        await using SankhyaClient cliente = Cliente(o =>
        {
            o.Timeout = TimeSpan.FromMilliseconds(400);
            o.SlowDeleteThreshold = TimeSpan.FromMinutes(5);
        });
        await cliente.Sql.QueryAsDictionaryAsync("SELECT 1 FROM TDDINS"); // aquece o processo antes do timeout curto

        var ex = await Assert.ThrowsAsync<SankhyaTransportException>(() => cliente.Repository<Produto>().DeleteAsync(new Produto { CodProd = 1 }));
        Assert.Equal(SankhyaErrorKind.Timeout, ex.Kind);

        Assert.Equal(2, (await AguardarAvisosAsync(2)).Length);
    }

    [Fact]
    public async Task Exclusao_rapida_nao_consulta_o_catalogo()
    {
        ComCatalogo().WithStatus(Remove, "1");
        await using SankhyaClient cliente = Cliente(o => o.SlowDeleteThreshold = TimeSpan.FromSeconds(30));

        await cliente.Repository<Produto>().DeleteAsync(new Produto { CodProd = 1 });
        await Task.Delay(500);

        Assert.Empty(_fake.Calls(Query));
        Assert.Empty(_logs.Avisos());
    }

    [Fact]
    public async Task Desligado_por_opcao_nao_consulta_o_catalogo()
    {
        ComCatalogo().WithDelay(Remove, TimeSpan.FromMilliseconds(600));
        await using SankhyaClient cliente = Cliente(o => o.MissingIndexWarnings = false);

        await cliente.Repository<Produto>().DeleteAsync(new Produto { CodProd = 1 });
        await Task.Delay(500);

        Assert.Empty(_fake.Calls(Query));
    }

    [Fact]
    public async Task Sessao_explicita_nao_dispara_o_aviso_automatico()
    {
        ComCatalogo().WithDelay(Remove, TimeSpan.FromMilliseconds(600));
        await using SankhyaClient cliente = Cliente();
        await cliente.Session.LoginAsync();

        await cliente.Repository<Produto>().DeleteAsync(new Produto { CodProd = 1 });
        await Task.Delay(500);

        Assert.Empty(_fake.Calls(Query));
    }

    [Fact]
    public async Task Falha_do_diagnostico_nao_afeta_a_exclusao_e_e_tentada_de_novo()
    {
        _fake.WithQueryError("catálogo indisponível").WithDelay(Remove, TimeSpan.FromMilliseconds(600));
        await using SankhyaClient cliente = Cliente();
        var produtos = cliente.Repository<Produto>();

        await produtos.DeleteAsync(new Produto { CodProd = 1 });
        await Task.Delay(500);
        await produtos.DeleteAsync(new Produto { CodProd = 2 });
        await Task.Delay(500);

        Assert.Equal(2, _fake.Calls(Query).Count);
        Assert.Empty(_logs.Avisos());
    }

    [Fact]
    public async Task Oracle_nao_tem_diagnostico()
    {
        ComCatalogo().WithDelay(Remove, TimeSpan.FromMilliseconds(600));
        await using SankhyaClient cliente = Cliente(o => o.Dialect = SqlDialect.Oracle);

        await cliente.Repository<Produto>().DeleteAsync(new Produto { CodProd = 1 });
        await Task.Delay(500);

        Assert.Empty(_fake.Calls(Query));
        await Assert.ThrowsAsync<NotSupportedException>(() => cliente.Diagnostics.FindMissingForeignKeyIndexesAsync("Produto"));
    }

    private sealed class ListaDeLogs : ILoggerProvider, ILogger
    {
        private readonly List<(LogLevel Nivel, string Mensagem)> _entradas = [];

        public string[] Avisos()
        {
            lock (_entradas) return _entradas.Where(e => e.Nivel == LogLevel.Warning && e.Mensagem.Contains("CREATE INDEX")).Select(e => e.Mensagem).ToArray();
        }

        public ILogger CreateLogger(string categoryName) => this;
        public void Dispose() { }
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            lock (_entradas) _entradas.Add((logLevel, formatter(state, exception)));
        }
    }
}
