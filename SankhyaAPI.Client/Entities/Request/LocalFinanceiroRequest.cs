using System.Xml.Serialization;
using SankhyaAPI.Client.Extensions;
using SankhyaAPI.Client.Requests;

namespace SankhyaAPI.Client.Entities.Request;

public class LocalFinanceiroRequest : XmlSerialable, IObjectWithKey
{
    [XmlIgnore]
    [PrimaryKeyElement("CODLOCAL")]
    public long CodLocal { get; set; }
}