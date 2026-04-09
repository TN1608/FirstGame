using UnityEngine;

/// <summary>
/// Optional editor/preview data container used by ProceduralWorldGenerator.
/// </summary>
[System.Serializable]
public class NoisePreviewData
{
    public Texture2D texture;
    public int previewSize = 64;
    public bool applyRidgeFold;
    public string layerName;
}

