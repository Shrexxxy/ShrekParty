using System.Buffers.Binary;
using System.IO.Compression;
using System.Net;
using System.Text;

namespace ShrekParty.Rehearsal;

/// <summary>Логины ботов — болотные персонажи.</summary>
public static class BotNames
{
    private static readonly string[] Names =
        ["Осёл", "Фиона", "Пряня", "Кот", "Дракоша", "Фаркуад", "Пиноккио", "Волк", "Гоблин", "Мышь", "Мерлин", "Артур"];

    /// <summary>
    /// Первые <paramref name="count"/> логинов; после конца списка — с номером.
    /// </summary>
    public static IEnumerable<string> Take(int count) =>
        Enumerable.Range(0, count).Select(i => $"Бот_{Names[i % Names.Length]}{(i < Names.Length ? "" : (i / Names.Length + 1).ToString())}");
}

/// <summary>
/// Cookie для любого обработчика, в том числе тестового сервера, который сам их не хранит.
/// </summary>
public sealed class CookieHandler(CookieContainer cookies) : DelegatingHandler
{
    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri!;
        var header = cookies.GetCookieHeader(uri);
        if (header.Length > 0)
        {
            request.Headers.Add("Cookie", header);
        }

        var response = await base.SendAsync(request, cancellationToken);
        if (response.Headers.TryGetValues("Set-Cookie", out var values))
        {
            foreach (var value in values)
            {
                cookies.SetCookies(uri, value);
            }
        }

        return response;
    }
}

/// <summary>
/// Минимальная PNG без зависимостей: заливка случайным цветом с полосой — чтобы фото ботов различались на стене.
/// </summary>
public static class TinyPng
{
    private const int Width = 320;
    private const int Height = 240;

    /// <summary>Байты PNG-файла.</summary>
    public static byte[] Create(Random random)
    {
        var (r, g, b) = ((byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256));
        var stripeTop = random.Next(Height - 40);
        var raw = new byte[Height * (Width * 3 + 1)];
        for (var y = 0; y < Height; y++)
        {
            var row = y * (Width * 3 + 1);
            var stripe = y >= stripeTop && y < stripeTop + 40;
            for (var x = 0; x < Width; x++)
            {
                var p = row + 1 + x * 3;
                (raw[p], raw[p + 1], raw[p + 2]) = stripe ? ((byte)(255 - r), (byte)(255 - g), (byte)(255 - b)) : (r, g, b);
            }
        }

        using var idat = new MemoryStream();
        using (var zlib = new ZLibStream(idat, CompressionLevel.Fastest, leaveOpen: true))
        {
            zlib.Write(raw);
        }

        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, Width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), Height);
        header[8] = 8; // бит на канал
        header[9] = 2; // RGB

        using var png = new MemoryStream();
        png.Write([0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A]);
        WriteChunk(png, "IHDR", header);
        WriteChunk(png, "IDAT", idat.ToArray());
        WriteChunk(png, "IEND", []);
        return png.ToArray();
    }

    private static void WriteChunk(Stream png, string type, byte[] data)
    {
        Span<byte> number = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(number, data.Length);
        png.Write(number);
        var typeAndData = Encoding.ASCII.GetBytes(type).Concat(data).ToArray();
        png.Write(typeAndData);
        BinaryPrimitives.WriteUInt32BigEndian(number, Crc32(typeAndData));
        png.Write(number);
    }

    private static uint Crc32(byte[] bytes)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in bytes)
        {
            crc ^= b;
            for (var i = 0; i < 8; i++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
            }
        }

        return ~crc;
    }
}
