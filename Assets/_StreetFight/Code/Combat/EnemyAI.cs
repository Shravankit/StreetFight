using System.Collections;
using StreetFight.Code.Abstract;
using StreetFight.Code.Data;
using UnityEngine;

namespace StreetFight.Code.Combat
{
    public class EnemyAI : Fighter
    {
        [Header("Target")]
        public Fighter target;              // auto-found if left empty
        public float sightRadius = 15f;

        [Header("Aggression")]
        public float attackRange = 1.5f;
        public Vector2 thinkTime = new Vector2(0.5f, 1.4f);       // pause between combos
        public Vector2Int comboLength = new Vector2Int(1, 3);
        public float lightWeight = 0.6f;
        public float heavyWeight = 0.3f;
        public float specialWeight = 0.1f;

        [Header("Defense")]
        [Range(0f, 1f)] public float dodgeChance = 0.3f;
        public Vector2 dodgeReaction = new Vector2(0.1f, 0.25f);  // seconds; fast attacks can beat this

        Fighter subscribedTarget;
        FighterState prevState;
        AttackData[] comboChain;
        int comboIndex, comboRemaining;
        float nextDecisionTime;
        bool inRange;

        protected override void Awake()
        {
            base.Awake();
            team = 1;
            nextDecisionTime = Time.time + Random.Range(thinkTime.x, thinkTime.y);
        }

        protected override void OnDisable()
        {
            if (subscribedTarget != null) subscribedTarget.AttackStarted -= OnTargetAttack;
            subscribedTarget = null;
            base.OnDisable();
        }

        protected override Fighter GetAimTarget()
        {
            return target != null && !target.IsDead ? target : null;
        }

        protected override void Tick()
        {
            if (FightOver) { if (State == FighterState.Free) SetMoving(false); return; }

            if (target == null || target.IsDead)
                target = FindNearest(transform.position, team, sightRadius);
            if (target != subscribedTarget) SetTarget(target);

            if (target == null) { if (State == FighterState.Free) SetMoving(false); return; }

            Vector3 to = Flat(target.transform.position - transform.position);
            float dist = to.magnitude;

            // after a combo (or getting hit) wait a moment before the next decision
            if (prevState == FighterState.Attacking && State != FighterState.Attacking)
                nextDecisionTime = Time.time + Random.Range(thinkTime.x, thinkTime.y);
            prevState = State;

            // ---- queue the remaining hits of a planned combo
            if (State == FighterState.Attacking)
            {
                if (comboRemaining > 0 && queuedAttack == null)
                {
                    if (dist > attackRange * 1.8f) comboRemaining = 0;   // hero escaped - abort
                    else { comboIndex++; RequestAttack(comboChain[comboIndex]); comboRemaining--; }
                }
                return;
            }
            comboRemaining = 0;
            if (State != FighterState.Free) return;

            // ---- move / attack
            if (dist > sightRadius) { SetMoving(false); return; }

            if (inRange && dist > attackRange + 0.4f) inRange = false;   // hysteresis so we don't jitter
            if (!inRange && dist <= attackRange) inRange = true;

            RotateTowards(to, turnSpeed);
            if (!inRange)
            {
                SetMoving(true);
                MoveBy(to.normalized * (walkSpeed * Time.deltaTime));   // straight along the lane toward the hero
            }
            else
            {
                SetMoving(false);
                if (Time.time >= nextDecisionTime) StartCombo();
            }
        }

        void StartCombo()
        {
            comboChain = PickChain();
            int len = Mathf.Clamp(Random.Range(comboLength.x, comboLength.y + 1), 1, comboChain.Length);
            comboIndex = 0;
            comboRemaining = len - 1;
            RequestAttack(comboChain[0]);
        }

        AttackData[] PickChain()
        {
            float total = lightWeight + heavyWeight + specialWeight;
            float r = Random.value * total;
            if (r < lightWeight) return lightChain;
            if (r < lightWeight + heavyWeight) return heavyChain;
            return specialChain;
        }

        // ---- reactive dodge
        void SetTarget(Fighter t)
        {
            if (subscribedTarget != null) subscribedTarget.AttackStarted -= OnTargetAttack;
            target = t;
            subscribedTarget = t;
            if (t != null) t.AttackStarted += OnTargetAttack;
        }

        void OnTargetAttack(Fighter attacker, AttackData attack)
        {
            if (State != FighterState.Free) return;
            if (Random.value > dodgeChance) return;
            if (Flat(attacker.transform.position - transform.position).magnitude > attackRange * 1.6f) return;
            StartCoroutine(ReactAndDodge());
        }

        IEnumerator ReactAndDodge()
        {
            yield return new WaitForSeconds(Random.Range(dodgeReaction.x, dodgeReaction.y));
            if (State == FighterState.Free) TryDodge(-transform.forward, Vector3.zero, false);
        }
    }
}