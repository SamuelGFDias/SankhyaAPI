using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace SankhyaAPI.Client.Envelopes;

public class FieldsMetadata
{
    [XmlIgnore] [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;

    [XmlIgnore]
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [XmlIgnore]
    [JsonPropertyName("order")]
    public int Order { get; set; }

    [XmlIgnore]
    [JsonPropertyName("userType")]
    public string UserType { get; set; } = string.Empty;
}