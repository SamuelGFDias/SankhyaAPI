using System.Xml.Serialization;
using SankhyaAPI.Client.Utils;

namespace SankhyaAPI.Client.Envelopes;

public class DataSet
{
    private Enum? _rootEntity;

    [XmlAttribute(AttributeName = "rootEntity")]
    public string? RootEntity
    {
        get => _rootEntity != null ? ObjectUtilsMethods.GetXmlEnumValue(_rootEntity) : string.Empty;
        set { }
    }

    public void SetRootEntity(Enum entity)
    {
        _rootEntity = entity;
    }

    [XmlAttribute(AttributeName = "includePresentationFields")]
    public string IncludePresentationFields { get; set; } = "S";

    [XmlAttribute(AttributeName = "parallelLoader")]
    public string ParallelLoader { get; set; } = "false";

    [XmlAttribute(AttributeName = "disableRowsLimit")]
    public string DisableRowsLimit { get; set; } = "false";

    [XmlAttribute(AttributeName = "orderByExpression")]
    public string? OrderByExpression { get; set; } = string.Empty;

    [XmlElement(ElementName = "entity")] public List<SankhyaEntity> Entity { get; set; } = [];
    [XmlElement(ElementName = "criteria")] public Criteria? Criteria { get; set; }
    [XmlElement(ElementName = "dataRow")] public List<DataRow>? DataRow { get; set; }
}