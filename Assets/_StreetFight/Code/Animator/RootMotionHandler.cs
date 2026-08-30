using StreetFight.Code.Combat;
using UnityEngine;

namespace StreetFight.Code.Animation
{
    [RequireComponent(typeof(Animator))]
    public class RootMotionHandler : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private CombatController combatController;
        [Tooltip("Optional — if present, motion is applied via CharacterController.Move instead of transform.position.")]
        [SerializeField] private CharacterController characterController;

        [Header("Grounding")]
        [SerializeField] private LayerMask groundMask = ~0;
        [SerializeField] private float groundCheckDistance = 0.3f;
        [SerializeField] private float groundSnapSpeed = 12f;

        private void Reset()
        {
            animator = GetComponent<Animator>();
            combatController = GetComponent<CombatController>();
            characterController = GetComponent<CharacterController>();
        }

        private void Awake()
        {
            if (animator == null) animator = GetComponent<Animator>();
        }

        // private void OnAnimatorMove()
        // {
        //     Vector3 delta = animator.deltaPosition;
        //     Quaternion rotDelta = animator.deltaRotation;

        //     // Never let a clip move the character vertically — this is what most commonly
        //     // causes "unexpected upward movement" bugs with root motion.
        //     delta.y = 0f;

        //     if (combatController != null && combatController.IsAttacking)
        //     {
        //         var attack = combatController.CurrentAttack;

        //         if (attack != null && !attack.allowRootTranslation)
        //         {
        //             delta = Vector3.zero;
        //         }
        //         else if (attack != null)
        //         {
        //             float maxStep = attack.maxForwardSpeed * Time.deltaTime;
        //             if (delta.magnitude > maxStep)
        //                 delta = delta.normalized * maxStep;
        //         }
        //     }

        //     Vector3 targetPos = transform.position + delta;
        //     targetPos = SnapToGround(targetPos);

        //     if (characterController != null && characterController.enabled)
        //         characterController.Move(targetPos - transform.position);
        //     else
        //         transform.position = targetPos;

        //     transform.rotation *= rotDelta;
        // }

        private Vector3 SnapToGround(Vector3 pos)
        {
            if (Physics.Raycast(pos + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 1f + groundCheckDistance, groundMask))
                pos.y = Mathf.Lerp(pos.y, hit.point.y, groundSnapSpeed * Time.deltaTime);

            return pos;
        }
    }
}