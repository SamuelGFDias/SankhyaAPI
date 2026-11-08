using System.Globalization;
using System.Text.Json;
using SankhyaAPI.Client.Exceptions;
using SankhyaAPI.Client.Session;
using SankhyaAPI.Client.Transport;
using SankhyaAPI.Client.Mapping;

namespace SankhyaAPI.Client.Sql;

/// <summary>
/// Consultas SQL pelo executeQuery. O SQL roda com o poder do usuário de integração e o servidor devolve no
/// máximo 5000 linhas.
/// </summary>
public interface ISankhyaSql
{
    /// <summary>
    /// Executa a consulta e mapeia cada linha para <typeparamref name="T"/> pelo nome da coluna
    /// (<c>[XmlElement]</c>/<c>[Key]</c> ou nome da propriedade).
    /// </summary>
    /// <exception cref="SankhyaTruncatedResultException">O resultado passou de 5000 linhas e foi cortado pelo servidor.</exception>
    Task<List<T>> QueryRawAsync<T>(SankhyaSql sql, CancellationToken cancellationToken = default) where T : class, new();

    /// <summary>
    /// Executa a consulta e devolve cada linha como dicionário coluna → valor .NET (<c>long</c>, <c>decimal</c>,
    /// <c>string</c> sem espaços nas pontas, <c>DateTime</c>, <c>bool</c> ou <c>null</c>).
    /// </summary>
    Task<List<Dictionary<string, object?>>> QueryAsDictionaryAsync(SankhyaSql sql, CancellationToken cancellationToken = default);

    /// <summary>
    /// Até <paramref name="maxRows"/> linhas (no máximo 5000), sem erro quando há mais: <see cref="SankhyaRows{T}.HasMore"/>
    /// diz se ficaram linhas de fora.
    /// </summary>
    Task<SankhyaRows<T>> QueryTopAsync<T>(SankhyaSql sql, int maxRows, CancellationToken cancellationToken = default) where T : class, new();

    /// <summary>
    /// Percorre um resultado de qualquer tamanho em páginas, embrulhando a consulta em
    /// <c>SELECT * FROM (sql) q ORDER BY ordenação OFFSET n ROWS FETCH NEXT tamanho ROWS ONLY</c>.
    /// </summary>
    /// <param name="orderBy">Ordenação estável (obrigatória), por exemplo <c>"CODPARC"</c>. Fixa no código, não do usuário.</param>
    /// <param name="pageSize">Linhas por chamada, até 5000.</param>
    /// <exception cref="ArgumentException">O SQL tem <c>ORDER BY</c> próprio (o SQL Server o recusa dentro da subconsulta): passe a ordenação em <paramref name="orderBy"/>.</exception>
    IAsyncEnumerable<T> QueryPagedAsync<T>(SankhyaSql sql, string orderBy, int pageSize = 1000, CancellationToken cancellationToken = default) where T : class, new();
}

/// <summary>
/// Linhas de uma consulta limitada e se ficaram outras de fora.
/// </summary>
public sealed record SankhyaRows<T>(IReadOnlyList<T> Items, bool HasMore);

internal sealed partial class SankhyaSqlClient(ISankhyaTransport transport, ISankhyaSession session, SqlDialect dialect) : ISankhyaSql
{
    private const int ServerRowLimit = 5000;

    public async Task<List<T>> QueryRawAsync<T>(SankhyaSql sql, CancellationToken cancellationToken = default)
        where T : class, new()
    {
        using JsonDocument document = await ExecuteAsync(sql.Render(dialect), null, cancellationToken);
        return Map<T>(document);
    }

    public async Task<SankhyaRows<T>> QueryTopAsync<T>(SankhyaSql sql, int maxRows, CancellationToken cancellationToken = default)
        where T : class, new()
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxRows, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maxRows, ServerRowLimit);

        // Com maxRows o servidor não marca burstLimit: pede uma linha a mais para saber se há outras.
        int asked = Math.Min(maxRows + 1, ServerRowLimit);
        using JsonDocument document = await ExecuteAsync(sql.Render(dialect), asked, cancellationToken);
        List<T> rows = Map<T>(document);
        bool hasMore = rows.Count > maxRows || (asked == ServerRowLimit && rows.Count == ServerRowLimit && maxRows == ServerRowLimit);
        return new SankhyaRows<T>(rows.Take(maxRows).ToList(), hasMore);
    }

    public async IAsyncEnumerable<T> QueryPagedAsync<T>(
        SankhyaSql sql,
        string orderBy,
        int pageSize = 1000,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        where T : class, new()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(orderBy);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pageSize, ServerRowLimit);

        string inner = sql.Render(dialect);
        if (HasTopLevelOrderBy(inner))
            throw new ArgumentException(
                "O SQL da consulta paginada não pode ter ORDER BY próprio (o SQL Server o recusa dentro da subconsulta). Passe a ordenação em orderBy.",
                nameof(sql));

        for (long offset = 0;; offset += pageSize)
        {
            string page = $"SELECT * FROM ({inner}) q ORDER BY {orderBy} OFFSET {offset} ROWS FETCH NEXT {pageSize} ROWS ONLY";
            using JsonDocument document = await ExecuteAsync(page, null, cancellationToken);
            List<T> rows = Map<T>(document);
            foreach (T row in rows) yield return row;
            if (rows.Count < pageSize) yield break;
        }
    }

    /// <summary>
    /// <c>ORDER BY</c> fora de parênteses e de literais.
    /// </summary>
    internal static bool HasTopLevelOrderBy(string sql)
    {
        int depth = 0;
        bool quoted = false;
        for (int i = 0; i < sql.Length; i++)
        {
            char c = sql[i];
            if (c == '\'') quoted = !quoted;
            if (quoted) continue;
            if (c == '(') depth++;
            else if (c == ')') depth--;
            else if (depth == 0 && OrderByAt().IsMatch(sql.AsSpan(i))) return true;
        }

        return false;
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"^ORDER\s+BY\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase)]
    private static partial System.Text.RegularExpressions.Regex OrderByAt();

    internal static List<T> Map<T>(JsonDocument document)
        where T : class, new()
    {
        (List<(string Name, int Index)> columns, JsonElement rows) = Read(document);

        EntityMap map = SankhyaMaps.ForRow(typeof(T));
        var byName = columns.ToDictionary(c => c.Name, c => c.Index, StringComparer.OrdinalIgnoreCase);
        List<(MemberMap Member, int[] Indexes)> targets = map.Members
            .Where(m => m.Read != null && m.Set != null && m.Read.Columns.All(byName.ContainsKey))
            .Select(m => (m, m.Read!.Columns.Select(c => byName[c]).ToArray()))
            .ToList();

        var result = new List<T>(rows.GetArrayLength());
        foreach (JsonElement row in rows.EnumerateArray())
        {
            var item = (T)map.Create();
            foreach ((MemberMap member, int[] indexes) in targets)
                member.Assign(item, member.Read!.Compose(indexes.Select(i => (string?)AsText(row[i])).ToArray()));
            result.Add(item);
        }

        return result;
    }

    public async Task<List<Dictionary<string, object?>>> QueryAsDictionaryAsync(SankhyaSql sql, CancellationToken cancellationToken = default)
    {
        using JsonDocument document = await ExecuteAsync(sql.Render(dialect), null, cancellationToken);
        (List<(string Name, int Index)> columns, JsonElement rows) = Read(document);

        return rows.EnumerateArray()
                   .Select(row => columns.ToDictionary(c => c.Name, c => AsClr(row[c.Index])))
                   .ToList();
    }

    private Task<JsonDocument> ExecuteAsync(string sql, int? maxRows, CancellationToken cancellationToken) =>
        session.ExecuteAsync(
            (id, ct) => transport.SendJsonAsync(
                SankhyaServices.ExecuteQuery,
                w =>
                {
                    w.WriteString("sql", sql);
                    if (maxRows != null) w.WriteString("maxRows", maxRows.Value.ToString(CultureInfo.InvariantCulture));
                },
                id,
                idempotent: true,
                ct),
            cancellationToken);

    /// <summary>
    /// Nome e posição de cada coluna. A posição é <c>order - 1</c>, não a ordem em que o <c>fieldsMetadata</c> chega.
    /// </summary>
    private static (List<(string Name, int Index)> Columns, JsonElement Rows) Read(JsonDocument document)
    {
        if (!document.RootElement.TryGetProperty("responseBody", out JsonElement body) || body.ValueKind != JsonValueKind.Object)
            throw new SankhyaException("O executeQuery não devolveu o resultado. Confira o SQL.", "1", SankhyaServices.ExecuteQuery);

        if (body.TryGetProperty("burstLimit", out JsonElement burst) && burst.ValueKind == JsonValueKind.True)
            throw new SankhyaTruncatedResultException(
                $"O Sankhya cortou o resultado da consulta no limite de {ServerRowLimit} linhas do executeQuery. "
              + "Pagine a consulta (ORDER BY ... OFFSET ... FETCH NEXT ...) ou use o loadRecords.",
                SankhyaServices.ExecuteQuery);

        var columns = new List<(string Name, int Index)>();
        if (body.TryGetProperty("fieldsMetadata", out JsonElement metadata) && metadata.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement field in metadata.EnumerateArray())
                columns.Add((field.GetProperty("name").GetString() ?? "", field.GetProperty("order").GetInt32() - 1));
        }

        string? repeated = columns.GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1)?.Key;
        if (repeated != null)
            throw new InvalidOperationException(
                $"A consulta devolveu a coluna '{repeated}' mais de uma vez. Dê um alias (AS) diferente a cada coluna.");

        JsonElement rows = body.TryGetProperty("rows", out JsonElement r) && r.ValueKind == JsonValueKind.Array
                               ? r
                               : JsonDocument.Parse("[]").RootElement;
        return (columns, rows);
    }

    private static string AsText(JsonElement value) =>
        value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? "",
            JsonValueKind.Null or JsonValueKind.Undefined => "",
            JsonValueKind.True => "S",
            JsonValueKind.False => "N",
            _ => value.GetRawText()
        };

    private static object? AsClr(JsonElement value) =>
        value.ValueKind switch
        {
            JsonValueKind.String => DateTime.TryParseExact(value.GetString(), "ddMMyyyy HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date)
                                        ? date
                                        : value.GetString()?.Trim(),
            JsonValueKind.Number => value.TryGetInt64(out long l) ? (object)l : value.GetDecimal(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        };
}
