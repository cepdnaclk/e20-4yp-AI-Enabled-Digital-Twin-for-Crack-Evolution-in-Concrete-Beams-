using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class StationExcelTableUI : MonoBehaviour
{
    [Header("References")]
    public DigitalTwinVisualizer visualizer;
    public RectTransform gridRoot;
    public TMP_InputField cellPrefab;

    [Header("Excel Layout")]
    public float cellWidth = 140f;
    public float cellHeight = 20f;

    // Table size: header + segments rows, 5 columns
    private int rows;
    private const int cols = 5;

    private TMP_InputField[,] cells;

    void Start()
    {
        if (visualizer == null || gridRoot == null || cellPrefab == null)
        {
            Debug.LogError("Assign visualizer, gridRoot, and cellPrefab.");
            return;
        }

        BuildGrid();
    }

    void Update()
    {
        if (cells == null) return;
        UpdateValues();
    }

    void BuildGrid()
    {
        rows = visualizer.segments + 1; // +1 header row
        cells = new TMP_InputField[rows, cols];

        // Configure grid layout
        var grid = gridRoot.GetComponent<GridLayoutGroup>();
        if (grid == null) grid = gridRoot.gameObject.AddComponent<GridLayoutGroup>();

        grid.cellSize = new Vector2(cellWidth, cellHeight);
        grid.spacing = new Vector2(6f, 6f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = cols;

        // Clear existing children
        for (int i = gridRoot.childCount - 1; i >= 0; i--)
            Destroy(gridRoot.GetChild(i).gameObject);

        // Create cells
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                var cell = Instantiate(cellPrefab, gridRoot);
                cell.readOnly = true;     // ✅ behave like display table
                cell.text = "";
                cells[r, c] = cell;
            }
        }

        // Header
        SetCell(0, 0, "Pt");
        SetCell(0, 1, "x (m)");
        SetCell(0, 2, "Stress (MPa)");
        SetCell(0, 3, "Strain (με)");
        SetCell(0, 4, "Disp (mm)");
    }

    void UpdateValues()
    {
        // Ensure arrays exist
        visualizer.ForceComputeStationsForUI();

        for (int i = 0; i < visualizer.segments; i++)
        {
            int r = i + 1;

            float x = visualizer.UI_stationX[i];
            float stressMPa = visualizer.UI_stationStress[i] / 1e6f;
            float strainMicro = visualizer.UI_stationStrain[i] * 1e6f;
            float dispMM = visualizer.UI_stationDisp[i] * 1000f;

            SetCell(r, 0, (i + 1).ToString());
            SetCell(r, 1, x.ToString("F3"));
            SetCell(r, 2, stressMPa.ToString("F2"));
            SetCell(r, 3, strainMicro.ToString("F1"));
            SetCell(r, 4, dispMM.ToString("F2"));
        }
    }

    void SetCell(int r, int c, string value)
    {
        if (cells[r, c].text != value)
            cells[r, c].text = value;
    }
}