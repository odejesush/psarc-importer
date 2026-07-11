using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using Pfim;

namespace PsarcImporter.Conversion;

internal static class ArtworkConverter
{
    public static string? ExtractArtworkPath(string contentDirectory)
    {
        string ddsPath = Path.Combine(contentDirectory, "cover.dds");
        if (!File.Exists(ddsPath))
            return null;

        string pngPath = Path.Combine(contentDirectory, "cover.png");
        try
        {
            using IImage image = Pfimage.FromFile(ddsPath);
            byte[] rgbaPixels = ConvertDecodedDdsToRgba(image);
            WriteRgbaPng(pngPath, image.Width, image.Height, rgbaPixels);
            return pngPath;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[PsarcImporter] Failed to convert cover art '{ddsPath}': {ex.Message}");
            try { if (File.Exists(pngPath)) File.Delete(pngPath); }
            catch { }
            return null;
        }
    }

    private static byte[] ConvertDecodedDdsToRgba(IImage image)
    {
        if (image.Width <= 0 || image.Height <= 0)
            throw new InvalidDataException($"Decoded DDS has invalid dimensions {image.Width}x{image.Height}.");

        byte[] output = new byte[checked(image.Width * image.Height * 4)];
        byte[] source = image.Data;

        switch (image.Format)
        {
            case ImageFormat.Rgba32:
                CopyBgraToRgba(source, image.Stride, image.Width, image.Height, output);
                return output;
            case ImageFormat.Rgb24:
                CopyBgrToRgba(source, image.Stride, image.Width, image.Height, output);
                return output;
            case ImageFormat.Rgb8:
                CopyGrayToRgba(source, image.Stride, image.Width, image.Height, output);
                return output;
            default:
                throw new NotSupportedException($"Unsupported decoded DDS format '{image.Format}'.");
        }
    }

    private static void CopyBgraToRgba(byte[] source, int stride, int width, int height, byte[] output)
    {
        for (int y = 0; y < height; y++)
        {
            int sourceOffset = y * stride;
            int outputOffset = y * width * 4;
            for (int x = 0; x < width; x++)
            {
                output[outputOffset++] = source[sourceOffset + 2];
                output[outputOffset++] = source[sourceOffset + 1];
                output[outputOffset++] = source[sourceOffset];
                output[outputOffset++] = source[sourceOffset + 3];
                sourceOffset += 4;
            }
        }
    }

    private static void CopyBgrToRgba(byte[] source, int stride, int width, int height, byte[] output)
    {
        for (int y = 0; y < height; y++)
        {
            int sourceOffset = y * stride;
            int outputOffset = y * width * 4;
            for (int x = 0; x < width; x++)
            {
                output[outputOffset++] = source[sourceOffset + 2];
                output[outputOffset++] = source[sourceOffset + 1];
                output[outputOffset++] = source[sourceOffset];
                output[outputOffset++] = 255;
                sourceOffset += 3;
            }
        }
    }

    private static void CopyGrayToRgba(byte[] source, int stride, int width, int height, byte[] output)
    {
        for (int y = 0; y < height; y++)
        {
            int sourceOffset = y * stride;
            int outputOffset = y * width * 4;
            for (int x = 0; x < width; x++)
            {
                byte value = source[sourceOffset++];
                output[outputOffset++] = value;
                output[outputOffset++] = value;
                output[outputOffset++] = value;
                output[outputOffset++] = 255;
            }
        }
    }

    private static void WriteRgbaPng(string path, int width, int height, byte[] rgbaPixels)
    {
        using FileStream output = File.Create(path);
        output.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });

        byte[] ihdr = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(0, 4), width);
        BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(4, 4), height);
        ihdr[8] = 8;
        ihdr[9] = 6;
        ihdr[10] = 0;
        ihdr[11] = 0;
        ihdr[12] = 0;
        WritePngChunk(output, "IHDR", ihdr);

        using MemoryStream idat = new();
        using (ZLibStream compressor = new(idat, CompressionLevel.Optimal, leaveOpen: true))
        {
            byte[] filterByte = { 0 };
            int sourceOffset = 0;
            int rowBytes = width * 4;
            for (int y = 0; y < height; y++)
            {
                compressor.Write(filterByte, 0, 1);
                compressor.Write(rgbaPixels, sourceOffset, rowBytes);
                sourceOffset += rowBytes;
            }
        }

        WritePngChunk(output, "IDAT", idat.ToArray());
        WritePngChunk(output, "IEND", System.Array.Empty<byte>());
    }

    private static void WritePngChunk(Stream output, string type, byte[] data)
    {
        byte[] typeBytes = Encoding.ASCII.GetBytes(type);
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(buffer, data.Length);
        output.Write(buffer);
        output.Write(typeBytes);
        output.Write(data);

        uint crc = UpdateCrc(UpdateCrc(0xffffffffu, typeBytes), data) ^ 0xffffffffu;
        BinaryPrimitives.WriteUInt32BigEndian(buffer, crc);
        output.Write(buffer);
    }

    private static uint UpdateCrc(uint crc, byte[] bytes)
    {
        for (int i = 0; i < bytes.Length; i++)
        {
            crc ^= bytes[i];
            for (int bit = 0; bit < 8; bit++)
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xedb88320u : crc >> 1;
        }

        return crc;
    }
}
