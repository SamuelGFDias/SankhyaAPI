using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SankhyaAPI.Client.Configuration;
using SankhyaAPI.Client.Diagnostics;
using SankhyaAPI.Client.Exceptions;
using SankhyaAPI.Client.Utils;

namespace SankhyaAPI.Client.Transport;

/// <summary>
/// Nomes dos serviços do <c>service.sbr</c> usados pelo pacote.
/// </summary>
public static class SankhyaServices
{
    public const string Login = "MobileLoginSP.login";
    public const string Logout = "MobileLoginSP.logout";
    public const string LoadRecords = "CRUDServiceProvider.loadRecords";
    public const string SaveRecord = "CRUDServiceProvider.saveRecord";
    public const string RemoveRecord = "CRUDServiceProvider.removeRecord";
    public const string ExecuteQuery = "DbExplorerSP.executeQuery";
    public const string Report = "VisualizadorRelatorios.visualizarRelatorio";
}

/// <summary>
/// Resposta XML já validada (status 1). <see cref="ResponseBody"/> é o elemento <c>&lt;responseBody&gt;</c>, quando há.
/// </summary>
public sealed record SankhyaXmlResponse(string ServiceName, string? Status, string? Message, XElement? ResponseBody);

/// <summary>
/// Chamadas cruas ao <c>service.sbr</c>: monta o envelope, envia, valida o <c>status</c> e devolve o corpo.
/// Use para serviços que o pacote não cobre; o resto da API é construído sobre ela.
/// </summary>
public interface ISankhyaTransport
{
    /// <summary>
    /// Envia <c>&lt;serviceRequest serviceName="..."&gt;&lt;requestBody&gt;...&lt;/requestBody&gt;&lt;/serviceRequest&gt;</c>
    /// com os elementos informados dentro do <c>requestBody</c>.
    /// </summary>
    /// <param name="idempotent">Se a operação pode ser repetida em falha transitória de transporte.</param>
    /// <exception cref="SankhyaException">O Sankhya respondeu com status diferente de 1.</exception>
    /// <exception cref="HttpRequestException">Falha HTTP ou resposta ilegível.</exception>
    Task<SankhyaXmlResponse> SendXmlAsync(
        string serviceName,
        IEnumerable<XElement> requestBody,
        string? sessionId,
        bool idempotent,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Envia o envelope JSON (<c>outputType=json</c>) e devolve o documento da resposta, já validado. Descarte o documento.
    /// </summary>
    Task<JsonDocument> SendJsonAsync(
        string serviceName,
        Action<Utf8JsonWriter> writeRequestBody,
        string? sessionId,
        bool idempotent,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Baixa um arquivo de uma rota relativa à URL base (por exemplo <c>visualizadorArquivos.mge?...</c>).
    /// O conteúdo é copiado para memória antes de devolver.
    /// </summary>
    Task<Stream> DownloadAsync(string relativePath, string sessionId, CancellationToken cancellationToken = default);
}

internal sealed class SankhyaTransport : ISankhyaTransport
{
    internal const string HttpClientName = "SankhyaAPI.Client";

    private static readonly XmlWriterSettings WriterSettings = new()
    {
        Encoding = new UTF8Encoding(false), OmitXmlDeclaration = false, Indent = false
    };

    private readonly HttpClient _http;
    private readonly SankhyaClientOptions _options;
    private readonly ILogger _logger;

    public SankhyaTransport(HttpClient http, SankhyaClientOptions options, ILogger<SankhyaTransport>? logger = null)
    {
        _http = http;
        _options = options;
        _logger = logger ?? NullLogger<SankhyaTransport>.Instance;
        _http.BaseAddress ??= options.ServiceBaseUri;
    }

    /// <summary>
    /// Handler sem <c>CookieContainer</c>: a sessão só vai no header que o pacote monta, então um cookie de uma
    /// sessão nunca vaza para outra.
    /// </summary>
    internal static HttpMessageHandler CreateHandler() => new SocketsHttpHandler
    {
        UseCookies = false, AutomaticDecompression = DecompressionMethods.All, PooledConnectionLifetime = TimeSpan.FromMinutes(5)
    };

    public async Task<SankhyaXmlResponse> SendXmlAsync(
        string serviceName,
        IEnumerable<XElement> requestBody,
        string? sessionId,
        bool idempotent,
        CancellationToken cancellationToken = default)
    {
        var envelope = new XElement("serviceRequest", new XAttribute("serviceName", serviceName), new XElement("requestBody", requestBody));
        byte[] body = Serialize(envelope);

        XDocument document = await SendAsync(
            serviceName,
            () => new ByteArrayContent(body) { Headers = { { "Content-Type", "text/xml; charset=UTF-8" } } },
            $"service.sbr?serviceName={serviceName}",
            sessionId,
            idempotent,
            ReadXmlAsync,
            cancellationToken);

        XElement root = document.Root ?? throw new HttpRequestException("A resposta do Sankhya veio sem conteúdo XML.");
        string? status = root.Attribute("status")?.Value;
        string? message = SankhyaText.DecodeBase64(root.Element("statusMessage")?.Value);
        XElement? tsError = root.Element("tsError");
        string? tsErrorCode = tsError?.Attribute("tsErrorCode")?.Value;
        message ??= SankhyaText.DecodeBase64(tsError?.Element("statusMessage")?.Value);

        SankhyaResponses.EnsureSuccess(serviceName, status, message, tsErrorCode);
        return new SankhyaXmlResponse(serviceName, status, message, root.Element("responseBody"));
    }

    public async Task<JsonDocument> SendJsonAsync(
        string serviceName,
        Action<Utf8JsonWriter> writeRequestBody,
        string? sessionId,
        bool idempotent,
        CancellationToken cancellationToken = default)
    {
        byte[] body = SerializeJson(serviceName, writeRequestBody);

        JsonDocument document = await SendAsync(
            serviceName,
            () => new ByteArrayContent(body) { Headers = { { "Content-Type", "application/json; charset=UTF-8" } } },
            $"service.sbr?serviceName={serviceName}&outputType=json",
            sessionId,
            idempotent,
            // Lido como texto: respeita o charset do Content-Type em vez de supor UTF-8.
            async (content, ct) => JsonDocument.Parse(await content.ReadAsStringAsync(ct)),
            cancellationToken);

        try
        {
            JsonElement root = document.RootElement;
            string? status = root.TryGetProperty("status", out JsonElement s) ? s.GetString() : null;
            string? message = root.TryGetProperty("statusMessage", out JsonElement m) ? SankhyaText.DecodeBase64(m.GetString()) : null;
            SankhyaResponses.EnsureSuccess(serviceName, status, message, null);
            return document;
        } catch
        {
            document.Dispose();
            throw;
        }
    }

    public async Task<Stream> DownloadAsync(string relativePath, string sessionId, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, relativePath);
        request.Headers.Add("Cookie", $"JSESSIONID={sessionId}");

        using HttpResponseMessage response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        EnsureHttpSuccess(response, relativePath);

        var buffer = new MemoryStream();
        await response.Content.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;
        return buffer;
    }

    private async Task<TResult> SendAsync<TResult>(
        string serviceName,
        Func<HttpContent> content,
        string path,
        string? sessionId,
        bool idempotent,
        Func<HttpContent, CancellationToken, Task<TResult>> read,
        CancellationToken cancellationToken)
    {
        int maxAttempts = idempotent ? _options.MaxRetries + 1 : 1;

        for (int attempt = 1;; attempt++)
        {
            using Activity? activity = SankhyaTelemetry.ActivitySource.StartActivity(serviceName, ActivityKind.Client);
            activity?.SetTag("sankhya.service", serviceName);
            var tags = new KeyValuePair<string, object?>("sankhya.service", serviceName);
            long start = Stopwatch.GetTimestamp();

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = content() };
                if (sessionId != null) request.Headers.Add("Cookie", $"JSESSIONID={sessionId}");

                using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(_options.Timeout);

                using HttpResponseMessage response = await _http.SendAsync(request, timeout.Token);
                EnsureHttpSuccess(response, serviceName);
                TResult result = await read(response.Content, timeout.Token);

                SankhyaTelemetry.Calls.Add(1, tags);
                Log.Called(_logger, serviceName, "http-ok", (long)Stopwatch.GetElapsedTime(start).TotalMilliseconds);
                return result;
            } catch (Exception ex) when (attempt < maxAttempts && IsTransient(ex, cancellationToken))
            {
                SankhyaTelemetry.Errors.Add(1, tags, new KeyValuePair<string, object?>("sankhya.status", "transport"));
                Log.TransientRetry(_logger, serviceName, attempt, maxAttempts, ex);
                await Task.Delay(TimeSpan.FromMilliseconds(200 * Math.Pow(2, attempt - 1)), cancellationToken);
            } catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                activity?.SetStatus(ActivityStatusCode.Error, "timeout");
                SankhyaTelemetry.Errors.Add(1, tags, new KeyValuePair<string, object?>("sankhya.status", "timeout"));
                throw new HttpRequestException($"Tempo esgotado na chamada ao Sankhya ({serviceName}).", ex);
            } finally
            {
                SankhyaTelemetry.Duration.Record(Stopwatch.GetElapsedTime(start).TotalMilliseconds, tags);
            }
        }
    }

    private static bool IsTransient(Exception ex, CancellationToken cancellationToken) =>
        ex switch
        {
            HttpRequestException { StatusCode: null or >= HttpStatusCode.InternalServerError } => true,
            OperationCanceledException => !cancellationToken.IsCancellationRequested,
            _ => false
        };

    private static void EnsureHttpSuccess(HttpResponseMessage response, string what)
    {
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException(
                $"Falha HTTP na chamada ao Sankhya ({what}): {(int)response.StatusCode} {response.ReasonPhrase}",
                null,
                response.StatusCode);
    }

    private static async Task<XDocument> ReadXmlAsync(HttpContent content, CancellationToken ct)
    {
        await using Stream stream = await content.ReadAsStreamAsync(ct);
        try
        {
            // A declaração XML diz a codificação (o Sankhya usa ISO-8859-1 ou UTF-8, conforme o serviço).
            return await XDocument.LoadAsync(stream, LoadOptions.None, ct);
        } catch (XmlException ex)
        {
            throw new HttpRequestException("A resposta do Sankhya veio sem conteúdo ou não pôde ser lida.", ex);
        }
    }

    private static byte[] Serialize(XElement envelope)
    {
        using var buffer = new MemoryStream();
        using (var writer = XmlWriter.Create(buffer, WriterSettings))
            envelope.WriteTo(writer);
        return buffer.ToArray();
    }

    private static byte[] SerializeJson(string serviceName, Action<Utf8JsonWriter> writeRequestBody)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("serviceName", serviceName);
            writer.WritePropertyName("requestBody");
            writer.WriteStartObject();
            writeRequestBody(writer);
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        return buffer.ToArray();
    }
}

internal static class SankhyaResponses
{
    private const string StatusSucesso = "1";
    private const string StatusNaoAutorizado = "3";

    /// <summary>
    /// Decide pelo <c>status</c> (1 = sucesso); a mensagem só compõe o texto. Sem <c>status</c>, vale a presença
    /// de mensagem.
    /// </summary>
    public static void EnsureSuccess(string serviceName, string? status, string? message, string? tsErrorCode)
    {
        bool falhou = string.IsNullOrEmpty(status) ? message != null : status != StatusSucesso;
        if (!falhou) return;

        string texto = message ?? $"O Sankhya devolveu status {status} sem mensagem.";
        string mensagemFinal = tsErrorCode != null ? $"SNK Error: {tsErrorCode} - {texto}" : $"SNK Error: {texto}";

        SankhyaTelemetry.Errors.Add(
            1,
            new KeyValuePair<string, object?>("sankhya.service", serviceName),
            new KeyValuePair<string, object?>("sankhya.status", status));

        if (status == StatusNaoAutorizado)
            throw new SankhyaSessionExpiredException(mensagemFinal, status, serviceName, tsErrorCode);

        throw new SankhyaException(mensagemFinal, status, serviceName, tsErrorCode);
    }
}
