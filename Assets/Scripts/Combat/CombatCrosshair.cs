using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class CombatCrosshair : MonoBehaviour
{
    [SerializeField] private float size = 6f;
    [SerializeField] private float thickness = 2f;
    [SerializeField] private float gap = 4f;
    [SerializeField] private Color color = new Color(1f, 1f, 1f, 0.95f);
    [SerializeField] private float healthBarWidth = 90f;
    [SerializeField] private float healthBarHeight = 10f;
    [SerializeField] private float healthBarBorder = 2f;
    [SerializeField] private Vector3 healthBarOffset = new Vector3(0f, 1.95f, 0f);

    private Texture2D _pixel;
    private Health _health;
    private CharacterController _characterController;
    private GUIStyle _deathStyle;
    private GUIStyle _buttonStyle;
    private float _damageFlashAlpha;

    private void Awake()
    {
        _health = GetComponent<Health>();
        _characterController = GetComponent<CharacterController>();
        _pixel = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        _pixel.SetPixel(0, 0, Color.white);
        _pixel.Apply();
    }

    private void OnEnable()
    {
        if (_health != null)
        {
            _health.Damaged += HandleDamaged;
            _health.Died += HandleDied;
        }
    }

    private void OnDisable()
    {
        if (_health != null)
        {
            _health.Damaged -= HandleDamaged;
            _health.Died -= HandleDied;
        }
    }

    private void LateUpdate()
    {
        _damageFlashAlpha = Mathf.MoveTowards(_damageFlashAlpha, 0f, Time.deltaTime * 1.8f);
    }

    private void OnGUI()
    {
        if (_pixel == null)
        {
            return;
        }

        EnsureGuiStyles();

        DrawHeadHealthBar();

        if (_damageFlashAlpha > 0f && _health != null && !_health.IsDead)
        {
            DrawRect(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0.85f, 0.05f, 0.05f, _damageFlashAlpha));
        }

        if (_health == null || _health.IsDead)
        {
            DrawDeathScreen();
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

    private void DrawHeadHealthBar()
    {
        if (_health == null || _health.IsDead)
        {
            return;
        }

        var cameraToUse = Camera.main;
        if (cameraToUse == null)
        {
            return;
        }

        var worldPoint = GetHealthBarWorldPoint();
        var screenPoint = cameraToUse.WorldToScreenPoint(worldPoint);
        if (screenPoint.z <= 0f)
        {
            return;
        }

        var backgroundRect = new Rect(
            screenPoint.x - (healthBarWidth * 0.5f),
            Screen.height - screenPoint.y,
            healthBarWidth,
            healthBarHeight);

        var ratio = Mathf.Clamp01(_health.CurrentHealth / _health.MaxHealth);
        var fillRect = new Rect(
            backgroundRect.x + healthBarBorder,
            backgroundRect.y + healthBarBorder,
            (backgroundRect.width - (healthBarBorder * 2f)) * ratio,
            backgroundRect.height - (healthBarBorder * 2f));

        DrawRect(backgroundRect, new Color(0.18f, 0f, 0f, 0.35f));
        DrawRect(fillRect, new Color(0.92f, 0.12f, 0.12f, 0.78f));
    }

    private Vector3 GetHealthBarWorldPoint()
    {
        if (_characterController != null)
        {
            var bounds = _characterController.bounds;
            return new Vector3(bounds.center.x, bounds.min.y, bounds.center.z) + healthBarOffset;
        }

        return transform.position + healthBarOffset;
    }

    private void DrawDeathScreen()
    {
        if (_health == null || !_health.IsDead)
        {
            return;
        }

        DrawRect(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.78f));

        var titleRect = new Rect(0f, Screen.height * 0.32f, Screen.width, 60f);
        GUI.Label(titleRect, "YOU DIED", _deathStyle);

        var buttonRect = new Rect((Screen.width * 0.5f) - 75f, Screen.height * 0.5f, 150f, 42f);
        if (GUI.Button(buttonRect, "Retry", _buttonStyle))
        {
            var activeScene = SceneManager.GetActiveScene();
            var sceneToLoad = string.IsNullOrWhiteSpace(activeScene.path) ? activeScene.name : activeScene.path;
            SceneManager.LoadScene(sceneToLoad);
        }
    }

    private void DrawRect(Rect rect, Color tint)
    {
        var previousColor = GUI.color;
        GUI.color = tint;
        GUI.DrawTexture(rect, _pixel);
        GUI.color = previousColor;
    }

    private void HandleDamaged(Health _)
    {
        _damageFlashAlpha = 0.28f;
    }

    private void HandleDied(Health _)
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void EnsureGuiStyles()
    {
        if (_deathStyle == null)
        {
            _deathStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 38,
                fontStyle = FontStyle.Bold
            };
            _deathStyle.normal.textColor = Color.white;
        }

        if (_buttonStyle == null)
        {
            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 18,
                fontStyle = FontStyle.Bold
            };
        }
    }
}
