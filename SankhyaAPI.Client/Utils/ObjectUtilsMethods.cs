using System.Globalization;
using System.Reflection;
using System.Xml.Linq;
using System.Xml.Serialization;
using SankhyaAPI.Client.Extensions;
using SankhyaAPI.Client.Interfaces;
using SankhyaAPI.Client.MetaData;

namespace SankhyaAPI.Client.Utils;

internal static class ObjectUtilsMethods
{
    #region PublicMethods

    public static string GetFormattedString(object value) =>
        value switch
        {
            string s      => s.Trim(),
            float f       => f.ToString("G", CultureInfo.InvariantCulture),
            decimal d     => d.ToString("G", CultureInfo.InvariantCulture),
            double db     => db.ToString("G", CultureInfo.InvariantCulture),
            Enum e        => e.GetXmlEnumValue(),
            DateTime dt   => dt.ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture),
            DateOnly dt   => dt.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
            TimeOnly time => time.ToString("HHmm", CultureInfo.InvariantCulture),
            bool b        => b ? "S" : "N",
            _             => Convert.ToString(value, CultureInfo.InvariantCulture)!
        };

    public static string? GetFormattedString<T>(this T obj, PropertyInfo property)
    {
        object? value = property.GetValue(obj);

        if (value == null)
            return null;

        bool isNullableState = property.PropertyType.IsGenericType
                            && property.PropertyType.GetGenericTypeDefinition() == typeof(NullableState<>);

        if (isNullableState)
        {
            PropertyInfo? innerValueProp = value.GetType().GetProperty("Value");
            if (innerValueProp != null)
            {
                value = innerValueProp.GetValue(value);
            }
        }

        if (value == null)
            return null;

        Type propertyType = GetPropertyValueType(property.PropertyType);

        return propertyType switch
        {
            // Caso seja string
            _ when propertyType == typeof(string) => (value as string)?.Trim(),

            // Caso seja float
            _ when propertyType == typeof(float) => ((float)value).ToString("G", CultureInfo.InvariantCulture),

            // Caso seja decimal
            _ when propertyType == typeof(decimal) => ((decimal)value).ToString("G", CultureInfo.InvariantCulture),

            // Caso seja double
            _ when propertyType == typeof(double) => ((double)value).ToString("G", CultureInfo.InvariantCulture),

            // Caso seja enum
            _ when propertyType.IsEnum => ((Enum)value).GetXmlEnumValue(),

            // Caso seja DateTime
            _ when propertyType == typeof(DateTime) => ((DateTime)value).ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture),

            // Caso seja DateOnly
            _ when propertyType == typeof(DateOnly) => ((DateOnly)value).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),

            // Caso seja TimeOnly
            _ when propertyType == typeof(TimeOnly) => ((TimeOnly)value).ToString("HHmm", CultureInfo.InvariantCulture),

            // Caso seja bool
            _ when propertyType == typeof(bool) => (bool)value ? "S" : "N",

            // Caso nenhum dos tipos acima seja encontrado
            _ => Convert.ToString(value, CultureInfo.InvariantCulture)
        };
    }


    public static object? ConvertForPropertyType(string xmlElementValue, PropertyInfo property)
    {
        Type propertyType = GetPropertyValueType(property.PropertyType);

        try
        {

            if (string.IsNullOrWhiteSpace(xmlElementValue) && propertyType != typeof(bool)) return null;

            object convertedValue = propertyType switch
            {
                // Conversão para TimeOnly
                not null when propertyType == typeof(TimeOnly) => ParseTimeOnly(xmlElementValue),

                // Conversão para DateOnly
                not null when propertyType == typeof(DateOnly) => ParseDateOnly(xmlElementValue),

                // Conversão para DateTime
                not null when propertyType == typeof(DateTime) => ParseDateTime(xmlElementValue),

                // Conversão para Enums
                { IsEnum: true } => GetEnumValueFromXml(propertyType, xmlElementValue),

                // Conversão para Boolean
                not null when propertyType == typeof(bool) => xmlElementValue switch
                {
                    "S" => true,
                    "N" => false,
                    ""  => false,
                    _   => throw new FormatException("Valor não é um boolean válido (esperado S ou N)")
                },

                // Conversão para String
                not null when propertyType == typeof(string) => !string.IsNullOrWhiteSpace(xmlElementValue)
                                                                    ? xmlElementValue.Trim()
                                                                    : xmlElementValue,

                // Conversão padrão para outros tipos
                _ => Convert.ChangeType(xmlElementValue, propertyType!, CultureInfo.InvariantCulture)
            };

            if (property.PropertyType is not { IsGenericType: true } genericType
             || genericType.GetGenericTypeDefinition() != typeof(NullableState<>)) return convertedValue;

            // Obtém o tipo genérico subjacente (T)
            Type innerType = property.PropertyType.GetGenericArguments()[0];

            // Cria a instância de NullableState<T> dinamicamente
            Type nullableStateType = typeof(NullableState<>).MakeGenericType(innerType);
            return Activator.CreateInstance(nullableStateType, convertedValue);
        } catch (Exception ex)
        {
            // O valor pode ser dado pessoal (CPF, nome, endereço) e mensagens de exceção vão para log.
            // Por isso ele não entra na mensagem nem na InnerException (cujas mensagens costumam repeti-lo):
            // fica só em Data, para quem quiser registrá-lo conscientemente.
            var erro = new FormatException(
                $"Não foi possível converter o valor recebido do Sankhya para a propriedade '{property.Name}' ({propertyType.Name}).");
            erro.Data["Valor"] = xmlElementValue;
            erro.Data["TipoDoErroOriginal"] = ex.GetType().FullName;
            throw erro;
        }
    }

    public static List<T> GetListOfObjectsFromDictionary<T>(Dictionary<string, List<dynamic>?> fields)
        where T : class, new()
    {
        if (fields.Values.Count == 0) return [];

        int maxCount = fields.Values.Max(list => list?.Count ?? 0);
        ModelMap map = ModelMap.For<T>();

        // Coluna de cada propriedade resolvida uma vez, não a cada linha.
        List<(PropertyMap Property, List<object> Values)> colunas = [];
        foreach (PropertyMap property in map.Mapped)
        {
            if (fields.TryGetValue(property.XmlName!, out List<object>? values) && values != null)
                colunas.Add((property, values));
        }

        var objs = new List<T>(maxCount);

        for (int i = 0; i < maxCount; i++)
        {
            var objectDest = new T();

            foreach ((PropertyMap property, List<object> values) in colunas)
            {
                if (values.Count <= i) continue;

                object? value = values[i];
                object? convertedValue = ConvertForPropertyType(Convert.ToString(value, CultureInfo.InvariantCulture) ?? "", property.Property);

                property.SetValue(objectDest, convertedValue);
            }

            objs.Add(objectDest);
        }

        return objs;
    }

    public static string GetXmlEnumValue(this Enum enumValue)
    {
        Type type = enumValue.GetType();
        string value = enumValue.ToString();
        FieldInfo? field = type.GetField(value);

        if (field == null) throw new Exception($"Nenhum enumerador encontrado para a valor passado: {value}");

        if (Attribute.GetCustomAttribute(field, typeof(XmlEnumAttribute)) is not XmlEnumAttribute { Name: not null } attribute)
        {
            throw new ArgumentException("Enumerador não pode conter propriedades sem o atributo XmlEnum implementado", field.Name);
        }

        return attribute.Name;
    }

    public static string? GetXmlElementName(this PropertyInfo prop)
    {
        var xmlElementAttr = prop.GetCustomAttribute<XmlElementAttribute>();
        KeyAttribute? primaryKeyAttr = prop.GetPrimaryKeyAttribute();
        return xmlElementAttr?.ElementName ?? primaryKeyAttr?.ElementName;
    }

    /// <summary>
    /// Valida as chaves antes de chamar o servidor.
    /// <list type="bullet">
    /// <item><description>Atualização e exclusão: toda chave precisa estar <c>Set</c>.</description></item>
    /// <item><description>Inserção: chave auto-enumerada não pode estar preenchida; chave sem auto numeração precisa estar <c>Set</c>.</description></item>
    /// </list>
    /// </summary>
    public static void ValidarCamposChave<T>(List<T> objs, bool isUpdate = false)
        where T : class, new()
    {
        IReadOnlyList<PropertyMap> keys = ModelMap.For<T>().Keys;
        if (keys.Count == 0) return;

        foreach (T item in objs)
        {
            foreach (PropertyMap key in keys)
            {
                bool isSet = key.GetState(item) == EPropertyState.Set;
                bool autoEnumerable = key.Key!.AutoEnumerable;

                if (isUpdate && !isSet)
                    throw new ArgumentNullException(
                        key.Name,
                        "Os campos que fazem parte da chave primária não podem estar vazios em operações de atualização.");

                if (!isUpdate && autoEnumerable && isSet)
                    throw new ArgumentException(
                        "Os campos que fazem parte da chave primária com atributos auto enumerados não podem estar preenchidos em operações de inserção.",
                        key.Name);

                if (!isUpdate && !autoEnumerable && !isSet)
                    throw new ArgumentException(
                        "Os campos que fazem parte da chave primária sem auto numeração precisam estar preenchidos em operações de inserção.",
                        key.Name);
            }
        }
    }

    public static T GetEnumValueFromXml<T>(string xmlValue)
        where T : Enum
    {
        Type enumType = typeof(T);
        FieldInfo[] fields = enumType.GetFields();
        FieldInfo? matchField = fields.FirstOrDefault(f => f.GetCustomAttribute<XmlEnumAttribute>()?.Name == xmlValue);

        return matchField != null
                   ? (T)Enum.Parse(enumType, matchField.Name)
                   : throw new ArgumentException($"Valor '{xmlValue}' não é válido para o enum '{enumType.Name}'");
    }

    public static object GetEnumValueFromXml(Type enumType, string xmlValue)
    {
        FieldInfo[] fields = enumType.GetFields();
        FieldInfo? matchField = fields.FirstOrDefault(f => f.GetCustomAttribute<XmlEnumAttribute>()?.Name == xmlValue);

        return matchField != null
                   ? Enum.Parse(enumType, matchField.Name)
                   : throw new ArgumentException($"Valor '{xmlValue}' não é válido para o enum '{enumType.Name}'");
    }

    //public static PropertyState GetPropertyState<T>(PropertyInfo propertyInfo)
    //{
    //    return nullableState.State;
    //}

    public static Type GetPropertyValueType(this Type propertyType)
    {
        Type? type = NullableState.GetUnderlyingType(propertyType);

        type ??= Nullable.GetUnderlyingType(propertyType);

        type ??= propertyType;

        return type;
    }

    #endregion

    public static void AddKeysAsXml<T>(this XElement element, T obj)
    {
        if (obj == null) throw new ArgumentNullException(nameof(obj), "A entidade deve ser definida");

        foreach (PropertyMap key in ModelMap.For(obj.GetType()).Keys)
        {
            if (key.GetValue(obj) == null)
                throw new ArgumentNullException(key.Name, $"Chave primária {key.Key!.ElementName} não pode ser nula");

            element.Add(new XElement(key.Key!.ElementName, obj.GetFormattedString(key.Property)));
        }
    }

    public static XElement AddEntityForFields<T>(this T obj, bool isUpdate = false)
        where T : class
    {
        var localFields = new XElement("localFields");

        foreach (PropertyMap property in ModelMap.For(obj.GetType()).Mapped)
        {
            if (property.Key != null && (isUpdate || property.Key.AutoEnumerable)) continue;

            string xmlElementName = property.XmlName!;

            EPropertyState? state = property.IsNullableState && property.GetValue(obj) is INullableState nullableState
                                        ? nullableState.State
                                        : null;
            bool isClear = state == EPropertyState.Clear;
            bool isUnSet = state == EPropertyState.UnSet;

            switch (isUpdate)
            {
                case false when state is not EPropertyState.Set:
                case true when isUnSet:
                    continue;
            }

            string? convertedValue = obj.GetFormattedString(property.Property);

            if (convertedValue != null || (isUpdate && isClear))
            {
                localFields.Add(
                    isClear
                        ? new XElement(xmlElementName) // UPDATE com Clear
                        : new XElement(xmlElementName, convertedValue));
            }
        }

        return localFields;
    }

    #region PrivateMethods

    private static readonly string[] DateTimeFormats =
    [
        "dd/MM/yyyy HH:mm:ss", "ddMMyyyy HH:mm:ss", "dd/MM/yyyy", "ddMMyyyy"
    ];

    private static readonly string[] DateFormats = ["dd/MM/yyyy", "ddMMyyyy"];

    // Os formatos do Sankhya são explícitos (dd/MM/yyyy); só o que não bate cai no parse ISO invariante.
    // Nenhum parse depende da cultura da máquina.
    private static DateTime ParseDateTime(string value) =>
        DateTime.TryParseExact(value, DateTimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dateTime)
            ? dateTime
            : DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out dateTime)
                ? dateTime
                : throw new FormatException("Valor não é uma data válida");

    private static DateOnly ParseDateOnly(string value) =>
        DateOnly.TryParseExact(value, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly date)
            ? date
            : DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out date)
                ? date
                : throw new FormatException("Valor não é uma data válida");

    private static TimeOnly ParseTimeOnly(string value) =>
        TimeOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out TimeOnly time)
            ? time
            : TimeOnly.TryParseExact(value.PadLeft(4, '0'), "HHmm", CultureInfo.InvariantCulture, DateTimeStyles.None, out time)
                ? time
                : throw new FormatException("Valor não é uma hora válida");


    private static KeyAttribute? GetPrimaryKeyAttribute(this PropertyInfo prop) => prop.GetCustomAttribute<KeyAttribute>();

    #endregion
}