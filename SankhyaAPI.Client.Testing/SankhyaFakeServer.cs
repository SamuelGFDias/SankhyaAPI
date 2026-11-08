using System.Text;
using SankhyaAPI.Client.Configuration;
using WireMock.Logging;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace SankhyaAPI.Client.Testing;

/// <summary>
/// Servidor HTTP local que imita o service.sbr do Sankhya. Cada teste sobe o seu, registra as respostas
/// dos serviços que vai chamar e depois inspeciona as requisições recebidas.
/// </summary>
/// <remarks>
/// O cliente espera a URL base com "/mge" (como no servidor real), então os serviços respondem em
/// "/mge/service.sbr?serviceName=...". As respostas imitam o formato do Sankhya: XML em ISO-8859-1 com
/// mensagens de erro em base64, e JSON no executeQuery.
/// </remarks>
public sealed class SankhyaFakeServer : IDisposable
{
    /// <summary>Caminho do serviço <c>service.sbr</c> usado pelo cliente.</summary>
    public const string ServicePath = "/mge/service.sbr";

    /// <summary>Identificador de sessão devolvido pelo login padrão.</summary>
    public const string SessionId = "FAKESESSION123";

    private readonly WireMockServer _server = WireMockServer.Start();

    /// <summary>URL base do servidor fake, já com o sufixo <c>/mge</c>.</summary>
    public string BaseUrl => _server.Url + "/mge";

    /// <summary>
    /// Cria o servidor com o login e o logout padrão já registrados.
    /// </summary>
    public SankhyaFakeServer()
    {
        WithLogin();
        WithLogout();
    }

    /// <summary>
    /// Registra o login com sucesso, usando o identificador de usuário "0" codificado em base64.
    /// </summary>
    /// <param name="sessionId">Identificador de sessão que o login devolve.</param>
    public SankhyaFakeServer WithLogin(string sessionId = SessionId) => WithLoginRawUserId(Base64("0"), sessionId);

    /// <summary>
    /// Login com o conteúdo de &lt;idusu&gt; exatamente como informado (vazio, base64 inválido...).
    /// </summary>
    /// <param name="idusu">Conteúdo bruto do campo &lt;idusu&gt; da resposta.</param>
    /// <param name="sessionId">Identificador de sessão que o login devolve.</param>
    public SankhyaFakeServer WithLoginRawUserId(string idusu, string sessionId = SessionId) =>
        Responder("MobileLoginSP.login",
            Xml("MobileLoginSP.login",
                $"<responseBody><jsessionid>{sessionId}</jsessionid><idusu>{idusu}</idusu><callID>1</callID></responseBody>"));

    /// <summary>
    /// Registra o logout com sucesso.
    /// </summary>
    public SankhyaFakeServer WithLogout() =>
        Responder("MobileLoginSP.logout", Xml("MobileLoginSP.logout", "<responseBody />"));

    /// <summary>
    /// Resposta de loadRecords/saveRecord no formato "f0, f1, ...", na ordem dos campos pedidos.
    /// </summary>
    /// <param name="serviceName">Nome do serviço a responder, por exemplo <c>CRUDServiceProvider.loadRecords</c>.</param>
    /// <param name="linhas">Uma matriz de valores por entidade retornada.</param>
    public SankhyaFakeServer WithEntities(string serviceName, params string[][] linhas) =>
        Responder(serviceName, Xml(serviceName, $"<responseBody>{Entidades(linhas)}</responseBody>"));

    private static string Entidades(string[][] linhas)
    {
        var entities = new StringBuilder($"<entities total=\"{linhas.Length}\">");
        foreach (string[] linha in linhas)
        {
            entities.Append("<entity>");
            for (int i = 0; i < linha.Length; i++)
                entities.Append($"<f{i}>{linha[i]}</f{i}>");
            entities.Append("</entity>");
        }

        entities.Append("</entities>");
        return entities.ToString();
    }

    /// <summary>
    /// Responde ao serviço com o corpo XML informado, sem nenhuma montagem adicional.
    /// </summary>
    /// <param name="serviceName">Nome do serviço a responder.</param>
    /// <param name="responseBody">Conteúdo que vai dentro do <c>serviceResponse</c>.</param>
    public SankhyaFakeServer WithRawXml(string serviceName, string responseBody) =>
        Responder(serviceName, Xml(serviceName, responseBody));

    /// <summary>
    /// Resposta XML com o status e o conteúdo informados, para exercitar combinações de status e mensagem.
    /// </summary>
    /// <param name="serviceName">Nome do serviço a responder.</param>
    /// <param name="status">Valor do atributo <c>status</c> da resposta.</param>
    /// <param name="conteudo">Conteúdo que vai dentro do <c>serviceResponse</c>.</param>
    public SankhyaFakeServer WithStatus(string serviceName, string status, string conteudo = "") =>
        Responder(serviceName, XmlStatus(serviceName, status, conteudo));

    private static string XmlStatus(string serviceName, string status, string conteudo) =>
        $"""<?xml version="1.0" encoding="ISO-8859-1"?><serviceResponse serviceName="{serviceName}" status="{status}" pendingPrinting="false">{conteudo}</serviceResponse>""";

    /// <summary>
    /// Codifica o texto em base64 no charset ISO-8859-1, como o Sankhya faz com as mensagens de erro.
    /// </summary>
    /// <param name="texto">Texto a codificar.</param>
    public static string Base64(string texto) => Convert.ToBase64String(Encoding.Latin1.GetBytes(texto));

    /// <summary>
    /// Responde ao serviço com status de erro e a mensagem informada, codificada em base64 dentro de CDATA.
    /// </summary>
    /// <param name="serviceName">Nome do serviço a responder.</param>
    /// <param name="mensagem">Mensagem de erro devolvida pelo Sankhya.</param>
    public SankhyaFakeServer WithError(string serviceName, string mensagem) =>
        Responder(serviceName,
            $"""<?xml version="1.0" encoding="ISO-8859-1"?><serviceResponse serviceName="{serviceName}" status="0" pendingPrinting="false"><statusMessage><![CDATA[{Base64(mensagem)}]]></statusMessage></serviceResponse>""");

    /// <summary>
    /// Responde ao executeQuery (JSON) com os metadados e as linhas informados.
    /// </summary>
    /// <param name="fieldsMetadataJson">Conteúdo JSON de <c>fieldsMetadata</c>, sem as chaves externas.</param>
    /// <param name="rowsJson">Conteúdo JSON do array <c>rows</c>.</param>
    /// <param name="burstLimit">Se <c>true</c>, sinaliza que a consulta atingiu o limite de linhas.</param>
    public SankhyaFakeServer WithQuery(string fieldsMetadataJson, string rowsJson, bool burstLimit = false) =>
        Responder("DbExplorerSP.executeQuery",
            $$$"""{"serviceName":"DbExplorerSP.executeQuery","status":"1","pendingPrinting":"false","responseBody":{"fieldsMetadata":{{{fieldsMetadataJson}}},"rows":{{{rowsJson}}},"burstLimit":{{{(burstLimit ? "true" : "false")}}}}}""",
            "application/json");

    /// <summary>
    /// Responde ao executeQuery (JSON) com status de erro e a mensagem informada.
    /// </summary>
    /// <param name="mensagem">Mensagem de erro devolvida pelo Sankhya.</param>
    public SankhyaFakeServer WithQueryError(string mensagem) =>
        Responder("DbExplorerSP.executeQuery",
            $$$"""{"serviceName":"DbExplorerSP.executeQuery","status":"0","pendingPrinting":"false","statusMessage":"{{{mensagem}}}"}""",
            "application/json");

    /// <summary>
    /// Responde <c>status="3"</c> (sessão expirada) na primeira chamada ao serviço e a resposta normal de entidades depois.
    /// </summary>
    /// <param name="serviceName">Nome do serviço a responder.</param>
    /// <param name="linhas">Uma matriz de valores por entidade retornada na segunda chamada.</param>
    public SankhyaFakeServer WithSessionExpiredOnce(string serviceName, params string[][] linhas)
    {
        string cenario = "expira-" + serviceName;
        _server.Given(Request.Create().WithPath(ServicePath).WithParam("serviceName", serviceName).UsingPost())
               .InScenario(cenario).WillSetStateTo("renovada")
               .RespondWith(Response.Create().WithHeader("Content-Type", "text/xml; charset=ISO-8859-1")
                                    .WithBody(XmlStatus(serviceName, "3", $"<statusMessage><![CDATA[{Base64("Não autorizado.")}]]></statusMessage>"), encoding: Encoding.Latin1));
        _server.Given(Request.Create().WithPath(ServicePath).WithParam("serviceName", serviceName).UsingPost())
               .InScenario(cenario).WhenStateIs("renovada")
               .RespondWith(Response.Create().WithHeader("Content-Type", "text/xml; charset=ISO-8859-1")
                                    .WithBody(Xml(serviceName, $"<responseBody>{Entidades(linhas)}</responseBody>"), encoding: Encoding.Latin1));
        return this;
    }

    /// <summary>
    /// Resposta de entidades com atraso, para testar cancelamento e timeout.
    /// </summary>
    /// <param name="serviceName">Nome do serviço a responder com atraso.</param>
    /// <param name="atraso">Tempo que o servidor espera antes de responder.</param>
    public SankhyaFakeServer WithDelay(string serviceName, TimeSpan atraso)
    {
        _server.Given(Request.Create().WithPath(ServicePath).WithParam("serviceName", serviceName).UsingPost())
               .RespondWith(Response.Create().WithDelay(atraso).WithHeader("Content-Type", "text/xml; charset=ISO-8859-1")
                                    .WithBody(Xml(serviceName, "<responseBody />"), encoding: Encoding.Latin1));
        return this;
    }

    /// <summary>
    /// Relatório: o visualizarRelatorio devolve a chave e o visualizadorArquivos.mge devolve o conteúdo.
    /// </summary>
    /// <param name="chave">Chave do arquivo gerado pelo relatório.</param>
    /// <param name="conteudo">Bytes do arquivo devolvido pelo download.</param>
    /// <param name="contentType">Media type do arquivo devolvido.</param>
    public SankhyaFakeServer WithReport(string chave, byte[] conteudo, string contentType = "application/pdf")
    {
        Responder("VisualizadorRelatorios.visualizarRelatorio",
            Xml("VisualizadorRelatorios.visualizarRelatorio",
                $"<responseBody><chave valor=\"{chave}\" contentType=\"{contentType}\" format=\"pdf\" permiteExportar=\"S\" canSendViaEmail=\"N\" /></responseBody>"));
        _server.Given(Request.Create().WithPath("/mge/visualizadorArquivos.mge").WithParam("chaveArquivo", chave).UsingPost())
               .RespondWith(Response.Create().WithHeader("Content-Type", contentType).WithBody(conteudo));
        return this;
    }

    /// <summary>
    /// Resposta de uma página do loadRecords (casa pelo <c>offsetPage</c> do pedido).
    /// </summary>
    /// <param name="pagina">Número da página (<c>offsetPage</c>) que esta resposta atende.</param>
    /// <param name="temMais"><c>true</c> para sinalizar que existem mais páginas (<c>hasMoreResult</c>).</param>
    /// <param name="linhas">Uma matriz de valores por entidade retornada na página.</param>
    public SankhyaFakeServer WithPage(int pagina, bool temMais, params string[][] linhas)
    {
        string entities = Entidades(linhas).Replace("<entities ", $"<entities hasMoreResult=\"{(temMais ? "true" : "false")}\" offsetPage=\"{pagina}\" ");
        _server.Given(Request.Create().WithPath(ServicePath).WithParam("serviceName", "CRUDServiceProvider.loadRecords").UsingPost()
                             .WithBody(new WireMock.Matchers.WildcardMatcher($"*offsetPage=\"{pagina}\"*")))
               .RespondWith(Response.Create().WithHeader("Content-Type", "text/xml; charset=ISO-8859-1")
                                    .WithBody(Xml("CRUDServiceProvider.loadRecords", $"<responseBody>{entities}</responseBody>"), encoding: Encoding.Latin1));
        return this;
    }

    /// <summary>
    /// executeQuery que responde conforme o SQL contenha o trecho informado (para a consulta paginada).
    /// </summary>
    /// <param name="trechoDoSql">Trecho que precisa aparecer no SQL enviado para esta resposta ser usada.</param>
    /// <param name="fieldsMetadataJson">Conteúdo JSON de <c>fieldsMetadata</c>, sem as chaves externas.</param>
    /// <param name="rowsJson">Conteúdo JSON do array <c>rows</c>.</param>
    public SankhyaFakeServer WithQueryWhen(string trechoDoSql, string fieldsMetadataJson, string rowsJson)
    {
        _server.Given(Request.Create().WithPath(ServicePath).WithParam("serviceName", "DbExplorerSP.executeQuery").UsingPost()
                             .WithBody(new WireMock.Matchers.WildcardMatcher($"*{trechoDoSql}*")))
               .RespondWith(Response.Create().WithHeader("Content-Type", "application/json; charset=UTF-8")
                                    .WithBody($$$"""{"serviceName":"DbExplorerSP.executeQuery","status":"1","responseBody":{"fieldsMetadata":{{{fieldsMetadataJson}}},"rows":{{{rowsJson}}},"burstLimit":false}}"""));
        return this;
    }

    /// <summary>
    /// Responde ao serviço com um status HTTP de erro (ou outro) e corpo vazio em texto puro.
    /// </summary>
    /// <param name="serviceName">Nome do serviço a responder.</param>
    /// <param name="status">Código HTTP da resposta.</param>
    public SankhyaFakeServer WithHttpStatus(string serviceName, int status) =>
        Responder(serviceName, "", "text/plain", status);

    /// <summary>
    /// Requisições recebidas para o serviço informado, na ordem de chegada.
    /// </summary>
    /// <param name="serviceName">Nome do serviço cujas requisições interessam.</param>
    public IReadOnlyList<ILogEntry> Calls(string serviceName) =>
        _server.LogEntries
               .Where(e => e.RequestMessage?.Query?.TryGetValue("serviceName", out var v) == true
                        && v.Contains(serviceName))
               .ToList();

    /// <summary>
    /// Todas as requisições recebidas, de qualquer serviço, na ordem de chegada.
    /// </summary>
    public IReadOnlyList<ILogEntry> AllCalls => _server.LogEntries.ToList();

    /// <summary>
    /// Para o servidor HTTP fake.
    /// </summary>
    public void Dispose() => _server.Stop();

    private SankhyaFakeServer Responder(string serviceName, string body, string contentType = "text/xml", int status = 200)
    {
        _server.Given(Request.Create().WithPath(ServicePath).WithParam("serviceName", serviceName).UsingPost())
               .RespondWith(Response.Create()
                                    .WithStatusCode(status)
                                    .WithHeader("Content-Type", contentType + "; charset=ISO-8859-1")
                                    .WithBody(body, encoding: Encoding.Latin1));
        return this;
    }

    private static string Xml(string serviceName, string responseBody) =>
        $"""<?xml version="1.0" encoding="ISO-8859-1"?><serviceResponse serviceName="{serviceName}" status="1" pendingPrinting="false" transactionId="T1">{responseBody}</serviceResponse>""";
}

/// <summary>
/// Atalhos de extensão para criar clientes apontados para o servidor fake.
/// </summary>
public static class SankhyaFakeServerExtensions
{
    /// <summary>
    /// Cria um cliente apontando para o servidor fake. Por padrão, uma sessão por operação (login e logout em cada chamada).
    /// </summary>
    /// <param name="fake">Servidor fake que recebe as requisições do cliente.</param>
    /// <param name="mode">Modo de sessão do cliente.</param>
    /// <param name="maxRetries">Número máximo de novas tentativas do cliente.</param>
    public static SankhyaClient CreateClient(this SankhyaFakeServer fake, SessionMode mode = SessionMode.PerOperation, int maxRetries = 2) =>
        SankhyaClient.Create(new SankhyaClientOptions
        {
            BaseUrl = fake.BaseUrl, Usuario = "usuario", Senha = "senha", AllowHttp = true, SessionMode = mode, MaxRetries = maxRetries
        });
}
