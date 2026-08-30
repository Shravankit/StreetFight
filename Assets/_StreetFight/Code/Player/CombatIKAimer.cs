using StreetFight.Code.Combat;
using StreetFight.Enum;
using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace StreetFight.Code.PLayer
{
    /// <summary>
    /// Adds a light IK correction on top of your baked punch/kick animations, using the
    /// Animation Rigging package. The animation still plays exactly as authored — this only
    /// nudges the hand/foot's Two Bone IK Constraint weight up during the reach portion of the
    /// swing, with the constraint's target tracking the opponent's actual position, so the
    /// attack visually connects even if the opponent isn't standing at exactly the distance/
    /// height the clip was authored for.
    ///
    /// Keep maxAimWeight well under 1 (0.4-0.7) — this should read as a subtle correction,
    /// not override the animation's own arm/leg motion.
    /// </summary>
    [RequireComponent(typeof(RigBuilder))]
    public class CombatIKAimer : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CombatController combat;
        [SerializeField] private RigBuilder rigBuilder;

        [Header("Punch IK")]
        [SerializeField] private TwoBoneIKConstraint punchHandIK;
        [Tooltip("Empty Transform assigned as the constraint's Target — this script moves it onto the opponent every frame while aiming.")]
        [SerializeField] private Transform punchAimTarget;

        [Header("Kick IK")]
        [SerializeField] private TwoBoneIKConstraint kickFootIK;
        [SerializeField] private Transform kickAimTarget;

        [Header("Opponent")]
        [Tooltip("A point on the opponent to aim at — ideally a chest/torso bone, not their root, so kicks and punches aim at a believable height. Falls back gracefully if left empty (aiming simply won't engage).")]
        [SerializeField] private Transform opponentAimPoint;

        [Header("Aim Blend")]
        [SerializeField] private float aimInSpeed = 8f;
        [SerializeField] private float aimOutSpeed = 6f;
        [Range(0f, 1f)]
        [Tooltip("Cap on IK weight — keep this a correction, not a replacement of the animation.")]
        [SerializeField] private float maxAimWeight = 0.6f;

        private TwoBoneIKConstraint _activeConstraint;
        private Transform _activeAimTarget;
        private float _targetWeight;

        private void Reset()
        {
            combat = GetComponent<CombatController>();
            rigBuilder = GetComponent<RigBuilder>();
        }

        public void SetOpponentAimPoint(Transform point) => opponentAimPoint = point;

        private void Update()
        {
            if (_activeConstraint == null) return;

            if (_activeAimTarget != null && opponentAimPoint != null)
                _activeAimTarget.position = opponentAimPoint.position;

            float speed = _targetWeight > _activeConstraint.weight ? aimInSpeed : aimOutSpeed;
            _activeConstraint.weight = Mathf.MoveTowards(_activeConstraint.weight, _targetWeight, speed * Time.deltaTime);
        }

        // ---- Animation Event hooks — add these bracketing the reach/extension portion of
        // ---- each punch/kick clip, alongside your existing combo-window events. ----

        public void Anim_OnAimStart()
        {
            if (combat == null || combat.CurrentAttack == null) return;

            bool isKick = combat.CurrentAttack.inputType == AttackInputType.Kick;
            _activeConstraint = isKick ? kickFootIK : punchHandIK;
            _activeAimTarget = isKick ? kickAimTarget : punchAimTarget;
            _targetWeight = maxAimWeight;
        }

        public void Anim_OnAimEnd()
        {
            _targetWeight = 0f;
        }
    }
}
