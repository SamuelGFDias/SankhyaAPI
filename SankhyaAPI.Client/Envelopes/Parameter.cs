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
}