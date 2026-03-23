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
    [SerializeField] private float aimPlaneHeightOffset = 0f;
    [SerializeField] private float turnSpeed = 18f;

    private HitscanWeapon _weapon;
    private Health _health;
    private StarterAssetsInputs _inputs;
    private Transform _weaponVisualRoot;

    private void Awake()
    {
        _weapon = GetComponent<HitscanWeapon>();
        _health = GetComponent<Health>();
        _inputs = GetComponent<StarterAssetsInputs>();

        EnsureWeaponVisual();
        EnsureCrosshair();
    }

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (_inputs != null)
        {
            _inputs.cursorLocked = true;
            _inputs.cursorInputForLook = true;
        }
    }

    private void Update()
    {
        if (_health != null && _health.IsDead)
        {
            return;
        }

        if (aimCamera == null)
        {
            aimCamera = Camera.main;
        }

        if (aimCamera == null || !TryGetAimData(aimCamera, out var origin, out var aimPoint))
        {
            return;
        }

        var shotDirection = aimPoint - origin;
        RotateTowardsShot(shotDirection);
        RotateWeaponVisual(shotDirection);

        if (!IsFirePressed())
        {
            return;
        }

        _weapon.TryFire(origin, shotDirection);
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

    private bool TryGetAimData(Camera cameraToUse, out Vector3 origin, out Vector3 aimPoint)
    {
        origin = _weapon.GetFireOrigin();
        var ray = cameraToUse.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        var hits = Physics.RaycastAll(ray, _weapon.Range * 2f, _weapon.HitMask, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

        foreach (var hit in hits)
        {
            if (hit.collider.transform.IsChildOf(transform))
            {
                continue;
            }

            aimPoint = hit.point;
            return true;
        }

        var plane = new Plane(Vector3.up, new Vector3(0f, origin.y + aimPlaneHeightOffset, 0f));
        if (plane.Raycast(ray, out var distance))
        {
            aimPoint = ray.GetPoint(distance);
            return true;
        }

        aimPoint = origin + ray.direction.normalized * _weapon.Range;
        return true;
    }

    private void RotateTowardsShot(Vector3 shotDirection)
    {
        var flatDirection = Vector3.ProjectOnPlane(shotDirection, Vector3.up);
        if (flatDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        var targetRotation = Quaternion.LookRotation(flatDirection.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * turnSpeed);
    }

    private void RotateWeaponVisual(Vector3 shotDirection)
    {
        if (_weaponVisualRoot == null || shotDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        var targetRotation = Quaternion.LookRotation(shotDirection.normalized, Vector3.up);
        _weaponVisualRoot.rotation = Quaternion.Slerp(_weaponVisualRoot.rotation, targetRotation, Time.deltaTime * (turnSpeed * 1.35f));
    }

    private void EnsureWeaponVisual()
    {
        _weaponVisualRoot = transform.Find("RuntimeWeaponVisual");
        if (_weaponVisualRoot != null)
        {
            return;
        }

        var root = new GameObject("RuntimeWeaponVisual");
        root.transform.SetParent(transform, false);
        root.transform.localPosition = new Vector3(0.32f, 1.02f, 0.08f);
        root.transform.localRotation = Quaternion.identity;
        _weaponVisualRoot = root.transform;

        var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "Body";
        body.transform.SetParent(root.transform, false);
        body.transform.localPosition = new Vector3(0f, 0f, 0.22f);
        body.transform.localScale = new Vector3(0.12f, 0.12f, 0.52f);
        Destroy(body.GetComponent<Collider>());

        var grip = GameObject.CreatePrimitive(PrimitiveType.Cube);
        grip.name = "Grip";
        grip.transform.SetParent(root.transform, false);
        grip.transform.localPosition = new Vector3(0f, -0.12f, 0.02f);
        grip.transform.localRotation = Quaternion.Euler(28f, 0f, 0f);
        grip.transform.localScale = new Vector3(0.12f, 0.2f, 0.1f);
        Destroy(grip.GetComponent<Collider>());

        var barrel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        barrel.name = "Barrel";
        barrel.transform.SetParent(root.transform, false);
        barrel.transform.localPosition = new Vector3(0f, 0f, 0.48f);
        barrel.transform.localScale = new Vector3(0.06f, 0.06f, 0.28f);
        Destroy(barrel.GetComponent<Collider>());

        ApplyWeaponMaterial(body, new Color(0.13f, 0.13f, 0.13f));
        ApplyWeaponMaterial(grip, new Color(0.26f, 0.18f, 0.12f));
        ApplyWeaponMaterial(barrel, new Color(0.18f, 0.18f, 0.18f));
    }

    private void EnsureCrosshair()
    {
        if (GetComponent<CombatCrosshair>() != null)
        {
            return;
        }

        gameObject.AddComponent<CombatCrosshair>();
    }

    private static void ApplyWeaponMaterial(GameObject target, Color color)
    {
        var renderer = target.GetComponent<MeshRenderer>();
        if (renderer == null)
        {
            return;
        }

        var shader = Shader.Find("Standard");
        if (shader == null)
        {
            return;
        }

        var material = new Material(shader)
        {
            color = color,
        };
        renderer.sharedMaterial = material;
    }
}
