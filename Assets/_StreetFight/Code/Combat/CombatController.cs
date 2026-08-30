using System.Collections;
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
        // [Header("References")]
        // [SerializeField] private Animator animator;

        // [Header("Starter Attacks")]
        // [Tooltip("Played when Punch is pressed from Idle, or once the combo-reset grace window has expired.")]
        // [SerializeField] private AttackDataSO punchStarter;
        // [Tooltip("Played when Kick is pressed from Idle, or once the combo-reset grace window has expired.")]
        // [SerializeField] private AttackDataSO kickStarter;

        // [Header("Input")]
        // [SerializeField] private KeyCode punchKey = KeyCode.J;
        // [SerializeField] private KeyCode kickKey = KeyCode.K;

        // [Header("Idle / Return State")]
        // [Tooltip("Exact Animator state name to return to once a combo ends (e.g. your locomotion/idle state).")]
        // [SerializeField] private string idleStateName = "Idle";
        // [Tooltip("Crossfade duration used when returning to idle after the last attack in a combo.")]
        // [SerializeField] private float idleTransitionDuration = 0.15f;

        // [Header("Buffering & Combo Reset")]
        // [Tooltip("How long a press stays valid while waiting to be consumed — covers a press slightly BEFORE the combo window opens.")]
        // [SerializeField] private float inputBufferLifetime = 0.35f;
        // [Tooltip("Grace period after an attack fully ends during which a new press still continues the chain instead of restarting at the first attack.")]
        // [SerializeField] private float comboResetWindow = 0.8f;

        // [Header("Events")]
        // public UnityEvent<AttackDataSO> OnAttackStarted;
        // public UnityEvent<AttackDataSO> OnHitFrame;
        // public UnityEvent OnComboEnded;

        // public CombatState State { get; private set; } = CombatState.Idle;
        // public bool IsAttacking => State == CombatState.Attacking;
        // public AttackDataSO CurrentAttack => _currentAttack;

        // private AttackDataSO _currentAttack;
        // private AttackDataSO _lastCompletedAttack;
        // private float _lastAttackEndTime = -999f;

        // private bool _comboWindowOpen;
        // private bool _bufferHasInput;
        // private AttackInputType _bufferedInput;
        // private float _bufferedInputTime;

        // private Coroutine _safetyRoutine;

        // private void Reset()
        // {
        //     animator = GetComponent<Animator>();
        // }

        // private void Awake()
        // {
        //     if (animator == null) animator = GetComponent<Animator>();
        // }

        // private void Update()
        // {
        //     if (Input.GetKeyDown(punchKey)) RegisterInput(AttackInputType.Punch);
        //     if (Input.GetKeyDown(kickKey)) RegisterInput(AttackInputType.Kick);

        //     if (_bufferHasInput && Time.time - _bufferedInputTime > inputBufferLifetime)
        //     {
        //         _bufferHasInput = false; // stale buffered press, never got consumed
        //     }
        // }

        // /// <summary>Public entry point — call this from any input source (new Input System, UI button, etc).</summary>
        // public void RegisterInput(AttackInputType input)
        // {
        //     switch (State)
        //     {
        //         case CombatState.Idle:
        //             HandleIdleInput(input);
        //             break;

        //         case CombatState.Attacking:
        //             if (_comboWindowOpen)
        //                 TryConsumeAsCombo(input);
        //             else
        //                 BufferInput(input);
        //             break;

        //         default:
        //             // Blocking / Dodging / Stunned — extend here (e.g. buffer a counter-attack).
        //             break;
        //     }
        // }

        // private void HandleIdleInput(AttackInputType input)
        // {
        //     bool withinResetWindow = _lastCompletedAttack != null &&
        //                               (Time.time - _lastAttackEndTime) <= comboResetWindow;

        //     AttackDataSO next = null;
        //     if (withinResetWindow && _lastCompletedAttack.TryGetLink(input, out var linked))
        //         next = linked;
        //     else
        //         next = input == AttackInputType.Punch ? punchStarter : kickStarter;

        //     StartAttack(next);
        // }

        // private void TryConsumeAsCombo(AttackInputType input)
        // {
        //     if (_currentAttack != null && _currentAttack.TryGetLink(input, out var next))
        //         StartAttack(next);
        //     else
        //         BufferInput(input); // no defined chain yet — hold onto it, resolved by whatever starts next
        // }

        // private void BufferInput(AttackInputType input)
        // {
        //     _bufferHasInput = true;
        //     _bufferedInput = input;
        //     _bufferedInputTime = Time.time;
        // }

        // private void StartAttack(AttackDataSO attack)
        // {
        //     if (attack == null) return;

        //     _currentAttack = attack;
        //     State = CombatState.Attacking;
        //     _comboWindowOpen = false;
        //     _bufferHasInput = false;

        //     animator.CrossFadeInFixedTime(attack.animatorStateName, attack.transitionDuration, attack.animatorLayer, 0f);
        //     OnAttackStarted?.Invoke(attack);

        //     if (_safetyRoutine != null) StopCoroutine(_safetyRoutine);
        //     _safetyRoutine = StartCoroutine(SafetyTimeout(attack));
        // }

        // private IEnumerator SafetyTimeout(AttackDataSO attack)
        // {
        //     yield return new WaitForSeconds(attack.safetyDuration);

        //     // Only fires if the clip's own Animation Events never called back — guarantees
        //     // the state machine always resolves back to Idle instead of hanging forever.
        //     if (_currentAttack == attack && State == CombatState.Attacking)
        //     {
        //         Anim_OnComboWindowOpen();
        //         Anim_OnAttackEnd();
        //     }
        // }

        // // ---------------------------------------------------------------
        // // Animation Event hooks — add these as Animation Events on each
        // // attack clip, calling the matching method by name. See README.
        // // ---------------------------------------------------------------

        // public void Anim_OnAttackStart()
        // {
        //     // Hook for VFX/SFX or resetting per-attack hit flags.
        // }

        // public void Anim_OnHitFrame()
        // {
        //     OnHitFrame?.Invoke(_currentAttack);
        //     // Hit detection goes here, e.g.:
        //     // Physics.OverlapSphere(transform.TransformPoint(_currentAttack.hitOffset), _currentAttack.hitRadius, hittableMask);
        // }

        // public void Anim_OnComboWindowOpen()
        // {
        //     _comboWindowOpen = true;

        //     if (_bufferHasInput && Time.time - _bufferedInputTime <= inputBufferLifetime)
        //     {
        //         var input = _bufferedInput;
        //         _bufferHasInput = false;
        //         TryConsumeAsCombo(input);
        //     }
        // }

        // public void Anim_OnComboWindowClose()
        // {
        //     _comboWindowOpen = false;
        // }

        // public void Anim_OnAttackEnd()
        // {
        //     if (_safetyRoutine != null)
        //     {
        //         StopCoroutine(_safetyRoutine);
        //         _safetyRoutine = null;
        //     }

        //     // Last-chance resolution for a press that landed during recovery frames.
        //     if (_bufferHasInput && Time.time - _bufferedInputTime <= inputBufferLifetime)
        //     {
        //         var input = _bufferedInput;
        //         _bufferHasInput = false;
        //         if (_currentAttack != null && _currentAttack.TryGetLink(input, out var next))
        //         {
        //             StartAttack(next);
        //             return;
        //         }
        //     }

        //     _lastCompletedAttack = _currentAttack;
        //     _lastAttackEndTime = Time.time;
        //     _currentAttack = null;
        //     _comboWindowOpen = false;
        //     State = CombatState.Idle;

        //     // Explicitly return to idle rather than relying on the Animator graph's own
        //     // exit transition — this is what actually guarantees the character doesn't
        //     // freeze on the attack's last frame if that graph transition is missing,
        //     // misconfigured, or the clip has "Loop Time" off.
        //     animator.CrossFadeInFixedTime(idleStateName, idleTransitionDuration, 0);

        //     OnComboEnded?.Invoke();
        // }

        // [Header("References")]
        // [SerializeField] private Animator animator;

        // [Header("Starter Attacks")]
        // [Tooltip("Played when Punch is pressed from Idle, or once the combo-reset grace window has expired.")]
        // [SerializeField] private AttackDataSO punchStarter;
        // [Tooltip("Played when Kick is pressed from Idle, or once the combo-reset grace window has expired.")]
        // [SerializeField] private AttackDataSO kickStarter;

        // [Header("Input")]
        // [Tooltip("Turn off for AI-controlled characters — they should drive combat via RegisterInput() from their own decision logic, not the keyboard.")]
        // [SerializeField] private bool useKeyboardInput = true;
        // [SerializeField] private KeyCode punchKey = KeyCode.J;
        // [SerializeField] private KeyCode kickKey = KeyCode.K;

        // [Header("Idle / Return State")]
        // [Tooltip("Exact Animator state name to return to once a combo ends (e.g. your locomotion/idle state).")]
        // [SerializeField] private string idleStateName = "Idle";
        // [Tooltip("Crossfade duration used when returning to idle after the last attack in a combo.")]
        // [SerializeField] private float idleTransitionDuration = 0.15f;

        // [Header("Buffering & Combo Reset")]
        // [Tooltip("How long a press stays valid while waiting to be consumed — covers a press slightly BEFORE the combo window opens.")]
        // [SerializeField] private float inputBufferLifetime = 0.35f;
        // [Tooltip("Grace period after an attack fully ends during which a new press still continues the chain instead of restarting at the first attack.")]
        // [SerializeField] private float comboResetWindow = 0.8f;

        // [Header("Events")]
        // public UnityEvent<AttackDataSO> OnAttackStarted;
        // public UnityEvent<AttackDataSO> OnHitFrame;
        // public UnityEvent OnComboEnded;

        // public CombatState State { get; private set; } = CombatState.Idle;
        // public bool IsAttacking => State == CombatState.Attacking;
        // public AttackDataSO CurrentAttack => _currentAttack;

        // private AttackDataSO _currentAttack;
        // private AttackDataSO _lastCompletedAttack;
        // private float _lastAttackEndTime = -999f;

        // private bool _comboWindowOpen;
        // private bool _bufferHasInput;
        // private AttackInputType _bufferedInput;
        // private float _bufferedInputTime;

        // private Coroutine _safetyRoutine;

        // private void Reset()
        // {
        //     animator = GetComponent<Animator>();
        // }

        // private void Awake()
        // {
        //     if (animator == null) animator = GetComponent<Animator>();
        // }

        // private void Update()
        // {
        //     if (useKeyboardInput)
        //     {
        //         if (Input.GetKeyDown(punchKey)) RegisterInput(AttackInputType.Punch);
        //         if (Input.GetKeyDown(kickKey)) RegisterInput(AttackInputType.Kick);
        //     }

        //     if (_bufferHasInput && Time.time - _bufferedInputTime > inputBufferLifetime)
        //     {
        //         _bufferHasInput = false; // stale buffered press, never got consumed
        //     }
        // }

        // /// <summary>Public entry point — call this from any input source (new Input System, UI button, etc).</summary>
        // public void RegisterInput(AttackInputType input)
        // {
        //     switch (State)
        //     {
        //         case CombatState.Idle:
        //             HandleIdleInput(input);
        //             break;

        //         case CombatState.Attacking:
        //             if (_comboWindowOpen)
        //                 TryConsumeAsCombo(input);
        //             else
        //                 BufferInput(input);
        //             break;

        //         default:
        //             // Blocking / Dodging / Stunned — extend here (e.g. buffer a counter-attack).
        //             break;
        //     }
        // }

        // private void HandleIdleInput(AttackInputType input)
        // {
        //     bool withinResetWindow = _lastCompletedAttack != null &&
        //                               (Time.time - _lastAttackEndTime) <= comboResetWindow;

        //     AttackDataSO next = null;
        //     if (withinResetWindow && _lastCompletedAttack.TryGetLink(input, out var linked))
        //         next = linked;
        //     else
        //         next = input == AttackInputType.Punch ? punchStarter : kickStarter;

        //     StartAttack(next);
        // }

        // private void TryConsumeAsCombo(AttackInputType input)
        // {
        //     if (_currentAttack != null && _currentAttack.TryGetLink(input, out var next))
        //         StartAttack(next);
        //     else
        //         BufferInput(input); // no defined chain yet — hold onto it, resolved by whatever starts next
        // }

        // private void BufferInput(AttackInputType input)
        // {
        //     _bufferHasInput = true;
        //     _bufferedInput = input;
        //     _bufferedInputTime = Time.time;
        // }

        // private void StartAttack(AttackDataSO attack)
        // {
        //     if (attack == null) return;

        //     _currentAttack = attack;
        //     State = CombatState.Attacking;
        //     _comboWindowOpen = false;
        //     _bufferHasInput = false;

        //     animator.CrossFadeInFixedTime(attack.animatorStateName, attack.transitionDuration, attack.animatorLayer, 0f);
        //     OnAttackStarted?.Invoke(attack);

        //     if (_safetyRoutine != null) StopCoroutine(_safetyRoutine);
        //     _safetyRoutine = StartCoroutine(SafetyTimeout(attack));
        // }

        // private IEnumerator SafetyTimeout(AttackDataSO attack)
        // {
        //     yield return new WaitForSeconds(attack.safetyDuration);

        //     // Only fires if the clip's own Animation Events never called back — guarantees
        //     // the state machine always resolves back to Idle instead of hanging forever.
        //     if (_currentAttack == attack && State == CombatState.Attacking)
        //     {
        //         Anim_OnComboWindowOpen();
        //         Anim_OnAttackEnd();
        //     }
        // }

        // // ---------------------------------------------------------------
        // // Animation Event hooks — add these as Animation Events on each
        // // attack clip, calling the matching method by name. See README.
        // // ---------------------------------------------------------------

        // public void Anim_OnAttackStart()
        // {
        //     // Hook for VFX/SFX or resetting per-attack hit flags.
        // }

        // public void Anim_OnHitFrame()
        // {
        //     OnHitFrame?.Invoke(_currentAttack);
        //     // Hit detection goes here, e.g.:
        //     // Physics.OverlapSphere(transform.TransformPoint(_currentAttack.hitOffset), _currentAttack.hitRadius, hittableMask);
        // }

        // public void Anim_OnComboWindowOpen()
        // {
        //     _comboWindowOpen = true;

        //     if (_bufferHasInput && Time.time - _bufferedInputTime <= inputBufferLifetime)
        //     {
        //         var input = _bufferedInput;
        //         _bufferHasInput = false;
        //         TryConsumeAsCombo(input);
        //     }
        // }

        // public void Anim_OnComboWindowClose()
        // {
        //     _comboWindowOpen = false;
        // }

        // public void Anim_OnAttackEnd()
        // {
        //     if (_safetyRoutine != null)
        //     {
        //         StopCoroutine(_safetyRoutine);
        //         _safetyRoutine = null;
        //     }

        //     // Last-chance resolution for a press that landed during recovery frames.
        //     if (_bufferHasInput && Time.time - _bufferedInputTime <= inputBufferLifetime)
        //     {
        //         var input = _bufferedInput;
        //         _bufferHasInput = false;
        //         if (_currentAttack != null && _currentAttack.TryGetLink(input, out var next))
        //         {
        //             StartAttack(next);
        //             return;
        //         }
        //     }

        //     _lastCompletedAttack = _currentAttack;
        //     _lastAttackEndTime = Time.time;
        //     _currentAttack = null;
        //     _comboWindowOpen = false;
        //     State = CombatState.Idle;

        //     // Explicitly return to idle rather than relying on the Animator graph's own
        //     // exit transition — this is what actually guarantees the character doesn't
        //     // freeze on the attack's last frame if that graph transition is missing,
        //     // misconfigured, or the clip has "Loop Time" off.
        //     animator.CrossFadeInFixedTime(idleStateName, idleTransitionDuration, 0);

        //     OnComboEnded?.Invoke();
        // }

        [Header("References")]
        [SerializeField] private Animator animator;

        [Header("Starter Attacks")]
        [Tooltip("Played when Punch is pressed from Idle, or once the combo-reset grace window has expired.")]
        [SerializeField] private AttackDataSO punchStarter;
        [Tooltip("Played when Kick is pressed from Idle, or once the combo-reset grace window has expired.")]
        [SerializeField] private AttackDataSO kickStarter;

        [Header("Input")]
        [Tooltip("Turn off for AI-controlled characters — they should drive combat via RegisterInput() from their own decision logic, not the keyboard.")]
        [SerializeField] private bool useKeyboardInput = true;
        [SerializeField] private KeyCode punchKey = KeyCode.J;
        [SerializeField] private KeyCode kickKey = KeyCode.K;

        [Header("Idle / Return State")]
        [Tooltip("Exact Animator state name to return to once a combo ends (e.g. your locomotion/idle state).")]
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

        [Header("Events")]
        public UnityEvent<AttackDataSO> OnAttackStarted;
        public UnityEvent<AttackDataSO> OnHitFrame;
        public UnityEvent OnComboEnded;

        public CombatState State { get; private set; } = CombatState.Idle;
        public bool IsAttacking => State == CombatState.Attacking;
        public AttackDataSO CurrentAttack => _currentAttack;

        /// <summary>Set/read at runtime — e.g. spawn code does `player.SetTarget(opponentTransform)`.</summary>
        public void SetTarget(Transform newTarget) => target = newTarget;

        public bool IsStunned => State == CombatState.Stunned;

        private AttackDataSO _currentAttack;
        private AttackDataSO _lastCompletedAttack;
        private float _lastAttackEndTime = -999f;

        private bool _comboWindowOpen;
        private bool _bufferHasInput;
        private AttackInputType _bufferedInput;
        private float _bufferedInputTime;

        private Coroutine _safetyRoutine;

        private void Reset()
        {
            animator = GetComponent<Animator>();
        }

        private void Awake()
        {
            if (animator == null) animator = GetComponent<Animator>();
        }

        private void Update()
        {
            if (useKeyboardInput)
            {
                if (Input.GetKeyDown(punchKey)) RegisterInput(AttackInputType.Punch);
                if (Input.GetKeyDown(kickKey)) RegisterInput(AttackInputType.Kick);
            }

            if (_bufferHasInput && Time.time - _bufferedInputTime > inputBufferLifetime)
            {
                _bufferHasInput = false; // stale buffered press, never got consumed
            }
        }

        /// <summary>Public entry point — call this from any input source (new Input System, UI button, etc).</summary>
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

                default:
                    // Blocking / Dodging / Stunned — extend here (e.g. buffer a counter-attack).
                    break;
            }
        }

        private void HandleIdleInput(AttackInputType input)
        {
            bool withinResetWindow = _lastCompletedAttack != null &&
                                      (Time.time - _lastAttackEndTime) <= comboResetWindow;

            AttackDataSO next = null;
            if (withinResetWindow && _lastCompletedAttack.TryGetLink(input, out var linked))
                next = linked;
            else
                next = input == AttackInputType.Punch ? punchStarter : kickStarter;

            StartAttack(next);
        }

        private void TryConsumeAsCombo(AttackInputType input)
        {
            if (_currentAttack != null && _currentAttack.TryGetLink(input, out var next))
                StartAttack(next);
            else
                BufferInput(input); // no defined chain yet — hold onto it, resolved by whatever starts next
        }

        private void BufferInput(AttackInputType input)
        {
            _bufferHasInput = true;
            _bufferedInput = input;
            _bufferedInputTime = Time.time;
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

        public void Anim_OnHitFrame()
        {
            OnHitFrame?.Invoke(_currentAttack);
            Debug.Log($"Hit frame for {_currentAttack.name} at {Time.time:F2}s");
            if (_currentAttack == null) return;

            Vector3 origin = transform.TransformPoint(_currentAttack.hitOffset);
            Collider[] hits = Physics.OverlapSphere(origin, _currentAttack.hitRadius, hittableMask);

            foreach (var col in hits)
            {
                if (col.transform.root == transform.root) continue; // never hit yourself

                var damageable = col.GetComponentInParent<IDamageable>();
                if (damageable != null)
                    damageable.TakeDamage(_currentAttack.damage, gameObject);

                var reactable = col.GetComponentInParent<IHitReactable>();
                if (reactable != null)
                    reactable.ReactToHit(_currentAttack, gameObject);

                if (damageable != null || reactable != null)
                    break; // one target per swing — drop this line for an AoE hit
            }
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
                if (_currentAttack != null && _currentAttack.TryGetLink(input, out var next))
                {
                    StartAttack(next);
                    return;
                }
            }

            _lastCompletedAttack = _currentAttack;
            _lastAttackEndTime = Time.time;
            _currentAttack = null;
            _comboWindowOpen = false;
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

            _currentAttack = null;
            _comboWindowOpen = false;
            _bufferHasInput = false;
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

        private void ReturnToIdleAnimator()
        {
            animator.CrossFadeInFixedTime(idleStateName, idleTransitionDuration, 0);
        }
    }
}