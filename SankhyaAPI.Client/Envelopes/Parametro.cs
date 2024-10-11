using System.Xml.Serialization;

namespace SankhyaAPI.Client.Envelopes;


public class Parametro
{
    public Parametro()
    {
    }

    public Parametro(string? classe, string? nome, string? valor)
    {
        Classe = classe;
        Nome = nome;
        Valor = valor;
    }

    [XmlAttribute(AttributeName = "nome")]
    public string? Nome { get; set; }

    //[XmlAttribute(AttributeName = "descricao")]
    //public string? Descricao { get; set; }

    [XmlAttribute(AttributeName = "classe")]
    public string? Classe { get; set; }

    //[XmlAttribute(AttributeName = "instancia")]
    //public string? Istancia { get; set; }

    [XmlAttribute(AttributeName = "valor")]
    public string? Valor { get; set; }

    //[XmlAttribute(AttributeName = "pesquisa")]
    //public string? Pesquisa { get; set; }

    //[XmlAttribute(AttributeName = "requerido")]
    //public string? Requerido { get; set; }
}