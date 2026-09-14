using System.Collections;
using StreetFight.Code.Combat;
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

        [Header("Death")]
        [Tooltip("Auto-fetched if left empty. Disabled the instant health hits zero so no more input/attacks are processed.")]
        [SerializeField] private CombatController combat;
        [Tooltip("Other scripts to turn off on death — e.g. AIOpponentController, MovementController. Leave HitReactionController OFF this list; it needs to stay enabled to play the death animation itself.")]
        [SerializeField] private MonoBehaviour[] disableOnDeath;
        [Tooltip("Fallback only — used when this GameObject has no HitReactionController. If one is present, IT plays the death clip (from ReactToHit, on the same hit that killed this character) so there's no race between two scripts crossfading the Animator at once.")]
        [SerializeField] private Animator animator;
        [SerializeField] private string deadStateName = "Dead";
        [SerializeField] private float deadTransitionDuration = 0.1f;
        [Tooltip("Colliders (hurtbox/hitbox) to disable shortly after death, so a corpse can neither deal nor take further hits. Delayed slightly so the killing blow's own reaction/knockback still resolves cleanly first.")]
        [SerializeField] private Collider[] collidersToDisableOnDeath;
        [SerializeField] private float colliderDisableDelay = 0.15f;

        public UnityEvent<float, GameObject> OnDamaged; // (amount, source)
        public UnityEvent OnDied;

        /// <summary>True once health has hit zero. AI/other systems should stop acting on this once true.</summary>
        public bool IsDead => currentHealth <= 0f;

        /// <summary>Current health as a 0-1 fraction of max — used by systems (like AI) that want
        /// to react to "low health" without caring about the absolute numbers.</summary>
        public float HealthFraction => maxHealth > 0f ? currentHealth / maxHealth : 0f;

        private HitReactionController _hitReaction;

        private void Awake()
        {
            currentHealth = maxHealth;
            if (combat == null) combat = GetComponent<CombatController>();
            if (animator == null) animator = GetComponent<Animator>();
            _hitReaction = GetComponent<HitReactionController>();
            RefreshSlider();
        }

        public void TakeDamage(float amount, GameObject source)
        {
            if (IsDead) return;

            currentHealth = Mathf.Max(0f, currentHealth - amount);
            Debug.Log($"{name} took {amount} damage from {source.name} — {currentHealth}/{maxHealth} left");
            OnDamaged?.Invoke(amount, source);
            RefreshSlider();

            if (currentHealth <= 0f)
                Die();
        }

        private void Die()
        {
            if (combat != null) combat.enabled = false;

            if (disableOnDeath != null)
            {
                foreach (var script in disableOnDeath)
                    if (script != null) script.enabled = false;
            }

            // If there's a HitReactionController, it already plays the death animation itself
            // (see HitReactionController.ReactToHit) using the attacker/facing info from the
            // hit that just killed this character. Only crossfade here as a fallback for
            // simpler setups (e.g. a training dummy) that don't have the full reaction pipeline.
            if (_hitReaction == null && animator != null)
                animator.CrossFadeInFixedTime(deadStateName, deadTransitionDuration, 0);

            if (collidersToDisableOnDeath != null && collidersToDisableOnDeath.Length > 0)
                StartCoroutine(DisableCollidersAfterDelay());

            OnDied?.Invoke();
        }

        private IEnumerator DisableCollidersAfterDelay()
        {
            yield return new WaitForSeconds(colliderDisableDelay);
            if (collidersToDisableOnDeath == null) yield break;
            foreach (var col in collidersToDisableOnDeath)
                if (col != null) col.enabled = false;
        }

        private void RefreshSlider()
        {
            if (health == null) return;

            health.maxValue = maxHealth;
            health.value = currentHealth;
        }
    }
}