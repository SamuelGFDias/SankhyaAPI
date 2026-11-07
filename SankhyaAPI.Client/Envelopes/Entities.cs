using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using SankhyaAPI.Client.Utils;

namespace SankhyaAPI.Client.Envelopes;

public partial class Entities<TEntity> : IXmlSerializable where TEntity : class
{
    [XmlAttribute(AttributeName = "total")]
    public string? Total { get; set; }

    /// <summary>
    /// Há mais registros além desta página (loadRecords paginado).
    /// </summary>
    [XmlIgnore]
    public bool HasMoreResult { get; set; }

    /// <summary>
    /// Página devolvida (começa em 0), quando a consulta é paginada.
    /// </summary>
    [XmlIgnore]
    public int? OffsetPage { get; set; }

    [XmlElement(ElementName = "metadata")] public Metadata? Metadata { get; set; }

    [XmlElement(ElementName = "entity")] public List<TEntity> Entity { get; set; } = new();

    public XmlSchema? GetSchema()
    {
        return null;
    }

    /// <summary>
    /// Lê <c>&lt;entities&gt;</c> até o seu fechamento e para ali, sem consumir o que vem depois no
    /// <c>responseBody</c>. Cada coluna é mapeada pelo nome, não pela posição: <c>fN</c> é resolvido pela
    /// <c>&lt;metadata&gt;</c> da resposta ou, sem ela, pelos campos pedidos no envelope.
    /// </summary>
    public void ReadXml(XmlReader reader)
    {
        Total = reader.GetAttribute("total");
        HasMoreResult = reader.GetAttribute("hasMoreResult") == "true";
        OffsetPage = int.TryParse(reader.GetAttribute("offsetPage"), out int offsetPage) ? offsetPage : null;

        if (reader.IsEmptyElement)
        {
            reader.Read();
            return;
        }

        ModelMap map = ModelMap.For(typeof(TEntity));
        List<string?> colunas = map.Mapped.Select(p => p.XmlName).ToList();

        reader.ReadStartElement();

        while (reader.MoveToContent() == XmlNodeType.Element)
        {
            switch (reader.LocalName)
            {
                case "metadata":
                    Metadata = ReadMetadata(reader);
                    if (Metadata.Fields is { Count: > 0 } fields)
                        colunas = fields.Select(f => f.Nome).ToList();
                    break;

                case "entity":
                    Entity.Add(ReadEntity(reader, map, colunas));
                    break;

                default:
                    reader.Skip();
                    break;
            }
        }

        reader.ReadEndElement();
    }

    private static Metadata ReadMetadata(XmlReader reader)
    {
        var metadata = new Metadata { Fields = [] };

        using XmlReader subtree = reader.ReadSubtree();
        while (subtree.ReadToFollowing("field"))
            metadata.Fields.Add(new Field { Nome = subtree.GetAttribute("name") });

        reader.Skip();
        return metadata;
    }

    private static TEntity ReadEntity(XmlReader reader, ModelMap map, List<string?> colunas)
    {
        var entity = Activator.CreateInstance<TEntity>();

        if (reader.IsEmptyElement)
        {
            reader.Read();
            return entity;
        }

        reader.ReadStartElement();

        while (reader.MoveToContent() == XmlNodeType.Element)
        {
            PropertyMap? property = map.ByXmlName(NomeDaColuna(reader.LocalName, colunas));

            if (property == null)
            {
                reader.Skip();
                continue;
            }

            string value = reader.ReadElementContentAsString();
            property.SetValue(entity, ObjectUtilsMethods.ConvertForPropertyType(value, property.Property));
        }

        reader.ReadEndElement();
        return entity;
    }

    private static string NomeDaColuna(string elemento, List<string?> colunas)
    {
        Match match = ColunaPosicional().Match(elemento);
        if (!match.Success) return elemento;

        int indice = int.Parse(match.Groups[1].Value);
        return indice < colunas.Count ? colunas[indice] ?? elemento : elemento;
    }

    [GeneratedRegex(@"^f(\d+)$")]
    private static partial Regex ColunaPosicional();

    public void WriteXml(XmlWriter writer)
    {
        if (Total != null)
            writer.WriteAttributeString("total", Total);

        if (Metadata != null)
        {
            writer.WriteStartElement("metadata");
            var serializer = new XmlSerializer(typeof(Metadata));
            serializer.Serialize(writer, Metadata);
            writer.WriteEndElement();
        }

        foreach (TEntity? entity in Entity)
        {
            writer.WriteStartElement("entity");

            PropertyInfo[] properties = entity.GetType().GetProperties();

            foreach (PropertyInfo? property in properties)
            {
                string? xmlElementName = property.GetXmlElementName();

                // Escreve o valor da propriedade no XML
                if (xmlElementName == null) continue;

                object? value = property.GetValue(entity);
                if (value != null)
                {
                    writer.WriteElementString(xmlElementName, value.ToString());
                }
            }

            writer.WriteEndElement();
        }
    }
}
