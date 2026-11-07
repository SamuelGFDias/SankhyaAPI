using System.Text;
using WireMock.Logging;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace SankhyaAPI.Client.Tests.Fake;

/// <summary>
/// Servidor HTTP local que imita o service.sbr do Sankhya. Cada teste sobe o seu, registra as respostas
/// dos serviços que vai chamar e depois inspeciona as requisições recebidas.
/// </summary>
/// <remarks>
/// O pacote espera a URL base com "/mge" (como no servidor real), então os serviços respondem em
/// "/mge/service.sbr?serviceName=...". As respostas imitam o formato do Sankhya: XML em ISO-8859-1 com
/// mensagens de erro em base64, e JSON no executeQuery.
/// </remarks>
public sealed class SankhyaFakeServer : IDisposable
{
    public const string ServicePath = "/mge/service.sbr";
    public const string SessionId = "FAKESESSION123";

    private readonly WireMockServer _server = WireMockServer.Start();

    public string BaseUrl => _server.Url + "/mge";

    public SankhyaFakeServer()
    {
        ComLogin();
        ComLogout();
    }

    public SankhyaFakeServer ComLogin(string sessionId = SessionId) => ComLoginIdUsuBruto(Base64("0"), sessionId);

    /// <summary>
    /// Login com o conteúdo de &lt;idusu&gt; exatamente como informado (vazio, base64 inválido...).
    /// </summary>
    public SankhyaFakeServer ComLoginIdUsuBruto(string idusu, string sessionId = SessionId) =>
        Responder("MobileLoginSP.login",
            Xml("MobileLoginSP.login",
                $"<responseBody><jsessionid>{sessionId}</jsessionid><idusu>{idusu}</idusu><callID>1</callID></responseBody>"));

    public SankhyaFakeServer ComLogout() =>
        Responder("MobileLoginSP.logout", Xml("MobileLoginSP.logout", "<responseBody />"));

    /// <summary>
    /// Resposta de loadRecords/saveRecord no formato "f0, f1, ...", na ordem dos campos pedidos.
    /// </summary>
    public SankhyaFakeServer ComEntidades(string serviceName, params string[][] linhas)
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
        return Responder(serviceName, Xml(serviceName, $"<responseBody>{entities}</responseBody>"));
    }

    public SankhyaFakeServer ComXmlBruto(string serviceName, string responseBody) =>
        Responder(serviceName, Xml(serviceName, responseBody));

    /// <summary>
    /// Resposta XML com o status e o conteúdo informados, para exercitar combinações de status e mensagem.
    /// </summary>
    public SankhyaFakeServer ComStatus(string serviceName, string status, string conteudo = "") =>
        Responder(serviceName,
            $"""<?xml version="1.0" encoding="ISO-8859-1"?><serviceResponse serviceName="{serviceName}" status="{status}" pendingPrinting="false">{conteudo}</serviceResponse>""");

    public static string Base64(string texto) => Convert.ToBase64String(Encoding.Latin1.GetBytes(texto));

    public SankhyaFakeServer ComErro(string serviceName, string mensagem) =>
        Responder(serviceName,
            $"""<?xml version="1.0" encoding="ISO-8859-1"?><serviceResponse serviceName="{serviceName}" status="0" pendingPrinting="false"><statusMessage><![CDATA[{Base64(mensagem)}]]></statusMessage></serviceResponse>""");

    public SankhyaFakeServer ComQuery(string fieldsMetadataJson, string rowsJson) =>
        Responder("DbExplorerSP.executeQuery",
            $$$"""{"serviceName":"DbExplorerSP.executeQuery","status":"1","pendingPrinting":"false","responseBody":{"fieldsMetadata":{{{fieldsMetadataJson}}},"rows":{{{rowsJson}}},"burstLimit":false}}""",
            "application/json");

    public SankhyaFakeServer ComQueryErro(string mensagem) =>
        Responder("DbExplorerSP.executeQuery",
            $$$"""{"serviceName":"DbExplorerSP.executeQuery","status":"0","pendingPrinting":"false","statusMessage":"{{{mensagem}}}"}""",
            "application/json");

    public SankhyaFakeServer ComStatusHttp(string serviceName, int status) =>
        Responder(serviceName, "", "text/plain", status);

    /// <summary>
    /// Requisições recebidas para o serviço informado, na ordem de chegada.
    /// </summary>
    public IReadOnlyList<ILogEntry> Chamadas(string serviceName) =>
        _server.LogEntries
               .Where(e => e.RequestMessage.Query?.TryGetValue("serviceName", out var v) == true
                        && v.Contains(serviceName))
               .ToList();

    public IReadOnlyList<ILogEntry> TodasAsChamadas => _server.LogEntries.ToList();

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
