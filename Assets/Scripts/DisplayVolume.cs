using UnityEngine;
using System;
using System.IO;

public class DisplayVolume : MonoBehaviour
{
    [Header("Input files (in StreamingAssets)")]
    public string jsonFileName = "volume_meta.json";
    public string rawFileName  = "volume.raw";

    [Header("Debug rendering")]
    [Range(0f, 1f)]
    public float intensityThreshold = 0.5f; // spawn only voxels above this
    public float sphereRadius = 0.5f;        // in mm units before scaling below
    public int maxSpheres = 5000;            // safety cap

    [Header("Parent for spawned spheres")]
    public Transform sphereParent; // assign an empty GameObject in the scene

    [Serializable]
    public class VolumeMetadata {
        public int[] dim;              // [X,Y,Z]
        public float[] spacing_mm;     // [sx,sy,sz]
        public string dtype;           // "float32"
        public float[] intensity_range;// [min,max] e.g. [0,1]
        public float[][] affine;       // 4x4 matrix
    }

    void Start()
    {
        // 1. Load metadata
        string metaPath = Path.Combine(Application.streamingAssetsPath, jsonFileName);
        if (!File.Exists(metaPath)) {
            Debug.LogError("Meta file not found: " + metaPath);
            return;
        }
        string metaJson = File.ReadAllText(metaPath);
        VolumeMetadata meta = JsonUtility.FromJson<VolumeMetadata>(metaJson);

        if (meta == null || meta.dim == null || meta.dim.Length != 3) {
            Debug.LogError("Invalid metadata JSON");
            return;
        }

        int dimX = meta.dim[0];
        int dimY = meta.dim[1];
        int dimZ = meta.dim[2];

        // 2. Load raw volume data
        string rawPath = Path.Combine(Application.streamingAssetsPath, rawFileName);
        if (!File.Exists(rawPath)) {
            Debug.LogError("Raw file not found: " + rawPath);
            return;
        }

        int voxelCount = dimX * dimY * dimZ;
        float[] voxels = new float[voxelCount];

        // Read float32 little-endian
        using (BinaryReader br = new BinaryReader(File.Open(rawPath, FileMode.Open)))
        {
            for (int i = 0; i < voxelCount; i++)
            {
                // BinaryReader.ReadSingle() reads IEEE 754 float32 little-endian => matches our .raw
                voxels[i] = br.ReadSingle();
            }
        }

        // 3. Get affine (voxel -> world mm)
        // meta.affine est float[4][4] (lignes).
        // On l'emballe dans une Matrix4x4 Unity.
        Matrix4x4 affine = Matrix4x4.identity;
        if (meta.affine != null && meta.affine.Length == 4 &&
            meta.affine[0].Length == 4 &&
            meta.affine[1].Length == 4 &&
            meta.affine[2].Length == 4 &&
            meta.affine[3].Length == 4)
        {
            // attention: Json was row-major, Unity's Matrix4x4 is column-major internally,
            // BUT Matrix4x4.SetRow lets us assign row-major safely.
            affine.SetRow(0, new Vector4(meta.affine[0][0], meta.affine[0][1], meta.affine[0][2], meta.affine[0][3]));
            affine.SetRow(1, new Vector4(meta.affine[1][0], meta.affine[1][1], meta.affine[1][2], meta.affine[1][3]));
            affine.SetRow(2, new Vector4(meta.affine[2][0], meta.affine[2][1], meta.affine[2][2], meta.affine[2][3]));
            affine.SetRow(3, new Vector4(meta.affine[3][0], meta.affine[3][1], meta.affine[3][2], meta.affine[3][3]));
        }
        else
        {
            Debug.LogWarning("No valid affine in metadata. Fallback to spacing only.");
            // Fallback: diagonal affine from spacing
            float sx = meta.spacing_mm[0];
            float sy = meta.spacing_mm[1];
            float sz = meta.spacing_mm[2];
            affine = Matrix4x4.identity;
            affine.m00 = sx;
            affine.m11 = sy;
            affine.m22 = sz;
            affine.m03 = 0f;
            affine.m13 = 0f;
            affine.m23 = 0f;
        }

        // 4. Spawn spheres
        // Mapping from linear index -> (x,y,z):
        // We wrote data in x-fastest order:
        // idx = x + dimX * (y + dimY * z)
        int spawned = 0;

        for (int z = 0; z < dimZ; z++)
        {
            for (int y = 0; y < dimY; y++)
            {
                for (int x = 0; x < dimX; x++)
                {
                    if (spawned >= maxSpheres) {
                        Debug.Log($"Reached maxSpheres={maxSpheres}, stopping.");
                        z = dimZ; y = dimY; break; // escape triple loop
                    }

                    int idx = x + dimX * (y + dimY * z);
                    float val = voxels[idx];

                    // Skip empty / low-intensity voxels
                    if (val < intensityThreshold)
                        continue;

                    // Compute world position from voxel coords
                    // We treat voxel coordinate as [x, y, z, 1] and multiply by affine
                    Vector4 voxelCoord = new Vector4(x, y, z, 1f);
                    Vector4 worldMM = affine * voxelCoord; // still in mm

                    // Convert mm -> meters for Unity scale (1 Unity unit = 1 meter)
                    Vector3 worldPos = new Vector3(worldMM.x, worldMM.y, worldMM.z) * 0.001f;


                    // Create sphere
                    GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    sphere.transform.position = worldPos;
                    sphere.transform.localScale = Vector3.one * (sphereRadius * 0.001f * 2f); 
                    // *2f because Unity's default sphere has diameter 1 unit

                    if (sphereParent != null)
                        sphere.transform.SetParent(sphereParent, worldPositionStays:true);

                    // Color it by intensity (optional)
                    Color c = new Color(val, val, val, 1f);
                    var renderer = sphere.GetComponent<Renderer>();
                    renderer.material = new Material(renderer.material);
                    renderer.material.color = c;

                    spawned++;
                }
            }
        }

        Debug.Log($"Spawned {spawned} spheres.");
    }
}
