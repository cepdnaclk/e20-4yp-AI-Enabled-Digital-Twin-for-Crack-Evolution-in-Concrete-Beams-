using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class SegmentedBeamGenerator : MonoBehaviour
{
    [Header("Beam Dimensions (meters)")]
    public float length = 1f;   
    public float height = 0.75f;   // 300 mm
    public float width  = 0.8f;   // 150 mm

    [Header("Resolution")]
    [Range(1, 500)]
    public int segments = 68;      // increase for smoother mapping

    private void Awake()
    {
        var mesh = GenerateSegmentedBeam(segments, length, height, width);
        GetComponent<MeshFilter>().mesh = mesh;
    }

    Mesh GenerateSegmentedBeam(int seg, float L, float H, float W)
    {
        Mesh m = new Mesh();
        m.name = "SegmentedBeam";

        List<Vector3> vList = new List<Vector3>();
        List<int> tList = new List<int>();
        List<Vector3> nList = new List<Vector3>(); // normals (optional)
        List<Vector2> uvList = new List<Vector2>(); // UVs (helpful later)

        float hl = L * 0.5f;
        float hh = H * 0.5f;
        float hw = W * 0.5f;

        // Adds a quad with UVs (0..1) and a face normal
        void AddQuad(Vector3 bl, Vector3 tl, Vector3 tr, Vector3 br,
                     Vector3 normal,
                     Vector2 uvBL, Vector2 uvTL, Vector2 uvTR, Vector2 uvBR)
        {
            int idx = vList.Count;

            vList.Add(bl); vList.Add(tl); vList.Add(tr); vList.Add(br);

            nList.Add(normal); nList.Add(normal); nList.Add(normal); nList.Add(normal);

            uvList.Add(uvBL); uvList.Add(uvTL); uvList.Add(uvTR); uvList.Add(uvBR);

            // 2 triangles
            tList.Add(idx + 0); tList.Add(idx + 1); tList.Add(idx + 2);
            tList.Add(idx + 0); tList.Add(idx + 2); tList.Add(idx + 3);
        }

        // Build segmented faces: Top, Bottom, Front, Back
        for (int i = 0; i < seg; i++)
        {
            float t0 = (float)i / seg;
            float t1 = (float)(i + 1) / seg;

            float x0 = Mathf.Lerp(-hl, hl, t0);
            float x1 = Mathf.Lerp(-hl, hl, t1);

            // We'll map UV.x along length (0..1) for these faces
            // UV.y depends on face direction
            // TOP (normal up)
            AddQuad(
                new Vector3(x0, hh, -hw), new Vector3(x0, hh, hw),
                new Vector3(x1, hh, hw),  new Vector3(x1, hh, -hw),
                Vector3.up,
                new Vector2(t0, 0), new Vector2(t0, 1), new Vector2(t1, 1), new Vector2(t1, 0)
            );

            // BOTTOM (normal down)
            AddQuad(
                new Vector3(x0, -hh, hw), new Vector3(x0, -hh, -hw),
                new Vector3(x1, -hh, -hw), new Vector3(x1, -hh, hw),
                Vector3.down,
                new Vector2(t0, 0), new Vector2(t0, 1), new Vector2(t1, 1), new Vector2(t1, 0)
            );

            // FRONT (z = -hw, normal forward? actually negative Z face -> Vector3.back)
            AddQuad(
                new Vector3(x0, -hh, -hw), new Vector3(x0, hh, -hw),
                new Vector3(x1, hh, -hw),  new Vector3(x1, -hh, -hw),
                Vector3.back,
                new Vector2(t0, 0), new Vector2(t0, 1), new Vector2(t1, 1), new Vector2(t1, 0)
            );

            // BACK (z = +hw, normal Vector3.forward)
            AddQuad(
                new Vector3(x1, -hh, hw), new Vector3(x1, hh, hw),
                new Vector3(x0, hh, hw),  new Vector3(x0, -hh, hw),
                Vector3.forward,
                new Vector2(t1, 0), new Vector2(t1, 1), new Vector2(t0, 1), new Vector2(t0, 0)
            );
        }

        // Caps (Left and Right)
        // Left cap x = -hl
        AddQuad(
            new Vector3(-hl, -hh, hw), new Vector3(-hl, hh, hw),
            new Vector3(-hl, hh, -hw), new Vector3(-hl, -hh, -hw),
            Vector3.left,
            new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0)
        );

        // Right cap x = +hl
        AddQuad(
            new Vector3(hl, -hh, -hw), new Vector3(hl, hh, -hw),
            new Vector3(hl, hh, hw),   new Vector3(hl, -hh, hw),
            Vector3.right,
            new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0)
        );

        m.SetVertices(vList);
        m.SetTriangles(tList, 0);
        m.SetNormals(nList);
        m.SetUVs(0, uvList);

        // Safety
        m.RecalculateBounds();

        return m;
    }
}