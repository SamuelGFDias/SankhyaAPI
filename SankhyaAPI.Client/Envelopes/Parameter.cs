using System.Globalization;
using System.Xml.Serialization;

namespace SankhyaAPI.Client.Envelopes;

/// <remarks>
/// Quando temos parâmetros variáveis, como é o caso da expressão aqui usada, devemos colocar um '?' onde o valor do parâmetro será injetado.
/// Para cada '?' na expressão devemos ter um elemento 'parameter'. Os possíveis valores para o atributo 'type' são:
/// <list type="bullet">
/// <item>
/// <description>D = Data sem horário</description>
/// </item>
/// <item>
/// <description>H = Data com horário (00:00:00)</description>
/// </item>
/// <item>
/// <description>F = Número decimal (decimais separados por '.')</description>
/// </item>
/// <item>
/// <description>I = Número inteiro</description>
/// </item>
/// <item>
/// <description>S = Texto</description>
/// </item>
/// </list>
/// </remarks>
public class Parameter
{
    public Parameter()
    {
    }

    public Parameter(string tipo, string value)
    {
        Type = tipo;
        Value = value;
    }

    [XmlAttribute(AttributeName = "type")] public string? Type { get; set; }
    [XmlText] public string? Value { get; set; }

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
}