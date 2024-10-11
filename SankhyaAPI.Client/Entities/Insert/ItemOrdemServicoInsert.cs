using System.Xml.Serialization;
using SankhyaAPI.Client.Extensions;

namespace SankhyaAPI.Client.Entities.Insert;

public class ItemOrdemServicoInsert : XmlSerialable
{
    [XmlElement("NUMOS")] public long NumOs { get; set; }
    [XmlElement("NUMITEM")] public long NumItem { get; set; }
}