namespace WebHost.Protocol.Response.Headers;

public interface IHeaderCollection : IReadOnlyDictionary<string, string>, IDisposable;
