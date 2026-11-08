using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Xml.Serialization;

namespace SankhyaAPI.Client.Mapping;

/// <summary>
/// Registro dos mapas por tipo de modelo. Um tipo usa, nesta ordem: o mapa registrado (fluente ou gerado) ou o
/// mapa montado pelos atributos (<c>[SankhyaEntity]</c>, <c>[Key]</c>, <c>[XmlElement]</c>). Cada mapa é
/// compilado uma vez e reaproveitado.
/// </summary>
public static class SankhyaMaps
{
    private static readonly ConcurrentDictionary<Type, ISankhyaEntityMap> Registered = new();
    private static readonly ConcurrentDictionary<Type, EntityMap> Compiled = new();
    private static readonly ConcurrentDictionary<Type, EntityMap> Rows = new();

    /// <summary>Registra um mapa (substitui o anterior do mesmo modelo).</summary>
    public static void Register(ISankhyaEntityMap map)
    {
        Registered[map.ModelType] = map;
        Compiled.TryRemove(map.ModelType, out _);
    }

    /// <summary>Registra o mapa <typeparamref name="TMap"/>.</summary>
    public static void Register<TMap>() where TMap : ISankhyaEntityMap, new() => Register(new TMap());

    /// <summary>
    /// Valida o mapa do modelo agora (em vez de na primeira chamada): chave com mais de um campo auto-numerado,
    /// coluna repetida, tipo sem conversão.
    /// </summary>
    public static void Validate<T>() => _ = For(typeof(T));

    internal static EntityMap For(Type type) =>
        Compiled.GetOrAdd(type, t => Registered.TryGetValue(t, out ISankhyaEntityMap? map) ? map.Build() : AttributeMaps.Build(t, rowMode: false));

    /// <summary>
    /// Mapa para linhas do executeQuery: o mapa da entidade, se houver, senão atributos ou o nome da propriedade.
    /// </summary>
    internal static EntityMap ForRow(Type type) =>
        Registered.ContainsKey(type) ? For(type) : Rows.GetOrAdd(type, t => AttributeMaps.Build(t, rowMode: true));
}

/// <summary>
/// Monta o mapa a partir dos atributos do modelo (caminho da 5.x).
/// </summary>
internal static class AttributeMaps
{
    [UnconditionalSuppressMessage("Trimming", "IL2070", Justification = "Mapa por atributos usa reflexão por definição; o caminho AOT é o mapa fluente com o generator.")]
    [UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "Idem.")]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Idem.")]
    public static EntityMap Build(Type type, bool rowMode)
    {
        var members = new List<MemberMap>();

        foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.CanRead || property.GetIndexParameters().Length > 0) continue;

            var key = property.GetCustomAttribute<KeyAttribute>();
            string? column = property.GetCustomAttribute<XmlElementAttribute>()?.ElementName ?? key?.ElementName;
            var presentation = property.GetCustomAttribute<SankhyaPresentationAttribute>();
            if (column == null && presentation == null)
            {
                if (!rowMode) continue;
                column = property.Name;
            }

            Type propertyType = property.PropertyType;
            bool isNullableState = propertyType.IsGenericType && propertyType.GetGenericTypeDefinition() == typeof(NullableState<>);
            var member = new MemberMap
            {
                Name = property.Name,
                Type = propertyType,
                Get = property.GetValue,
                Set = property.CanWrite ? property.SetValue : null,
                DefaultValue = !isNullableState && propertyType.IsValueType ? Activator.CreateInstance(propertyType) : null,
                NullableState = isNullableState ? MemberColumns.AccessorFor(propertyType) : null,
                IsKey = key != null,
                AutoNumbered = key?.AutoEnumerable ?? false
            };

            if (presentation != null)
            {
                member.Presentation = true;
                member.Read = MemberColumns.SingleRead(member, presentation.Column, SankhyaType.Auto, null);
            } else
            {
                MemberColumns.AddColumn(member, column!, SankhyaType.Auto, null);
                if (rowMode) member.Writes.Clear();
            }

            members.Add(member);
        }

        string? entityName = type.GetCustomAttribute<SankhyaEntityAttribute>()?.Name;
        return new EntityMap(type, entityName, () => Activator.CreateInstance(type)!, members);
    }
}
