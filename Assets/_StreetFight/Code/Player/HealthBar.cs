using StreetFight.Code.Abstract;
using StreetFight.Code.Data;
using UnityEngine;
using UnityEngine.UI;

namespace StreetFight.Code.PLayer
{
    public class HealthBar : MonoBehaviour
    {
        [Header("Source")]
        [Tooltip("Leave empty to use the Fighter on a parent object (enemy bars parented to the enemy).")]
        public Fighter fighter;

        [Header("Visuals (assign what you use)")]
        [Tooltip("Image with Type = Filled, Method = Horizontal")]
        public Image fill;
        [Tooltip("Optional. Pale 'recent damage' bar drawn BEHIND the fill; it drains slowly after a hit.")]
        public Image trail;
        [Tooltip("Optional. Use a Slider instead of (or as well as) the fill Image.")]
        public Slider slider;
        public bool colorByHealth = true;
        public Color fullColor = new Color(0.35f, 0.85f, 0.35f);
        public Color lowColor = new Color(0.9f, 0.2f, 0.2f);

        [Header("Behaviour")]
        public float trailDelay = 0.4f;      // seconds before the trail starts draining
        public float trailSpeed = 1.2f;      // bar-fractions per second
        public float hitPunch = 0.08f;       // quick scale pop when damaged (0 = off)
        public bool hideWhenFull = false;    // nice for enemies: bar appears only after the first hit
        public bool hideOnDeath = true;      // untick for the hero
        public bool faceCamera = false;      // tick for world-space enemy bars

        CanvasGroup group;
        Camera cam;
        Vector3 baseScale;
        float trailValue, trailTimer, punch;

        void Awake()
        {
            if (!fighter) fighter = GetComponentInParent<Fighter>();
            group = GetComponent<CanvasGroup>();
            if (!group) group = gameObject.AddComponent<CanvasGroup>();
            baseScale = transform.localScale;
            cam = Camera.main;
        }

        void OnEnable()
        {
            if (fighter != null) fighter.Damaged += OnDamaged;
        }

        void OnDisable()
        {
            if (fighter != null) fighter.Damaged -= OnDamaged;
        }

        void Start()
        {
            if (fighter == null)
            {
                Debug.LogError("[HealthBar] No Fighter assigned. Drag the hero/enemy into the Fighter slot.", this);
                enabled = false;
                return;
            }
            trailValue = fighter.HealthNormalized;
            Apply(trailValue, trailValue);
            group.alpha = ShouldShow() ? 1f : 0f;
        }

        void OnDamaged(Fighter f, HitInfo hit)
        {
            trailTimer = trailDelay;   // trail waits, then catches up
            punch = 1f;
        }

        void Update()
        {
            if (fighter == null) { Destroy(gameObject); return; }   // enemy was removed

            float dt = Time.deltaTime;
            float hp = fighter.HealthNormalized;

            // trail: hold, then drain toward current health
            if (trailValue > hp)
            {
                if (trailTimer > 0f) trailTimer -= dt;
                else trailValue = Mathf.MoveTowards(trailValue, hp, trailSpeed * dt);
            }
            else trailValue = hp;

            Apply(hp, trailValue);

            // fade in/out
            group.alpha = Mathf.MoveTowards(group.alpha, ShouldShow() ? 1f : 0f, 4f * dt);

            // hit punch
            if (hitPunch > 0f)
            {
                punch = Mathf.MoveTowards(punch, 0f, 6f * dt);
                transform.localScale = baseScale * (1f + hitPunch * punch);
            }
        }

        void LateUpdate()
        {
            if (faceCamera && cam) transform.rotation = cam.transform.rotation;   // billboard
        }

        bool ShouldShow()
        {
            if (hideOnDeath && fighter.IsDead) return false;
            if (hideWhenFull && fighter.HealthNormalized >= 0.999f) return false;
            return true;
        }

        void Apply(float hp, float trailAmount)
        {
            if (fill)
            {
                fill.fillAmount = hp;
                if (colorByHealth) fill.color = Color.Lerp(lowColor, fullColor, hp);
            }
            if (trail) trail.fillAmount = trailAmount;
            if (slider) slider.value = Mathf.Lerp(slider.minValue, slider.maxValue, hp);   // works with 0-1, 0-100, etc.
        }
    }
}