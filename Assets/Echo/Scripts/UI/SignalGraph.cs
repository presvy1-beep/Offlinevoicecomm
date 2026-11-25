using UnityEngine;
using UnityEngine.UI;

namespace Echo {
public sealed class SignalGraph : Graphic {
    public SpectrumAnalyser analyser;
    public bool waveform;
    protected override void OnPopulateMesh(VertexHelper vh) {
        vh.Clear(); if (analyser == null) return;
        Rect r = GetPixelAdjustedRect();
        float previous = r.yMin + r.height * 0.5f;
        const int points = 96;
        for (int i = 0; i < points; i++) {
            float value;
            if (waveform) value = analyser.Waveform[i * 10] * 0.43f + 0.5f;
            else { int bin = Mathf.RoundToInt(Mathf.Pow((float)i / (points - 1), 1.65f) * 510); value = Mathf.Clamp01(Mathf.Log10(1 + analyser.Spectrum[bin] * 400) / 2); }
            float y = r.yMin + r.height * Mathf.Clamp01(value);
            if (i > 0) AddLine(vh, new Vector2(r.xMin + r.width * (i - 1) / (points - 1), previous), new Vector2(r.xMin + r.width * i / (points - 1), y), 2.5f);
            previous = y;
        }
    }
    void AddLine(VertexHelper vh, Vector2 a, Vector2 b, float width) {
        Vector2 n = new Vector2(b.y - a.y, a.x - b.x).normalized * width;
        int index = vh.currentVertCount;
        vh.AddVert(a + n, color, Vector2.zero); vh.AddVert(a - n, color, Vector2.zero);
        vh.AddVert(b - n, color, Vector2.zero); vh.AddVert(b + n, color, Vector2.zero);
        vh.AddTriangle(index, index + 1, index + 2); vh.AddTriangle(index, index + 2, index + 3);
    }
    public void Refresh() => SetVerticesDirty();
}
}
