using System.Media;

namespace InternetIPTaskbar;

/// <summary>
/// A double short "steamship horn". Synthesised on first use (a low tone with overtones
/// and a slight detune between two oscillators); no external sound files.
/// </summary>
internal static class HornSound
{
    const int Rate = 22050;
    static SoundPlayer? player;

    public static void Play()
    {
        try
        {
            player ??= new SoundPlayer(new MemoryStream(BuildWav()));
            player.Play(); // asynchronous, does not block the UI
        }
        catch (Exception ex) { Log.Info($"SOUND failed: {ex.GetType().Name}: {ex.Message}"); }
    }

    internal static byte[] BuildWav()
    {
        var samples = new List<double>();
        AddBlast(samples, 0.42);
        samples.AddRange(new double[(int)(Rate * 0.18)]); // pause between the two blasts
        AddBlast(samples, 0.42);

        double peak = samples.Max(Math.Abs);
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        int dataLen = samples.Count * 2;
        w.Write("RIFF"u8); w.Write(36 + dataLen); w.Write("WAVE"u8);
        w.Write("fmt "u8); w.Write(16); w.Write((short)1); w.Write((short)1);
        w.Write(Rate); w.Write(Rate * 2); w.Write((short)2); w.Write((short)16);
        w.Write("data"u8); w.Write(dataLen);
        foreach (var s in samples) w.Write((short)(s / peak * 0.7 * short.MaxValue));
        w.Flush();
        return ms.ToArray();
    }

    static void AddBlast(List<double> samples, double seconds)
    {
        const double f0 = 165;                       // fundamental, Hz
        double[] amp = [1.0, 0.7, 0.5, 0.35, 0.2, 0.1]; // overtone amplitudes
        int n = (int)(Rate * seconds);
        int attack = (int)(Rate * 0.03), release = (int)(Rate * 0.08);
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / Rate, v = 0;
            for (int k = 0; k < amp.Length; k++)
            {
                double f = f0 * (k + 1);
                v += amp[k] * (Math.Sin(2 * Math.PI * f * t) + 0.6 * Math.Sin(2 * Math.PI * f * 1.004 * t));
            }
            double env = Math.Min(1.0, Math.Min((double)i / attack, (double)(n - i) / release));
            samples.Add(v * env);
        }
    }
}
