using System;
using System.Collections;
using StreetFight.Code.Interfaces;
using StreetFight.Code.PLayer;
using StreetFight.Enum;
using StreetFight.ScriptableObjects;
using UnityEngine;
using UnityEngine.Events;

namespace StreetFight.Code.Combat
{
    /// <summary>
    /// Plays a hit-reaction animation and briefly stuns whoever it's attached to whenever
    /// CombatController's hit detection lands on it. Reactions come in three tiers (Light /
    /// Heavy / Knockdown) picked from the incoming attack's AttackCategory. A tier can only
    /// interrupt an in-progress reaction of an equal or lower tier — a jab won't cancel a
    /// knockdown, but a heavy hit will cut a light flinch short.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class HitReactionController : MonoBehaviour, IHitReactable
    {
        [Serializable]
        public struct ReactionData
        {
            [Tooltip("Exact Animator state name for this reaction clip.")]
            public string animatorStateName;
            [Tooltip("Crossfade duration into the reaction state.")]
            public float transitionDuration;
            [Tooltip("How long the character is stunned/locked out of input, in seconds. Should roughly match the clip length unless Anim_OnHitReactEnd() is wired up to end it early.")]
            public float stunDuration;
        }

        [Header("References")]
        [SerializeField] private Animator animator;
        [SerializeField] private CombatController combat;
        [Tooltip("Auto-fetched if left empty. When this reports IsDead on the hit that lands, the Death reaction takes over instead of a normal tiered reaction.")]
        [SerializeField] private Health health;

        [Header("Reaction Tiers")]
        [Tooltip("Used for AttackCategory.Light.")]
        [SerializeField] private ReactionData lightReaction = new ReactionData { animatorStateName = "HitLight", transitionDuration = 0.05f, stunDuration = 0.35f };
        [Tooltip("Used for AttackCategory.Heavy.")]
        [SerializeField] private ReactionData heavyReaction = new ReactionData { animatorStateName = "HitHeavy", transitionDuration = 0.05f, stunDuration = 0.6f };
        [Tooltip("Used for Special, Grab, and Counter — treat these as your big, un-interruptible hits (e.g. a knockdown).")]
        [SerializeField] private ReactionData knockdownReaction = new ReactionData { animatorStateName = "Knockdown", transitionDuration = 0.05f, stunDuration = 1.4f };
        [Tooltip("Plays instead of any of the above the instant Health reports the character as dead. Permanent — never times out and can't be interrupted by a later hit.")]
        [SerializeField] private ReactionData deathReaction = new ReactionData { animatorStateName = "Dead", transitionDuration = 0.1f, stunDuration = 0f };

        [Header("Facing")]
        [Tooltip("Snap to face the attacker the instant a reaction starts, so flinches always read as coming from the right direction.")]
        [SerializeField] private bool faceAttackerOnHit = true;

        [Header("Knockback (optional)")]
        [Tooltip("Instant push-back distance applied away from the attacker when a reaction starts. Leave at 0 to disable.")]
        [SerializeField] private float knockbackDistance = 0f;
        [SerializeField] private CharacterController characterController;

        [Header("Knockdown Invulnerability (optional)")]
        [Tooltip("While true and playing a knockdown reaction, IsInvulnerable reports true — wire this into your hit detection/TakeDamage if you want knocked-down characters immune to further hits.")]
        [SerializeField] private bool knockdownGrantsInvulnerability = true;

        [Header("Events")]
        public UnityEvent<AttackCategory> OnHitReactionStarted;
        public UnityEvent OnHitReactionEnded;

        public bool IsReacting { get; private set; }
        public bool IsInvulnerable { get; private set; }

        private const int DeathTier = 3; // above knockdown (2) — always wins, never times out

        private int _currentTier = -1; // 0 light, 1 heavy, 2 knockdown, 3 death
        private float _reactionEndTime = -999f;
        private Coroutine _safetyRoutine;

        private void Reset()
        {
            animator = GetComponent<Animator>();
            combat = GetComponent<CombatController>();
            health = GetComponent<Health>();
            characterController = GetComponent<CharacterController>();
        }

        private void Awake()
        {
            if (animator == null) animator = GetComponent<Animator>();
            if (combat == null) combat = GetComponent<CombatController>();
            if (health == null) health = GetComponent<Health>();
        }

        public void ReactToHit(AttackDataSO attack, GameObject attacker)
        {
            if (attack == null) return;

            // Checked first and unconditionally: Health.TakeDamage() always runs before
            // ReactToHit() on the same landed hit (see CombatController.ResolveHit), so on the
            // killing blow health.IsDead is already true by the time we get here. Routing death
            // through this same call — rather than having Health crossfade the Animator on its
            // own OnDied event — avoids a race where a normal hit-reaction crossfade fired from
            // this same hit could stomp the death animation right after it starts.
            if (health != null && health.IsDead)
            {
                PlayDeathReaction(attacker);
                return;
            }

            int tier = TierFor(attack.category);
            bool reactionInProgress = Time.time < _reactionEndTime;

            if (reactionInProgress && tier < _currentTier) return;
            if (reactionInProgress && knockdownGrantsInvulnerability && _currentTier == 2) return;

            var data = DataFor(tier);

            if (faceAttackerOnHit && attacker != null)
                FaceAttacker(attacker.transform);

            if (knockbackDistance > 0f && attacker != null)
                ApplyKnockback(attacker.transform);

            if (_safetyRoutine != null) StopCoroutine(_safetyRoutine);

            combat.EnterStunned();
            animator.CrossFadeInFixedTime(data.animatorStateName, data.transitionDuration, 0);

            _currentTier = tier;
            _reactionEndTime = Time.time + data.stunDuration;
            IsReacting = true;
            IsInvulnerable = knockdownGrantsInvulnerability && tier == 2;

            OnHitReactionStarted?.Invoke(attack.category);
            _safetyRoutine = StartCoroutine(SafetyEnd(data.stunDuration));
        }

        /// <summary>Top-priority, permanent reaction — plays once and never returns to idle.
        /// Called directly from ReactToHit() rather than through the tier-interrupt checks the
        /// other three tiers use, since death should always win regardless of what's currently
        /// playing.</summary>
        private void PlayDeathReaction(GameObject attacker)
        {
            if (_currentTier == DeathTier) return; // already dead and reacting — don't replay it

            if (_safetyRoutine != null)
            {
                StopCoroutine(_safetyRoutine);
                _safetyRoutine = null;
            }

            if (faceAttackerOnHit && attacker != null)
                FaceAttacker(attacker.transform);

            if (knockbackDistance > 0f && attacker != null)
                ApplyKnockback(attacker.transform);

            if (combat != null) combat.EnterStunned(); // locks out input/attacks; Health also disables the component separately

            animator.CrossFadeInFixedTime(deathReaction.animatorStateName, deathReaction.transitionDuration, 0);

            _currentTier = DeathTier;
            _reactionEndTime = Mathf.Infinity; // never times out — see SafetyEnd/EndReaction, which are simply never scheduled here
            IsReacting = true;
            IsInvulnerable = true; // a corpse can't be hit again

            OnHitReactionStarted?.Invoke(AttackCategory.Counter); // no dedicated category for death; treated as the same "big hit" bucket for listeners
        }

        private IEnumerator SafetyEnd(float duration)
        {
            yield return new WaitForSeconds(duration);
            EndReaction();
        }

        /// <summary>Animation Event hook — call from the reaction clip if you want the stun to
        /// end exactly on the recovery frame instead of waiting for the full stunDuration.</summary>
        public void Anim_OnHitReactEnd()
        {
            EndReaction();
        }

        private void EndReaction()
        {
            if (!IsReacting) return;

            if (_safetyRoutine != null)
            {
                StopCoroutine(_safetyRoutine);
                _safetyRoutine = null;
            }

            IsReacting = false;
            IsInvulnerable = false;
            _currentTier = -1;
            _reactionEndTime = -999f;

            combat.ExitStunned();
            OnHitReactionEnded?.Invoke();
        }

        private static int TierFor(AttackCategory category)
        {
            switch (category)
            {
                case AttackCategory.Light: return 0;
                case AttackCategory.Heavy: return 1;
                default: return 2; // Special / Grab / Counter — knockdown-tier
            }
        }

        private ReactionData DataFor(int tier)
        {
            switch (tier)
            {
                case 0: return lightReaction;
                case 1: return heavyReaction;
                default: return knockdownReaction;
            }
        }

        private void FaceAttacker(Transform attacker)
        {
            Vector3 dir = attacker.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(dir.normalized);
        }

        private void ApplyKnockback(Transform attacker)
        {
            Vector3 dir = transform.position - attacker.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return;
            Vector3 offset = dir.normalized * knockbackDistance;

            if (characterController != null && characterController.enabled)
                characterController.Move(offset);
            else
                transform.position += offset;
        }
    }
}