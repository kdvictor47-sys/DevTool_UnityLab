using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(HitscanWeapon))]
[RequireComponent(typeof(Health))]
public class EnemyShooterAI : MonoBehaviour
{
    [SerializeField] private float detectionRange = 55f;
    [SerializeField] private float attackRange = 40f;
    [SerializeField] private float turnSpeed = 6f;
    [SerializeField] private float aimHeight = 1.2f;
    [SerializeField] private LayerMask lineOfSightMask = Physics.DefaultRaycastLayers;

    private HitscanWeapon _weapon;
    private Health _health;
    private Health _playerHealth;
    private Transform _playerTransform;
    private float _targetRefreshTimer;

    private void Awake()
    {
        _weapon = GetComponent<HitscanWeapon>();
        _health = GetComponent<Health>();
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

        if (distance > attackRange || !HasLineOfSight())
        {
            return;
        }

        var origin = _weapon.GetFireOrigin();
        var targetPoint = _playerTransform.position + Vector3.up * aimHeight;
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
            return;
        }

        _playerHealth = null;
        _playerTransform = null;
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

    private bool HasLineOfSight()
    {
        var origin = _weapon.GetFireOrigin();
        var target = _playerTransform.position + Vector3.up * aimHeight;
        var direction = target - origin;
        var distance = direction.magnitude;

        if (distance <= 0.001f)
        {
            return false;
        }

        direction /= distance;
        var hits = Physics.RaycastAll(origin, direction, distance, lineOfSightMask, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

        foreach (var hit in hits)
        {
            if (hit.collider.transform.IsChildOf(transform))
            {
                continue;
            }

            return hit.collider.GetComponentInParent<Health>() == _playerHealth;
        }

        return false;
    }
}
