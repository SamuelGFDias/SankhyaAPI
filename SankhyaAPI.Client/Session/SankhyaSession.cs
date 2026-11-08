using System.Collections.Concurrent;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SankhyaAPI.Client.Configuration;
using SankhyaAPI.Client.Diagnostics;
using SankhyaAPI.Client.Exceptions;
using SankhyaAPI.Client.Transport;
using SankhyaAPI.Client.Utils;

namespace SankhyaAPI.Client.Session;

/// <summary>
/// Sessão aberta no Sankhya.
/// </summary>
/// <param name="SessionId">Valor do <c>JSESSIONID</c>.</param>
/// <param name="UserCode">Código do usuário (<c>idusu</c>), quando o servidor informa.</param>
public sealed record SankhyaSessionInfo(string SessionId, int? UserCode);

/// <summary>
/// Abre, reaproveita e encerra as sessões no Sankhya conforme o <see cref="SessionMode"/>.
/// </summary>
/// <remarks>
/// O Sankhya recusa duas requisições simultâneas na mesma sessão ("O serviço foi cancelado por situação de
/// concorrência"). Por isso cada operação usa uma sessão com exclusividade: no modo
/// <see cref="SessionMode.Shared"/>, até <see cref="SankhyaClientOptions.MaxSessions"/> sessões ficam abertas e
/// são reaproveitadas, uma operação por vez em cada uma.
/// </remarks>
public interface ISankhyaSession : IAsyncDisposable
{
    SessionMode Mode { get; }

    /// <summary>
    /// Abre (ou devolve a já aberta) sessão explícita. Enquanto ela estiver aberta, todas as operações a usam,
    /// uma por vez, em qualquer modo, até <see cref="LogoutAsync"/>.
    /// </summary>
    Task<SankhyaSessionInfo> LoginAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Encerra a sessão explícita, se houver.
    /// </summary>
    Task LogoutAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Executa a operação com uma sessão válida e exclusiva. Se o Sankhya responder que a sessão expirou, abre
    /// outra e repete uma vez (seguro: com <c>status="3"</c> nada foi executado no servidor).
    /// </summary>
    Task<T> ExecuteAsync<T>(Func<string, CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default);
}

internal sealed class SankhyaSession : ISankhyaSession, IDisposable
{
    private readonly ISankhyaTransport _transport;
    private readonly SankhyaClientOptions _options;
    private readonly ILogger _logger;

    private readonly SemaphoreSlim _slots;
    private readonly ConcurrentBag<SankhyaSessionInfo> _idle = [];
    private readonly ConcurrentDictionary<string, SankhyaSessionInfo> _open = new();

    private readonly SemaphoreSlim _explicitLock = new(1, 1);
    private volatile SankhyaSessionInfo? _explicit;
    private volatile bool _disposed;

    public SankhyaSession(ISankhyaTransport transport, SankhyaClientOptions options, ILogger<SankhyaSession>? logger = null)
    {
        _transport = transport;
        _options = options;
        _logger = logger ?? NullLogger<SankhyaSession>.Instance;
        _slots = new SemaphoreSlim(Math.Max(1, options.MaxSessions));
    }

    public SessionMode Mode => _options.SessionMode;

    /// <summary>Ids das sessões abertas no pool (para testes).</summary>
    internal IReadOnlyCollection<string> PooledSessionIds => _open.Keys.ToList();

    public async Task<SankhyaSessionInfo> LoginAsync(CancellationToken cancellationToken = default)
    {
        await _explicitLock.WaitAsync(cancellationToken);
        try
        {
            return _explicit ??= await LoginCoreAsync(cancellationToken);
        } finally
        {
            _explicitLock.Release();
        }
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        await _explicitLock.WaitAsync(cancellationToken);
        try
        {
            SankhyaSessionInfo? current = _explicit;
            _explicit = null;
            if (current != null) await LogoutCoreAsync(current.SessionId, cancellationToken);
        } finally
        {
            _explicitLock.Release();
        }
    }

    public Task<T> ExecuteAsync<T>(Func<string, CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
    {
        if (_explicit != null) return ExecuteExplicitAsync(operation, cancellationToken);

        return Mode == SessionMode.PerOperation
                   ? ExecutePerOperationAsync(operation, cancellationToken)
                   : ExecutePooledAsync(operation, cancellationToken);
    }

    private async Task<T> ExecuteExplicitAsync<T>(Func<string, CancellationToken, Task<T>> operation, CancellationToken cancellationToken)
    {
        await _explicitLock.WaitAsync(cancellationToken);
        try
        {
            SankhyaSessionInfo session = _explicit ??= await LoginCoreAsync(cancellationToken);
            try
            {
                return await operation(session.SessionId, cancellationToken);
            } catch (SankhyaSessionExpiredException)
            {
                Renewed();
                _explicit = await LoginCoreAsync(cancellationToken);
                return await operation(_explicit.SessionId, cancellationToken);
            }
        } finally
        {
            _explicitLock.Release();
        }
    }

    private async Task<T> ExecutePerOperationAsync<T>(Func<string, CancellationToken, Task<T>> operation, CancellationToken cancellationToken)
    {
        SankhyaSessionInfo session = await LoginCoreAsync(cancellationToken);
        try
        {
            return await operation(session.SessionId, cancellationToken);
        } finally
        {
            await TryLogoutAsync(session.SessionId);
        }
    }

    private async Task<T> ExecutePooledAsync<T>(Func<string, CancellationToken, Task<T>> operation, CancellationToken cancellationToken)
    {
        await _slots.WaitAsync(cancellationToken);
        SankhyaSessionInfo? session = null;
        try
        {
            session = _idle.TryTake(out SankhyaSessionInfo? idle) ? idle : await OpenPooledAsync(cancellationToken);
            try
            {
                return await operation(session.SessionId, cancellationToken);
            } catch (SankhyaSessionExpiredException)
            {
                Renewed();
                _open.TryRemove(session.SessionId, out _);
                session = null;
                session = await OpenPooledAsync(cancellationToken);
                return await operation(session.SessionId, cancellationToken);
            }
        } finally
        {
            if (session != null)
            {
                // Depois do dispose, a sessão que volta de uma operação em andamento é encerrada, não reaproveitada.
                if (_disposed) await TryLogoutAsync(session.SessionId);
                else _idle.Add(session);
            }

            _slots.Release();
        }
    }

    private async Task<SankhyaSessionInfo> OpenPooledAsync(CancellationToken cancellationToken)
    {
        SankhyaSessionInfo session = await LoginCoreAsync(cancellationToken);
        _open[session.SessionId] = session;
        return session;
    }

    private void Renewed()
    {
        Log.SessionRenewed(_logger);
        SankhyaTelemetry.SessionRenewals.Add(1);
    }

    private async Task<SankhyaSessionInfo> LoginCoreAsync(CancellationToken cancellationToken)
    {
        SankhyaXmlResponse response = await _transport.SendXmlAsync(
            SankhyaServices.Login,
            [new XElement("NOMUSU", _options.Usuario), new XElement("INTERNO", _options.Senha)],
            null,
            idempotent: true,
            cancellationToken);

        string? sessionId = response.ResponseBody?.Element("jsessionid")?.Value;
        if (string.IsNullOrWhiteSpace(sessionId))
            throw new SankhyaException("O login no Sankhya não devolveu a sessão.", response.Status, SankhyaServices.Login);

        int? userCode = int.TryParse(SankhyaText.DecodeBase64(response.ResponseBody?.Element("idusu")?.Value), out int codigo) ? codigo : null;

        SankhyaTelemetry.Logins.Add(1);
        Log.LoggedIn(_logger);
        return new SankhyaSessionInfo(sessionId.Trim(), userCode);
    }

    private Task LogoutCoreAsync(string sessionId, CancellationToken cancellationToken) =>
        _transport.SendXmlAsync(SankhyaServices.Logout, [], sessionId, idempotent: false, cancellationToken);

    private async Task TryLogoutAsync(string sessionId)
    {
        try
        {
            await LogoutCoreAsync(sessionId, CancellationToken.None);
        } catch (Exception ex)
        {
            // O resultado (ou o erro) da operação não pode se perder por causa do logout.
            Log.LogoutFailed(_logger, ex);
        }
    }

    /// <summary>Descarte síncrono (o contêiner de DI chama quando é descartado de forma síncrona).</summary>
    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        var sessions = _open.Values.ToList();
        _open.Clear();
        while (_idle.TryTake(out _)) { }

        SankhyaSessionInfo? explicitSession = Interlocked.Exchange(ref _explicit, null);
        if (explicitSession != null) sessions.Add(explicitSession);

        await Task.WhenAll(sessions.Select(s => TryLogoutAsync(s.SessionId)));
    }
}
