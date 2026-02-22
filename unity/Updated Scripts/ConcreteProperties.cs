using UnityEngine;

public class ConcreteProperties : MonoBehaviour
{
    [Header("Concrete Properties (typical units)")]
    public float fc_MPa = 25f;         // compressive strength magnitude (MPa)
    public bool compressionIsNegative = true;

    public float E_GPa = 30f;          // elastic modulus (GPa)
    public float nu = 0.2f;            // Poisson ratio
    public float density = 2400f;      // kg/m^3

    [Header("Steel (Rebar)")]
    public float fy_MPa = 314f;        // yield strength (MPa)

    public float FcSigned()
    {
        return compressionIsNegative ? -Mathf.Abs(fc_MPa) : Mathf.Abs(fc_MPa);
    }
}