namespace SankhyaAPI.Client.Mapping;

/// <summary>
/// Peças comuns aos mapas fluente, por atributos e gerado.
/// </summary>
internal static class MemberColumns
{
    /// <summary>Acesso ao <see cref="NullableState{T}"/> a partir do seu <c>default</c> encaixotado.</summary>
    public static NullableStateAccessor AccessorFor(object nullableStateDefault) => new((INullableStateValue)nullableStateDefault);

    public static void AddColumn(MemberMap member, string column, SankhyaType type, IReadOnlyDictionary<object, string>? options)
    {
        member.Writes.Add(new WriteColumn(column, v => SankhyaFormat.Format(v, type, options)));
        member.Read = SingleRead(member, column, type, options?.ToDictionary(kv => kv.Value, kv => kv.Key));
    }

    public static ReadSpec SingleRead(MemberMap member, string column, SankhyaType type, IReadOnlyDictionary<string, object>? options) =>
        new([column], t => SankhyaFormat.Parse(t[0], member.Type, type, options, member.Name));
}
