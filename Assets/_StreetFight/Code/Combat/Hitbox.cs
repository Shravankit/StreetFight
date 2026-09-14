using System;
using System.Collections.Generic;
using UnityEngine;

namespace StreetFight.Code.Combat
{
    /// <summary>
    /// An "active window" hit-detection volume, opened/closed by Animation Events instead of
    /// checked on a single instant. While open it overlap-checks every FixedUpdate, so a hit
    /// registers reliably even if the exact contact frame shifts by a tick due to frame-rate
    /// variance — that's the main reliability gap with a single OverlapSphere fired from one
    /// Animation Event.
    ///
    /// Attach this to the SAME GameObject as CombatController (or a child bone if you want the
    /// origin to follow a hand/foot). It reads its position from whatever transform it's on and
    /// takes localOffset/radius per-call, so it slots directly into the existing
    /// AttackDataSO.hitOffset / hitRadius fields with no data migration required.
    ///
    /// Deliberately knows nothing about damage, IDamageable, or IHitReactable — it only reports
    /// which Colliders it overlapped. CombatController decides what a hit means.
    /// </summary>
    public class Hitbox : MonoBehaviour
    {
        /// <summary>Raised once per newly-overlapped collider while the window is open.</summary>
        public event Action<Collider> OnOverlap;

        private readonly HashSet<Collider> _hitThisWindow = new HashSet<Collider>();
        private readonly Collider[] _overlapBuffer = new Collider[16];

        private bool _active;
        private Vector3 _localOffset;
        private float _radius;
        private LayerMask _mask;
        private Transform _excludeRoot;
        private bool _allowMultiHit;

        public bool IsActive => _active;

        /// <summary>Opens the hitbox for this attack. Call from an "Anim_OnHitboxOpen"
        /// Animation Event at the start of the active frames.</summary>
        public void Open(Vector3 localOffset, float radius, LayerMask hittableMask, Transform excludeRoot, bool allowMultiHit)
        {
            _localOffset = localOffset;
            _radius = radius;
            _mask = hittableMask;
            _excludeRoot = excludeRoot;
            _allowMultiHit = allowMultiHit;
            _hitThisWindow.Clear();
            _active = true;
        }

        /// <summary>Closes the hitbox. Call from an "Anim_OnHitboxClose" Animation Event at the
        /// end of the active frames.</summary>
        public void Close() => _active = false;

        /// <summary>Runs one overlap check immediately, regardless of the FixedUpdate cadence.
        /// Used to support a single-instant hook (a clip with only one hit-frame event) so it
        /// still resolves within the same frame instead of waiting on physics timing.</summary>
        public void SampleOnce()
        {
            if (_active) DoOverlapCheck();
        }

        private void FixedUpdate()
        {
            if (_active) DoOverlapCheck();
        }

        private void DoOverlapCheck()
        {
            Vector3 origin = transform.TransformPoint(_localOffset);
            int count = Physics.OverlapSphereNonAlloc(origin, _radius, _overlapBuffer, _mask);

            for (int i = 0; i < count; i++)
            {
                Collider col = _overlapBuffer[i];
                if (col == null) continue;
                if (_excludeRoot != null && col.transform.root == _excludeRoot) continue;
                if (!_allowMultiHit && _hitThisWindow.Contains(col)) continue;

                _hitThisWindow.Add(col);
                OnOverlap?.Invoke(col);
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!_active) return;
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.TransformPoint(_localOffset), _radius);
        }
    }
}