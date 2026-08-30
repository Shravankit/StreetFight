using StreetFight.Code.Interfaces;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace StreetFight.Code.PLayer
{
    /// <summary>
    /// Minimal example so you can immediately verify hits are landing. Put this on the player
    /// and the opponent (anywhere with a Collider on the hittableMask layer). Replace with your
    /// real health/damage system once targeting is confirmed working.
    /// </summary>
    public class Health : MonoBehaviour, IDamageable
    {
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float currentHealth;
        [SerializeField] private Slider health;

        public UnityEvent<float, GameObject> OnDamaged; // (amount, source)
        public UnityEvent OnDied;

        private void Awake()
        {
            currentHealth = maxHealth;
            RefreshSlider();
        }

        public void TakeDamage(float amount, GameObject source)
        {
            if (currentHealth <= 0f) return;

            currentHealth = Mathf.Max(0f, currentHealth - amount);
            Debug.Log($"{name} took {amount} damage from {source.name} — {currentHealth}/{maxHealth} left");
            OnDamaged?.Invoke(amount, source);
            RefreshSlider();

            if (currentHealth <= 0f)
                OnDied?.Invoke();
        }

        private void RefreshSlider()
        {
            if (health == null) return;

            health.maxValue = maxHealth;
            health.value = currentHealth;
        }
    }
}
