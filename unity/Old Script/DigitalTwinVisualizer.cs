using UnityEngine;
using Unity.Barracuda;

// 1. DATA STRUCTURES
[System.Serializable]
public class ScalerItem { public float mean; public float scale; }

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
    public float fc = 25f;
    public float fy = 314f;

    [Header("Beam Physics")]
    public float beamLengthMM = 1050f;
    public float beamHeightMM = 300f;

    [Header("Coordinate Mapping")]
    public bool centerIsZero = true;

    [Header("Visualization")]
    public Gradient colorGradient;
    
    [Range(0.1f, 5.0f)] 
    public float sensitivity = 1.0f;

    [Header("Crack Visualization")]
    [Tooltip("If stress (0.0 to 1.0) goes above this value, show cracks.")]
    [Range(0.0f, 1.0f)]
    public float crackThreshold = 0.85f; // New Setting
    
    [Tooltip("The color of the cracked area (usually Black).")]
    public Color crackColor = Color.black; // New Setting

    // Internals
    private IWorker worker;
    private ScalerData scalers;
    private Mesh mesh;
    private Vector3[] vertices;
    private Color[] colors;

    // OPTIMIZATION variables
    private float prevLoad, prevDefl, prevSens, prevCrackLimit;
    private bool prevCenterMapping;

    void Start()
    {
        // 1. Force Beam Generation (Supports both Generator types)
        if (TryGetComponent<GridMeshGenerator>(out GridMeshGenerator gridGen))
            gridGen.GenerateMesh();

        // 2. Get Mesh
        if (TryGetComponent<MeshFilter>(out MeshFilter mf))
        {
            mesh = mf.mesh;
            vertices = mesh.vertices;
            colors = new Color[vertices.Length];
        }
        else
        {
            Debug.LogError("❌ No MeshFilter found!");
            return;
        }

        // 3. Load Scalers & Model
        if (scalerJson) scalers = JsonUtility.FromJson<ScalerData>(scalerJson.text);
        if (modelAsset)
        {
            var model = ModelLoader.Load(modelAsset);
            worker = WorkerFactory.CreateWorker(WorkerFactory.Type.Auto, model);
        }
    }

    void Update()
    {
        if (worker == null || scalers == null || mesh == null) return;

        // CHECK: Also update if crack settings change
        bool inputsChanged = !Mathf.Approximately(loadVal, prevLoad) ||
                             !Mathf.Approximately(deflectionVal, prevDefl) ||
                             !Mathf.Approximately(sensitivity, prevSens) ||
                             !Mathf.Approximately(crackThreshold, prevCrackLimit) ||
                             centerIsZero != prevCenterMapping;

        if (!inputsChanged) return;

        prevLoad = loadVal;
        prevDefl = deflectionVal;
        prevSens = sensitivity;
        prevCrackLimit = crackThreshold;
        prevCenterMapping = centerIsZero;

        RunBatchInference();
    }

    void RunBatchInference()
    {
        int vCount = vertices.Length;
        float[] batchInput = new float[vCount * 6];

        float nLoad = (loadVal - scalers.load_mag.mean) / scalers.load_mag.scale;
        float nDefl = (deflectionVal - scalers.global_deflection.mean) / scalers.global_deflection.scale;
        float nFc = (fc - scalers.fc.mean) / scalers.fc.scale;
        float nFy = (fy - scalers.fy.mean) / scalers.fy.scale;

        for (int i = 0; i < vCount; i++)
        {
            float physX = 0;
            if (centerIsZero)
                physX = vertices[i].x * beamLengthMM;         
            else
                physX = (vertices[i].x + 0.5f) * beamLengthMM; 

            float physY = vertices[i].y * beamHeightMM;

            float nX = (physX - scalers.x.mean) / scalers.x.scale;
            float nY = (physY - scalers.y.mean) / scalers.y.scale;

            int baseIdx = i * 6;
            batchInput[baseIdx + 0] = nX;
            batchInput[baseIdx + 1] = nY;
            batchInput[baseIdx + 2] = nLoad;
            batchInput[baseIdx + 3] = nDefl;
            batchInput[baseIdx + 4] = nFc;
            batchInput[baseIdx + 5] = nFy;
        }

        using (Tensor inputTensor = new Tensor(vCount, 6, batchInput))
        {
            worker.Execute(inputTensor);
            Tensor output = worker.PeekOutput();

            for (int i = 0; i < vCount; i++)
            {
                float stressNorm = output[i, 1]; // Raw AI output

                // Calculate visual intensity (0.0 to 1.0)
                float t = Mathf.Abs(stressNorm) / sensitivity;

                // --- NEW CRACK LOGIC ---
                // If the normalized stress is higher than the limit, Paint it BLACK
                if (t >= crackThreshold)
                {
                    colors[i] = crackColor;
                }
                else
                {
                    // Otherwise, use the normal heatmap gradient
                    colors[i] = colorGradient.Evaluate(Mathf.Clamp01(t));
                }
            }
            output.Dispose();
        }

        mesh.colors = colors;
    }

    void OnDestroy()
    {
        worker?.Dispose();
    }
}