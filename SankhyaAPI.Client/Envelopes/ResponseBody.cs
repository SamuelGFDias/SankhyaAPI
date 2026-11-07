using SankhyaAPI.Client.Utils;
using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace SankhyaAPI.Client.Envelopes;

public class ResponseBody<TEntity> where TEntity : class
{
    private string? _idUsuDecoded;

    [XmlElement("jsessionid")] public string? SessionId { get; set; }

    [XmlElement("idusu")]
    public string? IdUsuario
    {
        get => _idUsuDecoded;
        set => _idUsuDecoded = SankhyaText.DecodeBase64(value);
    }

    [JsonPropertyName("callID")]
    [XmlElement(ElementName = "callID")]
    public string? CallId { get; set; }

    [JsonPropertyName("entities")]
    [XmlElement(ElementName = "entities")]
    public Entities<TEntity>? Entities { get; set; }

    [XmlIgnore]
    [JsonPropertyName("fieldsMetadata")]
    public List<FieldsMetadata>? FieldsMetadata { get; set; }

    [XmlIgnore]
    [JsonPropertyName("rows")]
    public List<List<object>>? Rows { get; set; }

    [JsonPropertyName("chave")]
    [XmlElement(ElementName = "chave")]
    public Chave? Chave { get; set; }
}