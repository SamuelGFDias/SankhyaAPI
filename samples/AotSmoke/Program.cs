using System.Xml.Serialization;
using SankhyaAPI.Client;
using SankhyaAPI.Client.Configuration;
using SankhyaAPI.Client.Mapping;
using SankhyaAPI.Client.Sql;

// Exercita, num binário NativeAOT, o que depende de reflexão fora do AOT: mapa fluente (acessores gerados),
// mapa por atributos, conversões, SankhyaSql e a criação do cliente.
SankhyaMaps.Register<PedidoMap>();
SankhyaMaps.Validate<Pedido>();
SankhyaMaps.Validate<Usuario>();

Check(SankhyaAccessorRegistry.IsGenerated(typeof(Pedido)), "acessores gerados para Pedido");
Check(SankhyaAccessorRegistry.IsGenerated(typeof(Usuario)), "acessores gerados para Usuario");

string nome = "O'Brien";
SankhyaSql sql = $"SELECT * FROM TGFPAR WHERE NOMEPARC = {nome} AND DTCAD >= {new DateOnly(2026, 1, 2)} AND STATUS = {Status.Fechado}";
Check(sql.ToString() == "SELECT * FROM TGFPAR WHERE NOMEPARC = 'O''Brien' AND DTCAD >= CONVERT(DATE, '2026-01-02', 23) AND STATUS = 'F'", "SankhyaSql");

await using SankhyaClient client = SankhyaClient.Create(new SankhyaClientOptions
{
    BaseUrl = "https://erp.exemplo.com.br/mge", Usuario = "u", Senha = "s"
});
Check(client.Repository<Pedido>().EntityName == "Pedido", "repositório pelo mapa fluente");
Check(client.Repository<Usuario>().EntityName == "Usuario", "repositório pelo mapa de atributos");

Console.WriteLine("AOT OK");
return 0;

static void Check(bool ok, string what)
{
    if (ok) return;
    Console.Error.WriteLine($"FALHOU: {what}");
    Environment.Exit(1);
}

public enum Status
{
    [XmlEnum("A")] Aberto,
    [XmlEnum("F")] Fechado
}

public class Pedido
{
    public int Numero { get; set; }
    public string? Obs { get; set; }
    public DateTime? Quando { get; set; }
    public Status Status { get; set; }
}

public sealed class PedidoMap : SankhyaEntityMap<Pedido>
{
    public PedidoMap()
    {
        Entity("Pedido");
        Key(x => x.Numero, "NUMERO").AutoNumbered();
        Property(x => x.Obs, "OBS");
        Computed(x => x.Quando)
            .Writes("DT", SankhyaType.Date)
            .Writes("HR", SankhyaType.Time)
            .Reads<DateOnly?, TimeOnly?>("DT", "HR", (d, h) => d?.ToDateTime(h ?? TimeOnly.MinValue));
        Property(x => x.Status, "STATUS");
    }
}

[SankhyaEntity("Usuario")]
public class Usuario
{
    [Key("CODUSU")] public NullableState<int> CodUsu { get; set; }
    [XmlElement("NOMEUSU")] public NullableState<string> NomeUsu { get; set; }
}
