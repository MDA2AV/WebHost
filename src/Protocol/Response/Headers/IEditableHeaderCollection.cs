namespace WebHost.Protocol.Response.Headers;

public interface IEditableHeaderCollection : IDictionary<string, string>, IDisposable;