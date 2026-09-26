using System.Collections;
using System.Collections.Generic;
using StreetFight.Code.Data;
using StreetFight.Code.Effects;
using UnityEngine;

namespace StreetFight.Code.Abstract
{
    public enum FighterState { Free, Attacking, Dodging, HitStun, Dead }

    /// <summary>
    /// Shared brain for the hero and the NPC: health, attacks, hit detection, hit reactions,
    /// dodging, knockback, hit-stop, ragdoll death, VFX/SFX and camera-shake triggers.
    /// Animations are driven by CrossFade on state NAMES, so the Animator needs no transitions or parameters.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public abstract class Fighter : MonoBehaviour
    {
        public static readonly List<Fighter> All = new List<Fighter>();

        // ------------------------------------------------------------------ inspector
        [Header("Stats")]
        public float maxHealth = 100f;
        public float damageMultiplier = 1f;
        [HideInInspector] public int team;   // hero = 0, enemies = 1 (set by the subclasses)

        [Header("Animator (names must match the Animator window exactly)")]
        public Animator animator;
        public string idleState = "Idle";
        public string walkState = "Walk_Fwd";       // must live on the Legs layer (legs-only Avatar Mask)
        [Tooltip("Animator layer that holds Walk_Fwd with a legs-only Avatar Mask")]
        public string legsLayerName = "Legs";
        public float legsBlendSpeed = 12f;
        [Tooltip("Restart Walk_Fwd if its clip is not set to Loop Time (better: tick Loop Time on the clip)")]
        public bool forceWalkLoop = true;
        public string dodgeFwdState = "Dodge_Fwd";
        public string dodgeBwdState = "Dodge_Bwd";
        public string hitState = "Damage_Front_Big_ver_A";
        [Tooltip("Optional. Leave empty to use a ragdoll on death.")]
        public string deathState = "";

        [Header("Movement")]
        public float walkSpeed = 2f;
        public float turnSpeed = 720f;
        public float gravity = -20f;
        [Tooltip("Left/right only. Everyone stays on one lane (world X axis, fixed Z).")]
        public bool sideScroll = true;
        public float laneZ = 0f;

        [Header("Dodge")]
        public float dodgeDistance = 2.6f;
        public float dodgeDuration = 0.5f;
        public float dodgeCooldown = 0.4f;
        [Range(0f, 1f)] public float iFrameStart = 0.05f;
        [Range(0f, 1f)] public float iFrameEnd = 0.6f;

        [Header("Combat")]
        public LayerMask hurtMask = ~0;
        public float aimAssistRadius = 3f;
        public float lungeStopDistance = 1.1f;
        public float corpseLifetime = 10f;

        [Header("Limb transforms (auto-filled from a Humanoid rig; assign manually for Generic)")]
        public Transform leftHand;
        public Transform rightHand;
        public Transform leftFoot;
        public Transform rightFoot;

        [Header("Feedback assets (all optional)")]
        public GameObject lightHitVfx;
        public GameObject heavyHitVfx;
        public AudioClip[] whooshSfx;
        public AudioClip[] impactSfx;
        public AudioClip[] heavyImpactSfx;
        public AudioClip[] hurtVoiceSfx;

        // Default chains use the states from your Animator. Reorder / retune in the Inspector.
        // Hit windows and limbs are educated guesses - scrub each clip and adjust.
        [Header("Attack chains")]
        public AttackData[] lightChain =
        {
        Atk("Attack_3combo_1", Limb.RightHand, 0.25f, 0.45f, 6f,  1.0f, 0.30f),
        Atk("Attack_3combo_2", Limb.LeftHand,  0.25f, 0.45f, 6f,  1.0f, 0.30f),
        Atk("Attack_3combo_3", Limb.RightFoot, 0.30f, 0.50f, 10f, 2.2f, 0.55f, true),
    };
        public AttackData[] heavyChain =
        {
        Atk("Attack_Left_High_Kick",    Limb.LeftFoot,  0.35f, 0.55f, 14f, 2.8f, 0.65f, true),
        Atk("Attack_Right_High_Kick_B", Limb.RightFoot, 0.35f, 0.55f, 14f, 2.8f, 0.65f, true),
    };
        public AttackData[] specialChain =
        {
        Atk("Attack_5combo_B_2", Limb.LeftHand,  0.25f, 0.45f, 7f,  1.0f, 0.30f),
        Atk("Attack_5combo_B_5", Limb.RightFoot, 0.30f, 0.50f, 11f, 2.4f, 0.55f, true),
        Atk("Attack_4combo_3",   Limb.RightHand, 0.30f, 0.50f, 12f, 2.6f, 0.60f, true),
    };

        static AttackData Atk(string state, Limb limb, float hitStart, float hitEnd,
                              float damage, float knockback, float stun, bool heavy = false)
        {
            return new AttackData
            {
                stateName = state,
                limb = limb,
                hitStart = hitStart,
                hitEnd = hitEnd,
                comboWindowStart = hitEnd,
                damage = damage,
                knockback = knockback,
                stunTime = stun,
                heavy = heavy,
                hitStop = heavy ? 0.09f : 0.05f,
                shake = heavy ? 0.45f : 0.22f,
                lunge = heavy ? 0.8f : 0.5f,
                endTime = 0.85f,
            };
        }

        // ------------------------------------------------------------------ public state
        public FighterState State { get; private set; } = FighterState.Free;
        public AttackData CurrentAttack { get; private set; }
        public float Health { get; private set; }
        public float HealthNormalized => Health / maxHealth;
        public bool IsDead => State == FighterState.Dead;
        public virtual bool IsPlayer => false;
        public bool IsInvulnerable => State == FighterState.Dodging && dodgeT >= iFrameStart && dodgeT <= iFrameEnd;

        public event System.Action<Fighter, AttackData> AttackStarted;
        public event System.Action<Fighter, HitInfo> Damaged;
        public event System.Action<Fighter> Died;

        /// <summary>True once one whole side is defeated: the hero is dead, or every enemy is.</summary>
        public static bool FightOver { get; private set; }
        /// <summary>Raised once when the fight ends. The argument is the fighter whose death ended it.</summary>
        public static event System.Action<Fighter> FightEnded;

        /// <summary>False during round intro/countdown so Tick() (input/AI) doesn't run yet.</summary>
        public static bool RoundActive { get; private set; }
        public static void SetRoundActive(bool active) => RoundActive = active;

        /// <summary>Middle of the body. Follows the ragdoll after death (the root object stays where the fighter fell from).</summary>
        public Vector3 FocusPoint => hips ? hips.position : transform.position + Vector3.up;

        // ------------------------------------------------------------------ internals
        protected CharacterController cc;
        protected AttackData queuedAttack;
        protected float attackNorm;

        const float KnockbackDecay = 8f;
        static bool slowMoActive;

        AudioSource audioSource;
        Transform hips;
        Coroutine actionRoutine, freezeRoutine;
        Rigidbody[] ragdollBodies = new Rigidbody[0];
        readonly HashSet<Fighter> hitVictims = new HashSet<Fighter>();
        readonly Collider[] overlapBuffer = new Collider[32];
        Vector3 knockbackVelocity, lastLimbPos;
        bool haveLastLimb, isMoving;
        float verticalVelocity, dodgeT, nextDodgeTime;
        int legsLayer = -1, walkHash;
        float legsWeight, lastWalkRestart;

        // ------------------------------------------------------------------ lifecycle
        protected virtual void Awake()
        {
            cc = GetComponent<CharacterController>();
            if (!animator) animator = GetComponentInChildren<Animator>();
            animator.applyRootMotion = false;   // scripts move the character

            audioSource = GetComponent<AudioSource>();
            if (!audioSource) { audioSource = gameObject.AddComponent<AudioSource>(); audioSource.spatialBlend = 1f; }

            Health = maxHealth;

            // ---- legs-only walk layer
            walkHash = Animator.StringToHash(walkState);
            legsLayer = animator.GetLayerIndex(legsLayerName);
            if (legsLayer > 0) animator.SetLayerWeight(legsLayer, 0f);
            else
            {
                legsLayer = -1;
                Debug.LogWarning($"[{name}] No Animator layer called '{legsLayerName}'. Walking will use the whole body. " +
                                 "Add a Legs layer with a legs-only Avatar Mask to move just the legs.", this);
            }

            // ---- side-scroll: put everyone on the same lane and face left or right
            if (sideScroll)
            {
                cc.enabled = false;
                Vector3 p = transform.position; p.z = laneZ;
                transform.position = p;
                float side = Vector3.Dot(transform.forward, Vector3.right) >= 0f ? 1f : -1f;
                transform.rotation = Quaternion.LookRotation(Vector3.right * side);
                cc.enabled = true;
            }

            CacheLimbs();
            SetupRagdoll();
        }

        protected virtual void Start()
        {
            ValidateStates();
            animator.Play(Animator.StringToHash(idleState), 0, 0f);   // never start in the Animator's default (kick) state
        }

        protected virtual void OnEnable() { All.Add(this); }
        protected virtual void OnDisable()
        {
            All.Remove(this);
            if (All.Count == 0) FightOver = false;   // scene reloaded / everything destroyed
        }

        void Update()
        {
            if (IsDead) return;

            if (RoundActive) Tick();
            else SetMoving(false);

            UpdateLegsLayer();

            // gravity + knockback
            float dt = Time.deltaTime;
            if (cc.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
            verticalVelocity += gravity * dt;
            cc.Move((knockbackVelocity + Vector3.up * verticalVelocity) * dt);
            knockbackVelocity *= Mathf.Exp(-KnockbackDecay * dt);

            KeepOnLane();
        }

        /// <summary>Undo any sideways drift (e.g. sliding off another fighter's collider).</summary>
        void KeepOnLane()
        {
            if (!sideScroll || !cc.enabled) return;
            float dz = laneZ - transform.position.z;
            if (Mathf.Abs(dz) > 0.001f) cc.Move(new Vector3(0f, 0f, dz));
        }

        /// <summary>
        /// Fades the legs-only walk layer in while walking and out otherwise. The base layer keeps playing
        /// Idle, so the upper body never changes; only the legs take the walk cycle.
        /// </summary>
        void UpdateLegsLayer()
        {
            if (legsLayer < 0) return;

            float target = (State == FighterState.Free && isMoving) ? 1f : 0f;
            legsWeight = Mathf.MoveTowards(legsWeight, target, legsBlendSpeed * Time.deltaTime);
            animator.SetLayerWeight(legsLayer, legsWeight);

            // Keep the cycle running for as long as we're moving, even if the clip isn't set to loop.
            if (forceWalkLoop && target > 0f && !animator.IsInTransition(legsLayer) && Time.time - lastWalkRestart > 0.25f)
            {
                AnimatorStateInfo s = animator.GetCurrentAnimatorStateInfo(legsLayer);
                if (s.shortNameHash == walkHash && !s.loop && s.normalizedTime >= 0.98f)
                {
                    lastWalkRestart = Time.time;
                    animator.CrossFadeInFixedTime(walkHash, 0.05f, legsLayer, 0f);
                }
            }
        }

        /// <summary>Called every frame while alive. Hero reads input, NPC runs AI.</summary>
        protected abstract void Tick();

        /// <summary>Who should attacks auto-aim / lunge toward?</summary>
        protected virtual Fighter GetAimTarget()
        {
            return FindNearest(transform.position, team, aimAssistRadius, transform.forward, 100f);
        }

        // ------------------------------------------------------------------ public API
        public bool RequestAttack(AttackData a)
        {
            if (State == FighterState.Free) { StartAction(AttackRoutine(a)); return true; }
            if (State == FighterState.Attacking) { queuedAttack = a; return true; }   // buffered until the combo window opens
            return false;
        }

        public bool TryDodge(Vector3 moveDir, Vector3 faceDir, bool forwardAnim)
        {
            if (Time.time < nextDodgeTime) return false;
            bool cancelOutOfAttack = State == FighterState.Attacking && CurrentAttack != null
                                     && attackNorm >= CurrentAttack.comboWindowStart;
            if (State != FighterState.Free && !cancelOutOfAttack) return false;
            StartAction(DodgeRoutine(moveDir, faceDir, forwardAnim));
            return true;
        }

        public void TakeHit(HitInfo hit)
        {
            if (IsDead || IsInvulnerable) return;

            Health = Mathf.Max(0f, Health - hit.damage);
            Damaged?.Invoke(this, hit);

            // ---- feedback
            if (IsPlayer || (hit.attacker != null && hit.attacker.IsPlayer))
                CameraShake.Shake(hit.shake * (IsPlayer ? 1.25f : 1f), hit.heavy ? 2.5f : 0.8f);
            SpawnHitVfx(hit);
            PlayRandom(hit.heavy ? heavyImpactSfx : impactSfx, 1f);
            PlayRandom(hurtVoiceSfx, 0.8f);
            Freeze(hit.hitStop);

            // ---- face the attacker so the "Front" hit animation looks right from any angle
            Vector3 toAttacker = -hit.direction;
            if (toAttacker.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(toAttacker);

            knockbackVelocity = hit.direction * (hit.knockback * KnockbackDecay);

            if (Health <= 0f) { Die(hit); return; }
            StartAction(HitStunRoutine(hit.stun));
        }

        public void ForceTimeoutLoss()
        {
            if (IsDead) return;
            Die(new HitInfo { attacker = null, damage = 0f, direction = -transform.forward });
        }

        public static Fighter FindNearest(Vector3 from, int myTeam, float radius,
                                          Vector3 facing = default(Vector3), float maxAngle = 180f)
        {
            Fighter best = null;
            float bestSqr = radius * radius;
            bool useAngle = facing.sqrMagnitude > 0.0001f && maxAngle < 180f;
            foreach (Fighter f in All)
            {
                if (f.team == myTeam || f.IsDead) continue;
                Vector3 d = f.transform.position - from; d.y = 0f;
                float sqr = d.sqrMagnitude;
                if (sqr > bestSqr) continue;
                if (useAngle && Vector3.Angle(facing, d) > maxAngle) continue;
                best = f; bestSqr = sqr;
            }
            return best;
        }

        // ------------------------------------------------------------------ actions
        void StartAction(IEnumerator routine)
        {
            if (actionRoutine != null) StopCoroutine(actionRoutine);
            actionRoutine = StartCoroutine(routine);
        }

        IEnumerator AttackRoutine(AttackData a)
        {
            State = FighterState.Attacking;
            CurrentAttack = a;
            queuedAttack = null;
            attackNorm = 0f;
            haveLastLimb = false;
            hitVictims.Clear();
            ResetLocomotion();

            int hash = Animator.StringToHash(a.stateName);
            animator.CrossFadeInFixedTime(hash, a.crossFade, 0, 0f);
            AttackStarted?.Invoke(this, a);

            float startTime = Time.time;
            float lastNorm = 0f;
            bool whooshed = false;

            while (true)
            {
                yield return null;

                // During a crossfade the new state is reported as the "next" state.
                AnimatorStateInfo info = animator.IsInTransition(0)
                    ? animator.GetNextAnimatorStateInfo(0)
                    : animator.GetCurrentAnimatorStateInfo(0);

                if (info.shortNameHash != hash)
                {
                    if (Time.time - startTime > 0.35f)
                    {
                        Debug.LogWarning($"[{name}] Animator never entered state '{a.stateName}'. Check the name.", this);
                        break;
                    }
                    continue;
                }

                float norm = info.normalizedTime;
                attackNorm = norm;

                // ---- aim assist + lunge during the wind-up
                if (norm < a.hitStart)
                {
                    Fighter t = GetAimTarget();
                    if (t != null)
                    {
                        Vector3 to = Flat(t.transform.position - transform.position);
                        RotateTowards(to, turnSpeed * 3f);
                        if (to.magnitude > lungeStopDistance && norm > lastNorm)
                            MoveBy(transform.forward * (a.lunge * (norm - lastNorm) / Mathf.Max(a.hitStart, 0.05f)));
                    }
                    else if (norm > lastNorm)
                    {
                        MoveBy(transform.forward * (a.lunge * 0.5f * (norm - lastNorm) / Mathf.Max(a.hitStart, 0.05f)));
                    }
                }
                lastNorm = norm;

                if (!whooshed && norm >= a.hitStart - 0.05f) { whooshed = true; PlayRandom(whooshSfx, 0.9f); }

                // ---- damage window
                if (norm >= a.hitStart && norm <= a.hitEnd) HitCheck(a);
                else haveLastLimb = false;

                // ---- chain into the queued attack
                if (queuedAttack != null && norm >= a.comboWindowStart)
                {
                    AttackData next = queuedAttack;
                    StartAction(AttackRoutine(next));
                    yield break;
                }

                if (norm >= a.endTime) break;
            }
            ReturnToIdle();
        }

        void HitCheck(AttackData a)
        {
            Transform limb = GetLimb(a.limb);
            if (!limb) return;

            Vector3 p = limb.position;
            Vector3 from = haveLastLimb ? lastLimbPos : p;   // sweep from last frame so fast kicks can't skip through
            lastLimbPos = p; haveLastLimb = true;

            int n = Physics.OverlapCapsuleNonAlloc(from, p, a.hitRadius, overlapBuffer, hurtMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                Fighter f = overlapBuffer[i].GetComponentInParent<Fighter>();
                if (f == null || f == this || f.team == team || f.IsDead || hitVictims.Contains(f)) continue;

                hitVictims.Add(f);                 // one hit per victim per attack
                if (f.IsInvulnerable) continue;    // dodged it (i-frames)
                DeliverHit(f, a, p);
            }
        }

        void DeliverHit(Fighter victim, AttackData a, Vector3 point)
        {
            Vector3 dir = Flat(victim.transform.position - transform.position).normalized;
            if (dir.sqrMagnitude < 0.001f) dir = transform.forward;

            victim.TakeHit(new HitInfo
            {
                attacker = this,
                damage = a.damage * damageMultiplier,
                direction = dir,
                point = point,
                knockback = a.knockback,
                stun = a.stunTime,
                heavy = a.heavy,
                shake = a.shake,
                hitStop = a.hitStop,
            });
            Freeze(a.hitStop);   // attacker freezes too - this is what makes hits feel heavy
        }

        IEnumerator HitStunRoutine(float duration)
        {
            State = FighterState.HitStun;
            CurrentAttack = null; queuedAttack = null; attackNorm = 0f;
            ResetLocomotion();
            Play(hitState, 0.03f);
            yield return new WaitForSeconds(duration);   // cutting the "big" reaction short keeps fights snappy
            ReturnToIdle();
        }

        IEnumerator DodgeRoutine(Vector3 moveDir, Vector3 faceDir, bool forwardAnim)
        {
            State = FighterState.Dodging;
            CurrentAttack = null; queuedAttack = null; attackNorm = 0f;
            ResetLocomotion();

            faceDir = Flat(faceDir);
            if (faceDir.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(faceDir);
            moveDir = Flat(moveDir).normalized;

            nextDodgeTime = Time.time + dodgeDuration + dodgeCooldown;
            Play(forwardAnim ? dodgeFwdState : dodgeBwdState, 0.05f);

            dodgeT = 0f;
            while (dodgeT < 1f)
            {
                float speed = dodgeDistance / dodgeDuration * 2f * (1f - dodgeT);   // ease-out burst
                MoveBy(moveDir * speed * Time.deltaTime);
                dodgeT += Time.deltaTime / dodgeDuration;
                yield return null;
            }
            dodgeT = 0f;
            ReturnToIdle();
        }

        void ReturnToIdle()
        {
            State = FighterState.Free;
            CurrentAttack = null; queuedAttack = null; attackNorm = 0f;
            ResetLocomotion();
            Play(idleState, 0.15f);
        }

        void Die(HitInfo hit)
        {
            if (actionRoutine != null) StopCoroutine(actionRoutine);
            State = FighterState.Dead;
            CurrentAttack = null; queuedAttack = null;
            cc.enabled = false;
            Died?.Invoke(this);

            CameraShake.Shake(0.6f, 4f);
            if (((hit.attacker != null && hit.attacker.IsPlayer) || IsPlayer) && !slowMoActive)
                StartCoroutine(SlowMoRoutine(0.25f, 0.55f));

            if (!string.IsNullOrEmpty(deathState)) Play(deathState, 0.05f);
            else if (ragdollBodies.Length > 0) EnableRagdoll(hit);
            // else: stays frozen in the hit pose until you add a death animation or a ragdoll

            // Fight is over when a whole side has been knocked out.
            bool sideDefeated = true;
            foreach (Fighter f in All)
                if (f.team == team && !f.IsDead) { sideDefeated = false; break; }
            if (sideDefeated && !FightOver)
            {
                FightOver = true;
                FightEnded?.Invoke(this);
            }

            if (!IsPlayer && corpseLifetime > 0f && !FightOver) Destroy(gameObject, corpseLifetime);
        }

        static IEnumerator SlowMoRoutine(float scale, float realSeconds)
        {
            slowMoActive = true;
            Time.timeScale = scale;
            yield return new WaitForSecondsRealtime(realSeconds);
            Time.timeScale = 1f;
            slowMoActive = false;
        }

        // ------------------------------------------------------------------ ragdoll
        void SetupRagdoll()
        {
            var list = new List<Rigidbody>();
            foreach (Rigidbody rb in GetComponentsInChildren<Rigidbody>(true))
                if (rb.gameObject != gameObject) list.Add(rb);
            ragdollBodies = list.ToArray();

            foreach (Rigidbody rb in ragdollBodies)
            {
                rb.isKinematic = true;
                // Ragdoll colliders stay OFF while alive so they don't fight the CharacterController.
                foreach (Collider c in rb.GetComponents<Collider>()) c.enabled = false;
            }
        }

        void EnableRagdoll(HitInfo hit)
        {
            animator.enabled = false;
            Vector3 push = (hit.direction * 4f + Vector3.up * 1.5f) * Mathf.Max(1f, hit.knockback * 0.5f);
            foreach (Rigidbody rb in ragdollBodies)
            {
                foreach (Collider c in rb.GetComponents<Collider>()) c.enabled = true;
                rb.isKinematic = false;
                rb.AddForce(push, ForceMode.VelocityChange);
            }
        }

        // ------------------------------------------------------------------ locomotion helpers
        /// <summary>Call every frame from Tick(). Only the legs animate; the base layer keeps the Idle pose.</summary>
        protected void SetMoving(bool moving)
        {
            if (moving == isMoving) return;
            isMoving = moving;

            if (legsLayer >= 0)
            {
                if (moving) animator.CrossFadeInFixedTime(walkHash, 0f, legsLayer, 0f);   // start the cycle at frame 0; weight fades in
            }
            else
            {
                Play(moving ? walkState : idleState, 0.1f);   // fallback: no Legs layer, whole body walks
            }
        }

        void ResetLocomotion() { isMoving = false; }

        protected void MoveBy(Vector3 delta) { if (cc.enabled) cc.Move(Flat(delta)); }

        protected void RotateTowards(Vector3 dir, float degPerSec)
        {
            dir = Flat(dir);
            if (dir.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), degPerSec * Time.deltaTime);
        }

        /// <summary>Flattens a vector to the movement plane. In side-scroll mode that's the left/right axis only.</summary>
        protected Vector3 Flat(Vector3 v)
        {
            return sideScroll ? new Vector3(v.x, 0f, 0f) : new Vector3(v.x, 0f, v.z);
        }

        void Play(string state, float fade)
        {
            animator.CrossFadeInFixedTime(Animator.StringToHash(state), fade, 0, 0f);
        }

        // ------------------------------------------------------------------ feedback helpers
        public void Freeze(float seconds)
        {
            if (seconds <= 0f || !isActiveAndEnabled || IsDead) return;
            if (freezeRoutine != null) StopCoroutine(freezeRoutine);
            freezeRoutine = StartCoroutine(FreezeRoutine(seconds));
        }

        IEnumerator FreezeRoutine(float seconds)
        {
            animator.speed = 0f;
            yield return new WaitForSecondsRealtime(seconds);
            animator.speed = 1f;
            freezeRoutine = null;
        }

        void SpawnHitVfx(HitInfo hit)
        {
            GameObject prefab = hit.heavy ? heavyHitVfx : lightHitVfx;
            if (!prefab) return;
            Quaternion rot = hit.direction.sqrMagnitude > 0.001f ? Quaternion.LookRotation(-hit.direction) : Quaternion.identity;
            Destroy(Instantiate(prefab, hit.point, rot), 2f);   // swap for pooling if you spawn lots
        }

        void PlayRandom(AudioClip[] clips, float volume)
        {
            if (clips == null || clips.Length == 0) return;
            audioSource.pitch = Random.Range(0.92f, 1.08f);
            audioSource.PlayOneShot(clips[Random.Range(0, clips.Length)], volume);
        }

        // ------------------------------------------------------------------ setup helpers
        void CacheLimbs()
        {
            if (animator && animator.isHuman)
            {
                hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                if (!leftHand) leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
                if (!rightHand) rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
                if (!leftFoot) leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                if (!rightFoot) rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            }
            if (!leftHand || !rightHand || !leftFoot || !rightFoot)
                Debug.LogWarning($"[{name}] Limb transforms missing. Use a Humanoid rig or assign hands/feet manually.", this);
        }

        Transform GetLimb(Limb l)
        {
            switch (l)
            {
                case Limb.LeftHand: return leftHand;
                case Limb.RightHand: return rightHand;
                case Limb.LeftFoot: return leftFoot;
                default: return rightFoot;
            }
        }

        void ValidateStates()
        {
            var names = new List<string> { idleState, dodgeFwdState, dodgeBwdState, hitState };
            foreach (AttackData[] chain in new[] { lightChain, heavyChain, specialChain })
                foreach (AttackData a in chain) names.Add(a.stateName);

            foreach (string n in names)
                if (!animator.HasState(0, Animator.StringToHash(n)))
                    Debug.LogError($"[{name}] Animator has no state named '{n}' on layer 0. Check spelling/case.", this);

            int walkLayer = legsLayer >= 0 ? legsLayer : 0;
            if (!animator.HasState(walkLayer, walkHash))
                Debug.LogError($"[{name}] '{walkState}' not found on Animator layer {walkLayer} " +
                               $"({(legsLayer >= 0 ? "'" + legsLayerName + "'" : "Base Layer")}). Put the walk state on that layer.", this);
        }

        void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying || CurrentAttack == null) return;
            Transform limb = GetLimb(CurrentAttack.limb);
            if (!limb) return;
            bool live = attackNorm >= CurrentAttack.hitStart && attackNorm <= CurrentAttack.hitEnd;
            Gizmos.color = live ? Color.red : Color.yellow;
            Gizmos.DrawWireSphere(limb.position, CurrentAttack.hitRadius);
        }
    }
}