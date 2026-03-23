using UnityEngine;

[DisallowMultipleComponent]
public class CombatCrosshair : MonoBehaviour
{
    [SerializeField] private float size = 6f;
    [SerializeField] private float thickness = 2f;
    [SerializeField] private float gap = 4f;
    [SerializeField] private Color color = new Color(1f, 1f, 1f, 0.95f);

    private Texture2D _pixel;

    private void Awake()
    {
        _pixel = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        _pixel.SetPixel(0, 0, Color.white);
        _pixel.Apply();
    }

    private void OnGUI()
    {
        if (_pixel == null)
        {
            return;
        }

        GUI.color = color;

        var centerX = Screen.width * 0.5f;
        var centerY = Screen.height * 0.5f;

        DrawRect(new Rect(centerX - thickness * 0.5f, centerY - gap - size, thickness, size));
        DrawRect(new Rect(centerX - thickness * 0.5f, centerY + gap, thickness, size));
        DrawRect(new Rect(centerX - gap - size, centerY - thickness * 0.5f, size, thickness));
        DrawRect(new Rect(centerX + gap, centerY - thickness * 0.5f, size, thickness));
    }

    private void OnDestroy()
    {
        if (_pixel != null)
        {
            Destroy(_pixel);
        }
    }

    private void DrawRect(Rect rect)
    {
        GUI.DrawTexture(rect, _pixel);
    }
}
