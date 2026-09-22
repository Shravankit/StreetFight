using StreetFight.Code.Abstract;
using UnityEngine;

namespace StreetFight.Code.Effects
{
    public class FightCameraRig : MonoBehaviour
    {
        public Transform target;                 // the hero
        public float distance = 4.5f;
        public float focusHeight = 1.4f;
        public float pitch = 10f;
        public float mouseSensitivity = 2.5f;
        public float followSmooth = 8f;
        public bool lockCursor = true;

        [Header("Side-scroll")]
        [Tooltip("Fixed side-on view: the camera looks along +Z, so screen-right = world +X. Mouse orbit is disabled.")]
        public bool sideScroll = true;
        public float sideYaw = 0f;

        [Header("Fight framing")]
        public float engageRadius = 7f;
        [Range(0f, 1f)] public float opponentPull = 0.35f;   // how far the focus slides toward the enemy
        public float pullBack = 0.35f;                        // extra distance per meter of separation

        [Header("End of fight")]
        [Tooltip("How quickly the camera glides into the end shot (lower = slower, more cinematic)")]
        public float endSmooth = 2.5f;
        [Tooltip("Never come closer than this, even when the fighters are almost touching")]
        public float endMinDistance = 3f;
        [Tooltip("Extra room (meters) on each side so both bodies stay fully in frame")]
        public float endPadding = 1.3f;
        public float endPitch = 6f;                           // slightly lower, more dramatic angle
        public float endYawOffset = 25f;                      // swing a little off the side-on view for depth

        float yaw, smoothDist, endBlend;
        Vector3 smoothFocus;
        Fighter heroFighter, endLoser, endWinner;
        Camera cam;
        bool endShot;

        void OnEnable() { Fighter.FightEnded += OnFightEnded; }
        void OnDisable() { Fighter.FightEnded -= OnFightEnded; }

        void Start()
        {
            if (!target) { enabled = false; return; }
            heroFighter = target.GetComponent<Fighter>();
            cam = GetComponentInChildren<Camera>();
            if (!cam) cam = Camera.main;

            yaw = sideScroll ? sideYaw : transform.eulerAngles.y;
            smoothFocus = target.position + Vector3.up * focusHeight;
            smoothDist = distance;
            if (lockCursor && !sideScroll) Cursor.lockState = CursorLockMode.Locked;
        }

        void OnFightEnded(Fighter loser)
        {
            endLoser = loser;
            endWinner = Fighter.FindNearest(loser.transform.position, loser.team, 50f);   // nearest surviving opponent
            endShot = true;
        }

        void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;   // keeps the camera smooth during the KO slow-mo

            Vector3 focus;
            float dist, smooth = followSmooth, pitchNow = pitch, yawNow;

            bool haveEndSubjects = endShot && (endLoser || endWinner);
            if (haveEndSubjects)
            {
                // ---- end shot: centre on the two fighters, back off just enough to fit both
                Vector3 a = endLoser ? endLoser.FocusPoint : endWinner.FocusPoint;
                Vector3 b = endWinner ? endWinner.FocusPoint : a;
                focus = (a + b) * 0.5f;
                dist = FitDistance(a, b);
                smooth = endSmooth;

                endBlend = Mathf.MoveTowards(endBlend, 1f, dt / 1.2f);
                float e = Mathf.SmoothStep(0f, 1f, endBlend);
                pitchNow = Mathf.Lerp(pitch, endPitch, e);
                yawNow = yaw + endYawOffset * e;
            }
            else
            {
                // ---- normal fight camera
                if (sideScroll) yaw = sideYaw;
                else yaw += Input.GetAxis("Mouse X") * mouseSensitivity;
                yawNow = yaw;

                focus = target.position + Vector3.up * focusHeight;
                dist = distance;

                int heroTeam = heroFighter ? heroFighter.team : 0;
                Fighter opp = Fighter.FindNearest(target.position, heroTeam, engageRadius);
                if (opp)
                {
                    Vector3 o = opp.transform.position + Vector3.up * focusHeight;
                    focus = Vector3.Lerp(focus, o, opponentPull);
                    dist += Vector3.Distance(target.position, opp.transform.position) * pullBack;
                }
            }

            float k = 1f - Mathf.Exp(-smooth * dt);
            smoothFocus = Vector3.Lerp(smoothFocus, focus, k);
            smoothDist = Mathf.Lerp(smoothDist, dist, k);

            Quaternion rot = Quaternion.Euler(pitchNow, yawNow, 0f);
            transform.SetPositionAndRotation(smoothFocus - rot * Vector3.forward * smoothDist, rot);
        }

        /// <summary>Distance at which both points fit horizontally in the camera's view, plus padding.</summary>
        float FitDistance(Vector3 a, Vector3 b)
        {
            float vFov = (cam ? cam.fieldOfView : 60f) * Mathf.Deg2Rad;
            float aspect = cam ? cam.aspect : 16f / 9f;
            float hFov = 2f * Mathf.Atan(Mathf.Tan(vFov * 0.5f) * aspect);

            float halfWidth = Vector3.Distance(a, b) * 0.5f + endPadding;
            return Mathf.Max(endMinDistance, halfWidth / Mathf.Tan(hFov * 0.5f));
        }
    }
}