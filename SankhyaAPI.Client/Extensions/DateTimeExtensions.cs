using System.Globalization;
using System.Text.RegularExpressions;

namespace SankhyaAPI.Client.Extensions;

public static class DateTimeExtensions
{
    public static DateTime? ParseDateFromString(this string dateString, string format = "ddMMyyyy HH:mm:ss")
    {
        if (string.IsNullOrEmpty(dateString)) return null;
        DateTime.TryParseExact(dateString, format, CultureInfo.InvariantCulture, DateTimeStyles.None,
            out var parsedDate);
        return parsedDate;
    }
    private static bool VerificarData(string dataString, string format)
    {
        Regex regex = new Regex(format);

        return regex.IsMatch(dataString);
    }
}