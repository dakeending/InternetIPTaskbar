using System.Reflection;
using System.Runtime.InteropServices;

namespace InternetIPTaskbar;

/// <summary>
/// Country flags: first from resources embedded in the exe (the Flags\ folder at build time), then from the disk cache,
/// then downloaded from flagcdn.com. Corrupt cache files are deleted.
/// </summary>
internal static class FlagStore
{
    static readonly string CacheDir = Path.Combine(AppInfo.DataDir, "flags-w80");
    static readonly Assembly Asm = typeof(FlagStore).Assembly;

    public static async Task<Bitmap?> LoadAsync(string country, CancellationToken ct)
    {
        var code = country.ToLowerInvariant();
        if (code.Length != 2 || !code.All(char.IsAsciiLetter)) return null;

        // 1. embedded resource
        using (var res = Asm.GetManifestResourceStream($"Flags.{code}.png"))
        {
            if (res is not null)
            {
                var bmp = Decode(res);
                if (bmp is not null) return bmp;
            }
        }

        // 2. disk cache
        var path = Path.Combine(CacheDir, code + ".png");
        if (File.Exists(path))
        {
            var bytes = await File.ReadAllBytesAsync(path, ct);
            using var ms = new MemoryStream(bytes);
            var bmp = Decode(ms);
            if (bmp is not null) return bmp;
            Log.Info($"FLAG cache file for {code} is corrupt, deleting");
            try { File.Delete(path); } catch { }
        }

        // 3. network; only successfully decoded images are written to the cache
        try
        {
            var data = await IpClient.GetBytesAsync($"https://flagcdn.com/w80/{code}.png", ct);
            using var ms = new MemoryStream(data);
            var bmp = Decode(ms);
            if (bmp is null) { Log.Info($"FLAG download for {code} is not a valid image"); return null; }
            try
            {
                Directory.CreateDirectory(CacheDir);
                var tmp = path + ".tmp";
                await File.WriteAllBytesAsync(tmp, data, ct);
                File.Move(tmp, path, true);
            }
            catch (Exception ex) when (ex is not OperationCanceledException) { Log.Debug($"FLAG cache write failed: {ex.Message}"); }
            return bmp;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            Log.Info($"FLAG download failed for {code}: {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    static Bitmap? Decode(Stream stream)
    {
        try
        {
            using var img = Image.FromStream(stream);
            return new Bitmap(img); // an independent copy, so the stream can be closed
        }
        catch (Exception ex) when (ex is ArgumentException or OutOfMemoryException or ExternalException)
        {
            return null;
        }
    }
}

