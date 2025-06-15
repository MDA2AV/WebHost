namespace WebHost.Http11.Response.Contents;

public sealed class RawContent(ReadOnlyMemory<byte> data) : IContent
{
    public ulong? Length { get; } = (ulong)data.Length;

    public ValueTask<ulong?> CalculateChecksumAsync() => new((ulong)data.GetHashCode());

    public async ValueTask WriteAsync(Stream target, uint bufferSize)
    {
        await target.WriteAsync(data);
        await target.FlushAsync();
    }
}
