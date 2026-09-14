using StreetFight.Code.Combat;
using StreetFight.Code.PLayer;
using StreetFight.Enum;
using UnityEngine;

namespace CombatSystem
{
    /// <summary>
    /// Drives an NPC opponent using the exact same CombatController/AttackDataSO setup as the
    /// player — it just calls CombatController.RegisterInput() from timed AI decisions instead
    /// of reading the keyboard. That means combo chaining, input buffering, and the combo-reset
    /// window all apply to the AI for free; this script only decides *when* and *what* to press.
    ///
    /// What makes this read as an actual opponent rather than a punching bag on a timer:
    ///  - It watches the target's CombatController state (attacking / staggered) and reacts to
    ///    it, instead of throwing attacks on a blind schedule regardless of what you're doing.
    ///  - It keeps a spacing band and circles within it rather than walking to one spot and
    ///    planting there.
    ///  - It occasionally feints — steps into range without swinging.
    ///  - It gets more cautious as its own health drops.
    ///
    /// Put this on the opponent alongside CombatController (with "Use Keyboard Input" UNCHECKED
    /// on that CombatController) and RootMotionHandler, using the same Animator Controller as
    /// the player since it shares the same animation set.
    /// </summary>
    [RequireComponent(typeof(CombatController))]
    public class AIOpponentController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CombatController combat;
        [SerializeField] private Animator animator;
        [Tooltip("Optional — used for movement if present.")]
        [SerializeField] private CharacterController characterController;
        [Tooltip("The player (or whatever this NPC should fight).")]
        [SerializeField] private Transform target;
        [Tooltip("Auto-fetched from target if left empty. Lets the AI react to the target's attacking/staggered state instead of only tracking distance.")]
        [SerializeField] private CombatController targetCombat;

        [SerializeField] private Health health;

        [Header("Movement / Spacing")]
        [SerializeField] private float moveSpeed = 3f;
        [SerializeField] private float rotationSpeed = 8f;
        [Tooltip("Distance the AI tries to hover at — it circles around this, not sits exactly on it.")]
        [SerializeField] private float preferredRange = 1.6f;
        [Tooltip("Max reach an attack can land from. Should roughly match your AttackDataSO hit ranges — keep tighter than preferredRange so the AI doesn't swing at air.")]
        [SerializeField] private float attackRange = 1.4f;
        [Tooltip("Sideways drift speed used while circling in range and not currently attacking — this is what stops the fight from looking like two statues trading in one spot.")]
        [SerializeField] private float circleSpeed = 1.4f;

        [Header("Attack Decision")]
        [Tooltip("Seconds between the AI reconsidering whether to throw another attack — keeps it from machine-gunning inputs.")]
        [SerializeField] private float minDecisionInterval = 0.4f;
        [SerializeField] private float maxDecisionInterval = 1.1f;
        [Range(0f, 1f)]
        [Tooltip("Chance, each decision tick while in range, that the AI actually throws an attack rather than repositioning.")]
        [SerializeField] private float attackChance = 0.6f;
        [Range(0f, 1f)]
        [Tooltip("Of the attacks it decides to throw, the fraction that are kicks rather than punches.")]
        [SerializeField] private float kickBias = 0.4f;
        [Range(0f, 1f)]
        [Tooltip("Chance, per decision tick while in range, of feinting — stepping in without swinging — instead of attacking or circling.")]
        [SerializeField] private float feintChance = 0.15f;
        [Range(0f, 1f)]
        [Tooltip("Chance the AI backs off instead of continuing to press forward when the target is mid-swing. 0 = always trades face-first, 1 = never risks a counter-hit.")]
        [SerializeField] private float caution = 0.5f;

        [Header("Self-preservation")]
        [Tooltip("Own health fraction (0-1) below which the AI keeps extra distance and attacks less eagerly. Requires Health to expose a HealthFraction property (0-1) — see note in chat.")]
        [Range(0f, 1f)][SerializeField] private float retreatHealthFraction = 0.3f;
        [SerializeField] private float retreatRangeBonus = 1f;

        private float _nextDecisionTime;
        private bool _feinting;
        private float _feintReleaseTime;
        private int _circleDir = 1;
        private float _nextCircleFlipTime;
        private static readonly int SpeedHash = Animator.StringToHash("Speed");

        private void Reset()
        {
            combat = GetComponent<CombatController>();
            animator = GetComponent<Animator>();
            characterController = GetComponent<CharacterController>();
            health = GetComponent<Health>();
        }

        private void Awake()
        {
            if (combat == null) combat = GetComponent<CombatController>();
            if (animator == null) animator = GetComponent<Animator>();
            if (health == null) health = GetComponent<Health>();
            if (targetCombat == null && target != null) targetCombat = target.GetComponent<CombatController>();

            _circleDir = Random.value < 0.5f ? -1 : 1; // per-instance so multiple NPCs don't circle identically
            ScheduleNextDecision();
        }

        private void Update()
        {
            if (target == null || combat == null) return;
            if (health != null && health.IsDead) return;
            if (targetCombat == null) targetCombat = target.GetComponent<CombatController>(); // in case target was assigned after Awake

            Vector3 toTarget = target.position - transform.position;
            toTarget.y = 0f;
            float distance = toTarget.magnitude;
            Vector3 forward = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : transform.forward;
            bool canAct = !combat.IsAttacking && !combat.IsStunned; // never move, turn, or decide while root motion owns an attack

            if (canAct && toTarget.sqrMagnitude > 0.001f)
            {
                Quaternion lookRot = Quaternion.LookRotation(forward);
                transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, rotationSpeed * Time.deltaTime);
            }

            bool targetStaggered = targetCombat != null && targetCombat.IsStunned;
            bool targetSwinging = targetCombat != null && targetCombat.IsAttacking;
            bool lowHealth = health != null && health.HealthFraction <= retreatHealthFraction;

            float effectivePreferredRange = lowHealth ? preferredRange + retreatRangeBonus : preferredRange;

            // Punish window overrides the normal decision cadence — a staggered opponent is a
            // free hit, and a real fighter takes it immediately rather than waiting on its next
            // scheduled "should I attack" tick.
            if (canAct && targetStaggered && distance <= attackRange)
            {
                combat.RegisterInput(Random.value < kickBias ? AttackInputType.Kick : AttackInputType.Punch);
                ScheduleNextDecision();
                if (animator != null) animator.SetFloat(SpeedHash, 0f, 0.1f, Time.deltaTime);
                return;
            }

            // Target mid-swing: back off instead of blindly closing distance into their active
            // hitbox, unless this fighter is feeling bold enough (low caution) to risk a trade.
            bool retreatFromSwing = canAct && targetSwinging && !targetStaggered && Random.value < caution;

            if (_feinting && Time.time >= _feintReleaseTime)
                _feinting = false;

            float speedParam = 0f;

            if (canAct)
            {
                Vector3 move = Vector3.zero;
                float rangeError = distance - effectivePreferredRange;
                // Widened from 0.05 — at moveSpeed=3 a 0.05 deadzone is about one frame's worth
                // of travel, so the AI would step past it and immediately reverse next frame,
                // reading as a constant forward/backward vibration instead of settling.
                const float deadzone = 0.15f;

                if (retreatFromSwing || (lowHealth && distance < effectivePreferredRange))
                {
                    move = -forward * moveSpeed;
                }
                else if (rangeError > deadzone || _feinting)
                {
                    move = forward * moveSpeed;
                }
                else if (rangeError < -deadzone)
                {
                    move = -forward * moveSpeed * 0.6f;
                }
                else
                {
                    // In the pocket and not attacking this tick — circle instead of standing still.
                    if (Time.time >= _nextCircleFlipTime)
                    {
                        _nextCircleFlipTime = Time.time + Random.Range(1.5f, 3.5f);
                        if (Random.value < 0.3f) _circleDir *= -1;
                    }
                    Vector3 right = Vector3.Cross(Vector3.up, forward);
                    move = right * _circleDir * circleSpeed;
                }

                if (move.sqrMagnitude > 0.0001f)
                {
                    Vector3 delta = move * Time.deltaTime;

                    if (characterController != null && characterController.enabled)
                        characterController.Move(delta);
                    else
                        transform.position += delta;

                    speedParam = move.magnitude / Mathf.Max(moveSpeed, 0.01f);
                }
            }

            if (animator != null)
                animator.SetFloat(SpeedHash, speedParam, 0.1f, Time.deltaTime);

            if (canAct && Time.time >= _nextDecisionTime)
            {
                ScheduleNextDecision();

                bool inRange = distance <= attackRange && !retreatFromSwing;
                if (inRange)
                {
                    float roll = Random.value;
                    if (!_feinting && roll < feintChance)
                    {
                        _feinting = true;
                        _feintReleaseTime = Time.time + Random.Range(0.2f, 0.45f);
                    }
                    else if (roll < feintChance + attackChance)
                    {
                        combat.RegisterInput(Random.value < kickBias ? AttackInputType.Kick : AttackInputType.Punch);
                    }
                }
            }
        }

        private void ScheduleNextDecision()
        {
            _nextDecisionTime = Time.time + Random.Range(minDecisionInterval, maxDecisionInterval);
        }
    }
}