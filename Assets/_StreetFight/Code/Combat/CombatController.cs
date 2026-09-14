using System.Collections;
using System.Collections.Generic;
using StreetFight.Code.Interfaces;
using StreetFight.Enum;
using StreetFight.ScriptableObjects;
using UnityEngine;
using UnityEngine.Events;

namespace StreetFight.Code.Combat
{
    [RequireComponent(typeof(Animator))]
    public class CombatController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Animator animator;
        [Tooltip("Optional. If present, hit-frame Animation Events open a physics-driven active window instead of a single instant OverlapSphere. Add a Hitbox component to this same GameObject to enable it — leave empty to keep the original one-shot behaviour.")]
        [SerializeField] private Hitbox hitbox;
        [Tooltip("Used for the dodge dash and block-related knockback. Optional — falls back to transform.position movement if absent.")]
        [SerializeField] private CharacterController characterController;

        [Header("Starter Attacks")]
        [Tooltip("Played when Punch is pressed from Idle, or once the combo-reset grace window has expired.")]
        [SerializeField] private AttackDataSO punchStarter;
        [Tooltip("Played when Kick is pressed from Idle, or once the combo-reset grace window has expired.")]
        [SerializeField] private AttackDataSO kickStarter;
        [SerializeField] private AttackDataSO heavyPunchStarter;
        [SerializeField] private AttackDataSO heavyKickStarter;
        [SerializeField] private AttackDataSO grabStarter;
        [SerializeField] private AttackDataSO specialStarter;

        [Header("Input")]
        [Tooltip("Turn off for AI-controlled characters — they should drive combat via RegisterInput() from their own decision logic, not the keyboard.")]
        [SerializeField] private bool useKeyboardInput = true;
        [SerializeField] private KeyCode punchKey = KeyCode.J;
        [SerializeField] private KeyCode kickKey = KeyCode.K;
        [SerializeField] private KeyCode heavyPunchKey = KeyCode.U;
        [SerializeField] private KeyCode heavyKickKey = KeyCode.I;
        [SerializeField] private KeyCode grabKey = KeyCode.G;
        [SerializeField] private KeyCode specialKey = KeyCode.L;

        [Header("Block / Dodge")]
        [SerializeField] private bool useKeyboardBlockDodge = true;
        [SerializeField] private KeyCode blockKey = KeyCode.LeftShift;
        [SerializeField] private KeyCode dodgeKey = KeyCode.LeftAlt;
        [Tooltip("Exact Animator state name for the block pose.")]
        [SerializeField] private string blockStateName = "Block";
        [Tooltip("Exact Animator state name for the dodge clip.")]
        [SerializeField] private string dodgeStateName = "Dodge";
        [Tooltip("Total time spent in the Dodging state before returning to Idle.")]
        [SerializeField] private float dodgeDuration = 0.4f;
        [Tooltip("How much of dodgeDuration, starting from the beginning, grants hit-invulnerability. Must be <= dodgeDuration.")]
        [SerializeField] private float dodgeInvulnerabilityWindow = 0.25f;
        [Tooltip("Distance covered over the course of the dodge, applied via CharacterController if one is assigned.")]
        [SerializeField] private float dodgeDistance = 2f;
        [Range(0f, 1f)]
        [Tooltip("Fraction of an attack's damage that still gets through while the defender is Blocking (chip damage). 0 = fully blocked.")]
        [SerializeField] private float blockedDamageMultiplier = 0.15f;

        [Header("Idle / Return State")]
        [Tooltip("Exact Animator state name to return to once a combo ends (e.g. your locomotion/idle state). Double-check this against your actual Animator Controller — it must be the real idle state name, not just \"Idle\".")]
        [SerializeField] private string idleStateName = "Idle";
        [Tooltip("Crossfade duration used when returning to idle after the last attack in a combo.")]
        [SerializeField] private float idleTransitionDuration = 0.15f;

        [Header("Targeting & Hit Detection")]
        [Tooltip("The opponent this character is fighting. Set at runtime (e.g. spawn/matchmaking code) or drag in the Inspector for a fixed 1v1 scene.")]
        [SerializeField] private Transform target;
        [Tooltip("Snap to face the target the instant an attack starts, so the swing is always aimed correctly regardless of which way the character happened to be facing.")]
        [SerializeField] private bool faceTargetOnAttack = true;
        [Tooltip("Layers that can receive hits (put your character/opponent colliders on one of these).")]
        [SerializeField] private LayerMask hittableMask = ~0;

        [Header("Buffering & Combo Reset")]
        [Tooltip("How long a press stays valid while waiting to be consumed — covers a press slightly BEFORE the combo window opens.")]
        [SerializeField] private float inputBufferLifetime = 0.35f;
        [Tooltip("Grace period after an attack fully ends during which a new press still continues the chain instead of restarting at the first attack.")]
        [SerializeField] private float comboResetWindow = 0.8f;

        [Header("Combo Damage Scaling")]
        [Tooltip("Damage multiplier applied per hit already landed in the current unbroken combo. 0.85 means each successive hit does 85% of what the scaling curve gave the previous one.")]
        [SerializeField] private float damageFalloffPerHit = 0.85f;
        [Range(0f, 1f)]
        [Tooltip("Floor on the damage scale so very long combos don't trend to zero.")]
        [SerializeField] private float minDamageScale = 0.35f;

        [Header("Events")]
        public UnityEvent<AttackDataSO> OnAttackStarted;
        public UnityEvent<AttackDataSO> OnHitFrame;
        public UnityEvent<AttackDataSO> OnAttackLanded;
        public UnityEvent<AttackDataSO> OnAttackBlocked;
        public UnityEvent OnDodgeStarted;
        public UnityEvent OnComboEnded;

        public CombatState State { get; private set; } = CombatState.Idle;
        public bool IsAttacking => State == CombatState.Attacking;
        public bool IsStunned => State == CombatState.Stunned;
        public bool IsBlocking => State == CombatState.Blocking;
        public bool IsDodging => State == CombatState.Dodging;
        public bool IsDodgeInvulnerable { get; private set; }
        public AttackDataSO CurrentAttack => _currentAttack;

        /// <summary>Set/read at runtime — e.g. spawn code does `player.SetTarget(opponentTransform)`.</summary>
        public void SetTarget(Transform newTarget) => target = newTarget;

        private AttackDataSO _currentAttack;
        private AttackDataSO _lastCompletedAttack;
        private float _lastAttackEndTime = -999f;

        private bool _comboWindowOpen;
        private bool _bufferHasInput;
        private AttackInputType _bufferedInput;
        private float _bufferedInputTime;
        private int _comboHitCount;

        private readonly Dictionary<AttackDataSO, float> _cooldownEndTime = new Dictionary<AttackDataSO, float>();

        private Coroutine _safetyRoutine;
        private Coroutine _dodgeRoutine;
        private Vector3 _dodgeDirection;

        private void Reset()
        {
            animator = GetComponent<Animator>();
            hitbox = GetComponent<Hitbox>();
            characterController = GetComponent<CharacterController>();
        }

        private void Awake()
        {
            if (animator == null) animator = GetComponent<Animator>();
            if (hitbox == null) hitbox = GetComponent<Hitbox>();
            if (characterController == null) characterController = GetComponent<CharacterController>();
            if (hitbox != null) hitbox.OnOverlap += HandleHitboxOverlap;
        }

        private void OnDestroy()
        {
            if (hitbox != null) hitbox.OnOverlap -= HandleHitboxOverlap;
        }

        private void Update()
        {
            if (useKeyboardInput)
            {
                if (Input.GetKeyDown(punchKey)) RegisterInput(AttackInputType.Punch);
                if (Input.GetKeyDown(kickKey)) RegisterInput(AttackInputType.Kick);
                if (Input.GetKeyDown(heavyPunchKey)) RegisterInput(AttackInputType.HeavyPunch);
                if (Input.GetKeyDown(heavyKickKey)) RegisterInput(AttackInputType.HeavyKick);
                if (Input.GetKeyDown(grabKey)) RegisterInput(AttackInputType.Grab);
                if (Input.GetKeyDown(specialKey)) RegisterInput(AttackInputType.Special);
            }

            if (useKeyboardBlockDodge)
            {
                if (Input.GetKeyDown(blockKey)) EnterBlocking();
                if (Input.GetKeyUp(blockKey)) ExitBlocking();
                if (Input.GetKeyDown(dodgeKey)) EnterDodging();
            }

            if (_bufferHasInput && Time.time - _bufferedInputTime > inputBufferLifetime)
            {
                _bufferHasInput = false; // stale buffered press, never got consumed
            }

            if (State == CombatState.Dodging && characterController != null && characterController.enabled && dodgeDuration > 0f)
            {
                float speed = dodgeDistance / dodgeDuration;
                characterController.Move(_dodgeDirection * speed * Time.deltaTime);
            }
        }

        /// <summary>Public entry point — call this from any input source (new Input System, UI button, AI, etc).</summary>
        public void RegisterInput(AttackInputType input)
        {
            switch (State)
            {
                case CombatState.Idle:
                    HandleIdleInput(input);
                    break;

                case CombatState.Attacking:
                    if (_comboWindowOpen)
                        TryConsumeAsCombo(input);
                    else
                        BufferInput(input);
                    break;

                case CombatState.Dodging:
                    // Resolved once the dodge ends and we're back in Idle.
                    BufferInput(input);
                    break;

                case CombatState.Blocking:
                case CombatState.Stunned:
                    // No attacking out of a block or a stun in this pass — extend here for a
                    // block-cancel-into-counter or parry system later.
                    break;
            }
        }

        private AttackDataSO GetStarterFor(AttackInputType input)
        {
            switch (input)
            {
                case AttackInputType.Punch: return punchStarter;
                case AttackInputType.Kick: return kickStarter;
                case AttackInputType.HeavyPunch: return heavyPunchStarter;
                case AttackInputType.HeavyKick: return heavyKickStarter;
                case AttackInputType.Grab: return grabStarter;
                case AttackInputType.Special: return specialStarter;
                default: return null;
            }
        }

        private void HandleIdleInput(AttackInputType input)
        {
            bool withinResetWindow = _lastCompletedAttack != null &&
                                      (Time.time - _lastAttackEndTime) <= comboResetWindow;

            AttackDataSO next = null;
            if (withinResetWindow && _lastCompletedAttack.TryGetLink(input, out var linked) && !IsOnCooldown(linked))
                next = linked;
            else
            {
                var starter = GetStarterFor(input);
                next = IsOnCooldown(starter) ? null : starter;
            }

            StartAttack(next);
        }

        private void TryConsumeAsCombo(AttackInputType input)
        {
            if (_currentAttack != null && _currentAttack.TryGetLink(input, out var next) && !IsOnCooldown(next))
                StartAttack(next);
            else
                BufferInput(input); // no usable link right now — hold onto it, resolved by whatever starts next
        }

        private void BufferInput(AttackInputType input)
        {
            _bufferHasInput = true;
            _bufferedInput = input;
            _bufferedInputTime = Time.time;
        }

        private bool IsOnCooldown(AttackDataSO attack)
        {
            if (attack == null || attack.cooldown <= 0f) return false;
            return _cooldownEndTime.TryGetValue(attack, out float end) && Time.time < end;
        }

        private void StartAttack(AttackDataSO attack)
        {
            if (attack == null) return;

            if (faceTargetOnAttack && target != null)
                FaceTarget();

            _currentAttack = attack;
            State = CombatState.Attacking;
            _comboWindowOpen = false;
            _bufferHasInput = false;

            if (attack.cooldown > 0f)
                _cooldownEndTime[attack] = Time.time + attack.cooldown;

            animator.CrossFadeInFixedTime(attack.animatorStateName, attack.transitionDuration, attack.animatorLayer, 0f);
            OnAttackStarted?.Invoke(attack);

            if (_safetyRoutine != null) StopCoroutine(_safetyRoutine);
            _safetyRoutine = StartCoroutine(SafetyTimeout(attack));
        }

        /// <summary>Instant flat-plane rotation towards the current target. Called right as an
        /// attack starts so the swing is always aimed at the opponent, independent of whatever
        /// direction movement/AI facing left the character in.</summary>
        private void FaceTarget()
        {
            Vector3 dir = target.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(dir.normalized);
        }

        private IEnumerator SafetyTimeout(AttackDataSO attack)
        {
            yield return new WaitForSeconds(attack.safetyDuration);

            // Only fires if the clip's own Animation Events never called back — guarantees
            // the state machine always resolves back to Idle instead of hanging forever.
            if (_currentAttack == attack && State == CombatState.Attacking)
            {
                Anim_OnComboWindowOpen();
                Anim_OnAttackEnd();
            }
        }

        // ---------------------------------------------------------------
        // Animation Event hooks — add these as Animation Events on each
        // attack clip, calling the matching method by name. See README.
        // ---------------------------------------------------------------

        public void Anim_OnAttackStart()
        {
            // Hook for VFX/SFX or resetting per-attack hit flags.
        }

        /// <summary>Preferred new hook — pair with Anim_OnHitboxClose to bracket the active
        /// frames of the swing. Requires a Hitbox component on this GameObject.</summary>
        public void Anim_OnHitboxOpen()
        {
            if (_currentAttack == null || hitbox == null) return;
            hitbox.Open(_currentAttack.hitOffset, _currentAttack.hitRadius, hittableMask, transform.root, _currentAttack.allowMultiHit);
        }

        public void Anim_OnHitboxClose()
        {
            hitbox?.Close();
        }

        /// <summary>Legacy single-instant hook, kept so already-authored clips keep working
        /// unchanged. New clips should use Anim_OnHitboxOpen/Anim_OnHitboxClose instead — a
        /// window sampled across a couple of physics steps is far more reliable than a single
        /// instant, especially at variable frame rate.</summary>
        public void Anim_OnHitFrame()
        {
            OnHitFrame?.Invoke(_currentAttack);
            if (_currentAttack == null) return;

            if (hitbox != null)
            {
                hitbox.Open(_currentAttack.hitOffset, _currentAttack.hitRadius, hittableMask, transform.root, _currentAttack.allowMultiHit);
                hitbox.SampleOnce();
                hitbox.Close();
            }
            else
            {
                LegacyOverlapCheck();
            }
        }

        /// <summary>Used only when no Hitbox component is present at all, so the project keeps
        /// working before one has been added to a prefab.</summary>
        private void LegacyOverlapCheck()
        {
            Vector3 origin = transform.TransformPoint(_currentAttack.hitOffset);
            Collider[] hits = Physics.OverlapSphere(origin, _currentAttack.hitRadius, hittableMask);

            foreach (var col in hits)
            {
                if (col.transform.root == transform.root) continue;

                var damageable = col.GetComponentInParent<IDamageable>();
                var reactable = col.GetComponentInParent<IHitReactable>();
                if (damageable == null && reactable == null) continue;

                var targetCombat = col.GetComponentInParent<CombatController>();
                ResolveHit(targetCombat, damageable, reactable);
                break;
            }
        }

        private void HandleHitboxOverlap(Collider col)
        {
            if (_currentAttack == null) return;

            var hurtbox = col.GetComponent<Hurtbox>();
            if (hurtbox == null) hurtbox = col.GetComponentInParent<Hurtbox>();

            CombatController targetCombat;
            IDamageable damageable;
            IHitReactable reactable;

            if (hurtbox != null)
            {
                targetCombat = hurtbox.Combat;
                damageable = hurtbox.Damageable;
                reactable = hurtbox.HitReactable;
            }
            else
            {
                // No Hurtbox on this collider — fall back to the interface lookup so it still
                // works without requiring every prefab to be migrated up front.
                targetCombat = col.GetComponentInParent<CombatController>();
                damageable = col.GetComponentInParent<IDamageable>();
                reactable = col.GetComponentInParent<IHitReactable>();
            }

            ResolveHit(targetCombat, damageable, reactable);
        }

        /// <summary>Single funnel for everything a landed (or blocked, or whiffed-on-invuln)
        /// hit needs to do. Both the Hitbox path and the legacy OverlapSphere path go through
        /// here so blocking/dodging/cooldowns/damage-scaling behave identically either way.</summary>
        private void ResolveHit(CombatController targetCombat, IDamageable damageable, IHitReactable reactable)
        {
            if (targetCombat == this) return; // paranoia guard against a shared-layer self-hit
            if (targetCombat != null && targetCombat.IsDodgeInvulnerable) return; // clean miss
            if (reactable is HitReactionController hr && hr.IsInvulnerable) return;
            if (damageable == null && reactable == null) return;

            bool blocked = targetCombat != null && targetCombat.IsBlocking;

            float scale = Mathf.Max(minDamageScale, Mathf.Pow(damageFalloffPerHit, _comboHitCount));
            float dmg = _currentAttack.damage * scale;
            if (blocked) dmg *= blockedDamageMultiplier;

            damageable?.TakeDamage(dmg, gameObject);

            if (blocked)
                targetCombat.OnAttackBlocked?.Invoke(_currentAttack);
            else
                reactable?.ReactToHit(_currentAttack, gameObject);

            OnAttackLanded?.Invoke(_currentAttack);
            _comboHitCount++;
            HitStopManager.Trigger(_currentAttack.category);
        }

        // Draws the hitbox in the Scene view so you can line hitOffset/hitRadius up with the
        // opponent's body while an attack is selected/previewed.
        private void OnDrawGizmosSelected()
        {
            if (_currentAttack == null) return;
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.TransformPoint(_currentAttack.hitOffset), _currentAttack.hitRadius);
        }

        public void Anim_OnComboWindowOpen()
        {
            _comboWindowOpen = true;

            if (_bufferHasInput && Time.time - _bufferedInputTime <= inputBufferLifetime)
            {
                var input = _bufferedInput;
                _bufferHasInput = false;
                TryConsumeAsCombo(input);
            }
        }

        public void Anim_OnComboWindowClose()
        {
            _comboWindowOpen = false;
        }

        public void Anim_OnAttackEnd()
        {
            if (_safetyRoutine != null)
            {
                StopCoroutine(_safetyRoutine);
                _safetyRoutine = null;
            }

            // Last-chance resolution for a press that landed during recovery frames.
            if (_bufferHasInput && Time.time - _bufferedInputTime <= inputBufferLifetime)
            {
                var input = _bufferedInput;
                _bufferHasInput = false;
                if (_currentAttack != null && _currentAttack.TryGetLink(input, out var next) && !IsOnCooldown(next))
                {
                    StartAttack(next);
                    return;
                }
            }

            _lastCompletedAttack = _currentAttack;
            _lastAttackEndTime = Time.time;
            _currentAttack = null;
            _comboWindowOpen = false;
            _comboHitCount = 0;
            State = CombatState.Idle;

            // Explicitly return to idle rather than relying on the Animator graph's own
            // exit transition — this is what actually guarantees the character doesn't
            // freeze on the attack's last frame if that graph transition is missing,
            // misconfigured, or the clip has "Loop Time" off.
            animator.CrossFadeInFixedTime(idleStateName, idleTransitionDuration, 0);

            OnComboEnded?.Invoke();
        }

        public void EnterStunned()
        {
            if (_safetyRoutine != null)
            {
                StopCoroutine(_safetyRoutine);
                _safetyRoutine = null;
            }
            if (_dodgeRoutine != null)
            {
                StopCoroutine(_dodgeRoutine);
                _dodgeRoutine = null;
            }

            _currentAttack = null;
            _comboWindowOpen = false;
            _bufferHasInput = false;
            _comboHitCount = 0;
            IsDodgeInvulnerable = false;
            State = CombatState.Stunned;
        }

        public void ExitStunned()
        {
            if (State != CombatState.Stunned) return;

            // Getting interrupted breaks the combo flow — don't let an unrelated hit-reaction
            // silently continue a combo chain that no longer makes sense.
            _lastCompletedAttack = null;
            State = CombatState.Idle;
            ReturnToIdleAnimator();
        }

        private void EnterBlocking()
        {
            if (State != CombatState.Idle) return;
            State = CombatState.Blocking;
            animator.CrossFadeInFixedTime(blockStateName, idleTransitionDuration, 0);
        }

        private void ExitBlocking()
        {
            if (State != CombatState.Blocking) return;
            State = CombatState.Idle;
            ReturnToIdleAnimator();
        }

        private void EnterDodging()
        {
            if (State != CombatState.Idle) return;

            Vector3 dir = target != null ? transform.position - target.position : -transform.forward;
            dir.y = 0f;
            _dodgeDirection = dir.sqrMagnitude > 0.0001f ? dir.normalized : -transform.forward;

            State = CombatState.Dodging;
            IsDodgeInvulnerable = true;
            animator.CrossFadeInFixedTime(dodgeStateName, idleTransitionDuration, 0);
            OnDodgeStarted?.Invoke();

            if (_dodgeRoutine != null) StopCoroutine(_dodgeRoutine);
            _dodgeRoutine = StartCoroutine(DodgeRoutine());
        }

        private IEnumerator DodgeRoutine()
        {
            float invulnWindow = Mathf.Clamp(dodgeInvulnerabilityWindow, 0f, dodgeDuration);

            yield return new WaitForSeconds(invulnWindow);
            IsDodgeInvulnerable = false;

            yield return new WaitForSeconds(dodgeDuration - invulnWindow);

            if (State == CombatState.Dodging)
            {
                State = CombatState.Idle;
                ReturnToIdleAnimator();
                TryConsumeBufferedInputFromIdle();
            }
            _dodgeRoutine = null;
        }

        private void TryConsumeBufferedInputFromIdle()
        {
            if (_bufferHasInput && Time.time - _bufferedInputTime <= inputBufferLifetime)
            {
                var input = _bufferedInput;
                _bufferHasInput = false;
                HandleIdleInput(input);
            }
        }

        private void ReturnToIdleAnimator()
        {
            animator.CrossFadeInFixedTime(idleStateName, idleTransitionDuration, 0);
        }

        /// <summary>
        /// Timescale-based hit-stop, self-hosting so nothing external needs to call Init() —
        /// the previous version required a manual Init(runner) call that nothing in the
        /// project actually made, which meant the very first Trigger() would have thrown a
        /// NullReferenceException. It now creates a tiny persistent runner GameObject the
        /// first time it's needed.
        /// </summary>
        public static class HitStopManager
        {
            private static Coroutine _active;
            private static MonoBehaviour _runner;

            private static MonoBehaviour Runner
            {
                get
                {
                    if (_runner == null)
                    {
                        var go = new GameObject("~HitStopRunner");
                        Object.DontDestroyOnLoad(go);
                        _runner = go.AddComponent<HitStopRunnerBehaviour>();
                    }
                    return _runner;
                }
            }

            /// <summary>Optional — no longer required. Kept so any existing call site still
            /// compiles; lets you supply your own persistent runner (e.g. a GameManager)
            /// instead of the auto-created one.</summary>
            public static void Init(MonoBehaviour runner)
            {
                if (runner != null) _runner = runner;
            }

            public static void Trigger(AttackCategory category)
            {
                float duration = category switch
                {
                    AttackCategory.Light => 0.03f,
                    AttackCategory.Heavy => 0.06f,
                    _ => 0.12f // Special/Grab/Counter/knockdown
                };
                float scale = category == AttackCategory.Light ? 0.2f : 0.05f;

                if (_active != null) Runner.StopCoroutine(_active);
                _active = Runner.StartCoroutine(Run(duration, scale));
            }

            private static IEnumerator Run(float duration, float scale)
            {
                Time.timeScale = scale;
                yield return new WaitForSecondsRealtime(duration);
                Time.timeScale = 1f;
                _active = null;
            }
        }

        private class HitStopRunnerBehaviour : MonoBehaviour { }
    }
}