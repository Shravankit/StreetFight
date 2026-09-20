using StreetFight.Code.Abstract;
using StreetFight.Code.Data;
using UnityEngine;

namespace StreetFight.Code.Combat
{
    public class HeroController : Fighter
    {
        [Header("Input")]
        public KeyCode lightKey = KeyCode.Mouse0;
        public KeyCode heavyKey = KeyCode.Mouse1;
        public KeyCode specialKey = KeyCode.Q;
        public KeyCode dodgeKey = KeyCode.Space;
        [Tooltip("Side-scroll only: tick if D moves you LEFT on screen (camera looking the other way)")]
        public bool flipInput = false;

        Camera cam;
        Vector3 moveInput;

        public override bool IsPlayer => true;

        protected override void Awake()
        {
            base.Awake();
            team = 0;
            cam = Camera.main;   // camera must be tagged "MainCamera"
        }

        protected override Fighter GetAimTarget()
        {
            // aim toward whoever you're steering at; falls back to where you're facing
            Vector3 facing = moveInput.sqrMagnitude > 0.01f ? moveInput : transform.forward;
            return FindNearest(transform.position, team, aimAssistRadius, facing, 100f);
        }

        protected override void Tick()
        {
            if (FightOver) { SetMoving(false); return; }   // fight ended: stand still while the end camera plays
            moveInput = ReadMove();

            if (Input.GetKeyDown(dodgeKey)) HeroDodge();
            if (Input.GetKeyDown(lightKey)) PressAttack(lightChain);
            if (Input.GetKeyDown(heavyKey)) PressAttack(heavyChain);
            if (Input.GetKeyDown(specialKey)) PressAttack(specialChain);

            if (State == FighterState.Free)
            {
                bool moving = moveInput.sqrMagnitude > 0.01f;
                SetMoving(moving);
                if (moving)
                {
                    RotateTowards(moveInput, turnSpeed);
                    Vector3 step = sideScroll ? moveInput : transform.forward * moveInput.magnitude;   // side-scroll: no waiting for the turn
                    MoveBy(step * (walkSpeed * Time.deltaTime));
                }
            }
        }

        Vector3 ReadMove()
        {
            float h = Input.GetAxisRaw("Horizontal");
            if (sideScroll)
                return Vector3.right * (flipInput ? -h : h);   // A/D or arrows only; W/S are ignored

            float v = Input.GetAxisRaw("Vertical");
            Vector3 fwd = cam ? Flat(cam.transform.forward).normalized : Vector3.forward;
            Vector3 right = cam ? Flat(cam.transform.right).normalized : Vector3.right;
            return Vector3.ClampMagnitude(fwd * v + right * h, 1f);
        }

        void PressAttack(AttackData[] chain)
        {
            if (chain == null || chain.Length == 0) return;

            // If we're mid-attack and it belongs to this chain, continue the chain; otherwise start it.
            int idx = 0;
            if (CurrentAttack != null)
            {
                int i = System.Array.IndexOf(chain, CurrentAttack);
                if (i >= 0) idx = (i + 1) % chain.Length;
            }
            RequestAttack(chain[idx]);   // buffered automatically if we're mid-attack
        }

        void HeroDodge()
        {
            Fighter t = FindNearest(transform.position, team, 6f);
            Vector3 toTarget = t ? Flat(t.transform.position - transform.position).normalized : Vector3.zero;

            if (moveInput.sqrMagnitude > 0.01f)
            {
                Vector3 dir = moveInput.normalized;
                bool away = t != null && Vector3.Dot(dir, toTarget) < -0.3f;
                if (away) TryDodge(dir, toTarget, false);   // keep facing the enemy, step back
                else TryDodge(dir, dir, true);         // burst toward the stick direction
            }
            else
            {
                TryDodge(-transform.forward, Vector3.zero, false);   // no input: back-step
            }
        }
    }
}