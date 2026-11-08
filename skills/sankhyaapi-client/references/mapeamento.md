# Mapeamento de entidades

Dois caminhos produzem o mesmo mapa. Prefira o **mapa fluente**: o modelo de domínio fica limpo, há campos calculados e o caminho é compatível com AOT.

## Mapa fluente (recomendado)

```csharp
using SankhyaAPI.Client.Mapping;

public class OrdemServico          // domínio: sem atributos, sem tipos do pacote
{
    public int Numero { get; set; }
    public string? Descricao { get; set; }
    public DateTime? DataExecucao { get; set; }
    public StatusOs Status { get; set; }
    public string? NomeParceiro { get; set; }
}

public sealed class OrdemServicoMap : SankhyaEntityMap<OrdemServico>
{
    public OrdemServicoMap()
    {
        Entity("OrdemServico");                                   // NOMEINSTANCIA (TDDINS)
        Key(x => x.Numero, "NUMOS").AutoNumbered();               // servidor gera no insert
        Property(x => x.Descricao, "DESCRICAO");
        Computed(x => x.DataExecucao)                             // 1 campo -> 2 colunas
            .Writes("DT_FINAL", SankhyaType.Date)
            .Writes("HR_FINAL", SankhyaType.Time)                 // hora HHMM
            .Reads<DateOnly?, TimeOnly?>("DT_FINAL", "HR_FINAL", (d, h) => d?.ToDateTime(h ?? TimeOnly.MinValue));
        Property(x => x.Status, "STATUS").Options(("A", StatusOs.Aberta), ("F", StatusOs.Fechada));
        Joined(x => x.NomeParceiro, "Parceiro", "NOMEPARC");      // campo de entidade ligada (só leitura)
    }
}
```

Registre com `AddEntity<OrdemServico, OrdemServicoMap>()` (DI) ou `SankhyaMaps.Register<OrdemServicoMap>()`.

| Método | Uso |
|---|---|
| `Entity("Nome")` | nome da instância no Sankhya (obrigatório) |
| `Key(x => x.P, "COL")` | campo de chave. Chave composta: chame uma vez por campo. `.AutoNumbered()` em **no máximo um** (SNK001). |
| `Property(x => x.P, "COL", SankhyaType.X?)` | lido e escrito. `.Options(...)` para lista de opções sem `[XmlEnum]`; `.ReadOnly()` para nunca gravar. |
| `Property(x => x.P, "COL", converter)` | tipo próprio via `ISankhyaConverter<T>` |
| `Computed(x => x.P)` | `.Writes(col, SankhyaType)` uma ou mais vezes, `.Writes(col, v => projecao)`, `.Reads<T1>(col, f)` / `.Reads<T1,T2>(c1, c2, f)` / `.ReadsRaw(cols, f)`. Sem `Reads`, é só de escrita (SNK004 avisa). |
| `Joined(x => x.P, "Ligacao", "CAMPO")` | pede `<entity path="Ligacao">` e lê `Ligacao_CAMPO`. O caminho é o nome da instância ligada (`TDDLIG`). |
| `Presentation(x => x.P, "Ligacao_CAMPO")` | lê os campos de apresentação que o Sankhya acrescenta |

`SankhyaType`: `Auto` (padrão, inferido), `Text`, `Integer`, `Decimal`, `Date`, `DateTime`, `Time` (HHMM), `YesNo` (S/N), `Options`. Tipos inferidos: string, números, `DateOnly`, `DateTime`, `TimeOnly`, `bool`, enum (código por `[XmlEnum("X")]` ou `.Options`). As conversões não dependem da cultura da máquina.

**Chave em tipo de valor não anulável**: a chave auto-numerada com valor `default` (0) conta como não informada. Campos não chave de tipo de valor vão sempre no insert; para não enviar, use tipo anulável (`int?`).

## Mapa por atributos (compatível com a 5.x)

```csharp
[SankhyaEntity("Produto")]
public class Produto
{
    [Key("CODPROD", autoEnumerable: true)] public NullableState<long> CodProd { get; set; }
    [XmlElement("DESCRPROD")] public NullableState<string> DescrProd { get; set; }
    [SankhyaPresentation("GrupoProduto_DESCRGRUPOPROD")] public NullableState<string> Grupo { get; set; }
}
```

Use `NullableState<T>` quando quiser update parcial pelo estado do campo ([`operacoes.md`](operacoes.md)). Propriedades sem atributo não vão no envelope.

## Diagnósticos de compilação

| Código | Significado | Correção |
|---|---|---|
| SNK001 | mais de um auto-numerado na chave | deixe só o campo que o servidor gera |
| SNK002 | coluna repetida | um campo por coluna |
| SNK003 | tipo sem conversão | informe `SankhyaType` ou um `ISankhyaConverter<T>` |
| SNK004 | `Computed` sem `Reads` | adicione `Reads(...)` ou aceite que é só de escrita |
| SNK005 | SQL concatenado | use string interpolada direto na chamada |
| SNK006 | `null` em `NullableState<T>` | use `NullableState<T>.Clear()` |
| SNK010 | modelo inacessível ao gerador | torne o modelo `public`/`internal`, não genérico, com construtor sem parâmetros (necessário para AOT) |

## Conferir um mapa sem servidor

```csharp
var volta = SankhyaMapAssert.RoundTrip(new OrdemServico { Descricao = "Ação", DataExecucao = new(2026, 11, 7, 14, 30, 0) });
Assert.Equal(new DateTime(2026, 11, 7, 14, 30, 0), volta.DataExecucao);
```
