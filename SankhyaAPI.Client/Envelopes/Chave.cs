using System.Xml.Serialization;

namespace SankhyaAPI.Client.Envelopes;

public class Chave
{
    [XmlAttribute(AttributeName = "valor")]
    public string? Valor { get; set; }

    [XmlAttribute(AttributeName = "permiteExportar")]
    public string? PermiterExportar { get; set; }

    [XmlAttribute(AttributeName = "contentType")]
    public string? ContentType { get; set; }

    [XmlAttribute(AttributeName = "format")]
    public string? Format { get; set; }

    [XmlAttribute(AttributeName = "canSendViaEmail")]
    public string? CanSendViaEmail { get; set; }

}