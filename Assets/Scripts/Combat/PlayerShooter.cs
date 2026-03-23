using StarterAssets;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
[RequireComponent(typeof(HitscanWeapon))]
public class PlayerShooter : MonoBehaviour
{
    [SerializeField] private Camera aimCamera;
    [SerializeField] private bool holdToFire = true;

    private HitscanWeapon _weapon;
    private Health _health;
    private StarterAssetsInputs _inputs;

    private void Awake()
    {
        _weapon = GetComponent<HitscanWeapon>();
        _health = GetComponent<Health>();
        _inputs = GetComponent<StarterAssetsInputs>();
    }

    private void Update()
    {
        if (_health != null && _health.IsDead)
        {
            return;
        }

        if (_inputs != null && !_inputs.cursorLocked)
        {
            return;
        }

        if (!IsFirePressed())
        {
            return;
        }

        if (aimCamera == null)
        {
            aimCamera = Camera.main;
        }

        if (aimCamera == null)
        {
            return;
        }

        var ray = aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        _weapon.TryFire(ray.origin, ray.direction);
    }

    private bool IsFirePressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            return holdToFire ? Mouse.current.leftButton.isPressed : Mouse.current.leftButton.wasPressedThisFrame;
        }
#endif
        return holdToFire ? Input.GetMouseButton(0) : Input.GetMouseButtonDown(0);
    }
}
