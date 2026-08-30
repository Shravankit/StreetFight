using StreetFight.Code.Combat;
using UnityEngine;

namespace StreetFight.Code.PLayer
{
    [RequireComponent(typeof(CharacterController))]
    public class MovementController : MonoBehaviour
    {
        [SerializeField] private CombatController combat;
        [SerializeField] private Animator animator;
        [SerializeField] private CharacterController controller;
        [SerializeField] private float moveSpeed = 4f;
        [SerializeField] private float rotationSpeed = 10f;

        private static readonly int SpeedHash = Animator.StringToHash("Speed");

        private void Update()
        {
            bool canMove = combat == null || !combat.IsAttacking;

            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            Vector3 input = new Vector3(h, 0f, v);

            if (canMove && input.sqrMagnitude > 0.01f)
            {
                Vector3 dir = input.normalized;
                controller.Move(dir * moveSpeed * Time.deltaTime);

                Quaternion targetRot = Quaternion.LookRotation(dir);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);

                animator.SetFloat(SpeedHash, input.magnitude, 0.1f, Time.deltaTime);
            }
            else if (canMove)
            {
                animator.SetFloat(SpeedHash, 0f, 0.1f, Time.deltaTime);
            }
            // else: attacking — CombatController + RootMotionHandler own the Animator/position entirely.
        }
    }
}
