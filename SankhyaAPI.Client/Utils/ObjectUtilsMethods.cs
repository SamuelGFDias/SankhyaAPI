using System.Globalization;
using System.Reflection;
using System.Xml.Linq;
using System.Xml.Serialization;
using SankhyaAPI.Client.Envelopes;
using SankhyaAPI.Client.Extensions;
using SankhyaAPI.Client.Interfaces;
using SankhyaAPI.Client.MetaData;

namespace SankhyaAPI.Client.Utils;

public static class ObjectUtilsMethods
{
    #region PublicMethods

    public static List<Field> GetFieldsFromObject(object obj)
    {
        PropertyInfo[] props = PropertiesFromObject(obj);

        return props.Select(t => new Field { Nome = t.GetXmlElementName() }).ToList();
    }

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
        try
        {
            Type? propertyType = GetPropertyValueType(property.PropertyType);

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
                    _   => throw new Exception($"Valor '{xmlElementValue}' não é um boolean válido")
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
            throw new Exception($"Erro ao converter o valor '{xmlElementValue}' para a propriedade '{property.Name}': {ex.Message}");
        }
    }

    public static List<T> GetListOfObjectsFromDictionary<T>(Dictionary<string, List<dynamic>?> fields)
        where T : class, new()
    {
        if (fields.Values.Count == 0) return [];

        int maxCount = fields.Values.Max(list => list?.Count ?? 0);

        var objs = new List<T>();

        for (int i = 0; i < maxCount; i++)
        {
            var objectDest = new T();
            PropertyInfo[] props = PropertiesFromObject(objectDest);

            foreach (PropertyInfo prop in props)
            {
                KeyValuePair<string, List<dynamic>?> field = fields.FirstOrDefault(a => a.Key == prop.GetXmlElementName());

                if (field.Value == null || field.Value.Count <= i) continue;

                dynamic? value = field.Value[i];

                // Não tirar? do convertedValue
                // ReSharper disable once ConditionalAccessQualifierIsNonNullableAccordingToAPIContract
                dynamic? convertedValue = ConvertForPropertyType(value?.ToString() ?? "", prop);

                prop.SetValue(objectDest, convertedValue);
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

    public static void ValidarCamposChave<T>(List<T> objs, bool isUpdate = false)
        where T : class, new()
    {
        List<PropertyInfo> autoEnumerableKeys = GetKeysPropertiesFromObject<T>(true);
        List<PropertyInfo> keys = GetKeysPropertiesFromObject<T>();


        if (!isUpdate ? autoEnumerableKeys is not { Count: > 0 } : keys is not { Count : > 0 }) return;

        foreach (T item in objs)
        {
            List<PropertyInfo> propertyKeys = item.GetType()
                                                  .GetProperties()
                                                  .Where(!isUpdate ? autoEnumerableKeys.Contains : keys.Contains)
                                                  .ToList();

            foreach (PropertyInfo property in propertyKeys)
            {
                object? rawValue = property.GetValue(item);

                bool isSet = rawValue is INullableState state ? state.State == EPropertyState.Set : rawValue != null;

                switch (isUpdate)
                {
                    case true when !isSet:
                        throw new ArgumentNullException(
                            property.Name,
                            "Os campos que fazem parte da chave primária não podem estar vazios em operações de atualização.");

                    case false when isSet:
                        throw new ArgumentException(
                            "Os campos que fazem parte da chave primária com atributos auto enumerados não podem estar preenchidos em operações de inserção.",
                            property.Name);
                }
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
        if (obj == null) throw new Exception("A entidade deve ser definida");

        foreach (PropertyInfo? prop in obj.GetType().GetProperties())
        {
            var keyAttribute = prop.GetCustomAttribute<KeyAttribute>();
            if (keyAttribute == null) continue;

            object? value = prop.GetValue(obj);

            if (value == null) throw new Exception($"Chave primária {keyAttribute.ElementName} não pode ser nula");

            string? convertedValue = obj.GetFormattedString(prop);

            element.Add(new XElement(keyAttribute.ElementName, convertedValue));
        }
    }

    public static XElement AddEntityForFields<T>(this T obj, bool isUpdate = false)
        where T : class
    {
        var localFields = new XElement("localFields");

        foreach (PropertyInfo? prop in obj.GetType().GetProperties())
        {
            var keyAttribute = prop.GetCustomAttribute<KeyAttribute>();
            if (keyAttribute != null)
            {
                if (isUpdate || keyAttribute.AutoEnumerable) continue;
            }

            string? xmlElementName = prop.GetXmlElementName();
            if (xmlElementName == null) continue;

            object? propertyValue = prop.GetValue(obj);

            bool isNullableState = prop.PropertyType.IsGenericType
                                && prop.PropertyType.GetGenericTypeDefinition() == typeof(NullableState<>);

            EPropertyState? state = null;
            bool isClear = false;
            bool isUnSet = false;

            if (isNullableState && propertyValue != null)
            {
                var stateProperty = propertyValue.GetType().GetProperty(nameof(NullableState<int>.State));
                if (stateProperty != null)
                {
                    object? stateValue = stateProperty.GetValue(propertyValue);
                    if (stateValue is EPropertyState stateEnum)
                    {
                        state = stateEnum;
                        isClear = stateEnum == EPropertyState.Clear;
                        isUnSet = stateEnum == EPropertyState.UnSet;
                    }
                }
            }

            switch (isUpdate)
            {
                case false when state is not EPropertyState.Set:
                case true when isUnSet:
                    continue;
            }

            string? convertedValue = obj.GetFormattedString(prop);

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
                : throw new Exception($"Valor '{value}' não é uma data válida");

    private static DateOnly ParseDateOnly(string value) =>
        DateOnly.TryParseExact(value, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly date)
            ? date
            : DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out date)
                ? date
                : throw new Exception($"Valor '{value}' não é uma data válida");

    private static TimeOnly ParseTimeOnly(string value) =>
        TimeOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out TimeOnly time)
            ? time
            : TimeOnly.TryParseExact(value.PadLeft(4, '0'), "HHmm", CultureInfo.InvariantCulture, DateTimeStyles.None, out time)
                ? time
                : throw new Exception($"Valor '{value}' não é uma hora válida");


    private static PropertyInfo[] PropertiesFromObject(object obj)
    {
        return obj.GetType().GetProperties();
    }


    private static List<PropertyInfo> GetKeysPropertiesFromObject<T>(bool isAutoEnumerable = false)
        where T : class, new()
    {
        var obj = new T();
        PropertyInfo[] properties = obj.GetType().GetProperties();

        if (isAutoEnumerable)
            return properties.Where(
                                  p => isAutoEnumerable
                                           ? p.GetPrimaryKeyAttribute() is { AutoEnumerable: true }
                                           : p.GetPrimaryKeyAttribute() != null)
                             .ToList();

        return properties.Where(p => p.GetPrimaryKeyAttribute() != null).ToList();
    }

    private static KeyAttribute? GetPrimaryKeyAttribute(this PropertyInfo prop) => prop.GetCustomAttribute<KeyAttribute>();

    #endregion
}