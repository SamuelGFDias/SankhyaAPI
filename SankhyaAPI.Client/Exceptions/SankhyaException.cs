namespace SankhyaAPI.Client.Exceptions;

/// <summary>
/// Erro devolvido pelo Sankhya: a resposta chegou (HTTP 200), mas com <c>status</c> diferente de <c>1</c>.
/// </summary>
/// <remarks>
/// Herda de <see cref="Exception"/>: quem já fazia <c>catch (Exception)</c> continua funcionando.
/// </remarks>
public class SankhyaException : Exception
{
    public SankhyaException(
        string message,
        string? status = null,
        string? serviceName = null,
        string? tsErrorCode = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Status = status;
        ServiceName = serviceName;
        TsErrorCode = tsErrorCode;
    }

    /// <summary>
    /// Atributo <c>status</c> da resposta (<c>0</c> = erro, <c>3</c> = sessão inválida ou expirada).
    /// </summary>
    public string? Status { get; }

    /// <summary>
    /// Serviço chamado, por exemplo <c>CRUDServiceProvider.saveRecord</c>.
    /// </summary>
    public string? ServiceName { get; }

    /// <summary>
    /// Código do <c>tsError</c>, quando o Sankhya envia um.
    /// </summary>
    public string? TsErrorCode { get; }
}

/// <summary>
/// A sessão usada na chamada não vale mais (<c>status="3"</c>, "Não autorizado."): expirou, foi encerrada
/// ou nunca existiu. Repetir a operação com um novo login resolve.
/// </summary>
public class SankhyaSessionExpiredException(
    string message,
    string? status = null,
    string? serviceName = null,
    string? tsErrorCode = null)
    : SankhyaException(message, status, serviceName, tsErrorCode);
