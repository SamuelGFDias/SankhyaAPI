using System.Xml.Serialization;
using SankhyaAPI.Client.Utils;

namespace SankhyaAPI.Client.Envelopes;

public class DataSet
{
    private string? _rootEntity;

    [XmlAttribute(AttributeName = "rootEntity")]
    public string RootEntity => _rootEntity ?? string.Empty;

    public void SetRootEntity(Enum entityName)
    {
        _rootEntity = entityName.GetXmlEnumValue();
    }
    
    public void SetRootEntity(string entityName)
    {
        _rootEntity = entityName;
    }

    [XmlAttribute(AttributeName = "includePresentationFields")]
    public string IncludePresentationFields { get; set; } = "S";

    [XmlAttribute(AttributeName = "parallelLoader")]
    public string ParallelLoader { get; private set; } = "false";

    [XmlAttribute(AttributeName = "disableRowsLimit")]
    public string DisableRowsLimit { get; set; } = "false";

    [XmlAttribute(AttributeName = "orderByExpression")]
    public string? OrderByExpression { get; set; } = string.Empty;

    [XmlElement(ElementName = "entity")] public List<SankhyaEntity> Entity { get; set; } = [];
    [XmlElement(ElementName = "criteria")] public Criteria? Criteria { get; set; }
    [XmlElement(ElementName = "dataRow")] public List<DataRow>? DataRow { get; set; }
}