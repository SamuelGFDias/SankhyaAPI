using System.Xml.Serialization;

namespace SankhyaAPI.Client.Responses;

public class LoginEntity
{
    [XmlElement("jsessionid")] public string SessionId { get; init; } = string.Empty;
    public string JSessionId => $"JSESSIONID={SessionId}";
    private int CodUsu { get; set; }

    [XmlElement("idusu")]
    public string? IdUsu
    {
        set => CodUsu = Convert.ToInt32(value ?? "");
    }
}