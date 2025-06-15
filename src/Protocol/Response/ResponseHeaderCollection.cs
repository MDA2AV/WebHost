using WebHost.MemoryBuffers;
using WebHost.Protocol.Response.Headers;

namespace WebHost.Protocol.Response;

public sealed class ResponseHeaderCollection : PooledDictionary<string, string>, IHeaderCollection,
    IEditableHeaderCollection
{
}