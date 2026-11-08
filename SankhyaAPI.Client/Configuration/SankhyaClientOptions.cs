using Microsoft.Extensions.Options;

namespace SankhyaAPI.Client.Configuration;

/// <summary>
/// Como o pacote abre e mantém a sessão no Sankhya.
/// </summary>
public enum SessionMode
{
    /// <summary>
    /// Sessões abertas sob demanda e reaproveitadas (até <see cref="SankhyaClientOptions.MaxSessions"/>, uma operação
    /// por vez em cada). Renovadas (e a operação repetida uma vez) quando o Sankhya responde que expiraram.
    /// Encerradas no <c>DisposeAsync</c>.
    /// </summary>
    Shared,

    /// <summary>
    /// Login e logout a cada operação, como na 5.x. Mais chamadas, isolamento total.
    /// </summary>
    PerOperation
}

/// <summary>
/// Configuração do cliente Sankhya. Valide com <see cref="SankhyaClientOptionsValidator"/>, que o
/// <c>AddSankhya</c> já registra com <c>ValidateOnStart</c>.
/// </summary>
public sealed class SankhyaClientOptions
{
    /// <summary>
    /// URL do Sankhya terminada em <c>/mge</c>, por exemplo <c>https://erp.empresa.com.br/mge</c>.
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    public string Usuario { get; set; } = string.Empty;

    /// <summary>
    /// Senha do usuário de integração. Leia de um cofre ou de user-secrets; nunca fixe no código.
    /// </summary>
    public string Senha { get; set; } = string.Empty;

    public SessionMode SessionMode { get; set; } = SessionMode.Shared;

    /// <summary>
    /// Máximo de sessões abertas ao mesmo tempo no modo <see cref="SessionMode.Shared"/>. O Sankhya não aceita duas
    /// requisições simultâneas na mesma sessão, então este é também o máximo de chamadas em paralelo. Padrão: 4.
    /// </summary>
    public int MaxSessions { get; set; } = 4;

    /// <summary>
    /// Tempo máximo de cada chamada HTTP. Padrão: 100 segundos.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(100);

    /// <summary>
    /// Novas tentativas em falha transitória de transporte (5xx, timeout, conexão), só em operações idempotentes
    /// (login, loadRecords, executeQuery). Escritas nunca são repetidas por falha de transporte. Padrão: 2.
    /// </summary>
    public int MaxRetries { get; set; } = 2;

    /// <summary>
    /// Banco por trás do Sankhya, para formatar datas no SQL cru (<see cref="Sql.SankhyaSql"/>). Padrão: SQL Server.
    /// </summary>
    public Sql.SqlDialect Dialect { get; set; } = Sql.SqlDialect.SqlServer;

    /// <summary>
    /// Permite URL <c>http://</c>. Só para desenvolvimento e testes: a senha e a sessão trafegam em texto puro.
    /// </summary>
    public bool AllowHttp { get; set; }

    internal Uri ServiceBaseUri => new(BaseUrl.TrimEnd('/') + "/");
}

/// <summary>
/// Valida <see cref="SankhyaClientOptions"/> na inicialização.
/// </summary>
public sealed class SankhyaClientOptionsValidator : IValidateOptions<SankhyaClientOptions>
{
    public ValidateOptionsResult Validate(string? name, SankhyaClientOptions options)
    {
        var erros = new List<string>();

        if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out Uri? uri))
        {
            erros.Add("BaseUrl precisa ser uma URL absoluta, terminada em /mge.");
        } else
        {
            if (uri.Scheme != Uri.UriSchemeHttps && !(options.AllowHttp && uri.Scheme == Uri.UriSchemeHttp))
                erros.Add("BaseUrl precisa usar https (AllowHttp = true libera http só para desenvolvimento).");
            if (!uri.AbsolutePath.TrimEnd('/').EndsWith("/mge", StringComparison.OrdinalIgnoreCase))
                erros.Add("BaseUrl precisa terminar em /mge.");
        }

        if (string.IsNullOrWhiteSpace(options.Usuario)) erros.Add("Usuario é obrigatório.");
        if (string.IsNullOrWhiteSpace(options.Senha)) erros.Add("Senha é obrigatória.");
        if (options.Timeout <= TimeSpan.Zero) erros.Add("Timeout precisa ser positivo.");
        if (options.MaxRetries < 0) erros.Add("MaxRetries não pode ser negativo.");
        if (options.MaxSessions < 1) erros.Add("MaxSessions precisa ser pelo menos 1.");

        return erros.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(erros);
    }

    internal static void ThrowIfInvalid(SankhyaClientOptions options)
    {
        ValidateOptionsResult result = new SankhyaClientOptionsValidator().Validate(null, options);
        if (result.Failed) throw new OptionsValidationException(nameof(SankhyaClientOptions), typeof(SankhyaClientOptions), result.Failures);
    }
}
