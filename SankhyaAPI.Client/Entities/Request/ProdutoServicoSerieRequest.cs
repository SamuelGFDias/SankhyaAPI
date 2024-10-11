using System.Xml.Serialization;
using SankhyaAPI.Client.Extensions;
using SankhyaAPI.Client.Requests;

namespace SankhyaAPI.Client.Entities.Request;

public class ProdutoServicoSerieRequest : XmlSerialable, IObjectWithKey
{
    [XmlIgnore]
    [PrimaryKeyElement("SERIE")]
    public string? Serie { get; set; }

    [XmlIgnore]
    [PrimaryKeyElement("NUMCONTRATO")]
    public long NumContrato { get; set; }

    [XmlIgnore]
    [PrimaryKeyElement("CODPROD")]
    public long CodProd { get; set; }
}