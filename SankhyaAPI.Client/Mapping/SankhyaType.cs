using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Xml.Serialization;

namespace SankhyaAPI.Client.Mapping;

/// <summary>
/// Formato de um campo no Sankhya, como ele vai no XML e volta do servidor.
/// </summary>
public enum SankhyaType
{
    /// <summary>Inferido pelo tipo da propriedade.</summary>
    Auto,

    /// <summary>Texto (<c>S</c>), sem espaços nas pontas.</summary>
    Text,

    /// <summary>Inteiro (<c>I</c>).</summary>
    Integer,

    /// <summary>Decimal (<c>F</c>), com ponto.</summary>
    Decimal,

    /// <summary>Data sem hora (<c>D</c>), <c>dd/MM/yyyy</c>. Aceita <see cref="DateOnly"/> e <see cref="DateTime"/> (usa só a data).</summary>
    Date,

    /// <summary>Data e hora (<c>H</c>), <c>dd/MM/yyyy HH:mm:ss</c>.</summary>
    DateTime,

    /// <summary>Hora (<c>T</c>), número HHMM. Aceita <see cref="TimeOnly"/> e <see cref="System.DateTime"/> (usa só a hora).</summary>
    Time,

    /// <summary>Sim/não: <c>S</c> ou <c>N</c>.</summary>
    YesNo,

    /// <summary>Lista de opções: o código da opção (<c>[XmlEnum]</c> no enum ou <c>.Options(...)</c> no mapa).</summary>
    Options
}

/// <summary>
/// Conversor próprio de um tipo do domínio para o texto do Sankhya, para tipos que <see cref="SankhyaType"/> não cobre.
/// </summary>
public interface ISankhyaConverter<T>
{
    /// <summary>Texto enviado ao Sankhya; <c>null</c> para não enviar.</summary>
    string? Format(T value);

    /// <summary>Valor a partir do texto devolvido pelo Sankhya (nunca vazio: vazio vira <c>default</c> antes).</summary>
    T Parse(string text);
}

/// <summary>
/// Conversão entre valores .NET e o texto do Sankhya. Nenhuma conversão depende da cultura da máquina, e
/// erros de conversão nunca trazem o valor na mensagem (ele fica em <c>Exception.Data["Valor"]</c>).
/// </summary>
internal static class SankhyaFormat
{
    private static readonly string[] DateTimeFormats = ["dd/MM/yyyy HH:mm:ss", "ddMMyyyy HH:mm:ss", "dd/MM/yyyy", "ddMMyyyy"];
    private static readonly string[] DateFormats = ["dd/MM/yyyy", "ddMMyyyy", "dd/MM/yyyy HH:mm:ss", "ddMMyyyy HH:mm:ss"];

    private static readonly ConcurrentDictionary<Type, (Dictionary<object, string> ToCode, Dictionary<string, object> FromCode)> EnumCodes = new();

    /// <summary>
    /// Tipo efetivo do valor, sem <see cref="Nullable{T}"/> nem <see cref="NullableState{T}"/>.
    /// </summary>
    public static Type Underlying(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(NullableState<>))
            type = type.GetGenericArguments()[0];
        return Nullable.GetUnderlyingType(type) ?? type;
    }

    public static SankhyaType Infer(Type type)
    {
        type = Underlying(type);
        if (type == typeof(string) || type == typeof(char) || type == typeof(Guid)) return SankhyaType.Text;
        if (type == typeof(bool)) return SankhyaType.YesNo;
        if (type.IsEnum) return SankhyaType.Options;
        if (type == typeof(DateOnly)) return SankhyaType.Date;
        if (type == typeof(System.DateTime) || type == typeof(DateTimeOffset)) return SankhyaType.DateTime;
        if (type == typeof(TimeOnly)) return SankhyaType.Time;
        if (type == typeof(decimal) || type == typeof(double) || type == typeof(float)) return SankhyaType.Decimal;
        if (type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(byte)
         || type == typeof(uint) || type == typeof(ulong) || type == typeof(ushort) || type == typeof(sbyte))
            return SankhyaType.Integer;
        throw new InvalidOperationException($"Tipo sem conversão conhecida para o Sankhya: {type.Name}. Informe um SankhyaType ou um ISankhyaConverter.");
    }

    /// <summary>
    /// Texto a enviar para o valor; <c>null</c> quando o valor é nulo.
    /// </summary>
    public static string? Format(object? value, SankhyaType type, IReadOnlyDictionary<object, string>? options = null)
    {
        if (value == null) return null;
        if (type == SankhyaType.Auto) type = Infer(value.GetType());

        return type switch
        {
            SankhyaType.Text => value is string s ? s.Trim() : Convert.ToString(value, CultureInfo.InvariantCulture),
            SankhyaType.Integer => Convert.ToString(value, CultureInfo.InvariantCulture),
            SankhyaType.Decimal => value switch
            {
                double d => d.ToString("R", CultureInfo.InvariantCulture),
                float f => f.ToString("R", CultureInfo.InvariantCulture),
                _ => Convert.ToDecimal(value, CultureInfo.InvariantCulture).ToString("G", CultureInfo.InvariantCulture)
            },
            SankhyaType.Date => value switch
            {
                DateOnly d => d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
                System.DateTime dt => dt.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
                DateTimeOffset dto => dto.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
                _ => throw Unsupported(value, type)
            },
            SankhyaType.DateTime => value switch
            {
                System.DateTime dt => dt.ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture),
                DateTimeOffset dto => dto.ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture),
                DateOnly d => d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) + " 00:00:00",
                _ => throw Unsupported(value, type)
            },
            SankhyaType.Time => value switch
            {
                TimeOnly t => t.ToString("HHmm", CultureInfo.InvariantCulture),
                System.DateTime dt => dt.ToString("HHmm", CultureInfo.InvariantCulture),
                DateTimeOffset dto => dto.ToString("HHmm", CultureInfo.InvariantCulture),
                TimeSpan ts => (ts.Hours * 100 + ts.Minutes).ToString("D4", CultureInfo.InvariantCulture),
                _ => throw Unsupported(value, type)
            },
            SankhyaType.YesNo => value is bool b ? b ? "S" : "N" : throw Unsupported(value, type),
            SankhyaType.Options => options != null && options.TryGetValue(value, out string? code)
                                       ? code
                                       : value is Enum e ? EnumCode(e) : Convert.ToString(value, CultureInfo.InvariantCulture),
            _ => throw Unsupported(value, type)
        };
    }

    /// <summary>
    /// Valor .NET do tipo <paramref name="target"/> a partir do texto do Sankhya. Texto vazio vira <c>null</c>
    /// (exceto sim/não, que vira <c>false</c>).
    /// </summary>
    /// <exception cref="FormatException">Sem o valor na mensagem; o valor fica em <c>Data["Valor"]</c>.</exception>
    public static object? Parse(string? text, Type target, SankhyaType type, IReadOnlyDictionary<string, object>? options, string memberName)
    {
        Type underlying = Underlying(target);
        if (type == SankhyaType.Auto) type = Infer(underlying);

        try
        {
            if (string.IsNullOrWhiteSpace(text))
                return type == SankhyaType.YesNo && underlying == typeof(bool) && text != null ? false : null;

            text = text.Trim();
            return type switch
            {
                SankhyaType.Text => underlying == typeof(string) ? text : Convert.ChangeType(text, underlying, CultureInfo.InvariantCulture),
                SankhyaType.Integer or SankhyaType.Decimal => Convert.ChangeType(text, underlying, CultureInfo.InvariantCulture),
                SankhyaType.Date => ParseDate(text, underlying),
                SankhyaType.DateTime => ParseDateTime(text, underlying),
                SankhyaType.Time => ParseTime(text, underlying),
                SankhyaType.YesNo => text switch
                {
                    "S" => true,
                    "N" => false,
                    _ => throw new FormatException("Valor não é S nem N.")
                },
                SankhyaType.Options => options != null && options.TryGetValue(text, out object? option)
                                           ? option
                                           : underlying.IsEnum ? EnumFromCode(underlying, text) : Convert.ChangeType(text, underlying, CultureInfo.InvariantCulture),
                _ => throw new FormatException("Tipo sem conversão.")
            };
        } catch (Exception ex)
        {
            // O valor pode ser dado pessoal e mensagens de exceção vão para log: ele só fica em Data.
            var erro = new FormatException(
                $"Não foi possível converter o valor recebido do Sankhya para '{memberName}' ({underlying.Name}).");
            erro.Data["Valor"] = text;
            erro.Data["TipoDoErroOriginal"] = ex.GetType().FullName;
            throw erro;
        }
    }

    public static string EnumCode(Enum value)
    {
        (Dictionary<object, string> toCode, _) = Codes(value.GetType());
        return toCode.TryGetValue(value, out string? code)
                   ? code
                   : throw new InvalidOperationException($"O membro {value.GetType().Name}.{value} não tem [XmlEnum] com o código do Sankhya.");
    }

    private static object EnumFromCode(Type enumType, string code)
    {
        (_, Dictionary<string, object> fromCode) = Codes(enumType);
        return fromCode.TryGetValue(code, out object? value) ? value : throw new FormatException("Código sem opção correspondente no enum.");
    }

    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2070",
        Justification = "O trimmer preserva os campos de enums usados; os códigos vêm de [XmlEnum] nesses campos.")]
    private static (Dictionary<object, string>, Dictionary<string, object>) Codes(Type enumType) =>
        EnumCodes.GetOrAdd(enumType, t =>
        {
            var toCode = new Dictionary<object, string>();
            var fromCode = new Dictionary<string, object>();
            foreach (FieldInfo field in t.GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                object value = field.GetValue(null)!;
                string code = field.GetCustomAttribute<XmlEnumAttribute>()?.Name ?? field.Name;
                toCode[value] = code;
                fromCode[code] = value;
            }

            return (toCode, fromCode);
        });

    private static object ParseDate(string text, Type target)
    {
        System.DateTime value = System.DateTime.TryParseExact(text, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out System.DateTime d)
                                    ? d
                                    : System.DateTime.Parse(text, CultureInfo.InvariantCulture);
        if (target == typeof(DateOnly)) return DateOnly.FromDateTime(value);
        return value.Date;
    }

    private static object ParseDateTime(string text, Type target)
    {
        System.DateTime value = System.DateTime.TryParseExact(text, DateTimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out System.DateTime d)
                                    ? d
                                    : System.DateTime.Parse(text, CultureInfo.InvariantCulture);
        if (target == typeof(DateOnly)) return DateOnly.FromDateTime(value);
        if (target == typeof(DateTimeOffset)) return new DateTimeOffset(value);
        return value;
    }

    private static object ParseTime(string text, Type target)
    {
        TimeOnly time = TimeOnly.TryParseExact(text.PadLeft(4, '0'), "HHmm", CultureInfo.InvariantCulture, DateTimeStyles.None, out TimeOnly t)
                            ? t
                            : TimeOnly.Parse(text, CultureInfo.InvariantCulture);
        if (target == typeof(TimeSpan)) return time.ToTimeSpan();
        if (target == typeof(System.DateTime)) return System.DateTime.MinValue.Add(time.ToTimeSpan());
        return time;
    }

    private static InvalidOperationException Unsupported(object value, SankhyaType type) =>
        new($"O tipo {value.GetType().Name} não pode ser enviado como {type}.");
}
