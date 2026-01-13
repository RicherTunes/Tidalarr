using System.Text;

namespace Tidalarr.Integration;

internal static class TidalDownloadPayloadValidator
{
    private static readonly byte[] FlacMagic = Encoding.ASCII.GetBytes("fLaC");
    private static readonly byte[] OggMagic = Encoding.ASCII.GetBytes("OggS");
    private static readonly byte[] RiffMagic = Encoding.ASCII.GetBytes("RIFF");
    private static readonly byte[] Id3Magic = Encoding.ASCII.GetBytes("ID3");
    private static readonly byte[] FtypMagic = Encoding.ASCII.GetBytes("ftyp");

    public static void ValidateOrThrow(ReadOnlySpan<byte> sample, string? fileExtension, string? mimeType)
    {
        if (sample.IsEmpty)
        {
            throw new InvalidDataException("Downloaded stream contained no data.");
        }

        if (LooksLikeTextPayload(sample))
        {
            throw new InvalidDataException("Download returned non-audio content (HTML/JSON).");
        }

        string ext = NormalizeExtension(fileExtension);
        if (!LooksLikeAudioPayload(sample, ext, mimeType))
        {
            throw new InvalidDataException("Download returned content that does not look like audio.");
        }
    }

    private static string NormalizeExtension(string? fileExtension)
    {
        return string.IsNullOrWhiteSpace(fileExtension) ? string.Empty : fileExtension.Trim().TrimStart('.').ToLowerInvariant();
    }

    internal static bool LooksLikeTextPayload(ReadOnlySpan<byte> sample)
    {
        // Fast-path: check first non-whitespace char
        int index = 0;
        while (index < sample.Length && sample[index] is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n')
        {
            index++;
        }

        if (index >= sample.Length)
        {
            return false;
        }

        byte first = sample[index];
        if (first is (byte)'<' or (byte)'{' or (byte)'[')
        {
            return true;
        }

        // Heuristic: look for common HTML markers even if not first char
        int max = Math.Min(sample.Length, 256);
        string text = Encoding.UTF8.GetString(sample[..max]);
        return text.Contains("<!doctype", StringComparison.OrdinalIgnoreCase)
               || text.Contains("<html", StringComparison.OrdinalIgnoreCase)
               || text.Contains("<script", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeAudioPayload(ReadOnlySpan<byte> sample, string ext, string? mimeType)
    {
        // Signature checks (independent of extension)
        bool hasFlac = HasMagic(sample, FlacMagic, 0);
        bool hasOgg = HasMagic(sample, OggMagic, 0);
        bool hasRiff = HasMagic(sample, RiffMagic, 0);
        bool hasId3 = HasMagic(sample, Id3Magic, 0);
        bool hasMpegSync = sample.Length >= 2 && sample[0] == 0xFF && (sample[1] & 0xE0) == 0xE0;
        bool hasFtyp = HasMagic(sample, FtypMagic, 4);

        bool hasAnyAudioSignature = hasFlac || hasOgg || hasRiff || hasId3 || hasMpegSync || hasFtyp;
        if (string.IsNullOrEmpty(ext))
        {
            return hasAnyAudioSignature;
        }

        // Extension-specific strictness (Tidal frequently uses FLAC or M4A/MP4 containers)
        if (ext is "flac")
        {
            return hasFlac;
        }

        if (ext is "m4a" or "mp4")
        {
            return hasFtyp;
        }

        if (ext is "mp3")
        {
            return hasId3 || hasMpegSync;
        }

        // Fallback: accept any recognized audio signature, even if extension is unexpected.
        // This avoids hard failures when Tidal returns a container not covered by our mapping.
        _ = mimeType;
        return hasAnyAudioSignature;
    }

    private static bool HasMagic(ReadOnlySpan<byte> sample, byte[] magic, int offset)
    {
        return offset < 0 ? false : sample.Length >= offset + magic.Length && sample.Slice(offset, magic.Length).SequenceEqual(magic);
    }
}

