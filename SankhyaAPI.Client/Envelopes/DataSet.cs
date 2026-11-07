using System.Xml.Serialization;
using SankhyaAPI.Client.Utils;

namespace SankhyaAPI.Client.Envelopes;

public class DataSet
{
    private string? _rootEntity;

    [XmlAttribute(AttributeName = "rootEntity")]
    public string RootEntity
    {
        get => _rootEntity ?? string.Empty;
        set => _rootEntity = value;
    }

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
    public string ParallelLoader { get; set; } = "false";

    [XmlAttribute(AttributeName = "disableRowsLimit")]
    public string DisableRowsLimit { get; set; } = "false";

    /// <summary>
    /// Página a carregar (começa em 0), usada com <see cref="DisableRowsLimit"/> = "false". Nula não é enviada.
    /// </summary>
    [XmlAttribute(AttributeName = "offsetPage")]
    public string? OffsetPage { get; set; }

    [XmlAttribute(AttributeName = "orderByExpression")]
    public string? OrderByExpression { get; set; } = string.Empty;

    [XmlElement(ElementName = "entity")] public List<SankhyaEntity> Entity { get; set; } = [];
    [XmlElement(ElementName = "criteria")] public Criteria? Criteria { get; set; }
    [XmlElement(ElementName = "dataRow")] public List<DataRow>? DataRow { get; set; }
}