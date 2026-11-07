using SankhyaAPI.Client.Exceptions;
using SankhyaAPI.Client.Responses;

namespace SankhyaAPI.Client.Extensions;

public static class ServiceResponseExtensions
{
    private const string StatusSucesso = "1";
    private const string StatusNaoAutorizado = "3";

    /// <summary>
    /// Lança <see cref="SankhyaException"/> quando a resposta indica erro. Quem decide é o atributo
    /// <c>status</c> (<c>1</c> = sucesso); a mensagem só compõe o texto. Sem <c>status</c>, vale a presença
    /// de mensagem de erro, como antes.
    /// </summary>
    /// <exception cref="SankhyaSessionExpiredException">Sessão inválida ou expirada (<c>status="3"</c>).</exception>
    /// <exception cref="SankhyaException">Qualquer outro status diferente de <c>1</c>.</exception>
    public static void VerificarErros<TEntity>(
        this ServiceResponse<TEntity> serviceResponse)
        where TEntity : class
    {
        string? status = serviceResponse.Status;
        string? mensagem = serviceResponse.StatusMessage ?? serviceResponse.Error.StatusMessage;

        bool falhou = string.IsNullOrEmpty(status) ? mensagem != null : status != StatusSucesso;
        if (!falhou) return;

        string? tsErrorCode = serviceResponse.Error.TsErrorCode;
        string texto = mensagem ?? $"O Sankhya devolveu status {status} sem mensagem.";
        string mensagemFinal = tsErrorCode != null ? $"SNK Error: {tsErrorCode} - {texto}" : $"SNK Error: {texto}";
        string? serviceName = string.IsNullOrEmpty(serviceResponse.ServiceName) ? null : serviceResponse.ServiceName;

        if (status == StatusNaoAutorizado)
            throw new SankhyaSessionExpiredException(mensagemFinal, status, serviceName, tsErrorCode);

        throw new SankhyaException(mensagemFinal, status, serviceName, tsErrorCode);
    }
}
