using System;
using System.IO;

namespace KeyboardSwitch.Services;

/// <summary>
/// Writes a simple 16-bit PCM WAV file (two short descending tones) if the target path is missing.
/// Keeps us from shipping a binary asset while satisfying "WAV next to exe".
/// </summary>
internal static class WavGenerator
{
    public static void EnsureAlertWav(string path)
    {
        if (File.Exists(path)) return;

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        const int sampleRate = 44100;
        const short bitsPerSample = 16;
        const short channels = 1;

        // two beeps: 880 Hz (110 ms) + 40 ms silence + 660 Hz (130 ms)
        var tone1 = Tone(sampleRate, 880, 0.11, 0.25);
        var silence = new short[(int)(sampleRate * 0.04)];
        var tone2 = Tone(sampleRate, 660, 0.13, 0.25);

        var totalSamples = tone1.Length + silence.Length + tone2.Length;
        int dataLen = totalSamples * (bitsPerSample / 8);

        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
        using var w = new BinaryWriter(fs);

        // RIFF header
        w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
        w.Write(36 + dataLen);
        w.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
        // fmt chunk
        w.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
        w.Write(16); // PCM chunk size
        w.Write((short)1); // PCM format
        w.Write(channels);
        w.Write(sampleRate);
        w.Write(sampleRate * channels * (bitsPerSample / 8)); // byte rate
        w.Write((short)(channels * (bitsPerSample / 8))); // block align
        w.Write(bitsPerSample);
        // data chunk
        w.Write(System.Text.Encoding.ASCII.GetBytes("data"));
        w.Write(dataLen);

        WriteSamples(w, tone1);
        WriteSamples(w, silence);
        WriteSamples(w, tone2);
    }

    private static short[] Tone(int sampleRate, double freq, double seconds, double amplitude)
    {
        int count = (int)(sampleRate * seconds);
        var buf = new short[count];
        for (int i = 0; i < count; i++)
        {
            double t = i / (double)sampleRate;
            // Short linear fade-in/out to avoid click.
            double fade = Math.Min(1.0, Math.Min(i, count - i) / (double)(sampleRate * 0.005));
            double v = Math.Sin(2 * Math.PI * freq * t) * amplitude * fade;
            buf[i] = (short)(v * short.MaxValue);
        }
        return buf;
    }

    private static void WriteSamples(BinaryWriter w, short[] samples)
    {
        foreach (var s in samples) w.Write(s);
    }
}
