using System.Text;

namespace Generellem.Document;

/// <summary>
/// Reads text stored inside PNG tEXt/iTXt chunks and JPEG COM markers.
/// </summary>
public static class EmbeddedImageText
{
    public static string Read(ReadOnlySpan<byte> bytes, string fileName)
    {
        StringBuilder text = new();
        text.Append("Image file: ");
        text.Append(Path.GetFileName(fileName));

        string embedded = ReadPng(bytes);
        if (embedded.Length == 0)
            embedded = ReadJpeg(bytes);

        if (embedded.Length > 0)
        {
            text.AppendLine();
            text.Append(embedded);
        }

        return text.ToString();
    }

    public static string Merge(string embedded, string? vision)
    {
        if (string.IsNullOrWhiteSpace(vision))
            return embedded;

        return embedded + Environment.NewLine + vision.Trim();
    }

    static string ReadPng(ReadOnlySpan<byte> bytes)
    {
        ReadOnlySpan<byte> signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        if (bytes.Length < signature.Length || !bytes[..signature.Length].SequenceEqual(signature))
            return string.Empty;

        StringBuilder text = new();
        int offset = signature.Length;
        while (offset + 8 <= bytes.Length)
        {
            int length = ReadInt32(bytes[offset..]);
            if (length < 0 || offset + 12 + length > bytes.Length)
                break;

            string type = Encoding.ASCII.GetString(bytes.Slice(offset + 4, 4));
            ReadOnlySpan<byte> data = bytes.Slice(offset + 8, length);
            if (type is "tEXt" or "iTXt")
            {
                string chunk = type == "tEXt" ? ReadTextChunk(data) : ReadInternationalChunk(data);
                if (chunk.Length > 0)
                {
                    if (text.Length > 0)
                        text.AppendLine();
                    text.Append(chunk);
                }
            }

            if (type == "IEND")
                break;

            offset += 12 + length;
        }

        return text.ToString();
    }

    static string ReadTextChunk(ReadOnlySpan<byte> data)
    {
        int split = data.IndexOf((byte)0);
        if (split < 0 || split + 1 >= data.Length)
            return string.Empty;

        return Encoding.Latin1.GetString(data[(split + 1)..]).Trim();
    }

    static string ReadInternationalChunk(ReadOnlySpan<byte> data)
    {
        int split = data.IndexOf((byte)0);
        if (split < 0 || split + 1 >= data.Length)
            return string.Empty;

        // keyword, compression flag, compression method, language, translated keyword, text
        int cursor = split + 1;
        if (cursor + 1 >= data.Length)
            return string.Empty;

        byte compression = data[cursor];
        cursor += 2;
        if (compression != 0)
            return string.Empty;

        cursor = SkipNull(data, cursor);
        cursor = SkipNull(data, cursor);
        if (cursor >= data.Length)
            return string.Empty;

        return Encoding.UTF8.GetString(data[cursor..]).Trim();
    }

    static int SkipNull(ReadOnlySpan<byte> data, int cursor)
    {
        int split = data[cursor..].IndexOf((byte)0);
        if (split < 0)
            return data.Length;

        return cursor + split + 1;
    }

    static string ReadJpeg(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 4 || bytes[0] != 0xFF || bytes[1] != 0xD8)
            return string.Empty;

        StringBuilder text = new();
        int offset = 2;
        while (offset + 4 <= bytes.Length)
        {
            if (bytes[offset] != 0xFF)
            {
                offset++;
                continue;
            }

            byte marker = bytes[offset + 1];
            if (marker == 0xD9 || marker == 0xDA)
                break;

            if (marker == 0x00 || marker == 0xFF)
            {
                offset++;
                continue;
            }

            int length = (bytes[offset + 2] << 8) | bytes[offset + 3];
            if (length < 2 || offset + 2 + length > bytes.Length)
                break;

            if (marker == 0xFE)
            {
                string comment = Encoding.Latin1.GetString(bytes.Slice(offset + 4, length - 2)).Trim();
                if (comment.Length > 0)
                {
                    if (text.Length > 0)
                        text.AppendLine();
                    text.Append(comment);
                }
            }

            offset += 2 + length;
        }

        return text.ToString();
    }

    static int ReadInt32(ReadOnlySpan<byte> bytes) =>
        (bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3];
}
