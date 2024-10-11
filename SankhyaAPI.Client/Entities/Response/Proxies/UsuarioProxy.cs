using System.Xml.Serialization;
using Newtonsoft.Json;

namespace SankhyaAPI.Client.Entities.Response.Proxies;

public enum UsuarioElementName
{
    None,
    f0,
    CODUSU,
}

public class UsuarioProxy
{
    [XmlElement("f0")]
    [XmlElement("CODUSU")]
    [XmlChoiceIdentifier(nameof(CodUsuElementName))]
    public long CodUsu { get; set; }

    [XmlIgnore] [JsonIgnore] public UsuarioElementName CodUsuElementName { get; set; }
}