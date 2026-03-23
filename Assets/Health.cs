using System;
using UnityEngine;

public enum CombatTeam
{
    Neutral,
    Player,
    Enemy,
}

public class Health : MonoBehaviour
{
    [field: SerializeField] public CombatTeam Team { get; set; } = CombatTeam.Neutral;
    [field: SerializeField] public float MaxHealth { get; set; } = 100f;
    [field: SerializeField] public bool DestroyOnDeath { get; set; } = true;

    public float CurrentHealth { get; private set; }
    public bool IsDead { get; private set; }

    public event Action<Health> Died;
    public event Action<Health> Damaged;

    private void Awake()
    {
        CurrentHealth = MaxHealth;
    }

    public bool ApplyDamage(float amount, GameObject instigator = null, CombatTeam attackerTeam = CombatTeam.Neutral)
    {
        if (IsDead || amount <= 0f)
        {
            return false;
        }

        if (attackerTeam != CombatTeam.Neutral && attackerTeam == Team)
        {
            return false;
        }

        CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
        Damaged?.Invoke(this);

        if (CurrentHealth > 0f)
        {
            return true;
        }

        IsDead = true;
        Died?.Invoke(this);

        if (DestroyOnDeath)
        {
            Destroy(gameObject);
        }

        return true;
    }

    public void RestoreFullHealth()
    {
        IsDead = false;
        CurrentHealth = MaxHealth;
    }

    private void OnValidate()
    {
        MaxHealth = Mathf.Max(1f, MaxHealth);

        if (!Application.isPlaying)
        {
            CurrentHealth = MaxHealth;
        }
    }
}
