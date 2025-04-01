namespace SankhyaAPI.Client.Extensions;

[AttributeUsage(AttributeTargets.Property)]
public class PrimaryKeyElementAttribute(string elementName, bool autoEnumerable = false) : Attribute
{
    public string ElementName => !string.IsNullOrWhiteSpace(elementName)
        ? elementName
        : throw new Exception("Elementos XML para chaves primárias não podem ser vazios");
    public bool AutoEnumerable => autoEnumerable;
}

public class Alunos
{
    [PrimaryKeyElement("IDALUNO", autoEnumerable: true)]
    public int Id { get; set; }
}
