using UnityEngine;

namespace Echo {
public sealed class SpectrumAnalyser {
    public const int Size = 1024;
    readonly float[] real = new float[Size], imag = new float[Size];
    public readonly float[] Spectrum = new float[Size / 2];
    public readonly float[] Waveform = new float[Size];
    public float Amplitude { get; private set; }
    public void Analyze(float[] samples, int count) {
        if (count < Size) return;
        float power = 0;
        for (int i = 0; i < Size; i++) {
            float s = samples[i]; Waveform[i] = s; power += s * s;
            float w = 0.5f - 0.5f * Mathf.Cos(2 * Mathf.PI * i / (Size - 1));
            real[i] = s * w; imag[i] = 0;
        }
        Amplitude = Mathf.Sqrt(power / Size);
        for (int i = 1, j = 0; i < Size; i++) {
            int bit = Size >> 1;
            for (; (j & bit) != 0; bit >>= 1) j ^= bit;
            j ^= bit;
            if (i < j) { float t = real[i]; real[i] = real[j]; real[j] = t; t = imag[i]; imag[i] = imag[j]; imag[j] = t; }
        }
        for (int len = 2; len <= Size; len <<= 1) {
            float angle = -2 * Mathf.PI / len;
            for (int start = 0; start < Size; start += len)
                for (int j = 0; j < len / 2; j++) {
                    float c = Mathf.Cos(angle * j), s = Mathf.Sin(angle * j);
                    int a = start + j, b = a + len / 2;
                    float tr = c * real[b] - s * imag[b], ti = s * real[b] + c * imag[b];
                    real[b] = real[a] - tr; imag[b] = imag[a] - ti;
                    real[a] += tr; imag[a] += ti;
                }
        }
        for (int i = 0; i < Spectrum.Length; i++) Spectrum[i] = Mathf.Sqrt(real[i] * real[i] + imag[i] * imag[i]) / Size;
    }
}
}
