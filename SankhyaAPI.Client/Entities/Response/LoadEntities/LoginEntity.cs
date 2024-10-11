namespace SankhyaAPI.Client.Entities.Response.LoadEntities;

public class LoginEntity
{
    public string SessionId { get; init; } = string.Empty;
    public string JSessionId => $"JSESSIONID={SessionId}";
    private int CodUsu { get; set; }

    public string? IdUsu
    {
        set => CodUsu = Convert.ToInt32(value ?? "");
    }
}