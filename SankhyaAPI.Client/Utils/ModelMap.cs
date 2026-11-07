using System.Collections.Concurrent;
using System.Reflection;
using System.Xml.Serialization;
using SankhyaAPI.Client.Extensions;
using SankhyaAPI.Client.Interfaces;
using SankhyaAPI.Client.MetaData;

namespace SankhyaAPI.Client.Utils;

/// <summary>
/// Propriedade de um modelo com o que o pacote precisa saber dela, lido por reflexão uma vez só.
/// </summary>
internal sealed class PropertyMap
{
    public PropertyMap(PropertyInfo property)
    {
        Property = property;
        Key = property.GetCustomAttribute<KeyAttribute>();
        XmlName = property.GetCustomAttribute<XmlElementAttribute>()?.ElementName ?? Key?.ElementName;
        IsNullableState = property.PropertyType is { IsGenericType: true } type
                       && type.GetGenericTypeDefinition() == typeof(NullableState<>);
        ValueType = property.PropertyType.GetPropertyValueType();
    }

    public PropertyInfo Property { get; }
    public string Name => Property.Name;

    /// <summary>
    /// Nome do campo no Sankhya (<c>[XmlElement]</c> ou <c>[Key]</c>); nulo quando a propriedade não é mapeada.
    /// </summary>
    public string? XmlName { get; }

    public KeyAttribute? Key { get; }
    public bool IsNullableState { get; }

    /// <summary>
    /// Tipo do valor, sem <see cref="NullableState{T}"/> nem <see cref="Nullable{T}"/>.
    /// </summary>
    public Type ValueType { get; }

    public object? GetValue(object obj) => Property.GetValue(obj);

    public void SetValue(object obj, object? value) => Property.SetValue(obj, value);

    /// <summary>
    /// Estado do valor: o de <see cref="NullableState{T}"/>, ou Set/UnSet conforme o valor seja nulo.
    /// </summary>
    public EPropertyState GetState(object obj) =>
        GetValue(obj) switch
        {
            INullableState state => state.State,
            null                 => EPropertyState.UnSet,
            _                    => EPropertyState.Set
        };
}

/// <summary>
/// Mapa de um modelo (propriedades, campos e chaves), montado por reflexão e guardado em cache por tipo.
/// </summary>
/// <remarks>
/// É o contrato que o source generator da 6.0 vai implementar sem reflexão. Fica interno até lá, para não
/// congelar a API antes do generator existir.
/// </remarks>
internal sealed class ModelMap
{
    private static readonly ConcurrentDictionary<Type, ModelMap> Cache = new();

    private readonly Dictionary<string, PropertyMap> _byXmlName;

    private ModelMap(Type type)
    {
        Type = type;
        Properties = type.GetProperties().Select(p => new PropertyMap(p)).ToList();
        Mapped = Properties.Where(p => p.XmlName != null).ToList();
        Keys = Properties.Where(p => p.Key != null).ToList();

        _byXmlName = new Dictionary<string, PropertyMap>(StringComparer.OrdinalIgnoreCase);
        foreach (PropertyMap property in Mapped)
            _byXmlName.TryAdd(property.XmlName!, property);
    }

    public Type Type { get; }

    /// <summary>
    /// Todas as propriedades públicas, na ordem de <see cref="Type.GetProperties()"/>.
    /// </summary>
    public IReadOnlyList<PropertyMap> Properties { get; }

    /// <summary>
    /// Propriedades com nome de campo no Sankhya, na ordem em que são pedidas no envelope.
    /// </summary>
    public IReadOnlyList<PropertyMap> Mapped { get; }

    public IReadOnlyList<PropertyMap> Keys { get; }

    public PropertyMap? ByXmlName(string xmlName) => _byXmlName.GetValueOrDefault(xmlName);

    public static ModelMap For(Type type) => Cache.GetOrAdd(type, t => new ModelMap(t));

    public static ModelMap For<T>() => For(typeof(T));
}
