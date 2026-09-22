using StreetFight.Code.Interfaces;
using UnityEngine;

namespace StreetFight.Code.Combat
{
    /// <summary>
    /// Optional fast-path marker for a hittable collider — caches the owning character's
    /// IDamageable / IHitReactable / CombatController references once at Awake instead of
    /// doing a GetComponentInParent walk on every single hit.
    ///
    /// Fully optional: CombatController still falls back to GetComponentInParent for any
    /// collider that doesn't have one of these, so existing colliders keep working with zero
    /// changes. Add this to a character's main body collider for a small perf win, or to a
    /// second collider (e.g. a head hitbox) later if you want per-region hit differences.
    /// </summary>
    public class Hurtbox : MonoBehaviour
    {
        [Tooltip("Leave empty to use this collider's transform.root.")]
        [SerializeField] private Transform ownerRoot;

        public CombatController Combat { get; private set; }
        public IDamageable Damageable { get; private set; }
        public IHitReactable HitReactable { get; private set; }

        private void Awake()
        {
            Transform root = ownerRoot != null ? ownerRoot : transform.root;
            Combat = root.GetComponentInParent<CombatController>();
            Damageable = root.GetComponentInParent<IDamageable>();
            HitReactable = root.GetComponentInParent<IHitReactable>();
        }
    }
}