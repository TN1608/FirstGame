using UnityEngine;

public class HoverIndicator : MonoBehaviour
{
    private LineRenderer lr;

    void Awake()
    {
        lr = gameObject.AddComponent<LineRenderer>();
        lr.loop = true;
        lr.positionCount = 4;
        lr.startWidth = 0.04f;
        lr.endWidth = 0.04f;
        lr.useWorldSpace = false;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = new Color(1f, 1f, 1f, 0.8f);
        lr.endColor   = new Color(1f, 1f, 1f, 0.8f);
        lr.sortingOrder = 99;

        // Diamond shape khớp với isometric cell (Cell Size X=1, Y=0.5)
        lr.SetPosition(0, new Vector3( 0f,    0.25f, 0));  // top
        lr.SetPosition(1, new Vector3( 0.5f,  0f,    0));  // right
        lr.SetPosition(2, new Vector3( 0f,   -0.25f, 0));  // bottom
        lr.SetPosition(3, new Vector3(-0.5f,  0f,    0));  // left
    }
}