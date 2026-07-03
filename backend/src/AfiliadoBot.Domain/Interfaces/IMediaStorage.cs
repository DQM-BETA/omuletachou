namespace AfiliadoBot.Domain.Interfaces;

public interface IMediaStorage
{
    Task<string> UploadAsync(Stream content, string fileName, CancellationToken ct = default);
    Task<Stream> DownloadAsync(string path, CancellationToken ct = default);
}
