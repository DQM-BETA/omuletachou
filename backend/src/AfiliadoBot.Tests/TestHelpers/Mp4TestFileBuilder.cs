namespace AfiliadoBot.Tests.TestHelpers;

/// <summary>
/// Helper de testes para gerar arquivos MP4 sinteticos minimos (apenas boxes
/// <c>ftyp/moov/mvhd/mdat</c>) com duracao controlada, usado por
/// <c>Mp4DurationReaderTests</c> e <c>TikTokPublisherTests</c> — evita depender de um arquivo de
/// video real fixo no repositorio.
/// </summary>
public static class Mp4TestFileBuilder
{
    private static byte[] WriteUInt32BE(uint value) => new[]
    {
        (byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value,
    };

    private static byte[] BuildBox(string type, byte[] payload)
    {
        var size = (uint)(8 + payload.Length);
        var box = new List<byte>();
        box.AddRange(WriteUInt32BE(size));
        box.AddRange(System.Text.Encoding.ASCII.GetBytes(type));
        box.AddRange(payload);
        return box.ToArray();
    }

    private static byte[] BuildMvhdV0(uint timescale, uint duration)
    {
        var payload = new List<byte> { 0x00, 0x00, 0x00, 0x00 };
        payload.AddRange(WriteUInt32BE(0));
        payload.AddRange(WriteUInt32BE(0));
        payload.AddRange(WriteUInt32BE(timescale));
        payload.AddRange(WriteUInt32BE(duration));
        return BuildBox("mvhd", payload.ToArray());
    }

    /// <summary>
    /// Cria um arquivo MP4 sintetico com a duracao (em segundos) informada, preenchendo o
    /// restante ("mdat") com bytes suficientes para atingir (ao menos) <paramref name="minTotalBytes"/>
    /// no total do arquivo — util para exercitar o upload em multiplos chunks.
    /// </summary>
    public static string CreateFileWithDuration(double durationSeconds, int minTotalBytes = 0)
    {
        const uint timescale = 1000;
        var duration = (uint)(durationSeconds * timescale);

        var ftyp = BuildBox("ftyp", new byte[] { (byte)'i', (byte)'s', (byte)'o', (byte)'m', 0, 0, 0, 1 });
        var moov = BuildBox("moov", BuildMvhdV0(timescale, duration));

        var headerLength = ftyp.Length + moov.Length + 8; // +8 = header do proprio box mdat
        var mdatPayloadSize = Math.Max(4, minTotalBytes - headerLength);
        var mdat = BuildBox("mdat", new byte[mdatPayloadSize]);

        var bytes = ftyp.Concat(moov).Concat(mdat).ToArray();

        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.mp4");
        File.WriteAllBytes(path, bytes);
        return path;
    }
}
