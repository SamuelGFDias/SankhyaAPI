namespace SankhyaAPI.Client.Tests.Real;

/// <summary>
/// Configuração dos testes contra um Sankhya de verdade. Lê variáveis de ambiente e, na falta delas,
/// o arquivo de credenciais (padrão: ~/.config/sankhyaapi/teste.env, formato CHAVE=valor por linha).
/// Sem configuração completa, os testes reais ficam como "skipped".
/// </summary>
public static class SankhyaReal
{
    private static readonly Lazy<Dictionary<string, string>> Arquivo = new(LerArquivo);

    public static string? BaseUrl => Valor("SANKHYA_BASE_URL");
    public static string? Usuario => Valor("SANKHYA_USER");
    public static string? Senha => Valor("SANKHYA_PASSWORD");

    public static bool Configurado =>
        !string.IsNullOrWhiteSpace(BaseUrl) && !string.IsNullOrWhiteSpace(Usuario) && !string.IsNullOrWhiteSpace(Senha);

    public static string CaminhoArquivo =>
        Environment.GetEnvironmentVariable("SANKHYA_CREDENTIALS_FILE")
     ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "sankhyaapi", "teste.env");

    private static string? Valor(string chave) =>
        Environment.GetEnvironmentVariable(chave) is { Length: > 0 } valor
            ? valor
            : Arquivo.Value.GetValueOrDefault(chave);

    private static Dictionary<string, string> LerArquivo()
    {
        var valores = new Dictionary<string, string>();
        if (!File.Exists(CaminhoArquivo)) return valores;

        foreach (string linha in File.ReadAllLines(CaminhoArquivo))
        {
            int separador = linha.IndexOf('=');
            if (separador <= 0 || linha.TrimStart().StartsWith('#')) continue;
            valores[linha[..separador].Trim()] = linha[(separador + 1)..];
        }

        return valores;
    }
}

/// <summary>
/// Fact que só roda quando há um Sankhya real configurado.
/// </summary>
public sealed class RealFactAttribute : FactAttribute
{
    public RealFactAttribute()
    {
        if (!SankhyaReal.Configurado)
            Skip = $"Sankhya real não configurado (defina SANKHYA_BASE_URL/SANKHYA_USER/SANKHYA_PASSWORD ou o arquivo {SankhyaReal.CaminhoArquivo}).";
    }
}
