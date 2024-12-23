namespace WebHost.Attributes;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public class RouteAttribute(string route) : Attribute
{
    public string Route { get; } = route;
}