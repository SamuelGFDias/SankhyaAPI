using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using SankhyaAPI.Client.Mapping;

namespace SankhyaAPI.Client.Sql;

/// <summary>
/// Banco por trás do Sankhya, para formatar valores no SQL cru.
/// </summary>
public enum SqlDialect
{
    SqlServer,
    Oracle
}

/// <summary>
/// SQL para o executeQuery, escrito como string interpolada: cada <c>{valor}</c> é formatado pelo tipo e
/// escapado (texto entre aspas com <c>'</c> dobrado; número, data e hora em formato invariante; <c>null</c> como
/// <c>NULL</c>). Tipos sem formatação conhecida são recusados.
/// </summary>
/// <remarks>
/// O executeQuery não aceita parâmetros: escapar não é o mesmo que parametrizar. Para filtrar entidades por
/// valores vindos do usuário, o caminho seguro é o loadRecords (<c>Query().Where("... ?", valor)</c>).
/// Uma <c>string</c> comum também é aceita (SQL fixo); montar SQL concatenando valores gera o aviso SNK005.
/// </remarks>
[InterpolatedStringHandler]
public readonly struct SankhyaSql
{
    private readonly List<object?> _parts;

    public SankhyaSql(int literalLength, int formattedCount)
    {
        _parts = new List<object?>(formattedCount * 2 + 1);
    }

    private SankhyaSql(string sql)
    {
        _parts = [new Literal(sql)];
    }

    public void AppendLiteral(string value) => _parts.Add(new Literal(value));

    public void AppendFormatted<T>(T value) => _parts.Add(value);

    /// <summary>
    /// Trecho de SQL fixo, sem escape (nomes de tabela ou coluna escolhidos pelo código, nunca pelo usuário).
    /// </summary>
    public static SankhyaSql Raw(string sql) => new(sql);

    public static implicit operator SankhyaSql(string sql) => new(sql);

    internal string Render(SqlDialect dialect)
    {
        var sql = new StringBuilder();
        foreach (object? part in _parts ?? [])
            sql.Append(part is Literal literal ? literal.Text : Format(part, dialect));
        return sql.ToString();
    }

    public override string ToString() => Render(SqlDialect.SqlServer);

    internal static string Format(object? value, SqlDialect dialect) =>
        value switch
        {
            null => "NULL",
            SankhyaSql nested => nested.Render(dialect),
            string s => Quote(s),
            char c => Quote(c.ToString()),
            Guid g => Quote(g.ToString()),
            bool b => b ? "'S'" : "'N'",
            byte or sbyte or short or ushort or int or uint or long or ulong => Convert.ToString(value, CultureInfo.InvariantCulture)!,
            decimal d => d.ToString(CultureInfo.InvariantCulture),
            double d => d.ToString("R", CultureInfo.InvariantCulture),
            float f => f.ToString("R", CultureInfo.InvariantCulture),
            DateOnly d => Date(d.ToDateTime(TimeOnly.MinValue), dialect, withTime: false),
            DateTime dt => Date(dt, dialect, withTime: true),
            DateTimeOffset dto => Date(dto.DateTime, dialect, withTime: true),
            Enum e => Quote(SankhyaFormat.EnumCode(e)),
            System.Collections.IEnumerable list => "(" + string.Join(", ", list.Cast<object?>().Select(v => Format(v, dialect))) + ")",
            _ => throw new ArgumentException($"Tipo sem formatação segura para SQL: {value.GetType().Name}.")
        };

    private static string Quote(string text)
    {
        if (text.Contains('\0')) throw new ArgumentException("Texto com caractere nulo não pode ir para o SQL.");
        return "'" + text.Replace("'", "''") + "'";
    }

    private static string Date(DateTime value, SqlDialect dialect, bool withTime) =>
        dialect switch
        {
            SqlDialect.Oracle => withTime
                                     ? $"TO_DATE('{value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)}', 'YYYY-MM-DD HH24:MI:SS')"
                                     : $"TO_DATE('{value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}', 'YYYY-MM-DD')",
            _ => withTime
                     ? $"CONVERT(DATETIME, '{value.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture)}', 126)"
                     : $"CONVERT(DATE, '{value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}', 23)"
        };

    private sealed record Literal(string Text);
}
