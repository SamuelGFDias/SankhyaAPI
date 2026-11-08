using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using SankhyaAPI.Client.Mapping;

namespace SankhyaAPI.Client.Benchmarks;

/// <summary>
/// Dados de entrada dos benchmarks, montados uma vez no GlobalSetup (fora da medição).
/// </summary>
internal static class Dados
{
    /// <summary>Colunas da entidade Produto, na ordem do <c>fieldsMetadata</c>.</summary>
    private static readonly string[] Colunas = ["CODPROD", "DESCRPROD", "PRECO", "DTALTER", "ATIVO"];

    private static readonly DateTime DataBase = new(2024, 1, 1, 10, 0, 0);

    /// <summary>Registra o mapa fluente do POCO, para que <see cref="SankhyaMaps.For"/> o use.</summary>
    public static void RegistrarMapas() => SankhyaMaps.Register<ProdutoPocoMap>();

    public static List<ProdutoAtributos> Atributos(int quantidade) =>
        Enumerable.Range(0, quantidade)
                  .Select(i => new ProdutoAtributos
                  {
                      DescrProd = $"Produto {i + 1}",
                      Preco = 10.5m + i % 100,
                      DtAlter = DataBase.AddMinutes(i),
                      Ativo = i % 2 == 0
                  })
                  .ToList();

    public static List<ProdutoPoco> Pocos(int quantidade) =>
        Enumerable.Range(0, quantidade)
                  .Select(i => new ProdutoPoco
                  {
                      DescrProd = $"Produto {i + 1}",
                      Preco = 10.5m + i % 100,
                      DtAlter = DataBase.AddMinutes(i),
                      Ativo = i % 2 == 0
                  })
                  .ToList();

    /// <summary>
    /// Resposta de loadRecords: <c>&lt;responseBody&gt;&lt;entities&gt;</c> com <c>&lt;metadata&gt;</c> e uma
    /// <c>&lt;entity&gt;</c> por linha, colunas posicionais <c>f0</c>..<c>f4</c>.
    /// </summary>
    public static XElement RespostaEntities(int quantidade)
    {
        var metadata = new XElement("metadata", new XElement("fields", Colunas.Select(c => new XElement("field", new XAttribute("name", c)))));
        var entities = new XElement("entities", new XAttribute("total", quantidade), new XAttribute("hasMoreResult", "false"), metadata);

        for (int i = 0; i < quantidade; i++)
        {
            entities.Add(new XElement(
                "entity",
                new XElement("f0", (i + 1).ToString(CultureInfo.InvariantCulture)),
                new XElement("f1", $"Produto {i + 1}"),
                new XElement("f2", (10.5m + i % 100).ToString("0.00", CultureInfo.InvariantCulture)),
                new XElement("f3", DataBase.AddMinutes(i).ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture)),
                new XElement("f4", i % 2 == 0 ? "S" : "N")));
        }

        return new XElement("responseBody", entities);
    }

    /// <summary>
    /// Corpo JSON do executeQuery: <c>{"status":"1","responseBody":{"fieldsMetadata":[...],"rows":[[...],...]}}</c>.
    /// </summary>
    public static string RespostaExecuteQuery(int quantidade)
    {
        using var memoria = new MemoryStream();
        using (var json = new Utf8JsonWriter(memoria))
        {
            json.WriteStartObject();
            json.WriteString("status", "1");
            json.WriteStartObject("responseBody");

            json.WriteStartArray("fieldsMetadata");
            for (int i = 0; i < Colunas.Length; i++)
            {
                json.WriteStartObject();
                json.WriteString("name", Colunas[i]);
                json.WriteNumber("order", i + 1);
                json.WriteEndObject();
            }
            json.WriteEndArray();

            json.WriteStartArray("rows");
            for (int i = 0; i < quantidade; i++)
            {
                json.WriteStartArray();
                json.WriteNumberValue(i + 1);
                json.WriteStringValue($"Produto {i + 1}");
                json.WriteStringValue((10.5m + i % 100).ToString("0.00", CultureInfo.InvariantCulture));
                json.WriteStringValue(DataBase.AddMinutes(i).ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture));
                json.WriteStringValue(i % 2 == 0 ? "S" : "N");
                json.WriteEndArray();
            }
            json.WriteEndArray();

            json.WriteEndObject();
            json.WriteEndObject();
        }

        return Encoding.UTF8.GetString(memoria.ToArray());
    }
}
