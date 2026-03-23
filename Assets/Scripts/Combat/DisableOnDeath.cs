using UnityEngine;

[DisallowMultipleComponent]
public class DisableOnDeath : MonoBehaviour
{
    public Health Health;
    public Behaviour[] BehavioursToDisable;
    public Collider[] CollidersToDisable;
    public CharacterController CharacterControllerToDisable;

    private void Awake()
    {
        if (Health == null)
        {
            Health = GetComponent<Health>();
        }
    }

    private void OnEnable()
    {
        if (Health != null)
        {
            Health.Died += HandleDied;
        }
    }

    private void OnDisable()
    {
        if (Health != null)
        {
            Health.Died -= HandleDied;
        }
    }

    private void HandleDied(Health _)
    {
        if (BehavioursToDisable != null)
        {
            foreach (var behaviour in BehavioursToDisable)
            {
                if (behaviour != null)
                {
                    behaviour.enabled = false;
                }
            }
        }

        if (CollidersToDisable != null)
        {
            foreach (var collider in CollidersToDisable)
            {
                if (collider != null)
                {
                    collider.enabled = false;
                }
            }
        }

        if (CharacterControllerToDisable != null)
        {
            CharacterControllerToDisable.enabled = false;
        }
    }
}
