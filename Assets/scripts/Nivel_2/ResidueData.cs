using UnityEngine;

public enum ResidueKind
{
    Vidrio,
    Papel,
    Plastico
}

[CreateAssetMenu(fileName = "ResidueData", menuName = "Game/Residue")]
public class ResidueData : ScriptableObject
{
    public string residueName;

    //public Sprite icon;       
    public GameObject prefab; 
    public ResidueKind kind;

    public int scoreValue = 10;
    public int penaltyValue = -5;

    [Tooltip("Escala propia de este residuo (se multiplica por 'Escala Residuos' del manager). Util si una botella se ve mas grande que una hoja.")]
    public float escala = 1f;
}
