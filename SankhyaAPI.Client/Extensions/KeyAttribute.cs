namespace SankhyaAPI.Client.Extensions;

[AttributeUsage(AttributeTargets.Property)]
public class KeyAttribute(string elementName, bool autoEnumerable = false) : Attribute
{
    public string ElementName => !string.IsNullOrWhiteSpace(elementName)
        ? elementName
        : throw new Exception("Elementos XML para chaves primárias não podem ser vazios");
    public bool AutoEnumerable => autoEnumerable;
}
