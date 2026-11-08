namespace SankhyaAPI.Client.Exceptions;

/// <summary>
/// Erro devolvido pelo Sankhya: a resposta chegou (HTTP 200), mas com <c>status</c> diferente de <c>1</c>.
/// </summary>
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
/// ou nunca existiu. Nada foi executado no servidor; repetir com um novo login é seguro.
/// </summary>
public class SankhyaSessionExpiredException(
    string message,
    string? status = null,
    string? serviceName = null,
    string? tsErrorCode = null)
    : SankhyaException(message, status, serviceName, tsErrorCode);

/// <summary>
/// O executeQuery cortou o resultado no limite fixo de 5000 linhas do servidor (<c>burstLimit</c>).
/// Pagine a consulta (<c>QueryPagedAsync</c> ou <c>OFFSET ... FETCH</c> no SQL) em vez de receber um resultado incompleto.
/// </summary>
public class SankhyaTruncatedResultException(string message, string? serviceName = null)
    : SankhyaException(message, "1", serviceName);
