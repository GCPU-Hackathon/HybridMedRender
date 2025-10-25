using System;
using System.IO;
using System.Linq;
using UnityEngine;

[Serializable]
public class TFLabelEntry {
    public float label;
    public float[] color;
    public float alpha;
}

[Serializable]
public class TFLabelMap {
    public string type;
    public TFLabelEntry[] entries;
}

[Serializable]
public class TFCurvePoint {
    public float x;
    public float[] color;
    public float alpha;
}

[Serializable]
public class TFContinuousInfo {
    public float p1;
    public float p99;
}

[Serializable]
public class TFContinuous {
    public string type;
    public TFCurvePoint[] curve;
    public TFContinuousInfo intensity_normalization;
}

public static class TransferFunctionLoader
{
    // Charge le JSON brut et renvoie:
    // - la texture LUT (Texture2D)
    // - des infos optionnelles comme p1/p99 pour le mode continuous
    //
    // Pour "labelmap":
    //   On fabrique une 256x1 texture où l'index = valeur de classe
    // Pour "continuous":
    //   On fabrique une 256x1 texture où l'index = intensité normalisée * 255
    //
    public static Texture2D LoadTransferFunctionLUT(
        string tfJsonPath,
        out bool isLabelMap,
        out float p1,
        out float p99)
    {
        string jsonText = File.ReadAllText(tfJsonPath);

        TFLabelMap tfLabel = null;
        TFContinuous tfCont = null;

        try {
            tfLabel = JsonUtility.FromJson<TFLabelMap>(jsonText);
        } catch {}

        if (tfLabel == null || tfLabel.entries == null || tfLabel.entries.Length == 0)
        {
            tfCont = JsonUtility.FromJson<TFContinuous>(jsonText);
        }

        Texture2D lutTex = new Texture2D(256, 1, TextureFormat.RGBAFloat, false);
        lutTex.wrapMode = TextureWrapMode.Clamp;
        lutTex.filterMode = FilterMode.Bilinear;

        Color[] pixels = new Color[256];
        for (int i = 0; i < 256; i++)
            pixels[i] = Color.clear;

        if (tfLabel != null && tfLabel.entries != null && tfLabel.entries.Length > 0)
        {
            isLabelMap = true;

            p1 = 0f;
            p99 = 1f;

            foreach (var e in tfLabel.entries)
            {
                int idx = Mathf.Clamp(Mathf.RoundToInt(e.label), 0, 255);
                float r = e.color.Length > 0 ? e.color[0] : 0f;
                float g = e.color.Length > 1 ? e.color[1] : 0f;
                float b = e.color.Length > 2 ? e.color[2] : 0f;
                float a = e.alpha;
                pixels[idx] = new Color(r,g,b,a);
            }
        }
        else
        {
            isLabelMap = false;

            for (int i = 0; i < 256; i++)
            {
                var pt = tfCont.curve[i];
                float r = pt.color[0];
                float g = pt.color[1];
                float b = pt.color[2];
                float a = pt.alpha;
                pixels[i] = new Color(r,g,b,a);
            }

            p1  = tfCont.intensity_normalization.p1;
            p99 = tfCont.intensity_normalization.p99;
        }

        lutTex.SetPixels(pixels);
        lutTex.Apply(false);

        return lutTex;
    }
}
