namespace SankhyaAPI.Client.Providers;

public class SankhyaClientSettings(string username, string password, string baseUrl)
{
    public string Usuario => username;
    public string Senha => password;
    public string BaseUrl => baseUrl;
}