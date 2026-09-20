using UnityEngine;

namespace StreetFight.Code.Effects
{
    [RequireComponent(typeof(Camera))]
    public class CameraShake : MonoBehaviour
    {
        public static CameraShake Instance { get; private set; }

        [Header("Shake")]
        public float maxAngle = 4f;         // degrees at trauma = 1
        public float maxOffset = 0.12f;     // meters at trauma = 1
        public float frequency = 22f;
        public float traumaDecay = 1.6f;    // trauma lost per second

        [Header("FOV punch")]
        public float fovRecoverSpeed = 8f;

        Camera cam;
        float baseFov, trauma, fovOffset, seed;
        Vector3 baseLocalPos;
        Quaternion baseLocalRot;

        void Awake()
        {
            Instance = this;
            cam = GetComponent<Camera>();
            baseFov = cam.fieldOfView;
            baseLocalPos = transform.localPosition;
            baseLocalRot = transform.localRotation;
            seed = Random.value * 100f;
        }

        /// <param name="amount">0..1 trauma to add (0.2 light hit, 0.45 heavy, 0.6 KO)</param>
        /// <param name="fovPunch">degrees of quick zoom-in that eases back out</param>
        public static void Shake(float amount, float fovPunch = 0f)
        {
            if (Instance == null) return;
            Instance.trauma = Mathf.Clamp01(Instance.trauma + amount);
            Instance.fovOffset -= fovPunch;
        }

        void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;   // keeps shaking correctly during hit-stop / slow-mo
            trauma = Mathf.Max(0f, trauma - traumaDecay * dt);
            fovOffset = Mathf.Lerp(fovOffset, 0f, 1f - Mathf.Exp(-fovRecoverSpeed * dt));

            float s = trauma * trauma;           // squared = subtle at low trauma, violent at high
            float t = Time.unscaledTime * frequency;

            Vector3 pos = new Vector3(Noise(t, 0), Noise(t, 1), 0f) * (maxOffset * s);
            Vector3 rot = new Vector3(Noise(t, 2), Noise(t, 3), Noise(t, 4)) * (maxAngle * s);

            transform.localPosition = baseLocalPos + pos;
            transform.localRotation = baseLocalRot * Quaternion.Euler(rot);
            cam.fieldOfView = baseFov + fovOffset;
        }

        float Noise(float t, float channel)
        {
            return (Mathf.PerlinNoise(seed + channel * 17.3f, t) - 0.5f) * 2f;
        }
    }
}