using UnityEngine;
using Unity.Barracuda;

[System.Serializable]
public class ScalerItem
{
    public float mean;
    public float scale;
}

[System.Serializable]
public class ScalerData
{
    public ScalerItem x;
    public ScalerItem y;
    public ScalerItem load_mag;
    public ScalerItem global_deflection;
    public ScalerItem fc;
    public ScalerItem fy;
}

public class DigitalTwinVisualizer : MonoBehaviour
{
    [Header("AI Brain")]
    public NNModel modelAsset;
    public TextAsset scalerJson;

    [Header("Live Inputs")]
    [Range(0, 180000)] public float loadVal = 50000f;
    public float deflectionVal = 5.5f;
    public float fc = 25f;   // MPa
    public float fy = 314f;  // MPa

    [Header("Beam Dimensions (Physical mm)")]
    public float beamLengthMM = 1050f;
    public float beamHeightMM = 300f;

    [Header("Mesh Subdivision (for smooth visualization)")]
    [Range(30, 200)] public int lengthSegments = 80;
    [Range(2, 20)] public int heightSegments = 6;

    [Header("Visualization Settings")]
    public Gradient colorGradient;           // e.g. green (low) → yellow → red (high)
    [Range(0.1f, 10f)] public float sensitivity = 2.0f;  // increase if colors stay low

    [Header("Debug / Test")]
    public bool forceSolidRedTest = false;   // toggle ON to check if vertex colors work at all

    // Internals
    private IWorker worker;
    private ScalerData scalers;
    private Mesh mesh;
    private Vector3[] vertices;
    private Color[] colors;

    // For performance: only update when inputs change
    private float lastLoad = -1f;
    private float lastDefl = -1f;
    private float lastFc = -1f;
    private float lastFy = -1f;

    void Start()
    {
        // Create procedural mesh
        mesh = new Mesh();
        GetComponent<MeshFilter>().mesh = mesh;
        GenerateBeamMesh();

        vertices = mesh.vertices;
        colors = new Color[vertices.Length];

        // Load scalers
        if (scalerJson != null)
            scalers = JsonUtility.FromJson<ScalerData>(scalerJson.text);
        else
        {
            Debug.LogError("❌ Scaler JSON missing!");
            enabled = false;
            return;
        }

        // Load model
        if (modelAsset != null)
        {
            var model = ModelLoader.Load(modelAsset);
            worker = WorkerFactory.CreateWorker(WorkerFactory.Type.Auto, model);
        }
        else
        {
            Debug.LogError("❌ NNModel missing!");
            enabled = false;
        }

        // Force initial update
        UpdateColors();
    }

    void GenerateBeamMesh()
    {
        int xCount = lengthSegments + 1;
        int yCount = heightSegments + 1;

        Vector3[] verts = new Vector3[xCount * yCount];
        int[] tris = new int[(xCount - 1) * (yCount - 1) * 6];
        int triIndex = 0;

        for (int iy = 0; iy < yCount; iy++)
        {
            for (int ix = 0; ix < xCount; ix++)
            {
                int vi = iy * xCount + ix;

                float nx = (float)ix / lengthSegments - 0.5f;
                float ny = (float)iy / heightSegments - 0.5f;

                verts[vi] = new Vector3(nx, ny, 0f);

                if (ix < lengthSegments && iy < heightSegments)
                {
                    int a = vi;
                    int b = vi + 1;
                    int c = vi + xCount;
                    int d = vi + xCount + 1;

                    tris[triIndex++] = a; tris[triIndex++] = c; tris[triIndex++] = b;
                    tris[triIndex++] = b; tris[triIndex++] = c; tris[triIndex++] = d;
                }
            }
        }

        mesh.vertices = verts;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
    }

    void Update()
    {
        if (worker == null || scalers == null) return;

        // Only recompute if any input changed (performance fix)
        if (Mathf.Approximately(loadVal, lastLoad) &&
            Mathf.Approximately(deflectionVal, lastDefl) &&
            Mathf.Approximately(fc, lastFc) &&
            Mathf.Approximately(fy, lastFy))
        {
            return;
        }

        lastLoad = loadVal;
        lastDefl = deflectionVal;
        lastFc = fc;
        lastFy = fy;

        UpdateColors();
    }

    void UpdateColors()
    {
        // Normalize global inputs once
        float nLoad = (loadVal - scalers.load_mag.mean) / scalers.load_mag.scale;
        float nDefl = (deflectionVal - scalers.global_deflection.mean) / scalers.global_deflection.scale;
        float nFc = (fc - scalers.fc.mean) / scalers.fc.scale;
        float nFy = (fy - scalers.fy.mean) / scalers.fy.scale;

        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 v = vertices[i];

            // CENTERED coordinate: x = 0 at midspan (most common for symmetric central load)
            float physX = v.x * beamLengthMM;                  // -525 .. +525 mm
            float physY = v.y * beamHeightMM;                  // adjust if neutral axis not at 0

            float nX = (physX - scalers.x.mean) / scalers.x.scale;
            float nY = (physY - scalers.y.mean) / scalers.y.scale;

            float[] inputData = { nX, nY, nLoad, nDefl, nFc, nFy };

            using (Tensor inputTensor = new Tensor(1, 6, inputData))
            {
                worker.Execute(inputTensor);
                using (Tensor output = worker.PeekOutput())
                {
                    float stressNorm = output[0, 1];  // your stress output index

                    // Debug key points (left, center, right)
                    if (i == 0 || i == vertices.Length / 2 || i == vertices.Length - 1)
                    {
                        float approxX = physX + beamLengthMM * 0.5f; // for display as 0..1050
                        Debug.Log($"physX ≈ {approxX:F0} mm | nX = {nX:F4} | stress = {stressNorm:F4}");
                    }

                    float t = Mathf.Clamp01(Mathf.Abs(stressNorm) * sensitivity);
                    colors[i] = forceSolidRedTest ? Color.red : colorGradient.Evaluate(t);
                }
            }
        }

        mesh.colors = colors;
        Debug.Log("Colors updated — check if middle is now highest stress.");
    }

    void OnDestroy()
    {
        worker?.Dispose();
    }
}