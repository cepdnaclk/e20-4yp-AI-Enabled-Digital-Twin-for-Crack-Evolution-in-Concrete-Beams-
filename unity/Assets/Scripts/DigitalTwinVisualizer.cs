using UnityEngine;
using UnityEngine.UI;
using NativeWebSocket;

public class DigitalTwinVisualizer : MonoBehaviour
{
    [Header("Visualization")]
    public Gradient colorGradient;
    public float sensitivity = 1.0f;

    [Header("UI")]
    public Slider timeSlider;
    public Text damageText;
    public Text rulText;

    private WebSocket websocket;
    private Mesh mesh;
    private Vector3[] vertices;
    private Color[] colors;

    private float[] currentStress;
    private float currentDamage = 0f;
    private float? currentRUL = null;

    async void Start()
    {
        // Generate grid mesh
        if (TryGetComponent<GridMeshGenerator>(out GridMeshGenerator gen))
            gen.GenerateMesh();

        // Get mesh AFTER generation
        MeshFilter mf = GetComponent<MeshFilter>();
        if (mf == null)
        {
            Debug.LogError("❌ MeshFilter missing on BeamDigitalTwin");
            return;
        }

        mesh = mf.mesh;
        vertices = mesh.vertices;

        // Initialize vertex colors
        colors = new Color[vertices.Length];
        for (int i = 0; i < colors.Length; i++)
            colors[i] = Color.blue;

        mesh.colors = colors;

        // WebSocket connection
        websocket = new WebSocket("ws://127.0.0.1:8000/ws");

        websocket.OnOpen += () =>
        {
            Debug.Log("🟢 WebSocket connected to Python");
        };

        websocket.OnError += (e) =>
        {
            Debug.LogError("❌ WebSocket error: " + e);
        };

        websocket.OnClose += (e) =>
        {
            Debug.Log("🔴 WebSocket closed");
        };

        websocket.OnMessage += (bytes) =>
        {
            string json = System.Text.Encoding.UTF8.GetString(bytes);
            Packet p = JsonUtility.FromJson<Packet>(json);

            currentStress = p.stress_field;
            currentDamage = Mathf.Clamp01(p.damage_prediction);
            currentRUL = (p.rul > 0f) ? p.rul : (float?)null;

            ApplyStress();
            UpdateUI();
        };

        await websocket.Connect();
    }

    void ApplyStress()
    {
        if (currentStress == null)
            return;

        if (currentStress.Length != vertices.Length)
        {
            Debug.LogWarning($"❗ Stress length {currentStress.Length} != vertices {vertices.Length}");
            return;
        }

        // Crack amplification based on LSTM damage
        float crackAmplifier = Mathf.Lerp(1.0f, 2.5f, currentDamage);

        for (int i = 0; i < vertices.Length; i++)
        {
            float stressVal = Mathf.Abs(currentStress[i]) * crackAmplifier;
            float t = Mathf.Clamp01(stressVal / sensitivity);
            colors[i] = colorGradient.Evaluate(t);
        }

        mesh.colors = colors;
    }

    void UpdateUI()
    {
        if (damageText)
            damageText.text = $"Predicted Damage: {currentDamage:F2}";

        if (rulText)
            rulText.text = currentRUL.HasValue
                ? $"RUL: {currentRUL.Value:F1} steps"
                : "RUL: --";

        if (timeSlider)
            timeSlider.value = currentDamage;
    }

    void Update()
    {
        websocket?.DispatchMessageQueue();
    }

    async void OnDestroy()
    {
        if (websocket != null)
            await websocket.Close();
    }

    [System.Serializable]
    public class Packet
    {
        public float time;
        public float[] stress_field;
        public float damage_prediction;
        public float rul;
    }
}

