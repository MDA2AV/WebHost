namespace WebHost.Attributes;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public class KeyAttribute(string key) : Attribute
{
    public string Key { get; } = key;
}