using System.Collections.Concurrent;
using System.ComponentModel;

namespace SankhyaAPI.Client.Mapping;

/// <summary>
/// Acesso direto (sem reflexão nem compilação de expressões) a uma propriedade do modelo. Preenchido pelo código
/// que o source generator do pacote gera.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed record SankhyaPropertyAccessor(string Name, Func<object, object?> Get, Action<object, object?>? Set);

/// <summary>
/// Registro dos acessores gerados. O código gerado se registra num <c>[ModuleInitializer]</c>; o pacote usa os
/// acessores quando existem e cai para reflexão/compilação em tempo de execução quando não.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class SankhyaAccessorRegistry
{
    private static readonly ConcurrentDictionary<Type, (Func<object> Create, Dictionary<string, SankhyaPropertyAccessor> Properties)> Registered = new();

    public static void Register<T>(Func<object> create, IEnumerable<SankhyaPropertyAccessor> properties) =>
        Registered[typeof(T)] = (create, properties.ToDictionary(p => p.Name));

    internal static bool TryGet(Type type, out Func<object> create, out IReadOnlyDictionary<string, SankhyaPropertyAccessor> properties)
    {
        if (Registered.TryGetValue(type, out var entry))
        {
            create = entry.Create;
            properties = entry.Properties;
            return true;
        }

        create = null!;
        properties = null!;
        return false;
    }

    /// <summary>Se o modelo tem acessores gerados (útil em testes).</summary>
    public static bool IsGenerated(Type type) => Registered.ContainsKey(type);
}
