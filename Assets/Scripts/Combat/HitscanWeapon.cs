using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class HitscanWeapon : MonoBehaviour
{
    [field: SerializeField] public CombatTeam Team { get; set; } = CombatTeam.Neutral;
    [field: SerializeField] public Transform FirePoint { get; set; }
    [field: SerializeField] public float FireRate { get; set; } = 4f;
    [field: SerializeField] public float Damage { get; set; } = 20f;
    [field: SerializeField] public float Range { get; set; } = 80f;
    [field: SerializeField] public float HitRadius { get; set; } = 0f;
    [field: SerializeField] public LayerMask HitMask { get; set; } = Physics.DefaultRaycastLayers;
    [field: SerializeField] public bool DrawDebugShots { get; set; } = false;
    [field: SerializeField] public bool ShowTracer { get; set; } = true;
    [field: SerializeField] public float TracerDuration { get; set; } = 0.06f;
    [field: SerializeField] public float TracerWidth { get; set; } = 0.04f;

    public float DebugShotDuration = 0.15f;

    private float _nextShotTime;

    private void Awake()
    {
        ShowTracer = true;
    }

    public bool TryFire(Vector3 origin, Vector3 direction)
    {
        if (Time.time < _nextShotTime || direction.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        _nextShotTime = Time.time + (1f / Mathf.Max(0.01f, FireRate));
        direction.Normalize();

        var ray = new Ray(origin, direction);
        var hits = HitRadius > 0.001f
            ? Physics.SphereCastAll(ray, HitRadius, Range, HitMask, QueryTriggerInteraction.Ignore)
            : Physics.RaycastAll(ray, Range, HitMask, QueryTriggerInteraction.Ignore);
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

        if (ShowTracer)
        {
            StartCoroutine(ShowTracerLine(origin, shotEnd));
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

    private IEnumerator ShowTracerLine(Vector3 origin, Vector3 shotEnd)
    {
        var tracer = new GameObject("ShotTracer");
        tracer.transform.SetParent(null);

        var lineRenderer = tracer.AddComponent<LineRenderer>();
        lineRenderer.useWorldSpace = true;
        lineRenderer.positionCount = 2;
        lineRenderer.SetPosition(0, origin);
        lineRenderer.SetPosition(1, shotEnd);
        lineRenderer.startWidth = TracerWidth;
        lineRenderer.endWidth = TracerWidth * 0.55f;
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startColor = GetTracerColor();
        lineRenderer.endColor = new Color(lineRenderer.startColor.r, lineRenderer.startColor.g, lineRenderer.startColor.b, 0.1f);
        lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;

        yield return new WaitForSeconds(TracerDuration);

        if (tracer != null)
        {
            Destroy(tracer);
        }
    }

    private Color GetTracerColor()
    {
        return Team == CombatTeam.Enemy
            ? new Color(1f, 0.28f, 0.18f, 0.9f)
            : new Color(0.95f, 0.87f, 0.28f, 0.95f);
    }
}
