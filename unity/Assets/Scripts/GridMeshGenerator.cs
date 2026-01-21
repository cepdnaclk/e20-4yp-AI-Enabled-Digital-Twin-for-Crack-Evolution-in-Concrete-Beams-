using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class GridMeshGenerator : MonoBehaviour
{
    [Header("Mesh Settings")]
    [Tooltip("Number of subdivisions. Higher = smoother colors.")]
    public int resolution = 50;
    public Vector2 size = new Vector2(1, 1); // Keep X=1 for normalized width

    void Awake()
    {
        GenerateMesh();
    }

    public void GenerateMesh()
    {
        MeshFilter mf = GetComponent<MeshFilter>();
        Mesh mesh = new Mesh();

        // Arrays for vertex data
        Vector3[] vertices = new Vector3[(resolution + 1) * (resolution + 1)];
        Vector2[] uv = new Vector2[vertices.Length];
        int[] triangles = new int[resolution * resolution * 6];

        // 1. Generate Vertices
        // We generate them centered at (0,0). 
        // X will range from -0.5 to +0.5
        for (int i = 0, y = 0; y <= resolution; y++)
        {
            for (int x = 0; x <= resolution; x++, i++)
            {
                vertices[i] = new Vector3(
                    ((float)x / resolution - 0.5f) * size.x, // X: -0.5 to 0.5
                    ((float)y / resolution - 0.5f) * size.y, // Y: -0.5 to 0.5
                    0);
                uv[i] = new Vector2((float)x / resolution, (float)y / resolution);
            }
        }

        // 2. Generate Triangles (Connect the dots)
        int ti = 0;
        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                int val = y * (resolution + 1) + x;

                // First triangle
                triangles[ti] = val;
                triangles[ti + 1] = val + resolution + 1;
                triangles[ti + 2] = val + 1;

                // Second triangle
                triangles[ti + 3] = val + 1;
                triangles[ti + 4] = val + resolution + 1;
                triangles[ti + 5] = val + resolution + 2;
                ti += 6;
            }
        }

        // 3. Apply to Mesh
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uv;
        mesh.RecalculateNormals();

        mf.mesh = mesh;
    }
}