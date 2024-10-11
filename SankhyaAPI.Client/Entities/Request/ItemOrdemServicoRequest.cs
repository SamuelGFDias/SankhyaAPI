using System.Xml.Serialization;
using SankhyaAPI.Client.Extensions;
using SankhyaAPI.Client.Requests;

namespace SankhyaAPI.Client.Entities.Request;

public class ItemOrdemServicoRequest : XmlSerialable, IObjectWithKey
{
    [XmlIgnore]
    [PrimaryKeyElement("NUMOS")]
    public long NumOs { get; set; }

    [XmlIgnore]
    [PrimaryKeyElement("NUMITEM")]
    public long NumItem { get; set; }
}