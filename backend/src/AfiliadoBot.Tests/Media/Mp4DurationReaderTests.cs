using AfiliadoBot.Infrastructure.Media;
using FluentAssertions;

namespace AfiliadoBot.Tests.Media;

public class Mp4DurationReaderTests
{
    private static byte[] WriteUInt32BE(uint value) => new[]
    {
        (byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value,
    };

    private static byte[] WriteUInt64BE(ulong value) => new[]
    {
        (byte)(value >> 56), (byte)(value >> 48), (byte)(value >> 40), (byte)(value >> 32),
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
        var payload = new List<byte>
        {
            0x00, // version
            0x00, 0x00, 0x00, // flags
        };
        payload.AddRange(WriteUInt32BE(0)); // creation_time
        payload.AddRange(WriteUInt32BE(0)); // modification_time
        payload.AddRange(WriteUInt32BE(timescale));
        payload.AddRange(WriteUInt32BE(duration));
        return BuildBox("mvhd", payload.ToArray());
    }

    private static byte[] BuildMvhdV1(uint timescale, ulong duration)
    {
        var payload = new List<byte>
        {
            0x01, // version
            0x00, 0x00, 0x00, // flags
        };
        payload.AddRange(WriteUInt64BE(0)); // creation_time
        payload.AddRange(WriteUInt64BE(0)); // modification_time
        payload.AddRange(WriteUInt32BE(timescale));
        payload.AddRange(WriteUInt64BE(duration));
        return BuildBox("mvhd", payload.ToArray());
    }

    private static byte[] BuildMp4(byte[] mvhdBox)
    {
        var ftyp = BuildBox("ftyp", new byte[] { (byte)'i', (byte)'s', (byte)'o', (byte)'m', 0, 0, 0, 1 });
        var moov = BuildBox("moov", mvhdBox);
        var mdat = BuildBox("mdat", new byte[] { 1, 2, 3, 4 });

        return ftyp.Concat(moov).Concat(mdat).ToArray();
    }

    private static string WriteTempFile(byte[] bytes)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.mp4");
        File.WriteAllBytes(path, bytes);
        return path;
    }

    [Fact]
    public void TryGetDurationSeconds_RetornaDuracaoCorreta_QuandoMvhdVersao0()
    {
        // timescale=1000 (ms), duration=15000 (ms) -> 15s
        var mvhd = BuildMvhdV0(1000, 15000);
        var path = WriteTempFile(BuildMp4(mvhd));

        try
        {
            var result = Mp4DurationReader.TryGetDurationSeconds(path, out var seconds);

            result.Should().BeTrue();
            seconds.Should().BeApproximately(15.0, 0.001);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void TryGetDurationSeconds_RetornaDuracaoCorreta_QuandoMvhdVersao1()
    {
        // timescale=90000, duration=2700000 -> 30s (campos 64-bit)
        var mvhd = BuildMvhdV1(90000, 2700000);
        var path = WriteTempFile(BuildMp4(mvhd));

        try
        {
            var result = Mp4DurationReader.TryGetDurationSeconds(path, out var seconds);

            result.Should().BeTrue();
            seconds.Should().BeApproximately(30.0, 0.001);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void TryGetDurationSeconds_RetornaFalse_QuandoMoovAusente()
    {
        var ftyp = BuildBox("ftyp", new byte[] { (byte)'i', (byte)'s', (byte)'o', (byte)'m' });
        var mdat = BuildBox("mdat", new byte[] { 1, 2, 3 });
        var path = WriteTempFile(ftyp.Concat(mdat).ToArray());

        try
        {
            var result = Mp4DurationReader.TryGetDurationSeconds(path, out var seconds);

            result.Should().BeFalse();
            seconds.Should().Be(0);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void TryGetDurationSeconds_RetornaFalse_QuandoMvhdAusenteDentroDeMoov()
    {
        var ftyp = BuildBox("ftyp", new byte[] { (byte)'i', (byte)'s', (byte)'o', (byte)'m' });
        var moov = BuildBox("moov", new byte[] { 1, 2, 3, 4 }); // sem mvhd valido dentro
        var path = WriteTempFile(ftyp.Concat(moov).ToArray());

        try
        {
            var result = Mp4DurationReader.TryGetDurationSeconds(path, out var seconds);

            result.Should().BeFalse();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void TryGetDurationSeconds_RetornaFalse_QuandoArquivoInvalidoOuCorrompido()
    {
        var path = WriteTempFile(new byte[] { 0xFF, 0x00, 0x11, 0x22, 0x33 });

        try
        {
            var result = Mp4DurationReader.TryGetDurationSeconds(path, out var seconds);

            result.Should().BeFalse();
            seconds.Should().Be(0);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void TryGetDurationSeconds_RetornaFalse_QuandoArquivoNaoExiste()
    {
        var result = Mp4DurationReader.TryGetDurationSeconds(
            Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}-inexistente.mp4"), out var seconds);

        result.Should().BeFalse();
        seconds.Should().Be(0);
    }

    [Fact]
    public void TryGetDurationSeconds_RetornaFalse_QuandoTimescaleZero()
    {
        var mvhd = BuildMvhdV0(0, 15000);
        var path = WriteTempFile(BuildMp4(mvhd));

        try
        {
            var result = Mp4DurationReader.TryGetDurationSeconds(path, out var seconds);

            result.Should().BeFalse();
        }
        finally
        {
            File.Delete(path);
        }
    }
}
