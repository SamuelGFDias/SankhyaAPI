using System.Text;

namespace SankhyaAPI.Client.Utils;

internal static class SankhyaText
{
    private static readonly Encoding Latin1 = Encoding.GetEncoding("ISO-8859-1");

    /// <summary>
    /// O Sankhya manda mensagens (statusMessage) e o idusu em base64 sobre ISO-8859-1, às vezes dentro de CDATA.
    /// O que não for base64 válido volta como veio.
    /// </summary>
    public static string? DecodeBase64(string? value)
    {
        string? text = value?.Replace("<![CDATA[", "").Replace("]]>", "");
        if (text == null) return null;

        try
        {
            return Latin1.GetString(Convert.FromBase64String(text));
        } catch (FormatException)
        {
            return text;
        }
    }
}
