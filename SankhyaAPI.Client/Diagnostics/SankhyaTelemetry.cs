using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;

namespace SankhyaAPI.Client.Diagnostics;

/// <summary>
/// Tracing (OpenTelemetry) e métricas do cliente. Assine pelo nome <see cref="Name"/>:
/// <c>.AddSource("SankhyaAPI.Client")</c> e <c>.AddMeter("SankhyaAPI.Client")</c>.
/// </summary>
/// <remarks>
/// Nada aqui carrega valor de campo, parâmetro ou SQL: só serviço, entidade, status e duração.
/// </remarks>
public static class SankhyaTelemetry
{
    public const string Name = "SankhyaAPI.Client";

    internal static readonly ActivitySource ActivitySource = new(Name);
    internal static readonly Meter Meter = new(Name);

    internal static readonly Counter<long> Calls = Meter.CreateCounter<long>("sankhya.client.calls", description: "Chamadas ao service.sbr");
    internal static readonly Counter<long> Errors = Meter.CreateCounter<long>("sankhya.client.errors", description: "Chamadas com erro, por status");
    internal static readonly Counter<long> Logins = Meter.CreateCounter<long>("sankhya.client.logins", description: "Logins realizados");
    internal static readonly Counter<long> SessionRenewals = Meter.CreateCounter<long>("sankhya.client.session_renewals", description: "Sessões renovadas após expirar");
    internal static readonly Histogram<double> Duration = Meter.CreateHistogram<double>("sankhya.client.duration", "ms", "Duração das chamadas");
}

internal static partial class Log
{
    [LoggerMessage(1, LogLevel.Debug, "Sankhya {Service} respondeu HTTP {HttpStatus} em {ElapsedMs} ms")]
    public static partial void Called(ILogger logger, string service, int httpStatus, long elapsedMs);

    [LoggerMessage(2, LogLevel.Warning, "Sankhya {Service}: falha transitória de transporte, tentativa {Attempt} de {MaxAttempts}")]
    public static partial void TransientRetry(ILogger logger, string service, int attempt, int maxAttempts, Exception exception);

    [LoggerMessage(3, LogLevel.Information, "Sessão Sankhya expirada; renovando e repetindo a operação uma vez")]
    public static partial void SessionRenewed(ILogger logger);

    [LoggerMessage(4, LogLevel.Debug, "Login no Sankhya realizado")]
    public static partial void LoggedIn(ILogger logger);

    [LoggerMessage(5, LogLevel.Warning, "Falha ao encerrar a sessão no Sankhya (ela expira no servidor)")]
    public static partial void LogoutFailed(ILogger logger, Exception exception);

    [LoggerMessage(6, LogLevel.Warning, "Sessão Sankhya descartada: a operação terminou sem resposta (tempo esgotado ou cancelamento) e a requisição pode seguir no servidor")]
    public static partial void SessionDiscarded(ILogger logger);

    [LoggerMessage(7, LogLevel.Warning,
        "Sankhya {Entity}: exclusão lenta ({ElapsedMs} ms). A FK {ForeignKey} em {ChildTable} ({Columns}) não tem índice, e cada exclusão em {ParentTable} varre as {ChildRows} linhas dela. Índice sugerido: {CreateIndexSql}")]
    public static partial void MissingIndex(ILogger logger, string entity, long elapsedMs, string foreignKey, string childTable, string columns,
        long childRows, string parentTable, string createIndexSql);

    [LoggerMessage(8, LogLevel.Information, "Sankhya {Entity}: exclusão lenta ({ElapsedMs} ms), sem FK relevante sem índice; confira locks e triggers")]
    public static partial void NoMissingIndex(ILogger logger, string entity, long elapsedMs);

    [LoggerMessage(9, LogLevel.Information, "Sankhya {Entity}: o diagnóstico de índices falhou; será tentado de novo na próxima exclusão lenta")]
    public static partial void MissingIndexCheckFailed(ILogger logger, string entity, Exception exception);
}
