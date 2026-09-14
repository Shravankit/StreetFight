using System;
using System.Collections.Generic;
using StreetFight.Enum;
using UnityEngine;

namespace StreetFight.ScriptableObjects
{
    // Moved inside the namespace — it previously sat in the global namespace after the closing
    // brace below. It compiled fine (global-namespace fallback), but it's a landmine for the
    // first `using` that happens to shadow it. Field names/types are unchanged, so this does
    // not affect existing PunchStateData.asset / KickStateData.asset serialization.
    [Serializable]
    public struct ComboLink
    {
        public AttackInputType requiredInput;
        public AttackDataSO nextAttack;
    }

    [CreateAssetMenu(fileName = "NewAttack", menuName = "Combat System/Attack Data")]
    public class AttackDataSO : ScriptableObject
    {
        [Header("Identity")]
        public string attackId;
        public AttackInputType inputType;
        public AttackCategory category = AttackCategory.Light;

        [Header("Animator")]
        [Tooltip("Exact name of the Animator state for this attack (must exist in the Animator Controller, e.g. 'QuadPunch').")]
        public string animatorStateName;
        [Tooltip("Layer index in the Animator this state lives on. 0 = Base Layer.")]
        public int animatorLayer = 0;
        [Tooltip("Crossfade duration when entering this attack, in seconds. Keep short (0.05-0.12) for combo cancels to feel instant.")]
        public float transitionDuration = 0.08f;

        [Header("Timing Safety Net")]
        [Tooltip("Used ONLY if an Animation Event fails to fire (misconfigured clip). Guarantees the character never gets stuck mid-attack. Set to roughly the clip length.")]
        public float safetyDuration = 1.0f;

        [Header("Combat Data")]
        public float damage = 10f;
        public float hitRadius = 0.6f;
        public Vector3 hitOffset = new Vector3(0f, 1f, 1f);
        [Tooltip("If true, a single hitbox Open/Close window can hit the same target more than once (e.g. a multi-hit special). Leave off for normal single-contact attacks.")]
        public bool allowMultiHit = false;

        [Header("Cooldown")]
        [Tooltip("Minimum seconds between uses of THIS specific attack, independent of combo/idle state. 0 = no cooldown. Use this to rate-limit Specials/Grabs so they can't be spammed back-to-back.")]
        public float cooldown = 0f;

        [Header("Root Motion")]
        [Tooltip("If false, RootMotionHandler discards this attack's forward translation entirely (useful for a finisher that shouldn't move the character at all).")]
        public bool allowRootTranslation = true;
        [Tooltip("Clamp on root-motion forward speed while this attack plays, in m/s. Prevents 'excessive forward movement' from a hot clip.")]
        public float maxForwardSpeed = 2.5f;

        [Header("Combo Links")]
        [Tooltip("What this attack can chain into, keyed by the next input the player presses during the combo window (or the grace window right after this attack ends).")]
        public List<ComboLink> comboLinks = new List<ComboLink>();

        public bool TryGetLink(AttackInputType input, out AttackDataSO next)
        {
            for (int i = 0; i < comboLinks.Count; i++)
            {
                if (comboLinks[i].requiredInput == input && comboLinks[i].nextAttack != null)
                {
                    next = comboLinks[i].nextAttack;
                    return true;
                }
            }
            next = null;
            return false;
        }
    }
}