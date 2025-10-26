using UnityEngine;
using System;
using System.IO;
using Newtonsoft.Json;

[Serializable]
public class VolumeMetadataDVR {
    public int[] dim;
    public float[] spacing_mm;
    public string dtype;
    public float[] intensity_range;
    public float[][] affine;
}

public class VolumeDVR : MonoBehaviour
{
    [Header("Input files (in StreamingAssets)")]
    public string jsonFileName = "volume_meta.json";
    public string rawFileName  = "volume.raw";
    public string tfFileName   = "transfer_function.json";

    [Header("Material using the volume raymarch shader")]
    public Material volumeMaterial;

    Texture3D volumeTex;
    Texture2D tfTex;

    private Texture2D _labelCtrlTex;
    private Color[]   _labelCtrlPixels;

    void Start()
    {
        string metaPath = Path.Combine(Application.streamingAssetsPath, jsonFileName);
        string metaJson = File.ReadAllText(metaPath);
        VolumeMetadataDVR meta = JsonConvert.DeserializeObject<VolumeMetadataDVR>(metaJson);

        int dimX = meta.dim[0];
        int dimY = meta.dim[1];
        int dimZ = meta.dim[2];
        int voxelCount = dimX * dimY * dimZ;

        string rawPath = Path.Combine(Application.streamingAssetsPath, rawFileName);
        byte[] bytes = File.ReadAllBytes(rawPath);


        float[] voxels = new float[voxelCount];
        Buffer.BlockCopy(bytes, 0, voxels, 0, bytes.Length);

        Color[] cols = new Color[voxelCount];
        float maxVal = 0f;
        for (int i = 0; i < voxelCount; i++)
        {
            float v = voxels[i];
            cols[i] = new Color(v, v, v, v);
            if (v > maxVal) maxVal = v;
        }

        string tfPath = Path.Combine(Application.streamingAssetsPath, tfFileName);
        bool isLabelMap;
        float p1, p99;
        tfTex = TransferFunctionLoader.LoadTransferFunctionLUT(tfPath, out isLabelMap, out p1, out p99);

        volumeTex = new Texture3D(dimX, dimY, dimZ, TextureFormat.RFloat, false);
        volumeTex.wrapMode = TextureWrapMode.Clamp;
        volumeTex.filterMode = FilterMode.Bilinear;
        volumeTex.SetPixels(cols);
        volumeTex.Apply(false);

        volumeMaterial.SetTexture("_VolumeTex", volumeTex);
        volumeMaterial.SetTexture("_TFTex", tfTex);
        volumeMaterial.SetInt("_IsLabelMap", isLabelMap ? 1 : 0);
        volumeMaterial.SetFloat("_P1", p1);
        volumeMaterial.SetFloat("_P99", p99);
        volumeMaterial.SetVector("_Dim", new Vector4(dimX, dimY, dimZ, 1f));


        Matrix4x4 affine = Matrix4x4.identity;
        if (meta.affine != null && meta.affine.Length == 4 &&
            meta.affine[0].Length == 4 &&
            meta.affine[1].Length == 4 &&
            meta.affine[2].Length == 4 &&
            meta.affine[3].Length == 4)
        {
            affine.SetRow(0, new Vector4(meta.affine[0][0], meta.affine[0][1], meta.affine[0][2], meta.affine[0][3]));
            affine.SetRow(1, new Vector4(meta.affine[1][0], meta.affine[1][1], meta.affine[1][2], meta.affine[1][3]));
            affine.SetRow(2, new Vector4(meta.affine[2][0], meta.affine[2][1], meta.affine[2][2], meta.affine[2][3]));
            affine.SetRow(3, new Vector4(meta.affine[3][0], meta.affine[3][1], meta.affine[3][2], meta.affine[3][3]));
        }
        else
        {
            Debug.LogWarning("meta.affine is missing or not 4x4, using identity.");
        }

        Matrix4x4 invAffine = affine.inverse;

        volumeMaterial.SetMatrix("_Affine", affine);
        volumeMaterial.SetMatrix("_InvAffine", invAffine);

        Debug.Log($"Volume loaded {dimX}x{dimY}x{dimZ} maxVal={maxVal}");
        Debug.Log("Affine (voxel->mm):\n" + affine);
        Debug.Log("InvAffine (mm->voxel):\n" + invAffine);

        _labelCtrlTex = new Texture2D(256, 1, TextureFormat.RGBAFloat, false);
        _labelCtrlTex.wrapMode = TextureWrapMode.Clamp;
        _labelCtrlTex.filterMode = FilterMode.Point;

        _labelCtrlPixels = new Color[256];
        for (int i = 0; i < 256; i++)
        {
            _labelCtrlPixels[i] = new Color(1f, 1f, 1f, 1f);
        }
        _labelCtrlTex.SetPixels(_labelCtrlPixels);
        _labelCtrlTex.Apply(false);

        volumeMaterial.SetTexture("_LabelCtrlTex", _labelCtrlTex);

        FitGameObjectToMedicalBBox(meta, affine); // <<< NEW
    }

    void FitGameObjectToMedicalBBox(VolumeMetadataDVR meta, Matrix4x4 affineVoxelToMM)
    {
        int dimX = meta.dim[0];
        int dimY = meta.dim[1];
        int dimZ = meta.dim[2];

        Vector3[] cornersVoxel = new Vector3[8];
        cornersVoxel[0] = new Vector3(0,       0,       0      );
        cornersVoxel[1] = new Vector3(dimX-1,  0,       0      );
        cornersVoxel[2] = new Vector3(0,       dimY-1,  0      );
        cornersVoxel[3] = new Vector3(0,       0,       dimZ-1 );
        cornersVoxel[4] = new Vector3(dimX-1,  dimY-1,  0      );
        cornersVoxel[5] = new Vector3(dimX-1,  0,       dimZ-1 );
        cornersVoxel[6] = new Vector3(0,       dimY-1,  dimZ-1 );
        cornersVoxel[7] = new Vector3(dimX-1,  dimY-1,  dimZ-1 );

        Vector3[] cornersMM = new Vector3[8];
        for (int c = 0; c < 8; c++)
        {
            Vector3 ijk = cornersVoxel[c];
            Vector4 mmH = affineVoxelToMM * new Vector4(ijk.x, ijk.y, ijk.z, 1f);
            cornersMM[c] = new Vector3(mmH.x, mmH.y, mmH.z);
        }

        Vector3 minMM = cornersMM[0];
        Vector3 maxMM = cornersMM[0];
        for (int c = 1; c < 8; c++)
        {
            minMM = Vector3.Min(minMM, cornersMM[c]);
            maxMM = Vector3.Max(maxMM, cornersMM[c]);
        }

        Vector3 sizeMM = maxMM - minMM;
        Vector3 centerMM = (minMM + maxMM) * 0.5f;

        Vector3 sizeMeters   = sizeMM / 1000f;
        Vector3 centerMeters = centerMM / 1000f;

        transform.localScale = sizeMeters;

        transform.position = centerMeters;

        transform.rotation = Quaternion.Euler(new Vector3(-90, 0, 0));

        Debug.Log($"BBox mm min={minMM} max={maxMM} -> sizeMM={sizeMM}");
        Debug.Log($"Placed object at {centerMeters} m with scale {sizeMeters} m");
    }

    public void SetLabelVisible(int labelIndex, bool visible)
    {
        if (labelIndex < 0 || labelIndex > 255) return;
        var c = _labelCtrlPixels[labelIndex];
        c.a = visible ? 1f : 0f;
        _labelCtrlPixels[labelIndex] = c;
        _labelCtrlTex.SetPixels(_labelCtrlPixels);
        _labelCtrlTex.Apply(false);
    }

    public void SetLabelOpacity(int labelIndex, float opacity01)
    {
        if (labelIndex < 0 || labelIndex > 255) return;
        var c = _labelCtrlPixels[labelIndex];
        c.a = Mathf.Clamp01(opacity01);
        _labelCtrlPixels[labelIndex] = c;
        _labelCtrlTex.SetPixels(_labelCtrlPixels);
        _labelCtrlTex.Apply(false);
    }

    public void SetLabelTint(int labelIndex, Color tintRGB)
    {
        if (labelIndex < 0 || labelIndex > 255) return;
        var c = _labelCtrlPixels[labelIndex];
        c.r = tintRGB.r;
        c.g = tintRGB.g;
        c.b = tintRGB.b;
        _labelCtrlPixels[labelIndex] = c;
        _labelCtrlTex.SetPixels(_labelCtrlPixels);
        _labelCtrlTex.Apply(false);
    }

    public void SoloLabel(int soloIndex)
    {
        for (int i = 0; i < 256; i++)
        {
            var c = _labelCtrlPixels[i];
            c.a = (i == soloIndex) ? 1f : 0f;
            _labelCtrlPixels[i] = c;
        }
        _labelCtrlTex.SetPixels(_labelCtrlPixels);
        _labelCtrlTex.Apply(false);
    }

    public void ShowAll()
    {
        for (int i = 0; i < 256; i++)
        {
            var c = _labelCtrlPixels[i];
            c.a = 1f;
            c.r = 1f; c.g = 1f; c.b = 1f;
            _labelCtrlPixels[i] = c;
        }
        _labelCtrlTex.SetPixels(_labelCtrlPixels);
        _labelCtrlTex.Apply(false);
    }
}
