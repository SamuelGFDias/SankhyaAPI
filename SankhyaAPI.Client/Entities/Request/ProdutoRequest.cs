using System.Xml.Serialization;
using SankhyaAPI.Client.Extensions;
using SankhyaAPI.Client.Requests;

namespace SankhyaAPI.Client.Entities.Request;

public class ProdutoRequest : XmlSerialable, IObjectWithKey
{
    [XmlIgnore]
    [PrimaryKeyElement("CODPROD")]
    public long CodProd { get; set; }
}