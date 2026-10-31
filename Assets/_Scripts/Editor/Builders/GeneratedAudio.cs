using System.IO;
using UnityEditor;
using UnityEngine;

// Placeholder loops (v3 V7) until real recordings are dropped in: balls rolling on cloth (soft low rumble) and a
// quiet room tone. Seamless: the tail is cross-faded into the head. Deterministic (fixed seed).
public static class GeneratedAudio
{
    private const int SampleRate = 22050;

    public static void WriteLoops(string folder)
    {
        Directory.CreateDirectory(folder);
        Write(folder + "/ball_roll_loop.wav", Roll(2.5f));
        AssetDatabase.Refresh();
    }

    // Brown noise, low-passed, with a slow uneven flutter like a ball turning over cloth.
    private static float[] Roll(float seconds)
    {
        var random = new System.Random(11);
        var samples = new float[(int)(seconds * SampleRate)];
        float brown = 0f, low = 0f;
        for (int i = 0; i < samples.Length; i++)
        {
            brown = Mathf.Clamp(brown + ((float)random.NextDouble() * 2f - 1f) * 0.06f, -1f, 1f);
            low += (brown - low) * 0.08f;
            float t = i / (float)SampleRate;
            float flutter = 0.75f + 0.25f * Mathf.Sin(t * Mathf.PI * 2f * 7.2f) * Mathf.Sin(t * Mathf.PI * 2f * 0.8f);
            samples[i] = low * flutter * 1.6f;
        }
        return Loop(samples, 0.3f);
    }

    // Cross-fade the last `fade` seconds into the start, then drop them: the end flows into the start.
    private static float[] Loop(float[] samples, float fade)
    {
        int n = Mathf.Min((int)(fade * SampleRate), samples.Length / 3);
        int length = samples.Length - n;
        var looped = new float[length];
        for (int i = 0; i < length; i++)
        {
            float s = samples[i];
            if (i < n)
            {
                float k = i / (float)n;
                s = s * k + samples[length + i] * (1f - k);
            }
            looped[i] = s;
        }
        return looped;
    }

    private static void Write(string path, float[] samples)
    {
        using var stream = new FileStream(Path.GetFullPath(path), FileMode.Create);
        using var writer = new BinaryWriter(stream);
        int bytes = samples.Length * 2;
        writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + bytes);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(SampleRate);
        writer.Write(SampleRate * 2);
        writer.Write((short)2);
        writer.Write((short)16);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
        writer.Write(bytes);
        foreach (float s in samples)
            writer.Write((short)(Mathf.Clamp(s, -1f, 1f) * 32000f));
    }
}
