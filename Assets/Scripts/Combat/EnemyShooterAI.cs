using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(HitscanWeapon))]
[RequireComponent(typeof(Health))]
public class EnemyShooterAI : MonoBehaviour
{
    [SerializeField] private float detectionRange = 55f;
    [SerializeField] private float attackRange = 55f;
    [SerializeField] private float turnSpeed = 6f;
    [SerializeField] private float aimHeight = 1.2f;
    [SerializeField] private LayerMask lineOfSightMask = Physics.DefaultRaycastLayers;

    private HitscanWeapon _weapon;
    private Health _health;
    private Health _playerHealth;
    private Transform _playerTransform;
    private CharacterController _playerCharacterController;
    private Collider _playerCollider;
    private float _targetRefreshTimer;
    private Transform _weaponVisualRoot;
    private Transform _runtimeFirePoint;

    private void Awake()
    {
        _weapon = GetComponent<HitscanWeapon>();
        _health = GetComponent<Health>();
        attackRange = Mathf.Max(attackRange, detectionRange);
        _weapon.Damage = 5f;
        _weapon.HitRadius = 0.18f;
        EnsureWeaponVisual();
    }

    private void Update()
    {
        if (_health.IsDead)
        {
            return;
        }

        RefreshTarget();
        if (_playerTransform == null || _playerHealth == null || _playerHealth.IsDead)
        {
            return;
        }

        var toPlayer = _playerTransform.position - transform.position;
        var distance = toPlayer.magnitude;
        if (distance > detectionRange)
        {
            return;
        }

        RotateTowardsPlayer(toPlayer);
        RotateWeaponVisual(toPlayer);

        if (distance > attackRange)
        {
            return;
        }

        var origin = _weapon.GetFireOrigin();
        var targetPoint = GetPlayerTargetPoint();
        _weapon.TryFire(origin, targetPoint - origin);
    }

    private void RefreshTarget()
    {
        _targetRefreshTimer -= Time.deltaTime;
        if (_targetRefreshTimer > 0f && _playerTransform != null)
        {
            return;
        }

        _targetRefreshTimer = 1f;
        foreach (var candidate in FindObjectsByType<Health>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (candidate.Team != CombatTeam.Player)
            {
                continue;
            }

            _playerHealth = candidate;
            _playerTransform = candidate.transform;
            _playerCharacterController = candidate.GetComponent<CharacterController>();
            _playerCollider = candidate.GetComponent<Collider>();
            return;
        }

        _playerHealth = null;
        _playerTransform = null;
        _playerCharacterController = null;
        _playerCollider = null;
    }

    private void RotateTowardsPlayer(Vector3 toPlayer)
    {
        var flatDirection = Vector3.ProjectOnPlane(toPlayer, Vector3.up);
        if (flatDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        var targetRotation = Quaternion.LookRotation(flatDirection.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * turnSpeed);
    }

    private void RotateWeaponVisual(Vector3 toPlayer)
    {
        if (_weaponVisualRoot == null)
        {
            return;
        }

        if (toPlayer.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        var targetRotation = Quaternion.LookRotation(toPlayer.normalized, Vector3.up);
        _weaponVisualRoot.rotation = Quaternion.Slerp(_weaponVisualRoot.rotation, targetRotation, Time.deltaTime * (turnSpeed * 1.35f));
    }

    private void EnsureWeaponVisual()
    {
        _weaponVisualRoot = transform.Find("RuntimeWeaponVisual");
        if (_weaponVisualRoot == null)
        {
            var root = new GameObject("RuntimeWeaponVisual");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = new Vector3(0.24f, 1.1f, 0.08f);
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

        _runtimeFirePoint = _weaponVisualRoot.Find("RuntimeFirePoint");
        if (_runtimeFirePoint == null)
        {
            var firePoint = new GameObject("RuntimeFirePoint");
            firePoint.transform.SetParent(_weaponVisualRoot, false);
            firePoint.transform.localPosition = new Vector3(0f, 0f, 0.68f);
            firePoint.transform.localRotation = Quaternion.identity;
            _runtimeFirePoint = firePoint.transform;
        }

        if (_weapon != null)
        {
            _weapon.FirePoint = _runtimeFirePoint;
        }
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

    private Vector3 GetPlayerTargetPoint()
    {
        if (_playerCharacterController != null)
        {
            return _playerCharacterController.bounds.center;
        }

        if (_playerCollider != null)
        {
            return _playerCollider.bounds.center;
        }

        return _playerTransform.position + Vector3.up * aimHeight;
    }
}
