using System.Text.RegularExpressions;
using System.Xml.Linq;
using SankhyaAPI.Client.Extensions;
using SankhyaAPI.Client.Utils;

namespace SankhyaAPI.Client.Repositories;

/// <summary>
/// Monta os envelopes de loadRecords, saveRecord e removeRecord e lê <c>&lt;entities&gt;</c>.
/// </summary>
internal static partial class EntityXml
{
    public static IEnumerable<XElement> LoadRecords<T>(
        string entityName,
        string expression,
        IReadOnlyList<Parameter>? parameters,
        string? orderBy,
        int? page)
    {
        var dataSet = new XElement(
            "dataSet",
            new XAttribute("rootEntity", entityName),
            new XAttribute("includePresentationFields", "S"),
            new XAttribute("parallelLoader", "false"),
            new XAttribute("disableRowsLimit", page == null ? "true" : "false"));

        if (page != null) dataSet.Add(new XAttribute("offsetPage", page.Value));
        if (!string.IsNullOrWhiteSpace(orderBy)) dataSet.Add(new XAttribute("orderByExpression", orderBy));

        dataSet.Add(Fields<T>());
        dataSet.Add(new XElement(
            "criteria",
            new XElement("expression", expression),
            parameters?.Select(p => new XElement("parameter", new XAttribute("type", p.Type), p.Value))));

        return [dataSet];
    }

    public static IEnumerable<XElement> Insert<T>(string entityName, IReadOnlyList<T> entities)
        where T : class, new()
    {
        SankhyaModelBase.ValidateNullableStateProperties(entities.ToList());
        ObjectUtilsMethods.ValidarCamposChave(entities.ToList());

        return [SaveDataSet<T>(entityName, entities.Select(e => new XElement("dataRow", e.AddEntityForFields())))];
    }

    public static IEnumerable<XElement> Update<T>(string entityName, IReadOnlyList<T> entities)
        where T : class, new()
    {
        SankhyaModelBase.ValidateNullableStateProperties(entities.ToList(), true);
        ObjectUtilsMethods.ValidarCamposChave(entities.ToList(), true);

        return
        [
            SaveDataSet<T>(entityName, entities.Select(e =>
            {
                var key = new XElement("key");
                key.AddKeysAsXml(e);
                return new XElement("dataRow", e.AddEntityForFields(true), key);
            }))
        ];
    }

    public static IEnumerable<XElement> Remove<T>(string entityName, IReadOnlyList<T> entities)
        where T : class, new()
    {
        ObjectUtilsMethods.ValidarCamposChave(entities.ToList(), true);

        var entity = new XElement("entity", new XAttribute("rootEntity", entityName));
        foreach (T obj in entities)
        {
            var id = new XElement("id");
            id.AddKeysAsXml(obj);
            entity.Add(id);
        }

        return [entity];
    }

    /// <summary>
    /// Lê <c>&lt;entities&gt;</c> do responseBody, mapeando cada coluna pelo nome: <c>fN</c> é resolvido pela
    /// <c>&lt;metadata&gt;</c> da resposta ou, sem ela, pelos campos pedidos.
    /// </summary>
    public static (List<T> Items, bool HasMoreResult) ReadEntities<T>(XElement? responseBody)
        where T : class, new()
    {
        XElement? entities = responseBody?.Element("entities");
        if (entities == null) return ([], false);

        ModelMap map = ModelMap.For<T>();
        List<string?> columns = entities.Element("metadata")?.Element("fields")?.Elements("field")
                                        .Select(f => f.Attribute("name")?.Value)
                                        .ToList() is { Count: > 0 } fromMetadata
                                    ? fromMetadata
                                    : map.Mapped.Select(p => p.XmlName).ToList();

        var items = new List<T>();
        foreach (XElement row in entities.Elements("entity"))
        {
            var entity = new T();
            foreach (XElement column in row.Elements())
            {
                PropertyMap? property = map.ByXmlName(ColumnName(column.Name.LocalName, columns));
                if (property == null) continue;
                property.SetValue(entity, ObjectUtilsMethods.ConvertForPropertyType(column.Value, property.Property));
            }

            items.Add(entity);
        }

        return (items, entities.Attribute("hasMoreResult")?.Value == "true");
    }

    private static XElement SaveDataSet<T>(string entityName, IEnumerable<XElement> rows) =>
        new(
            "dataSet",
            new XAttribute("rootEntity", entityName),
            new XAttribute("includePresentationFields", "S"),
            Fields<T>(),
            rows);

    private static XElement Fields<T>() =>
        new("entity", new XAttribute("path", ""), ModelMap.For<T>().Mapped.Select(p => new XElement("field", new XAttribute("name", p.XmlName!))));

    private static string ColumnName(string element, List<string?> columns)
    {
        Match match = PositionalColumn().Match(element);
        if (!match.Success) return element;

        int index = int.Parse(match.Groups[1].Value);
        return index < columns.Count ? columns[index] ?? element : element;
    }

    [GeneratedRegex(@"^f(\d+)$")]
    private static partial Regex PositionalColumn();
}
