using System.Xml.Serialization;
using SankhyaAPI.Client.Extensions;

namespace SankhyaAPI.Client.Entities.Insert;

public class ItemCabecalhoNotaInsert : XmlSerialable
{
    [XmlElement("NUNOTA")] public long? NuNota { get; set; }
   
}