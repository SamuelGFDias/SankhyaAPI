using System.Xml.Serialization;
using SankhyaAPI.Client.Extensions;
using SankhyaAPI.Client.Requests;

namespace SankhyaAPI.Client.Entities.Request;

public class UsuarioRequest : XmlSerialable, IObjectWithKey
{
    [XmlIgnore]
    [PrimaryKeyElement("CODUSU")]
    public long CodUsu { get; set; }
}