using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SankhyaAPI.Client.Configuration;
using SankhyaAPI.Client.Mapping;
using SankhyaAPI.Client.Reports;
using SankhyaAPI.Client.Repositories;
using SankhyaAPI.Client.Session;
using SankhyaAPI.Client.Sql;
using SankhyaAPI.Client.Transport;

namespace SankhyaAPI.Client;

/// <summary>
/// Ponto de entrada do pacote: sessão, repositórios de entidade, SQL e relatórios. Com injeção de dependência,
/// use <see cref="SankhyaServiceCollectionExtensions.AddSankhya"/>; sem ela, <see cref="Create"/>.
/// Descarte para encerrar a sessão.
/// </summary>
public sealed class SankhyaClient : IAsyncDisposable
{
    private readonly HttpClient? _ownedHttpClient;

    internal SankhyaClient(ISankhyaTransport transport, ISankhyaSession session, HttpClient? ownedHttpClient = null)
    {
        Transport = transport;
        Session = session;
        Sql = new SankhyaSqlClient(transport, session);
        Reports = new SankhyaReports(transport, session);
        _ownedHttpClient = ownedHttpClient;
    }

    /// <summary>
    /// Cria um cliente fora de um contêiner de injeção de dependência (console, job, teste).
    /// </summary>
    public static SankhyaClient Create(SankhyaClientOptions options, ILoggerFactory? loggerFactory = null)
    {
        SankhyaClientOptionsValidator.ThrowIfInvalid(options);

        var http = new HttpClient(SankhyaTransport.CreateHandler(), disposeHandler: true)
        {
            BaseAddress = options.ServiceBaseUri, Timeout = System.Threading.Timeout.InfiniteTimeSpan
        };
        var transport = new SankhyaTransport(http, options, loggerFactory?.CreateLogger<SankhyaTransport>());
        var session = new SankhyaSession(transport, options, loggerFactory?.CreateLogger<SankhyaSession>());
        return new SankhyaClient(transport, session, http);
    }

    public ISankhyaSession Session { get; }

    /// <summary>
    /// Chamadas cruas ao <c>service.sbr</c>, para serviços que o pacote não cobre.
    /// </summary>
    public ISankhyaTransport Transport { get; }

    public ISankhyaSql Sql { get; }

    public ISankhyaReports Reports { get; }

    /// <summary>
    /// Repositório do modelo, pelo mapa registrado (<see cref="SankhyaMaps.Register{TMap}"/> ou <c>AddEntity</c>) ou pelos
    /// atributos. <paramref name="entityName"/> substitui o nome da entidade do mapa.
    /// </summary>
    public ISankhyaRepository<T> Repository<T>(string? entityName = null) where T : class, new() =>
        new SankhyaRepository<T>(Transport, Session, entityName);

    public async ValueTask DisposeAsync()
    {
        // Com injeção de dependência, o contêiner é o dono da sessão e do HttpClient.
        if (_ownedHttpClient == null) return;
        await Session.DisposeAsync();
        _ownedHttpClient.Dispose();
    }
}

/// <summary>
/// Registro do cliente Sankhya na injeção de dependência.
/// </summary>
public interface ISankhyaBuilder
{
    IServiceCollection Services { get; }

    /// <summary>
    /// Registra o mapa <typeparamref name="TMap"/> e o <see cref="ISankhyaRepository{T}"/> do modelo.
    /// </summary>
    ISankhyaBuilder AddEntity<T, TMap>() where T : class, new() where TMap : SankhyaEntityMap<T>, new();

    /// <summary>
    /// Registra <see cref="ISankhyaRepository{T}"/> para um modelo mapeado por atributos.
    /// </summary>
    ISankhyaBuilder AddRepository<T>(string? entityName = null) where T : class, new();
}

public static class SankhyaServiceCollectionExtensions
{
    /// <summary>
    /// Registra o cliente Sankhya: opções validadas na inicialização, <c>HttpClient</c> pelo
    /// <c>IHttpClientFactory</c> (sem cookies), sessão (singleton), <see cref="ISankhyaSql"/>,
    /// <see cref="ISankhyaReports"/> e <see cref="SankhyaClient"/>.
    /// </summary>
    /// <example>
    /// <code>
    /// builder.Services.AddSankhya(o => builder.Configuration.GetSection("Sankhya").Bind(o))
    ///                 .AddEntity&lt;Produto, ProdutoMap&gt;();
    /// </code>
    /// </example>
    public static ISankhyaBuilder AddSankhya(this IServiceCollection services, Action<SankhyaClientOptions> configure)
    {
        services.AddOptions<SankhyaClientOptions>().Configure(configure).ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<SankhyaClientOptions>, SankhyaClientOptionsValidator>());

        services.AddHttpClient(SankhyaTransport.HttpClientName, (sp, http) =>
                {
                    SankhyaClientOptions options = sp.GetRequiredService<IOptions<SankhyaClientOptions>>().Value;
                    http.BaseAddress = options.ServiceBaseUri;
                    http.Timeout = System.Threading.Timeout.InfiniteTimeSpan; // o timeout por chamada fica no transporte
                })
                .ConfigurePrimaryHttpMessageHandler(SankhyaTransport.CreateHandler);

        services.TryAddSingleton<ISankhyaTransport>(sp => new SankhyaTransport(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient(SankhyaTransport.HttpClientName),
            sp.GetRequiredService<IOptions<SankhyaClientOptions>>().Value,
            sp.GetService<ILogger<SankhyaTransport>>()));
        services.TryAddSingleton<ISankhyaSession>(sp => new SankhyaSession(
            sp.GetRequiredService<ISankhyaTransport>(),
            sp.GetRequiredService<IOptions<SankhyaClientOptions>>().Value,
            sp.GetService<ILogger<SankhyaSession>>()));
        services.TryAddSingleton(sp => new SankhyaClient(sp.GetRequiredService<ISankhyaTransport>(), sp.GetRequiredService<ISankhyaSession>()));
        services.TryAddSingleton(sp => sp.GetRequiredService<SankhyaClient>().Sql);
        services.TryAddSingleton(sp => sp.GetRequiredService<SankhyaClient>().Reports);

        return new SankhyaBuilder(services);
    }

    private sealed class SankhyaBuilder(IServiceCollection services) : ISankhyaBuilder
    {
        public IServiceCollection Services { get; } = services;

        public ISankhyaBuilder AddEntity<T, TMap>() where T : class, new() where TMap : SankhyaEntityMap<T>, new()
        {
            SankhyaMaps.Register<TMap>();
            return AddRepository<T>();
        }

        public ISankhyaBuilder AddRepository<T>(string? entityName = null) where T : class, new()
        {
            Services.TryAddSingleton<ISankhyaRepository<T>>(sp => sp.GetRequiredService<SankhyaClient>().Repository<T>(entityName));
            return this;
        }
    }
}
