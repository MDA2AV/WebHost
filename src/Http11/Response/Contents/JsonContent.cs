using System.Text;
using System.Text.Json;

namespace WebHost.Http11.Response.Contents;

public sealed class JsonContent(object data, JsonSerializerOptions options) : IContent
{
    public ulong? Length => null;

    public ValueTask<ulong?> CalculateChecksumAsync() => new((ulong)data.GetHashCode());

    public async ValueTask WriteAsync(Stream target, uint bufferSize)
    {
        var serialized = JsonSerializer.Serialize(data);

        await JsonSerializer.SerializeAsync(target, data, data.GetType(), options);
    }
}

public sealed class JsonContent<T> : IContent
    where T : notnull
{
    public JsonContent(T data, JsonSerializerOptions options)
    {
        _data = data;

        _serializedData = JsonSerializer.Serialize(data);

        Length = (ulong)_serializedData.Length;
    }

    private T _data;

    private readonly string _serializedData;

    public ulong? Length { get; set; }

    public ValueTask<ulong?> CalculateChecksumAsync() => new((ulong)_data.GetHashCode());

    public async ValueTask WriteAsync(Stream target, uint bufferSize)
    {
        await target.WriteAsync(Encoding.UTF8.GetBytes(_serializedData));
    }
}