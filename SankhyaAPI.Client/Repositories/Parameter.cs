using System.Globalization;

namespace SankhyaAPI.Client.Repositories;

/// <summary>
/// Valor de um <c>?</c> na expressão do loadRecords, enviado como <c>&lt;parameter type="X"&gt;</c>, separado da
/// expressão: um valor vindo do usuário nunca vira SQL.
/// </summary>
/// <param name="Type"><c>S</c> texto, <c>I</c> inteiro, <c>F</c> decimal, <c>D</c> data, <c>H</c> data e hora.</param>
/// <param name="Value">Valor já no formato do Sankhya.</param>
public sealed record Parameter(string Type, string Value)
{
    /// <summary>Texto (tipo <c>S</c>).</summary>
    public static Parameter Texto(string value) => new("S", value);

    /// <summary>Número inteiro (tipo <c>I</c>).</summary>
    public static Parameter Inteiro(long value) => new("I", value.ToString(CultureInfo.InvariantCulture));

    /// <summary>Número decimal (tipo <c>F</c>, separador '.').</summary>
    public static Parameter Decimal(decimal value) => new("F", value.ToString(CultureInfo.InvariantCulture));

    /// <summary>Data sem horário (tipo <c>D</c>).</summary>
    public static Parameter Data(DateOnly value) => new("D", value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture));

    /// <summary>Data com horário (tipo <c>H</c>).</summary>
    public static Parameter DataHora(DateTime value) => new("H", value.ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture));

    /// <summary>
    /// Converte um valor .NET pelo tipo: texto, inteiros, decimais, <see cref="DateOnly"/>, <see cref="System.DateTime"/>,
    /// <c>bool</c> (S/N) e enum (pelo código <c>[XmlEnum]</c>).
    /// </summary>
    /// <exception cref="ArgumentException">Tipo sem conversão conhecida.</exception>
    public static Parameter From(object value) =>
        value switch
        {
            Parameter p => p,
            string s => Texto(s),
            char c => Texto(c.ToString()),
            bool b => Texto(b ? "S" : "N"),
            byte or sbyte or short or ushort or int or uint or long => Inteiro(Convert.ToInt64(value, CultureInfo.InvariantCulture)),
            ulong u => Inteiro(checked((long)u)),
            decimal d => Decimal(d),
            double db => new("F", db.ToString("R", CultureInfo.InvariantCulture)),
            float f => new("F", f.ToString("R", CultureInfo.InvariantCulture)),
            DateOnly d => Data(d),
            System.DateTime dt => DataHora(dt),
            DateTimeOffset dto => DataHora(dto.DateTime),
            Enum e => Texto(Mapping.SankhyaFormat.EnumCode(e)),
            _ => throw new ArgumentException($"Tipo sem conversão para parâmetro do Sankhya: {value.GetType().Name}.", nameof(value))
        };
}

/// <summary>
/// Uma página de registros do loadRecords.
/// </summary>
/// <param name="Items">Registros da página (o Sankhya devolve até 50).</param>
/// <param name="Page">Número da página, começando em 0.</param>
/// <param name="HasMoreResult">Há mais páginas depois desta.</param>
public sealed record SankhyaPage<T>(IReadOnlyList<T> Items, int Page, bool HasMoreResult);
