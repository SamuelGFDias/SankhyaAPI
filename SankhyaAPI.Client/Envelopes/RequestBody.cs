using System.Text.Json.Serialization;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace SankhyaAPI.Client.Envelopes;

public class RequestBody
{
    [JsonIgnore] [XmlElement("dataSet")] public DataSet? DataSet { get; set; }

    [XmlAnyElement(Name = "entity")] public XElement? Entity { get; set; }

    [JsonIgnore] [XmlElement("NOMUSU")] public string? NomeUsu { get; set; }

    [JsonIgnore] [XmlElement("relatorio")] public Relatorio? Relatorio { get; set; }

    [JsonIgnore] [XmlElement("INTERNO")] public string? Interno { get; set; }

    [XmlIgnore] [JsonPropertyName("sql")] public string? Sql { get; set; }
}