using System.Xml.Serialization;

namespace SankhyaAPI.Client.Responses;

public class ResponseEntity
{
    [XmlArray] public List<object> F { get; set; } = new();
}