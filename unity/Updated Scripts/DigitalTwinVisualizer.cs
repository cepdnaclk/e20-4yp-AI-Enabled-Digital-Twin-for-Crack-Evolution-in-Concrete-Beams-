using UnityEngine;
using TMPro; // ✅ for BeamText (TextMeshPro)

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class DigitalTwinVisualizer : MonoBehaviour
{
    [Header("Real Data Config")]
    public float maxRealDeflectionMM = 6.7f;

    // Real dataset maximum load: 185.49 kN
    public float maxLoadN = 185490f;

    [Range(0f, 185490f)] public float loadVal = 0f;

    [Header("Visualization")]
    [Range(1f, 300f)] public float visualExaggeration = 1f;

    public enum LengthAxis { X, Z }
    public LengthAxis lengthAxis = LengthAxis.X;

    [Header("No-Load Color")]
    public Color noLoadColor = Color.green;

    [Header("Real-world Crack Coloring")]
    [Tooltip("How strongly damage prefers the bottom (tension zone). 1 = normal, 3 = much stronger bottom cracking.")]
    [Range(0.5f, 5f)] public float bottomBias = 2.0f;

    [Tooltip("How fast crack grows upward as load increases.")]
    [Range(0f, 3f)] public float upwardGrowth = 1.2f;

    [Tooltip("If enabled, crack region focuses around a load point (optional).")]
    public bool useLoadPoint = false;

    [Tooltip("Place an empty GameObject above the beam mid-span if you want crack localized to the load position.")]
    public Transform loadPointWorld;

    [Tooltip("Crack influence radius around load point (in local XZ). Only used if useLoadPoint = true.")]
    [Range(0.01f, 2f)] public float loadRadius = 0.25f;

    // ======================================================
    // ✅ ADDED: Manual crack location (type in Inspector)
    // ======================================================
    [Header("Manual Crack Location (Type Here)")]
    public bool useManualCrack = true;

    [Tooltip("Crack position along beam length (0..1). 0=left end, 1=right end")]
    [Range(0f, 1f)] public float manualCrackU = 0.5f;

    [Tooltip("Crack position along height (0..1). 0=bottom, 1=top")]
    [Range(0f, 1f)] public float manualCrackV = 0.1f;

    [Tooltip("How wide the crack influence is (0..1 in normalized space).")]
    [Range(0.001f, 0.5f)] public float manualCrackRadius = 0.05f;

    [Tooltip("How strong the manual crack is (0..1).")]
    [Range(0f, 1f)] public float manualCrackSeverity = 1.0f;

    [Tooltip("Higher = sharper/thinner crack")]
    [Range(0.1f, 10f)] public float manualCrackSharpness = 3.0f;

    // ======================================================
    // ✅ ADDED: BeamText support (drag your BeamText here)
    // ======================================================
    [Header("Beam Text (Drag BeamText Here)")]
    public TMP_Text beamText;
    public bool showDebugText = true;

    private Mesh mesh;
    private Vector3[] baseVertices;
    private Vector3[] vertices;
    private Color[] colors;

    private float meshMinA, meshMaxA;
    private float meshMinY, meshMaxY;

    void Start()
    {
        // Clone mesh so we edit instance
        mesh = Instantiate(GetComponent<MeshFilter>().mesh);
        GetComponent<MeshFilter>().mesh = mesh;

        baseVertices = mesh.vertices;
        vertices = new Vector3[baseVertices.Length];
        colors = new Color[baseVertices.Length];

        // Axis bounds (beam length axis)
        meshMinA = float.MaxValue;
        meshMaxA = float.MinValue;

        // Y bounds (bottom-to-top)
        meshMinY = float.MaxValue;
        meshMaxY = float.MinValue;

        for (int i = 0; i < baseVertices.Length; i++)
        {
            float a = (lengthAxis == LengthAxis.X) ? baseVertices[i].x : baseVertices[i].z;
            meshMinA = Mathf.Min(meshMinA, a);
            meshMaxA = Mathf.Max(meshMaxA, a);

            float y = baseVertices[i].y;
            meshMinY = Mathf.Min(meshMinY, y);
            meshMaxY = Mathf.Max(meshMaxY, y);
        }
    }

    void Update()
    {
        if (mesh == null) return;
        ApplyDeformationAndColor();
        UpdateBeamText(); // ✅ show text
    }

    void ApplyDeformationAndColor()
    {
        System.Array.Copy(baseVertices, vertices, baseVertices.Length);

        // --- NO LOAD: flat + one color ---
        if (loadVal <= 0.001f)
        {
            for (int i = 0; i < colors.Length; i++)
                colors[i] = noLoadColor;

            mesh.vertices = vertices;
            mesh.colors = colors;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return;
        }

        float loadRatio = Mathf.Clamp01(loadVal / maxLoadN);

        // Real deflection (mm) scaled, then converted to meters
        float realDeflectionMM = loadRatio * maxRealDeflectionMM;
        float deflectionMeters = (realDeflectionMM / 1000f) * visualExaggeration;

        // Compensate for non-uniform scaling
        float scaleY = transform.localScale.y;
        if (Mathf.Abs(scaleY) > 1e-6f)
            deflectionMeters /= scaleY;

        // Optional load point in LOCAL space (only affects coloring, not deformation)
        Vector3 loadLocal = Vector3.zero;
        if (useLoadPoint && loadPointWorld != null)
            loadLocal = transform.InverseTransformPoint(loadPointWorld.position);

        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 v = vertices[i];

            // ---------- 1) DEFORMATION (your same bending shape) ----------
            float axisVal = (lengthAxis == LengthAxis.X) ? v.x : v.z;
            float x01 = Mathf.InverseLerp(meshMinA, meshMaxA, axisVal);
            float xi = (x01 - 0.5f) * 2f;

            // bending shape: 0 at ends, 1 at center
            float bend = 1f - xi * xi;
            if (bend < 0f) bend = 0f;

            v.y -= bend * deflectionMeters;
            vertices[i] = v;

            // ---------- 2) REAL-WORLD CRACK COLOR (bottom tension + moment zone) ----------

            // Bottom tension weight: 1 at bottom, 0 at top
            float y01 = Mathf.InverseLerp(meshMinY, meshMaxY, baseVertices[i].y);
            float bottomWeight = 1f - y01;

            // Stronger preference to bottom (tension zone)
            bottomWeight = Mathf.Pow(Mathf.Clamp01(bottomWeight), bottomBias);

            // Make crack grow upward with load
            float grow = Mathf.Lerp(0f, upwardGrowth, loadRatio);
            float grownBottom = Mathf.Clamp01(bottomWeight + grow * (1f - bottomWeight) * bottomWeight);

            // Optional localization around load point (if you want a “point load” zone)
            float loadWeight = 1f;
            if (useLoadPoint && loadPointWorld != null)
            {
                Vector2 p = new Vector2(baseVertices[i].x, baseVertices[i].z);
                Vector2 lp = new Vector2(loadLocal.x, loadLocal.z);
                float d = Vector2.Distance(p, lp);

                // 1 at center, 0 after radius
                float near = 1f - Mathf.SmoothStep(0f, loadRadius, d);
                loadWeight = Mathf.Clamp01(near);
            }

            // Original intensity (KEEP)
            float intensity = bend * grownBottom * loadWeight * loadRatio;

            // ======================================================
            // ✅ MANUAL CRACK LOCATION OVERRIDE (ANYWHERE)
            // ======================================================
            if (useManualCrack)
            {
                // u along length (0..1)
                float u = x01;

                // v along height (0..1)
                float vv = Mathf.InverseLerp(meshMinY, meshMaxY, baseVertices[i].y);

                float du = u - manualCrackU;
                float dv = vv - manualCrackV;
                float dist = Mathf.Sqrt(du * du + dv * dv);

                float r = Mathf.Max(0.0001f, manualCrackRadius);
                float t = Mathf.Clamp01(1f - (dist / r));      // 1 at center, 0 at edge
                t = Mathf.Pow(t, manualCrackSharpness);        // sharp
                t *= manualCrackSeverity;                      // severity

                // Combine with your existing intensity
                intensity = Mathf.Max(intensity, t);
            }

            colors[i] = Color.Lerp(Color.green, Color.red, Mathf.Clamp01(intensity));
        }

        mesh.vertices = vertices;
        mesh.colors = colors;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }

    void UpdateBeamText()
    {
        if (!showDebugText) return;
        if (beamText == null) return;

        float loadRatio = Mathf.Clamp01(loadVal / maxLoadN);

        beamText.text =
            $"Load: {loadVal:F0} N\n" +
            $"Load Ratio: {loadRatio:F2}\n" +
            $"Manual Crack: {(useManualCrack ? "ON" : "OFF")}\n" +
            $"Crack U: {manualCrackU:F2}  V: {manualCrackV:F2}\n" +
            $"Radius: {manualCrackRadius:F3}  Sev: {manualCrackSeverity:F2}";
    }
}