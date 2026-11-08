using System.Text.RegularExpressions;
using System.Xml.Linq;
using SankhyaAPI.Client.Mapping;

namespace SankhyaAPI.Client.Repositories;

/// <summary>
/// Campo de um update: o membro e se ele vai apagado (vazio).
/// </summary>
internal readonly record struct UpdateField(MemberMap Member, bool Clear);

/// <summary>
/// Monta os envelopes de loadRecords, saveRecord e removeRecord a partir do mapa e lê <c>&lt;entities&gt;</c>.
/// </summary>
internal static partial class EntityXml
{
    public static IEnumerable<XElement> LoadRecords(
        EntityMap map,
        string entityName,
        string expression,
        IReadOnlyList<Parameter>? parameters,
        string? orderBy,
        int? page,
        IEnumerable<XAttribute>? extraAttributes = null)
    {
        var dataSet = new XElement(
            "dataSet",
            new XAttribute("rootEntity", entityName),
            new XAttribute("includePresentationFields", map.UsesPresentationFields ? "S" : "N"),
            new XAttribute("parallelLoader", "false"),
            new XAttribute("disableRowsLimit", page == null ? "true" : "false"));

        if (page != null) dataSet.Add(new XAttribute("offsetPage", page.Value));
        if (!string.IsNullOrWhiteSpace(orderBy)) dataSet.Add(new XAttribute("orderByExpression", orderBy));
        if (extraAttributes != null) dataSet.Add(extraAttributes);

        dataSet.Add(Fields(map));
        foreach ((string path, IReadOnlyList<string> fields) in map.Joins)
            dataSet.Add(new XElement("entity", new XAttribute("path", path), fields.Select(f => new XElement("field", new XAttribute("name", f)))));

        dataSet.Add(new XElement(
            "criteria",
            new XElement("expression", expression),
            parameters?.Select(p => new XElement("parameter", new XAttribute("type", p.Type), p.Value))));

        return [dataSet];
    }

    public static IEnumerable<XElement> Insert(EntityMap map, string entityName, IReadOnlyList<object> entities)
    {
        foreach (object entity in entities) ValidateInsertKeys(map, entity);

        return
        [
            SaveDataSet(map, entityName, entities.Select(entity =>
            {
                var localFields = new XElement("localFields");
                foreach (MemberMap member in map.Members.Where(m => m.Writable && !(m.IsKey && m.AutoNumbered)))
                {
                    if (member.State(entity) != EPropertyState.Set) continue;
                    AddColumns(localFields, member, member.Value(entity), clear: false);
                }

                return new XElement("dataRow", localFields);
            }))
        ];
    }

    public static IEnumerable<XElement> Update(
        EntityMap map,
        string entityName,
        IReadOnlyList<object> entities,
        Func<object, IEnumerable<UpdateField>> fields)
    {
        foreach (object entity in entities) ValidateKeys(map, entity, "atualização");

        return
        [
            SaveDataSet(map, entityName, entities.Select(entity =>
            {
                var localFields = new XElement("localFields");
                foreach (UpdateField field in fields(entity).Where(f => !f.Member.IsKey && f.Member.Writable))
                    AddColumns(localFields, field.Member, field.Clear ? null : field.Member.Value(entity), field.Clear);

                return new XElement("dataRow", localFields, KeyElement("key", map, entity));
            }))
        ];
    }

    public static IEnumerable<XElement> Remove(EntityMap map, string entityName, IReadOnlyList<object> entities)
    {
        foreach (object entity in entities) ValidateKeys(map, entity, "exclusão");

        return [new XElement("entity", new XAttribute("rootEntity", entityName), entities.Select(e => KeyElement("id", map, e)))];
    }

    /// <summary>
    /// Lê <c>&lt;entities&gt;</c> do responseBody, mapeando cada coluna pelo nome: <c>fN</c> é resolvido pela
    /// <c>&lt;metadata&gt;</c> da resposta ou, sem ela, pelos campos pedidos.
    /// </summary>
    public static (List<T> Items, bool HasMoreResult) ReadEntities<T>(EntityMap map, XElement? responseBody)
    {
        XElement? entities = responseBody?.Element("entities");
        if (entities == null) return ([], false);

        List<string?> columns = entities.Element("metadata")?.Element("fields")?.Elements("field")
                                        .Select(f => f.Attribute("name")?.Value)
                                        .ToList() is { Count: > 0 } fromMetadata
                                    ? fromMetadata
                                    : map.ResponseColumns.Cast<string?>().ToList();

        List<MemberMap> readable = map.Members.Where(m => m.Read != null && m.Set != null).ToList();
        var items = new List<T>();
        var row = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        foreach (XElement element in entities.Elements("entity"))
        {
            row.Clear();
            foreach (XElement column in element.Elements())
                row[ColumnName(column.Name.LocalName, columns)] = column.Value;

            object entity = map.Create();
            foreach (MemberMap member in readable)
            {
                ReadSpec read = member.Read!;
                if (!read.Columns.Any(row.ContainsKey)) continue;
                string?[] texts = read.Columns.Select(c => row.GetValueOrDefault(c)).ToArray();
                member.Assign(entity, read.Compose(texts));
            }

            items.Add((T)entity);
        }

        return (items, entities.Attribute("hasMoreResult")?.Value == "true");
    }

    private static void ValidateInsertKeys(EntityMap map, object entity)
    {
        foreach (MemberMap key in map.Keys)
        {
            if (key.State(entity) == EPropertyState.Clear)
                throw new ArgumentException($"A chave primária '{key.Name}' não pode estar com estado CLEAR.", key.Name);

            bool hasValue = key.HasKeyValue(entity);
            if (key.AutoNumbered && hasValue)
                throw new ArgumentException(
                    "Os campos que fazem parte da chave primária com atributos auto enumerados não podem estar preenchidos em operações de inserção.",
                    key.Name);
            if (!key.AutoNumbered && !hasValue)
                throw new ArgumentException(
                    "Os campos que fazem parte da chave primária sem auto numeração precisam estar preenchidos em operações de inserção.",
                    key.Name);
        }
    }

    private static void ValidateKeys(EntityMap map, object entity, string operacao)
    {
        if (map.Keys.Count == 0)
            throw new InvalidOperationException($"O mapa de {map.Type.Name} não tem chave: não é possível fazer {operacao}.");

        foreach (MemberMap key in map.Keys)
        {
            if (key.State(entity) != EPropertyState.Set || key.Value(entity) == null)
                throw new ArgumentNullException(
                    key.Name,
                    $"Os campos que fazem parte da chave primária não podem estar vazios em operações de {operacao}.");
        }
    }

    private static void AddColumns(XElement target, MemberMap member, object? value, bool clear)
    {
        foreach (WriteColumn column in member.Writes)
        {
            string? text = clear ? null : column.Format(value);
            if (text == null && !clear) continue;
            target.Add(text == null ? new XElement(column.Column) : new XElement(column.Column, text));
        }
    }

    private static XElement KeyElement(string name, EntityMap map, object entity) =>
        new(name, map.Keys.Select(k => new XElement(k.Writes[0].Column, k.Writes[0].Format(k.Value(entity)))));

    private static XElement SaveDataSet(EntityMap map, string entityName, IEnumerable<XElement> rows) =>
        new(
            "dataSet",
            new XAttribute("rootEntity", entityName),
            new XAttribute("includePresentationFields", "N"),
            Fields(map),
            rows);

    private static XElement Fields(EntityMap map) =>
        new("entity", new XAttribute("path", ""), map.RootFields.Select(f => new XElement("field", new XAttribute("name", f))));

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
