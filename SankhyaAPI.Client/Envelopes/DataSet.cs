using System.Xml.Serialization;

namespace SankhyaAPI.Client.Envelopes;

public class DataSet<TEntity> where TEntity : class
{
    [XmlAttribute(AttributeName = "rootEntity")]
    public string? RootEntity { get; set; } = string.Empty;

    [XmlAttribute(AttributeName = "includePresentationFields")]
    public string IncludePresentationFields { get; set; } = "S";

    [XmlAttribute(AttributeName = "parallelLoader")]
    public string ParallelLoader { get; set; } = "false";

    [XmlAttribute(AttributeName = "disableRowsLimit")]
    public string DisableRowsLimit { get; set; } = "false";

    [XmlAttribute(AttributeName = "orderByExpression")]
    public string? OrderByExpression { get; set; } = string.Empty;

    [XmlElement(ElementName = "entity")] public List<SankhyaEntity> Entity { get; set; } = new();
    [XmlElement(ElementName = "criteria")] public Criteria? Criteria { get; set; }
    [XmlElement(ElementName = "dataRow")] public List<DataRow>? DataRow { get; set; }
}