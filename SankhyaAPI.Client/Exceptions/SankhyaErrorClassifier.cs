using System.Text.RegularExpressions;

namespace SankhyaAPI.Client.Exceptions;

/// <summary>
/// Classifica a resposta de erro do Sankhya. As regras vêm das respostas reais da homologação (registradas em
/// <c>docs/sankhya-protocolo.md</c>, seção "Erros"): o <c>tsErrorCode</c> quando existe e, para erros do banco (que
/// chegam como a mensagem crua do SQL Server ou do Oracle), o padrão do texto em português e em inglês.
/// </summary>
internal static partial class SankhyaErrorClassifier
{
    private const string Login = "MobileLoginSP.login";
    private const string ExecuteQuery = "DbExplorerSP.executeQuery";

    private static readonly string[] AuthCodes = ["CORE_E01434", "CORE_E01428"];
    private static readonly string[] InvalidRequestCodes = ["CORE_E04064", "CORE_E03405"];
    private static readonly string[] InvalidValueCodes = ["CORE_E02271"];
    private static readonly string[] NotConfiguredCodes = ["CORE_E07537"];

    private static readonly string[] Concurrency = ["situação de concorrência", "situacao de concorrencia"];
    private static readonly string[] NotConfigured = ["Necessario habilitar o parametro", "Necessário habilitar o parâmetro"];
    private static readonly string[] Auth = ["Usuário/Senha inválido", "Usuario/Senha invalido"];

    private static readonly string[] DuplicateKey =
    [
        "PRIMARY KEY", "UNIQUE KEY", "chave duplicada", "duplicate key", "ORA-00001", "unique constraint", "índice exclusivo"
    ];

    private static readonly string[] Constraint =
    [
        "FOREIGN KEY", "REFERENCE", "CHECK constraint", "restrição CHECK", "restrição do CHECK", "ORA-02291", "ORA-02292", "ORA-02290",
        "não é possível inserir o valor NULL", "Cannot insert the value NULL", "ORA-01400"
    ];

    private static readonly string[] InvalidValue =
    [
        "Erro de conversão", "largura acima do limite", "seriam truncados", "would be truncated", "ORA-12899", "ORA-01722", "ORA-01858",
        "Conversion failed", "Falha na conversão", "fora do intervalo", "out-of-range"
    ];

    private static readonly string[] InvalidRequest =
    [
        "objeto de acesso a dados", "Campo não existe", "Parametros informados insuficientes", "Sintaxe incorreta", "formato inválido",
        "Nome de objeto", "Nome de coluna", "não pôde ser associado", "Permitido apenas a execução de consultas", "Incorrect syntax",
        "Invalid object name", "Invalid column name", "could not be bound", "ORA-00904", "ORA-00942", "ORA-00933", "ORA-00936",
        "Relacionamento:", "Descritor do campo"
    ];

    /// <summary>Frases padrão que o SQL Server acrescenta quando a trigger desfaz a transação.</summary>
    private static readonly string[] TriggerNoise =
    [
        "A transação foi encerrada no gatilho. O lote foi anulado.", "The transaction ended in the trigger. The batch has been aborted."
    ];

    public static SankhyaException Create(string serviceName, string? status, string? rawMessage, string? tsErrorCode)
    {
        string message = Clean(rawMessage) ?? $"O Sankhya devolveu status {status} sem mensagem.";
        (SankhyaErrorKind kind, string? field) = Classify(serviceName, status, message, tsErrorCode);

        return kind switch
        {
            SankhyaErrorKind.SessionExpired => new SankhyaSessionExpiredException(message, status, serviceName, tsErrorCode),
            SankhyaErrorKind.AuthenticationFailed => new SankhyaAuthenticationException(message, status, serviceName, tsErrorCode),
            SankhyaErrorKind.BusinessRule => new SankhyaBusinessRuleException(message, status, serviceName, tsErrorCode),
            _ => new SankhyaException(message, status, serviceName, tsErrorCode, null, kind, field)
        };
    }

    internal static (SankhyaErrorKind Kind, string? Field) Classify(string serviceName, string? status, string message, string? tsErrorCode)
    {
        string? field = FieldOf(message);

        if (status == "3") return (SankhyaErrorKind.SessionExpired, null);
        if (Is(tsErrorCode, AuthCodes) || serviceName == Login || Has(message, Auth)) return (SankhyaErrorKind.AuthenticationFailed, null);
        if (Has(message, Concurrency)) return (SankhyaErrorKind.Concurrency, null);
        if (Is(tsErrorCode, NotConfiguredCodes) || Has(message, NotConfigured)) return (SankhyaErrorKind.NotConfigured, null);
        if (Has(message, DuplicateKey)) return (SankhyaErrorKind.DuplicateKey, field);
        if (Has(message, Constraint)) return (SankhyaErrorKind.ConstraintViolation, field);
        if (Is(tsErrorCode, InvalidValueCodes) || Has(message, InvalidValue)) return (SankhyaErrorKind.InvalidValue, field);
        if (Is(tsErrorCode, InvalidRequestCodes) || Has(message, InvalidRequest)) return (SankhyaErrorKind.InvalidRequest, field);
        if (OracleUserError().IsMatch(message)) return (SankhyaErrorKind.BusinessRule, field);

        return serviceName switch
        {
            ExecuteQuery => (SankhyaErrorKind.QueryFailed, field),
            // Gravação recusada sem erro técnico reconhecido: regra de negócio (trigger, evento, validação).
            "CRUDServiceProvider.saveRecord" or "CRUDServiceProvider.removeRecord" => (SankhyaErrorKind.BusinessRule, field),
            _ => (SankhyaErrorKind.Unknown, field)
        };
    }

    /// <summary>
    /// Tira o que é ruído técnico e deixa o texto útil: o código <c>ORA-2xxxx:</c> e a pilha <c>ORA-06512</c> do Oracle,
    /// as frases que o SQL Server acrescenta quando a trigger desfaz a transação e espaços sobrando.
    /// </summary>
    internal static string? Clean(string? message)
    {
        if (string.IsNullOrWhiteSpace(message)) return null;

        string text = message;
        foreach (string noise in TriggerNoise) text = text.Replace(noise, " ");

        Match oracle = OracleUserError().Match(text);
        if (oracle.Success) text = oracle.Groups["texto"].Value;

        text = OracleStack().Replace(text, " ");
        text = Spaces().Replace(text, " ").Trim();
        return text.Length == 0 ? message.Trim() : text;
    }

    private static string? FieldOf(string message)
    {
        foreach (Regex pattern in FieldPatterns)
        {
            Match match = pattern.Match(message);
            if (match.Success) return match.Groups["campo"].Value;
        }

        return null;
    }

    private static readonly Regex[] FieldPatterns =
    [
        new(@"Propriedade '[\w$]+\.(?<campo>[\w$]+)'", RegexOptions.CultureInvariant),
        new(@"Descritor do campo '(?<campo>[\w$.]+)'", RegexOptions.CultureInvariant),
        new(@"Campo não existe: [\w$]+->(?<campo>[\w$]+)", RegexOptions.CultureInvariant),
        new(@"para o campo '(?<campo>[\w$]+)'", RegexOptions.CultureInvariant),
        new(@"(?:coluna|column) '(?<campo>[\w$]+)'", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase),
        new(@"""[\w$]+""\.""(?<campo>[\w$]+)""", RegexOptions.CultureInvariant)
    ];

    private static bool Is(string? code, string[] codes) => code != null && codes.Contains(code, StringComparer.OrdinalIgnoreCase);

    private static bool Has(string message, string[] patterns) =>
        patterns.Any(p => message.Contains(p, StringComparison.OrdinalIgnoreCase));

    /// <summary>Erro definido pelo usuário no Oracle (RAISE_APPLICATION_ERROR, -20000 a -20999).</summary>
    [GeneratedRegex(@"ORA-20\d{3}:\s*(?<texto>[^\r\n]*?)(?=\s*ORA-\d{5}|\r|\n|$)")]
    private static partial Regex OracleUserError();

    [GeneratedRegex(@"ORA-0(6512|4088):[^\r\n]*")]
    private static partial Regex OracleStack();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();
}
