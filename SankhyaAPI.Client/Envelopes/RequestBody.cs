using System.Text.Json.Serialization;
using System.Xml.Serialization;
using Newtonsoft.Json;

namespace SankhyaAPI.Client.Envelopes;

public class RequestBody<TEntity> where TEntity : class
{
    [Newtonsoft.Json.JsonIgnore]
    [System.Text.Json.Serialization.JsonIgnore]
    [XmlElement("dataSet")]
    public DataSet<TEntity> DataSet { get; set; } = new();

    [Newtonsoft.Json.JsonIgnore]
    [System.Text.Json.Serialization.JsonIgnore]
    [XmlElement("NOMUSU")]
    public string? NomeUsu { get; set; }

    [Newtonsoft.Json.JsonIgnore]
    [System.Text.Json.Serialization.JsonIgnore]
    [XmlElement("relatorio")]
    public Relatorio? Relatorio { get; set; }

    [Newtonsoft.Json.JsonIgnore]
    [System.Text.Json.Serialization.JsonIgnore]
    [XmlElement("INTERNO")]
    public string? Interno { get; set; }

    [XmlIgnore]
    [JsonProperty("sql")]
    [JsonPropertyName("sql")]
    public string? Sql { get; set; }
}