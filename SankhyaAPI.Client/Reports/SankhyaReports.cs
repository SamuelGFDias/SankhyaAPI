using System.Globalization;
using System.Xml.Linq;
using SankhyaAPI.Client.Exceptions;
using SankhyaAPI.Client.Session;
using SankhyaAPI.Client.Transport;

namespace SankhyaAPI.Client.Reports;

/// <summary>
/// Parâmetro de relatório.
/// </summary>
/// <param name="Name">Nome do parâmetro no relatório (atributo <c>nome</c>).</param>
/// <param name="Class">Classe Java do valor, por exemplo <c>java.lang.String</c>, <c>java.math.BigDecimal</c>, <c>java.sql.Timestamp</c>.</param>
/// <param name="Value">Valor já no formato esperado pelo relatório.</param>
public sealed record ReportParameter(string Name, string Class, string Value);

/// <summary>
/// Arquivo gerado por um relatório, já baixado. Descarte para liberar o conteúdo.
/// </summary>
public sealed class SankhyaReportFile(Stream content, string? contentType, string? format, bool canExport, bool canSendByEmail)
    : IAsyncDisposable, IDisposable
{
    public Stream Content { get; } = content;
    public string? ContentType { get; } = contentType;
    public string? Format { get; } = format;
    public bool CanExport { get; } = canExport;
    public bool CanSendByEmail { get; } = canSendByEmail;

    public ValueTask DisposeAsync() => Content.DisposeAsync();

    public void Dispose() => Content.Dispose();
}

/// <summary>
/// Relatórios do Sankhya (VisualizadorRelatorios).
/// </summary>
public interface ISankhyaReports
{
    /// <summary>
    /// Gera o relatório e baixa o arquivo na mesma sessão, numa chamada só.
    /// </summary>
    /// <param name="reportCode">Código do relatório (<c>NURFE</c>).</param>
    Task<SankhyaReportFile> GenerateAsync(
        int reportCode,
        IEnumerable<ReportParameter> parameters,
        CancellationToken cancellationToken = default);
}

internal sealed class SankhyaReports(ISankhyaTransport transport, ISankhyaSession session) : ISankhyaReports
{
    public Task<SankhyaReportFile> GenerateAsync(
        int reportCode,
        IEnumerable<ReportParameter> parameters,
        CancellationToken cancellationToken = default)
    {
        var body = new XElement(
            "relatorio",
            new XAttribute("nuRfe", reportCode.ToString(CultureInfo.InvariantCulture)),
            new XElement(
                "parametros",
                parameters.Select(p => new XElement(
                    "parametro",
                    new XAttribute("nome", p.Name),
                    new XAttribute("classe", p.Class),
                    new XAttribute("valor", p.Value)))));

        // Gerar e baixar precisam da mesma sessão: as duas chamadas ficam dentro de uma execução.
        return session.ExecuteAsync(async (id, ct) =>
        {
            SankhyaXmlResponse response = await transport.SendXmlAsync(SankhyaServices.Report, [body], id, idempotent: false, ct);
            XElement chave = response.ResponseBody?.Element("chave")
                          ?? throw new SankhyaException("O relatório não devolveu a chave do arquivo.", response.Status, SankhyaServices.Report);
            string key = chave.Attribute("valor")?.Value
                      ?? throw new SankhyaException("O relatório não devolveu a chave do arquivo.", response.Status, SankhyaServices.Report);

            Stream content = await transport.DownloadAsync(
                $"visualizadorArquivos.mge?chaveArquivo={Uri.EscapeDataString(key)}&download=S&hidemail=S",
                id,
                ct);

            return new SankhyaReportFile(
                content,
                chave.Attribute("contentType")?.Value,
                chave.Attribute("format")?.Value,
                chave.Attribute("permiteExportar")?.Value is "S" or "true",
                chave.Attribute("canSendViaEmail")?.Value is "S" or "true");
        }, cancellationToken);
    }
}
