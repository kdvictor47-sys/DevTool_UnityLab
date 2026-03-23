using System;
using UnityEngine;

[DisallowMultipleComponent]
public class HitscanWeapon : MonoBehaviour
{
    [field: SerializeField] public CombatTeam Team { get; set; } = CombatTeam.Neutral;
    [field: SerializeField] public Transform FirePoint { get; set; }
    [field: SerializeField] public float FireRate { get; set; } = 4f;
    [field: SerializeField] public float Damage { get; set; } = 20f;
    [field: SerializeField] public float Range { get; set; } = 80f;
    [field: SerializeField] public LayerMask HitMask { get; set; } = Physics.DefaultRaycastLayers;
    [field: SerializeField] public bool DrawDebugShots { get; set; } = false;

    public float DebugShotDuration = 0.15f;

    private float _nextShotTime;

    public bool TryFire(Vector3 origin, Vector3 direction)
    {
        if (Time.time < _nextShotTime || direction.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        _nextShotTime = Time.time + (1f / Mathf.Max(0.01f, FireRate));
        direction.Normalize();

        var ray = new Ray(origin, direction);
        var hits = Physics.RaycastAll(ray, Range, HitMask, QueryTriggerInteraction.Ignore);
        Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

        var shotEnd = origin + direction * Range;

        foreach (var hit in hits)
        {
            if (hit.collider.transform.IsChildOf(transform))
            {
                continue;
            }

            shotEnd = hit.point;
            var health = hit.collider.GetComponentInParent<Health>();

            if (health == null)
            {
                break;
            }

            if (health.Team == Team)
            {
                break;
            }

            health.ApplyDamage(Damage, gameObject, Team);
            break;
        }

        if (DrawDebugShots)
        {
            Debug.DrawLine(origin, shotEnd, Team == CombatTeam.Enemy ? Color.red : Color.green, DebugShotDuration);
        }

        return true;
    }

    public Vector3 GetFireOrigin()
    {
        if (FirePoint != null)
        {
            return FirePoint.position;
        }

        return transform.position + Vector3.up * 1.2f;
    }
}
