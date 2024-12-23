using System.Reflection;
using WebHost;
using WebHost.Extensions;

internal class Program
{
    public static void Main(string[] args)
    {
        var builder = WebHostApp.CreateBuilder()
            .AddHandlers(Assembly.GetExecutingAssembly())
            .SetEndpoint("127.0.0.1", 5000);


    }
}
