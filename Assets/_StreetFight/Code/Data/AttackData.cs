using StreetFight.Code.Abstract;
using UnityEngine;

namespace StreetFight.Code.Data
{
    public enum Limb { LeftHand, RightHand, LeftFoot, RightFoot }

    /// <summary>Everything about one attack. Tunable per-attack in the Inspector.</summary>
    [System.Serializable]
    public class AttackData
    {
        [Header("Animation")]
        [Tooltip("Exact Animator state name, e.g. Attack_3combo_1")]
        public string stateName = "Attack_3combo_1";
        public float crossFade = 0.06f;

        [Header("Timing (normalized 0-1 of the clip)")]
        [Range(0f, 1f)] public float hitStart = 0.25f;          // limb becomes dangerous
        [Range(0f, 1f)] public float hitEnd = 0.45f;            // limb stops being dangerous
        [Range(0f, 1f)] public float comboWindowStart = 0.45f;  // next queued attack may fire after this
        [Range(0f, 1f)] public float endTime = 0.85f;           // return to idle here

        [Header("Hit")]
        public Limb limb = Limb.RightHand;
        public float hitRadius = 0.25f;
        public float damage = 6f;
        public float knockback = 1.2f;   // meters the victim gets pushed
        public float stunTime = 0.3f;    // seconds the victim is staggered
        public bool heavy = false;       // heavy = bigger shake, bigger sfx/vfx

        [Header("Feel")]
        public float hitStop = 0.05f;    // freeze-frame length (seconds)
        [Range(0f, 1f)] public float shake = 0.22f;
        public float lunge = 0.5f;       // meters stepped toward the target before the hit
    }

    /// <summary>Passed from attacker to victim when a hit lands.</summary>
    public struct HitInfo
    {
        public Fighter attacker;
        public float damage;
        public Vector3 direction;   // flat, attacker -> victim
        public Vector3 point;       // world position of contact
        public float knockback;
        public float stun;
        public bool heavy;
        public float shake;
        public float hitStop;
    }
}