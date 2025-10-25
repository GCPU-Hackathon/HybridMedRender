using UnityEngine;
using System;
using System.IO;

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
    public string rawFileName = "volume.raw";
    public string tfFileName   = "transfer_function.json";

    [Header("Material using the volume raymarch shader")]
    public Material volumeMaterial;

    Texture3D volumeTex;
    Texture2D tfTex;

    void Start()
    {
        string metaPath = Path.Combine(Application.streamingAssetsPath, jsonFileName);
        string metaJson = File.ReadAllText(metaPath);
        VolumeMetadataDVR meta = JsonUtility.FromJson<VolumeMetadataDVR>(metaJson);

        int dimX = meta.dim[0];
        int dimY = meta.dim[1];
        int dimZ = meta.dim[2];
        int voxelCount = dimX * dimY * dimZ;

        string rawPath = Path.Combine(Application.streamingAssetsPath, rawFileName);
        byte[] bytes = File.ReadAllBytes(rawPath);

        if (bytes.Length != voxelCount * sizeof(float))
        {
            Debug.LogWarning($"Raw size mismatch. Expected {voxelCount * sizeof(float)} bytes, got {bytes.Length} bytes.");
        }

        float[] voxels = new float[voxelCount];
        Buffer.BlockCopy(bytes, 0, voxels, 0, bytes.Length);

        int nonZeroCount = 0;
        float maxVal = 0f;
        for (int i = 0; i < voxels.Length; i++)
        {
            if (voxels[i] > 0.0001f) nonZeroCount++;
            if (voxels[i] > maxVal) maxVal = voxels[i];
        }
        Debug.Log("Volume tex loaded: "
            + dimX + "x" + dimY + "x" + dimZ
            + " first voxel=" + voxels[0]
            + " nonZeroCount=" + nonZeroCount
            + " maxVal=" + maxVal);

        // 3. Normaliser dans [0,1] si besoin
        // Ici, pour une seg binaire tumeur >0, c’est déjà 0 ou 1.
        // Si tu veux du scanner/IRM intensité, tu peux remapper au min/max du meta.intensity_range.
        float minI = (meta.intensity_range != null && meta.intensity_range.Length == 2) ? meta.intensity_range[0] : 0f;
        float maxI = (meta.intensity_range != null && meta.intensity_range.Length == 2) ? meta.intensity_range[1] : 1f;
        float rangeI = Mathf.Max(maxI - minI, 1e-6f);

        // On convertit en Color (Unity ne sait pas remplir Texture3D en raw float[], il faut passer par SetPixels/SetPixelData selon le format)
        // On va utiliser format RFloat pour garder l'intensité en un seul canal.
        // Color[] cols = new Color[voxelCount];
        // for (int i = 0; i < voxelCount; i++)
        // {
        //     float v01 = Mathf.Clamp01((voxels[i] - minI) / rangeI);
        //     cols[i] = new Color(v01, v01, v01, v01); // RGBA = même valeur
        // }

        // // 4. Créer la Texture3D
        // volumeTex = new Texture3D(dimX, dimY, dimZ, TextureFormat.RFloat, false);
        // volumeTex.wrapMode = TextureWrapMode.Clamp;
        // volumeTex.filterMode = FilterMode.Bilinear; // interpolation trilineaire
        // volumeTex.SetPixels(cols);
        // volumeTex.Apply(updateMipmaps: false);

        // // 5. Donner la texture (et tailles) au matériau
        // volumeMaterial.SetTexture("_VolumeTex", volumeTex);

        Color[] cols = new Color[voxelCount];
        for (int i = 0; i < voxelCount; i++)
        {
            float v = voxels[i]; // 0,1,2,4,...
            cols[i] = new Color(v, v, v, v);
        }

        string tfPath = Path.Combine(Application.streamingAssetsPath, tfFileName);

        bool isLabelMap;
        float p1, p99;
        tfTex = TransferFunctionLoader.LoadTransferFunctionLUT(tfPath, out isLabelMap, out p1, out p99);

        // 5. Envoyer tout ça au material
        volumeMaterial.SetTexture("_VolumeTex", volumeTex);
        volumeMaterial.SetTexture("_TFTex", tfTex);

        // dire au shader si c'est du labelmap ou du continuous
        volumeMaterial.SetInt("_IsLabelMap", isLabelMap ? 1 : 0);

        // si continuous: on aura besoin de renormaliser intensity -> [0,1]
        // le shader aura besoin de ces bornes pour retransformer la valeur brute en 0..1
        volumeMaterial.SetFloat("_P1", p1);
        volumeMaterial.SetFloat("_P99", p99);

        volumeTex = new Texture3D(dimX, dimY, dimZ, TextureFormat.RFloat, false);
        volumeTex.wrapMode = TextureWrapMode.Clamp;
        volumeTex.filterMode = FilterMode.Bilinear;
        volumeTex.SetPixels(cols);
        volumeTex.Apply(false);

        volumeMaterial.SetTexture("_VolumeTex", volumeTex);

        volumeMaterial.SetVector("_VolumeDim", new Vector3(dimX, dimY, dimZ));

        Debug.Log("Volume tex loaded: "
            + volumeTex.width + "x"
            + volumeTex.height + "x"
            + volumeTex.depth
            + " first voxel=" + volumeTex.GetPixel(0, 0, 0).r);

        // On passe aussi une matrice voxel->monde si tu veux l'alignement scanner
        // Sinon on peut juste afficher dans l'espace [0,1]^3
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
        volumeMaterial.SetMatrix("_Affine", affine);

        // IMPORTANT :
        // Mets ce script sur un cube dans la scène.
        // Le cube doit utiliser 'volumeMaterial'.
        // Le shader va considérer le cube comme la boîte du volume.
    }
}
