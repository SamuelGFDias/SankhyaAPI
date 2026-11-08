using System.Globalization;
using System.Text.Json;
using SankhyaAPI.Client.Exceptions;
using SankhyaAPI.Client.Session;
using SankhyaAPI.Client.Transport;
using SankhyaAPI.Client.Utils;

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
    Task<List<T>> QueryRawAsync<T>(string sql, CancellationToken cancellationToken = default) where T : class, new();

    /// <summary>
    /// Executa a consulta e devolve cada linha como dicionário coluna → valor .NET (<c>long</c>, <c>decimal</c>,
    /// <c>string</c> sem espaços nas pontas, <c>DateTime</c>, <c>bool</c> ou <c>null</c>).
    /// </summary>
    Task<List<Dictionary<string, object?>>> QueryAsDictionaryAsync(string sql, CancellationToken cancellationToken = default);
}

internal sealed class SankhyaSqlClient(ISankhyaTransport transport, ISankhyaSession session) : ISankhyaSql
{
    private const int ServerRowLimit = 5000;

    public async Task<List<T>> QueryRawAsync<T>(string sql, CancellationToken cancellationToken = default)
        where T : class, new()
    {
        using JsonDocument document = await ExecuteAsync(sql, cancellationToken);
        (List<(string Name, int Index)> columns, JsonElement rows) = Read(document);

        ModelMap map = ModelMap.For<T>();
        var byName = columns.ToDictionary(c => c.Name, c => c.Index, StringComparer.OrdinalIgnoreCase);
        List<(PropertyMap Property, int Index)> targets = map.Properties
            .Select(p => (Property: p, Found: byName.TryGetValue(p.XmlName ?? p.Name, out int index), Index: index))
            .Where(t => t.Found)
            .Select(t => (t.Property, t.Index))
            .ToList();

        var result = new List<T>(rows.GetArrayLength());
        foreach (JsonElement row in rows.EnumerateArray())
        {
            var item = new T();
            foreach ((PropertyMap property, int index) in targets)
                property.SetValue(item, ObjectUtilsMethods.ConvertForPropertyType(AsText(row[index]), property.Property));
            result.Add(item);
        }

        return result;
    }

    public async Task<List<Dictionary<string, object?>>> QueryAsDictionaryAsync(string sql, CancellationToken cancellationToken = default)
    {
        using JsonDocument document = await ExecuteAsync(sql, cancellationToken);
        (List<(string Name, int Index)> columns, JsonElement rows) = Read(document);

        return rows.EnumerateArray()
                   .Select(row => columns.ToDictionary(c => c.Name, c => AsClr(row[c.Index])))
                   .ToList();
    }

    private Task<JsonDocument> ExecuteAsync(string sql, CancellationToken cancellationToken) =>
        session.ExecuteAsync(
            (id, ct) => transport.SendJsonAsync(SankhyaServices.ExecuteQuery, w => w.WriteString("sql", sql), id, idempotent: true, ct),
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
            JsonValueKind.Number => value.TryGetInt64(out long l) ? l : value.GetDecimal(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        };
}
