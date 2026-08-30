using StreetFight.Code.Combat;
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

        [Header("Movement")]
        [SerializeField] private float moveSpeed = 3f;
        [SerializeField] private float rotationSpeed = 8f;
        [Tooltip("Distance at which the NPC stops closing in and starts fighting.")]
        [SerializeField] private float preferredRange = 1.6f;

        [Header("Attack Decision")]
        [Tooltip("Seconds between the AI reconsidering whether to throw another attack — keeps it from machine-gunning inputs.")]
        [SerializeField] private float minDecisionInterval = 0.4f;
        [SerializeField] private float maxDecisionInterval = 1.1f;
        [Range(0f, 1f)]
        [Tooltip("Chance, each decision tick while in range, that the AI actually throws an attack rather than waiting.")]
        [SerializeField] private float attackChance = 0.6f;
        [Range(0f, 1f)]
        [Tooltip("Of the attacks it decides to throw, the fraction that are kicks rather than punches.")]
        [SerializeField] private float kickBias = 0.4f;

        private float _nextDecisionTime;
        private static readonly int SpeedHash = Animator.StringToHash("Speed");

        private void Reset()
        {
            combat = GetComponent<CombatController>();
            animator = GetComponent<Animator>();
            characterController = GetComponent<CharacterController>();
        }

        private void Awake()
        {
            if (combat == null) combat = GetComponent<CombatController>();
            if (animator == null) animator = GetComponent<Animator>();
            ScheduleNextDecision();
        }

        private void Update()
        {
            if (target == null || combat == null) return;

            Vector3 toTarget = target.position - transform.position;
            toTarget.y = 0f;
            float distance = toTarget.magnitude;
            bool canAct = !combat.IsAttacking && !combat.IsStunned; // never move or turn while root motion owns an attack

            if (canAct && toTarget.sqrMagnitude > 0.001f)
            {
                Quaternion lookRot = Quaternion.LookRotation(toTarget.normalized);
                transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, rotationSpeed * Time.deltaTime);
            }

            float speedParam = 0f;
            if (canAct && distance > preferredRange)
            {
                Vector3 move = toTarget.normalized * moveSpeed * Time.deltaTime;
                if (characterController != null && characterController.enabled)
                    characterController.Move(move);
                else
                    transform.position += move;

                speedParam = 1f;
            }

            if (animator != null)
                animator.SetFloat(SpeedHash, speedParam, 0.1f, Time.deltaTime);

            if (Time.time >= _nextDecisionTime)
            {
                ScheduleNextDecision();

                bool inRange = distance <= preferredRange * 1.15f;
                if (inRange && Random.value <= attackChance)
                {
                    var input = Random.value < kickBias ? AttackInputType.Kick : AttackInputType.Punch;
                    combat.RegisterInput(input);
                }
            }
        }

        private void ScheduleNextDecision()
        {
            _nextDecisionTime = Time.time + Random.Range(minDecisionInterval, maxDecisionInterval);
        }
    }
}
