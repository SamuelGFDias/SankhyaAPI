using System.Xml.Linq;
using SankhyaAPI.Client.Mapping;
using SankhyaAPI.Client.Repositories;

namespace SankhyaAPI.Client.Testing;

/// <summary>
/// Verificações de mapeamento que não precisam de servidor.
/// </summary>
public static class SankhyaMapAssert
{
    /// <summary>
    /// Ida e volta de um modelo pelo mapa, sem servidor: monta o insert do <paramref name="sample"/>, devolve as
    /// colunas gravadas como resposta e lê de volta. Compare o valor devolvido com o original para provar que o mapa
    /// preserva os dados.
    /// </summary>
    /// <typeparam name="T">Modelo mapeado, com construtor sem parâmetros.</typeparam>
    /// <param name="sample">Exemplo com os valores que devem sobreviver à ida e volta.</param>
    /// <returns>A instância lida de volta a partir das colunas gravadas.</returns>
    public static T RoundTrip<T>(T sample) where T : class, new()
    {
        var map = SankhyaMaps.For(typeof(T));
        XElement dataSet = EntityXml.Insert(map, map.EntityName ?? "X", [sample]).Single();
        XElement localFields = dataSet.Element("dataRow")!.Element("localFields")!;
        foreach (var key in map.Keys)
            localFields.Add(new XElement(key.Writes[0].Column, key.Writes[0].Format(key.Value(sample))));

        var response = new XElement("responseBody",
            new XElement("entities",
                new XElement("metadata", new XElement("fields", localFields.Elements().Select(e => new XElement("field", new XAttribute("name", e.Name.LocalName))))),
                new XElement("entity", localFields.Elements().Select((e, i) => new XElement($"f{i}", e.Value)))));

        return EntityXml.ReadEntities<T>(map, response).Items.Single();
    }
}
