using System.Xml.Serialization;

namespace SankhyaAPI.Client.Envelopes;

public class Relatorio
{
    [XmlAttribute(AttributeName = "nuRfe")]
    public string? NuRfe { get; set; }

    [XmlArray("parametros")]
    [XmlArrayItem("parametro", IsNullable = true)]
    public List<Parametro>? Parametros { get; set; }
}