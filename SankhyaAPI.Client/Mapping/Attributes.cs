namespace SankhyaAPI.Client.Mapping;

/// <summary>
/// Nome da entidade (instância) no Sankhya, para modelos mapeados por atributos.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class SankhyaEntityAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}

/// <summary>
/// Campo de chave primária, para modelos mapeados por atributos. Chave composta: um atributo por campo, com no
/// máximo um auto-numerado.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class KeyAttribute : Attribute
{
    public KeyAttribute(string elementName, bool autoEnumerable = false)
    {
        if (string.IsNullOrWhiteSpace(elementName))
            throw new ArgumentException("Elementos XML para chaves primárias não podem ser vazios", nameof(elementName));
        ElementName = elementName;
        AutoEnumerable = autoEnumerable;
    }

    public string ElementName { get; }

    public bool AutoEnumerable { get; }
}

/// <summary>
/// Campo só de leitura vindo de outra entidade ou dos campos de apresentação (por exemplo
/// <c>GrupoUsuario_NOMEGRUPO</c>), para modelos mapeados por atributos.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class SankhyaPresentationAttribute(string column) : Attribute
{
    public string Column { get; } = column;
}
