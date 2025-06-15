namespace WebHost.Http11.Response.Contents;

public interface IContent
{
    ulong? Length { get; }

    ValueTask<ulong?> CalculateChecksumAsync();

    ValueTask WriteAsync(Stream target, uint bufferSize);
}