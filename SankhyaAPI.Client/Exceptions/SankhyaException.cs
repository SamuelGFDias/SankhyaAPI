using System.Net;

namespace SankhyaAPI.Client.Exceptions;

/// <summary>
/// Categoria de um erro do Sankhya, para tratar sem comparar texto. Cada categoria tem um status HTTP sugerido
/// (<see cref="SankhyaException.SuggestedHttpStatus"/>) para quem expõe o erro numa API própria.
/// </summary>
public enum SankhyaErrorKind
{
    /// <summary>Erro do Sankhya sem categoria reconhecida. HTTP sugerido: 502.</summary>
    Unknown,

    /// <summary>Usuário ou senha inválidos no login. HTTP sugerido: 401.</summary>
    AuthenticationFailed,

    /// <summary>Sessão expirada, encerrada ou inexistente (<c>status="3"</c>). HTTP sugerido: 401.</summary>
    SessionExpired,

    /// <summary>Duas requisições simultâneas na mesma sessão. HTTP sugerido: 409.</summary>
    Concurrency,

    /// <summary>Pedido inválido para o servidor: entidade, campo ou relacionamento inexistente, expressão ou SQL com erro de sintaxe, parâmetros faltando, comando não permitido. Erro de programação. HTTP sugerido: 400.</summary>
    InvalidRequest,

    /// <summary>A consulta SQL falhou ao executar (por exemplo, divisão por zero). HTTP sugerido: 400.</summary>
    QueryFailed,

    /// <summary>Valor inválido para o campo: conversão, tamanho acima do limite. <see cref="SankhyaException.Field"/> traz o campo quando o servidor informa. HTTP sugerido: 422.</summary>
    InvalidValue,

    /// <summary>Chave primária ou única duplicada. HTTP sugerido: 409.</summary>
    DuplicateKey,

    /// <summary>Restrição do banco violada: chave estrangeira, CHECK, campo obrigatório nulo. HTTP sugerido: 409.</summary>
    ConstraintViolation,

    /// <summary>
    /// A gravação foi recusada por regra de negócio: trigger do banco (RAISERROR, RAISE_APPLICATION_ERROR), evento
    /// ou validação do Sankhya. <see cref="SankhyaException.ServerMessage"/> é a mensagem escrita pela regra, pronta para
    /// mostrar ao usuário. HTTP sugerido: 422.
    /// </summary>
    BusinessRule,

    /// <summary>Registro não encontrado (por exemplo, update de chave inexistente). HTTP sugerido: 404.</summary>
    NotFound,

    /// <summary>O executeQuery cortou o resultado em 5000 linhas. HTTP sugerido: 422.</summary>
    ResultTruncated,

    /// <summary>Recurso que depende de configuração do Sankhya não habilitada (por exemplo, LOGTABOPER para modifiedSince). HTTP sugerido: 501.</summary>
    NotConfigured,

    /// <summary>Tempo esgotado na chamada. HTTP sugerido: 504.</summary>
    Timeout,

    /// <summary>Sankhya indisponível: falha de conexão, HTTP 5xx, resposta ilegível. HTTP sugerido: 503.</summary>
    Unavailable
}

/// <summary>
/// Erro ao falar com o Sankhya. Toda falha do pacote chega como esta exceção (ou uma subclasse), com
/// <see cref="Kind"/> para decidir o tratamento e <see cref="SuggestedHttpStatus"/> para responder numa API.
/// </summary>
/// <remarks>
/// <see cref="Exception.Message"/> é a mensagem do servidor já decodificada e limpa (a mesma de <see cref="ServerMessage"/>).
/// Em <see cref="SankhyaErrorKind.BusinessRule"/>, ela é o texto da regra (trigger, evento), feito para o usuário final.
/// </remarks>
public class SankhyaException : Exception
{
    public SankhyaException(
        string message,
        string? status = null,
        string? serviceName = null,
        string? tsErrorCode = null,
        Exception? innerException = null,
        SankhyaErrorKind kind = SankhyaErrorKind.Unknown,
        string? field = null)
        : base(message, innerException)
    {
        Status = status;
        ServiceName = serviceName;
        TsErrorCode = tsErrorCode;
        Kind = kind;
        Field = field;
    }

    /// <summary>Categoria do erro.</summary>
    public SankhyaErrorKind Kind { get; }

    /// <summary>Atributo <c>status</c> da resposta (<c>0</c> = erro, <c>3</c> = sessão inválida), quando houve resposta.</summary>
    public string? Status { get; }

    /// <summary>Serviço chamado, por exemplo <c>CRUDServiceProvider.saveRecord</c>.</summary>
    public string? ServiceName { get; }

    /// <summary>Código do <c>tsError</c>, quando o Sankhya envia um (por exemplo <c>CORE_E01434</c>).</summary>
    public string? TsErrorCode { get; }

    /// <summary>Campo envolvido, quando a mensagem do servidor o identifica (por exemplo <c>TEXTO</c>).</summary>
    public string? Field { get; }

    /// <summary>Mensagem do servidor, decodificada e limpa.</summary>
    public string ServerMessage => Message;

    /// <summary>Status HTTP sugerido para expor este erro numa API (nunca 500 para erro conhecido).</summary>
    public HttpStatusCode SuggestedHttpStatus => SuggestedStatusFor(Kind);

    /// <summary>
    /// Resumo serializável do erro, no formato de um <c>ProblemDetails</c> (RFC 9457).
    /// </summary>
    public SankhyaProblem ToProblem() =>
        new($"urn:sankhya:error:{Kind}", Title(Kind), (int)SuggestedHttpStatus, Message, Kind, TsErrorCode, Field, ServiceName);

    public static HttpStatusCode SuggestedStatusFor(SankhyaErrorKind kind) =>
        kind switch
        {
            SankhyaErrorKind.AuthenticationFailed or SankhyaErrorKind.SessionExpired => HttpStatusCode.Unauthorized,
            SankhyaErrorKind.Concurrency or SankhyaErrorKind.DuplicateKey or SankhyaErrorKind.ConstraintViolation => HttpStatusCode.Conflict,
            SankhyaErrorKind.InvalidRequest or SankhyaErrorKind.QueryFailed => HttpStatusCode.BadRequest,
            SankhyaErrorKind.InvalidValue or SankhyaErrorKind.BusinessRule or SankhyaErrorKind.ResultTruncated => HttpStatusCode.UnprocessableEntity,
            SankhyaErrorKind.NotFound => HttpStatusCode.NotFound,
            SankhyaErrorKind.NotConfigured => HttpStatusCode.NotImplemented,
            SankhyaErrorKind.Timeout => HttpStatusCode.GatewayTimeout,
            SankhyaErrorKind.Unavailable => HttpStatusCode.ServiceUnavailable,
            _ => HttpStatusCode.BadGateway
        };

    private static string Title(SankhyaErrorKind kind) =>
        kind switch
        {
            SankhyaErrorKind.AuthenticationFailed => "Usuário ou senha inválidos no Sankhya",
            SankhyaErrorKind.SessionExpired => "Sessão do Sankhya expirada",
            SankhyaErrorKind.Concurrency => "Requisição concorrente na mesma sessão",
            SankhyaErrorKind.InvalidRequest => "Pedido inválido para o Sankhya",
            SankhyaErrorKind.QueryFailed => "A consulta falhou no Sankhya",
            SankhyaErrorKind.InvalidValue => "Valor inválido para o campo",
            SankhyaErrorKind.DuplicateKey => "Registro duplicado",
            SankhyaErrorKind.ConstraintViolation => "Restrição do banco violada",
            SankhyaErrorKind.BusinessRule => "Recusado por regra de negócio",
            SankhyaErrorKind.NotFound => "Registro não encontrado",
            SankhyaErrorKind.ResultTruncated => "Resultado acima do limite do Sankhya",
            SankhyaErrorKind.NotConfigured => "Recurso não habilitado no Sankhya",
            SankhyaErrorKind.Timeout => "Tempo esgotado no Sankhya",
            SankhyaErrorKind.Unavailable => "Sankhya indisponível",
            _ => "Erro no Sankhya"
        };
}

/// <summary>
/// Erro no formato de um <c>ProblemDetails</c> (RFC 9457), para devolver direto numa API.
/// </summary>
/// <param name="Type">URN da categoria, por exemplo <c>urn:sankhya:error:BusinessRule</c>.</param>
/// <param name="Title">Título da categoria.</param>
/// <param name="Status">Status HTTP sugerido.</param>
/// <param name="Detail">Mensagem do servidor.</param>
public sealed record SankhyaProblem(
    string Type,
    string Title,
    int Status,
    string Detail,
    SankhyaErrorKind Kind,
    string? Code,
    string? Field,
    string? Service);

/// <summary>Usuário ou senha inválidos no login (<see cref="SankhyaErrorKind.AuthenticationFailed"/>).</summary>
public class SankhyaAuthenticationException(string message, string? status = null, string? serviceName = null, string? tsErrorCode = null)
    : SankhyaException(message, status, serviceName, tsErrorCode, null, SankhyaErrorKind.AuthenticationFailed);

/// <summary>
/// A sessão usada na chamada não vale mais (<c>status="3"</c>, "Não autorizado."): expirou, foi encerrada
/// ou nunca existiu. Nada foi executado no servidor; repetir com um novo login é seguro (o pacote já faz uma vez).
/// </summary>
public class SankhyaSessionExpiredException(string message, string? status = null, string? serviceName = null, string? tsErrorCode = null)
    : SankhyaException(message, status, serviceName, tsErrorCode, null, SankhyaErrorKind.SessionExpired);

/// <summary>
/// O executeQuery cortou o resultado no limite fixo de 5000 linhas do servidor (<c>burstLimit</c>).
/// Pagine a consulta (<c>QueryPagedAsync</c> ou <c>OFFSET ... FETCH</c> no SQL) em vez de receber um resultado incompleto.
/// </summary>
public class SankhyaTruncatedResultException(string message, string? serviceName = null)
    : SankhyaException(message, "1", serviceName, null, null, SankhyaErrorKind.ResultTruncated);

/// <summary>
/// Gravação recusada por regra de negócio (trigger, evento ou validação do Sankhya). <see cref="Exception.Message"/>
/// é o texto da regra.
/// </summary>
public class SankhyaBusinessRuleException(string message, string? status = null, string? serviceName = null, string? tsErrorCode = null)
    : SankhyaException(message, status, serviceName, tsErrorCode, null, SankhyaErrorKind.BusinessRule);

/// <summary>Registro não encontrado (<see cref="SankhyaErrorKind.NotFound"/>).</summary>
public class SankhyaNotFoundException(string message, string? serviceName = null)
    : SankhyaException(message, null, serviceName, null, null, SankhyaErrorKind.NotFound);

/// <summary>
/// Falha de transporte: tempo esgotado (<see cref="SankhyaErrorKind.Timeout"/>) ou Sankhya indisponível
/// (<see cref="SankhyaErrorKind.Unavailable"/>). A exceção original fica em <see cref="Exception.InnerException"/>.
/// </summary>
public class SankhyaTransportException(string message, SankhyaErrorKind kind, string? serviceName = null, Exception? innerException = null, HttpStatusCode? httpStatus = null)
    : SankhyaException(message, null, serviceName, null, innerException, kind)
{
    /// <summary>Status HTTP recebido do Sankhya, quando houve resposta.</summary>
    public HttpStatusCode? HttpStatus { get; } = httpStatus;
}
