namespace AfiliadoBot.Infrastructure.Media;

/// <summary>
/// Leitor minimo (dependency-free, sem SDK/ffmpeg) da duracao real de um arquivo MP4, usado pelo
/// <c>TikTokPublisher</c> (Issue #10 / #77) para validar client-side o intervalo de duracao aceito
/// pela API do TikTok antes de qualquer chamada de upload.
///
/// Percorre os boxes ISO Base Media File Format (<c>size(4) + type(4) [+ largesize(8)]</c>) ate
/// localizar <c>moov/mvhd</c>, de onde extrai <c>timescale</c> e <c>duration</c> (suporta as
/// versoes 0 — campos de 32 bits — e 1 — campos de 64 bits — do box <c>mvhd</c>).
/// </summary>
public static class Mp4DurationReader
{
    /// <summary>
    /// Tenta ler a duracao (em segundos) do arquivo MP4 em <paramref name="filePath"/>.
    /// Qualquer falha de parsing (arquivo inexistente, formato invalido, box ausente,
    /// timescale zero) resulta em <c>false</c> — tratado pelo chamador como "fora do intervalo
    /// aceito", sem retry.
    /// </summary>
    public static bool TryGetDurationSeconds(string filePath, out double seconds)
    {
        seconds = 0;

        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);

            var moov = FindBox(stream, "moov", 0, stream.Length);
            if (moov is null)
                return false;

            var (moovOffset, moovSize) = moov.Value;

            var mvhd = FindBox(stream, "mvhd", moovOffset, moovOffset + moovSize);
            if (mvhd is null)
                return false;

            var (mvhdOffset, mvhdSize) = mvhd.Value;

            if (mvhdSize < 4)
                return false;

            stream.Seek(mvhdOffset, SeekOrigin.Begin);

            var version = stream.ReadByte();
            if (version < 0)
                return false;

            // 3 bytes de flags, ignorados.
            if (stream.Seek(3, SeekOrigin.Current) < 0)
                return false;

            long timescale;
            long duration;

            if (version == 1)
            {
                // creation_time (8) + modification_time (8), ambos ignorados.
                stream.Seek(16, SeekOrigin.Current);
                timescale = ReadUInt32BE(stream);
                duration = (long)ReadUInt64BE(stream);
            }
            else
            {
                // creation_time (4) + modification_time (4), ambos ignorados.
                stream.Seek(8, SeekOrigin.Current);
                timescale = ReadUInt32BE(stream);
                duration = ReadUInt32BE(stream);
            }

            if (timescale <= 0 || duration < 0)
                return false;

            seconds = (double)duration / timescale;
            return true;
        }
        catch
        {
            seconds = 0;
            return false;
        }
    }

    /// <summary>
    /// Localiza o primeiro box de <paramref name="type"/> no intervalo [<paramref name="start"/>,
    /// <paramref name="end"/>) do stream, devolvendo o offset e o tamanho do payload (apos o
    /// header do box). Retorna null quando o box nao e encontrado ou o arquivo esta corrompido.
    /// </summary>
    private static (long Offset, long Size)? FindBox(Stream stream, string type, long start, long end)
    {
        var pos = start;

        while (pos + 8 <= end)
        {
            stream.Seek(pos, SeekOrigin.Begin);

            long size = ReadUInt32BE(stream);
            var boxType = ReadFourCc(stream);
            long headerSize = 8;

            if (size == 1)
            {
                if (pos + 16 > end)
                    return null;

                size = (long)ReadUInt64BE(stream);
                headerSize = 16;
            }
            else if (size == 0)
            {
                size = end - pos;
            }

            if (size < headerSize || pos + size > end)
                return null;

            if (string.Equals(boxType, type, StringComparison.Ordinal))
                return (pos + headerSize, size - headerSize);

            pos += size;
        }

        return null;
    }

    private static uint ReadUInt32BE(Stream stream)
    {
        Span<byte> buffer = stackalloc byte[4];
        if (stream.Read(buffer) < 4)
            throw new EndOfStreamException();

        return ((uint)buffer[0] << 24) | ((uint)buffer[1] << 16) | ((uint)buffer[2] << 8) | buffer[3];
    }

    private static ulong ReadUInt64BE(Stream stream)
    {
        Span<byte> buffer = stackalloc byte[8];
        if (stream.Read(buffer) < 8)
            throw new EndOfStreamException();

        ulong result = 0;
        for (var i = 0; i < 8; i++)
            result = (result << 8) | buffer[i];

        return result;
    }

    private static string ReadFourCc(Stream stream)
    {
        Span<byte> buffer = stackalloc byte[4];
        if (stream.Read(buffer) < 4)
            throw new EndOfStreamException();

        return System.Text.Encoding.ASCII.GetString(buffer);
    }
}
