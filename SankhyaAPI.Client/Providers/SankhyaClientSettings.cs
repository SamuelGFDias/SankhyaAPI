namespace SankhyaAPI.Client.Providers;

/// <summary>
/// Classe de configuração para utilização dos serviços da API Sankhya.
/// </summary>
/// <remarks>
/// Para utilizar os serviços da Sankhya, é necessário informar as credenciais de acesso (usuário e senha) e a URL base da API.
/// Essas informações devem ser fornecidas no arquivo de configuração `appsettings.json`.
/// </remarks>
/// <example>
/// Exemplo de uso:
/// <code>
/// var settings = new SankhyaClientSettings
/// {
///     Usuario = "usuario",
///     Senha = "senha",
///     BaseUrl = "https://api.sankhya.com.br"
/// };
/// </code>
/// </example>
public class SankhyaClientSettings
{
    /// <summary>
    /// O nome de usuário para acessar a API Sankhya.
    /// </summary>
    public required string Usuario { get; set; }

    /// <summary>
    /// A senha correspondente ao usuário para acessar a API Sankhya.
    /// </summary>
    public required string Senha { get; set; }

    /// <summary>
    /// A URL base da API Sankhya.
    /// </summary>
    public required string BaseUrl { get; set; }
}