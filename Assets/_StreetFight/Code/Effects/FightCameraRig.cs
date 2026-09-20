using StreetFight.Code.Abstract;
using UnityEngine;

namespace StreetFight.Code.Effects
{
    public class FightCameraRig : MonoBehaviour
    {
        public Transform target;                 // the hero
        public float distance = 4.5f;
        public float focusHeight = 1.4f;
        public float pitch = 14f;
        public float mouseSensitivity = 2.5f;
        public float followSmooth = 8f;
        public bool lockCursor = true;

        [Header("Fight framing")]
        public float engageRadius = 7f;
        [Range(0f, 1f)] public float opponentPull = 0.35f;   // how far the focus slides toward the enemy
        public float pullBack = 0.35f;                        // extra distance per meter of separation

        float yaw, smoothDist;
        Vector3 smoothFocus;
        Fighter heroFighter;

        void Start()
        {
            if (!target) { enabled = false; return; }
            heroFighter = target.GetComponent<Fighter>();
            yaw = transform.eulerAngles.y;
            smoothFocus = target.position + Vector3.up * focusHeight;
            smoothDist = distance;
            if (lockCursor) Cursor.lockState = CursorLockMode.Locked;
        }

        void LateUpdate()
        {
            yaw += Input.GetAxis("Mouse X") * mouseSensitivity;

            Vector3 focus = target.position + Vector3.up * focusHeight;
            float dist = distance;

            int heroTeam = heroFighter ? heroFighter.team : 0;
            Fighter opp = Fighter.FindNearest(target.position, heroTeam, engageRadius);
            if (opp)
            {
                Vector3 o = opp.transform.position + Vector3.up * focusHeight;
                focus = Vector3.Lerp(focus, o, opponentPull);
                dist += Vector3.Distance(target.position, opp.transform.position) * pullBack;
            }

            float k = 1f - Mathf.Exp(-followSmooth * Time.deltaTime);
            smoothFocus = Vector3.Lerp(smoothFocus, focus, k);
            smoothDist = Mathf.Lerp(smoothDist, dist, k);

            Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
            transform.SetPositionAndRotation(smoothFocus - rot * Vector3.forward * smoothDist, rot);
        }
    }
}