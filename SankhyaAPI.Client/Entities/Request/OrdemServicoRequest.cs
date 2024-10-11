using System.Xml.Serialization;
using SankhyaAPI.Client.Extensions;
using SankhyaAPI.Client.Requests;

namespace SankhyaAPI.Client.Entities.Request;

public class OrdemServicoRequest : IObjectWithKey
{
    [XmlIgnore]
    [PrimaryKeyElement("NUMOS")]
    public long NumOs { get; set; }
}