namespace MagicTraining
{
    using UnityEngine;

    [DefaultExecutionOrder(-30)]
    public class FireballCaster : MonoBehaviour
    {
        public float formTime = 1.1f;
        public float spawnDelay = 0.12f;
        public float minThrowSpeed = 1.2f;
        public float throwBoost = 1.7f;
        [Range(0f, 1f)] public float aimAssist = 0.15f;
        public float growTime = 3f;
        public float maxGrow = 5f;

        class Slot { public Fireball ball; public MagicCircle circle; public bool pending; public float timer, haptic; }
        readonly Slot sl = new Slot(), sr = new Slot();

        public static bool HoldingAny { get; private set; }

        void OnDisable()
        {
            foreach (var s in new[] { sl, sr })
            {
                if (s.ball != null) Destroy(s.ball.gameObject);
                if (s.circle != null) Destroy(s.circle.gameObject);
                s.ball = null; s.circle = null; s.pending = false;
            }
            HoldingAny = false;
            SpellLock.Set(Spell.Fireball, false);
        }

        void Update()
        {
            var rig = MagicRig.I;
            if (rig == null || !rig.Ready) return;
            Handle(rig, rig.L, sl);
            Handle(rig, rig.R, sr);
            HoldingAny = sl.ball != null || sr.ball != null;
            SpellLock.Set(Spell.Fireball, HoldingAny || sl.pending || sr.pending);
        }

        static void ReleaseCircle(Slot s, bool burst)
        {
            if (s.circle != null) s.circle.Release(burst);
            s.circle = null;
        }

        void Handle(MagicRig rig, MagicRig.Hand h, Slot s)
        {
            if (h.grip && h.trigger)
            {
                s.pending = false;
                if (s.ball != null) { s.ball.Dissolve(true); s.ball = null; ReleaseCircle(s, false); }
                return;
            }
            if (LightningCaster.Active)
            {
                if (s.ball != null) { s.ball.Dissolve(true); s.ball = null; }
                ReleaseCircle(s, false);
                s.pending = false;
                return;
            }

            if (h.gripDown && !h.gripConsumed && s.ball == null && SpellLock.CanStart(Spell.Fireball))
            {
                s.pending = true;
                s.timer = spawnDelay;
                SpellLock.Set(Spell.Fireball, true);
            }

            if (s.pending)
            {
                if (!h.grip || h.gripConsumed) s.pending = false;
                else if ((s.timer -= Time.deltaTime) <= 0f)
                {
                    s.pending = false;
                    if (s.ball == null && !rig.IsGrabbingWithXRI(h))
                    {
                        s.ball = Fireball.Create(rig.IgnoreRoot, h.Palm);
                        s.ball.maxGrow = maxGrow;
                        s.circle = MagicCircle.Create(MagicCircle.Element.Fire);
                    }
                }
            }

            if (s.ball == null) return;

            if (h.grip)
            {
                bool growing = rig.L.primary && !DruidStaff.IsHeld && s.ball.Grow < maxGrow;
                HoldBall(rig, h, s, growing);
            }
            else
            {
                bool thrown = h.velocity.magnitude >= minThrowSpeed;
                if (thrown)
                {
                    s.ball.Throw(rig.ThrowVelocity(h, throwBoost, aimAssist));
                    rig.Haptic(h.left, 0.6f, 0.1f);
                }
                else s.ball.Dissolve(false);
                ReleaseCircle(s, thrown);
                s.ball = null;
            }
        }

        void HoldBall(MagicRig rig, MagicRig.Hand h, Slot s, bool growing)
        {
            {
                if (growing) s.ball.GrowBigger(Time.deltaTime, growTime);

                Vector3 pos = h.Palm + h.t.up * (s.ball.Radius - 0.08f * Mathf.Lerp(0.35f, 1f, s.ball.Charge));
                s.ball.Hold(pos, Time.deltaTime, formTime);

                if (s.circle != null)
                    s.circle.Follow(pos - h.t.up * (s.ball.Radius + 0.03f), Quaternion.LookRotation(h.t.up),
                        s.ball.Radius * 3.2f + 0.06f, growing ? 3f : 1f + s.ball.Charge);

                s.haptic -= Time.deltaTime;
                if (s.haptic <= 0f)
                {
                    s.haptic = growing ? 0.06f : 0.1f;
                    float g01 = Mathf.InverseLerp(1f, maxGrow, s.ball.Grow);
                    rig.Haptic(h.left, Mathf.Lerp(0.05f, 0.2f, s.ball.Charge) + (growing ? 0.15f + 0.4f * g01 : 0.2f * g01), 0.08f);
                }
            }
        }
    }
}

namespace MagicTraining
{
    using UnityEngine;
    using static MagicTraining.MagicFX;
    using MinMaxCurve = UnityEngine.ParticleSystem.MinMaxCurve;

    public class Fireball : MonoBehaviour
    {
        public float gravity = 3.5f;
        public float maxLifetime = 6f;

        Transform ignoreRoot;
        ParticleSystem core, halo, flames, orbit, embers;
        Light glow;
        AudioSource roar;
        TrailRenderer trail;

        float charge, life, grow = 1f;
        public float maxGrow = 5f;
        bool flying, dead;
        Vector3 velocity;

        public float Charge => charge;
        public float Grow => grow;
        public float Radius => 0.08f * Scale;
        float Scale => Mathf.Lerp(0.35f, 1f, charge) * grow;

        public static Fireball Create(Transform ignoreRoot, Vector3 pos)
        {
            var go = new GameObject("Fireball");
            go.transform.position = pos;
            var f = go.AddComponent<Fireball>();
            f.ignoreRoot = ignoreRoot;
            f.Build();
            return f;
        }

        void Build()
        {
            Transform t = transform;

            core = NewPS("Core", t, GlowMat, false, 40);
            {
                var m = core.main;
                m.startLifetime = new MinMaxCurve(0.08f, 0.16f);
                m.startSize = new MinMaxCurve(0.18f, 0.24f);
                m.startColor = A(FireWhite, 0.9f);
                var col = core.colorOverLifetime; col.enabled = true; col.color = FadeInOut;
                SetRate(core, 60f);
            }

            halo = NewPS("Halo", t, FlameMat, false, 150);
            {
                var m = halo.main;
                m.startLifetime = new MinMaxCurve(0.2f, 0.4f);
                m.startSize = new MinMaxCurve(0.08f, 0.16f);
                m.startSpeed = new MinMaxCurve(0.05f, 0.15f);
                m.startRotation = new MinMaxCurve(0f, Mathf.PI * 2f);
                var sh = halo.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.04f;
                var vel = halo.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.Local;
                vel.x = 0f; vel.y = 0f; vel.z = 0f;
                vel.orbitalX = 0f; vel.orbitalY = 4f; vel.orbitalZ = 0f;
                var col = halo.colorOverLifetime; col.enabled = true; col.color = FireOverLife;
                var rot = halo.rotationOverLifetime; rot.enabled = true; rot.z = new MinMaxCurve(-2f, 2f);
                SetRate(halo, 70f);
            }

            flames = NewPS("Flames", t, FlameMat, true, 500);
            {
                var m = flames.main;
                m.startLifetime = new MinMaxCurve(0.3f, 0.6f);
                m.startSize = new MinMaxCurve(0.06f, 0.14f);
                m.startSpeed = new MinMaxCurve(0.05f, 0.25f);
                m.startRotation = new MinMaxCurve(0f, Mathf.PI * 2f);
                m.gravityModifier = -0.2f;
                var sh = flames.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.05f;
                var col = flames.colorOverLifetime; col.enabled = true; col.color = FireOverLife;
                var sz = flames.sizeOverLifetime; sz.enabled = true; sz.size = new MinMaxCurve(1f, Curve((0f, 0.6f), (0.2f, 1f), (1f, 0.2f)));
                var rot = flames.rotationOverLifetime; rot.enabled = true; rot.z = new MinMaxCurve(-2f, 2f);
                var nz = flames.noise; nz.enabled = true; nz.strength = 0.3f; nz.frequency = 2f; nz.scrollSpeed = 1.2f;
                var em = flames.emission; em.rateOverDistance = 40f;
            }

            orbit = NewPS("Orbit Sparks", t, SparkMat, false, 120);
            {
                var m = orbit.main;
                m.startLifetime = 0.8f;
                m.startSize = new MinMaxCurve(0.01f, 0.02f);
                m.startColor = RandomFrom(ArcanePalette);
                var sh = orbit.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.09f; sh.radiusThickness = 0f;
                var vel = orbit.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.Local;
                vel.x = 0f; vel.y = 0f; vel.z = 0f;
                vel.orbitalX = 3f; vel.orbitalY = 5f; vel.orbitalZ = 2f;
                var col = orbit.colorOverLifetime; col.enabled = true; col.color = FadeInOut;
            }

            embers = NewPS("Embers", t, SparkMat, true, 120);
            {
                var m = embers.main;
                m.startLifetime = new MinMaxCurve(0.5f, 1.2f);
                m.startSize = new MinMaxCurve(0.005f, 0.012f);
                m.startSpeed = new MinMaxCurve(0.1f, 0.4f);
                m.gravityModifier = 0.2f;
                m.startColor = RandomFrom(Grad((0f, FireYellow), (1f, FireOrange)));
                var sh = embers.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.06f;
                var col = embers.colorOverLifetime; col.enabled = true; col.color = FadeInOut;
                var em = embers.emission; em.rateOverTime = 12f; em.rateOverDistance = 10f;
            }

            glow = NewLight(t, Vector3.zero, new Color(1f, 0.5f, 0.2f), 1.5f, 0f);

            trail = NewTrail("Trail", t, BeamMat, Vector3.zero, 0.25f, 0.12f, Curve((0f, 1f), (1f, 0f)),
                Grad((0f, A(FireYellow, 0.8f)), (0.4f, A(FireOrange, 0.6f)), (1f, A(ArcaneViolet, 0f))));

            roar = gameObject.AddComponent<AudioSource>();
            roar.clip = FireLoopClip;
            roar.loop = true;
            roar.spatialBlend = 1f;
            roar.minDistance = 0.6f;
            roar.maxDistance = 30f;
            roar.dopplerLevel = 0f;
            roar.volume = 0.1f;
            roar.Play();

            ApplyLook();
            core.Play(); halo.Play(); flames.Play(); orbit.Play(); embers.Play();
            PlayAt(WhooshClip, transform.position, 0.25f, 0.55f);
        }

        void ApplyLook()
        {
            float flick = 0.85f + 0.3f * Mathf.PerlinNoise(Time.time * 12f, 0.7f);
            transform.localScale = Vector3.one * Scale;
            float g01 = Mathf.InverseLerp(1f, maxGrow, grow);
            glow.intensity = Mathf.Lerp(0.6f, 2.5f, charge) * flick * Mathf.Lerp(1f, 2.5f, g01);
            glow.range = Mathf.Lerp(0.8f, 2.5f, charge) * grow;
            roar.volume = Mathf.Lerp(0.1f, 0.4f, charge) + 0.4f * g01;
            roar.pitch = Mathf.Lerp(1.3f, 0.9f, charge) * Mathf.Lerp(1f, 0.55f, g01);
            SetRate(flames, Mathf.Lerp(20f, 70f, charge));
            SetRate(orbit, Mathf.Lerp(10f, 35f, charge));
        }

        public void Hold(Vector3 pos, float dt, float formTime)
        {
            if (flying || dead) return;
            charge = Mathf.MoveTowards(charge, 1f, dt / Mathf.Max(0.05f, formTime));
            transform.position = pos;
            ApplyLook();
        }

        public void GrowBigger(float dt, float growTime)
        {
            if (flying || dead || grow >= maxGrow) return;
            float before = grow;
            grow = Mathf.MoveTowards(grow, maxGrow, (maxGrow - 1f) * dt / Mathf.Max(0.05f, growTime));
            if (grow >= maxGrow && before < maxGrow)
                SpawnChargeFlash(transform.position, Quaternion.LookRotation(Vector3.up));
        }

        public void Throw(Vector3 vel)
        {
            if (flying || dead) return;
            flying = true;
            velocity = vel;
            life = 0f;
            trail.Clear();
            trail.emitting = true;
            trail.widthMultiplier = 0.12f * Mathf.Lerp(0.5f, 1f, charge) * grow;
            var em = flames.emission; em.rateOverDistance = Mathf.Lerp(30f, 70f, charge);
            roar.dopplerLevel = 0.8f;
            PlayAt(WhooshClip, transform.position, 0.6f, 0.75f);
        }

        public void Dissolve(bool quiet)
        {
            if (dead) return;
            dead = true;
            if (!quiet) SpawnFizzle(transform.position, Quaternion.identity);
            DetachPS(flames, 1f);
            DetachPS(embers, 1.3f);
            Destroy(gameObject);
        }

        void Update()
        {
            if (!flying || dead) return;
            float dt = Time.deltaTime;
            life += dt;
            velocity += Vector3.down * gravity * dt;
            ApplyLook();

            Vector3 step = velocity * dt;
            float dist = step.magnitude;
            if (dist > 0f && CastIgnoring(transform.position, step / dist, dist, Radius, ignoreRoot,
                    out Vector3 point, out Vector3 normal, out _))
            {
                transform.position = point;
                Explode(point, normal);
                return;
            }
            transform.position += step;
            if (life >= maxLifetime) Explode(transform.position, -velocity.normalized);
        }

        void Explode(Vector3 point, Vector3 normal)
        {
            if (dead) return;
            dead = true;
            float p = Mathf.Lerp(0.35f, 1f, charge);
            SpawnImpact(point, normal, p, grow);
            PushBodies(point, Mathf.Lerp(1f, 2.8f, p) * grow, Mathf.Lerp(2.5f, 8f, p) * grow, ignoreRoot);
            DetachPS(flames, 1f);
            DetachPS(embers, 1.3f);
            DetachTrail(trail);
            Destroy(gameObject);
        }
    }
}

namespace MagicTraining
{
    using UnityEngine;

    [DefaultExecutionOrder(0)]
    public class FireArrowCaster : MonoBehaviour
    {
        [Header("Carga")]
        public float chargeTime = 1.4f;
        public float fullDrawDistance = 0.55f;
        public float minPowerToFire = 0.08f;

        public static bool IsCharging { get; private set; }

        static void SetCharging(bool v)
        {
            IsCharging = v;
            SpellLock.Set(Spell.FireArrow, v);
        }

        bool backIsLeft, fullHapticDone;
        float charge, hapticTimer;
        Vector3 lastDir = Vector3.forward, lastFront;
        float lastDraw;
        FireArrow arrow;

        void OnDisable()
        {
            SetCharging(false);
            if (arrow != null) Destroy(arrow.gameObject);
            arrow = null;
        }

        void Update()
        {
            var rig = MagicRig.I;
            if (rig == null || !rig.Ready) return;
            bool lHeld = rig.L.trigger, rHeld = rig.R.trigger;

            if (!IsCharging)
            {
                if (lHeld && rHeld && !MagicRig.FreeGrip(rig.L) && !MagicRig.FreeGrip(rig.R) && SpellLock.CanStart(Spell.FireArrow)) BeginCharge(rig);
                return;
            }

            if (arrow == null) { SetCharging(false); return; }

            UpdateBackFront(rig);

            if (!lHeld || !rHeld)
            {
                bool backReleased = backIsLeft ? !lHeld : !rHeld;
                if (backReleased) Fire(rig); else CancelCharge();
                return;
            }

            charge = Mathf.MoveTowards(charge, 1f, Time.deltaTime / Mathf.Max(0.05f, chargeTime));
            UpdateArrowPose(rig);
            UpdateHaptics(rig);
        }

        void BeginCharge(MagicRig rig)
        {
            Transform head = rig.Head;
            Vector3 f = head.forward;
            BeginCharge(rig, Vector3.Dot(rig.L.Position - head.position, f) < Vector3.Dot(rig.R.Position - head.position, f));
        }

        void BeginCharge(MagicRig rig, bool leftIsBack)
        {
            SetCharging(true);
            charge = 0f;
            fullHapticDone = false;
            hapticTimer = 0f;
            backIsLeft = leftIsBack;
            arrow = FireArrow.Create(rig.IgnoreRoot);
            UpdateArrowPose(rig);
        }

        void UpdateBackFront(MagicRig rig)
        {
            Transform head = rig.Head;
            Vector3 f = head.forward;
            float dl = Vector3.Dot(rig.L.Position - head.position, f);
            float dr = Vector3.Dot(rig.R.Position - head.position, f);
            const float hysteresis = 0.04f;
            if (backIsLeft && dl > dr + hysteresis) backIsLeft = false;
            else if (!backIsLeft && dr > dl + hysteresis) backIsLeft = true;
        }

        void UpdateArrowPose(MagicRig rig)
        {
            Transform head = rig.Head;
            Transform back = rig.Get(backIsLeft).t;
            Transform front = rig.Get(!backIsLeft).t;
            Vector3 frontAim = rig.Get(!backIsLeft).AimForward;
            Vector3 d = front.position - back.position;
            float dist = d.magnitude;
            Vector3 aim = dist > 0.001f ? d / dist : frontAim;
            float w = Mathf.InverseLerp(0.06f, 0.2f, dist);
            Vector3 dir = Vector3.Slerp(frontAim, aim, w).normalized;
            if (dir.sqrMagnitude < 0.5f) dir = head.forward;

            Vector3 up = head.up;
            if (Mathf.Abs(Vector3.Dot(dir, up)) > 0.98f) up = head.forward;

            lastDir = dir;
            lastFront = front.position;
            lastDraw = Mathf.Clamp01(dist / Mathf.Max(0.05f, fullDrawDistance));
            arrow.SetCharging(charge, back.position, dir, up, front.position);
        }

        void Fire(MagicRig rig)
        {
            SetCharging(false);
            if (arrow == null) return;
            float power = charge * Mathf.Lerp(0.55f, 1f, lastDraw);
            if (power < minPowerToFire) { CancelCharge(); return; }
            arrow.Launch(lastDir, power, lastFront);
            arrow = null;
            rig.Haptic(backIsLeft, 0.9f, 0.15f);
            rig.Haptic(!backIsLeft, 0.5f, 0.08f);
        }

        void CancelCharge()
        {
            SetCharging(false);
            if (arrow != null) arrow.Cancel();
            arrow = null;
        }

        void UpdateHaptics(MagicRig rig)
        {
            if (charge >= 1f && !fullHapticDone)
            {
                fullHapticDone = true;
                rig.Haptic(true, 0.7f, 0.12f);
                rig.Haptic(false, 0.7f, 0.12f);
                hapticTimer = 0.15f;
                return;
            }
            hapticTimer -= Time.deltaTime;
            if (hapticTimer > 0f) return;
            hapticTimer = 0.08f;
            float amp = Mathf.Lerp(0.05f, 0.3f, charge);
            rig.Haptic(backIsLeft, amp * 1.2f, 0.1f);
            rig.Haptic(!backIsLeft, amp * 0.6f, 0.1f);
        }
    }
}

namespace MagicTraining
{
    using System.Collections.Generic;
    using UnityEngine;
    using static MagicTraining.MagicFX;
    using MinMaxCurve = UnityEngine.ParticleSystem.MinMaxCurve;

    public class FireArrow : MonoBehaviour
    {
        public const float Length = 0.75f;

        public float minSpeed = 16f;
        public float maxSpeed = 42f;
        public float maxLifetime = 4f;
        public float hitRadius = 0.04f;

        Transform ignoreRoot;

        LineRenderer core, outer, head, headGlow;
        readonly List<LineRenderer> vanes = new List<LineRenderer>();
        ParticleSystem flames, spiral, embers, tipGlow, nockGlow, gather;
        TrailRenderer trail, trailCore;
        Light glowLight;
        AudioSource hum;

        Transform circleRoot, circleOuter, circleInner, circleGlow;
        Renderer outerR, innerR, glowR;

        float charge, power, life, vis;
        bool flying, dead, fullChargeShown;
        Vector3 velocity;

        public float Charge => charge;

        public static FireArrow Create(Transform ignoreRoot)
        {
            var go = new GameObject("Fire Arrow");
            var arrow = go.AddComponent<FireArrow>();
            arrow.ignoreRoot = ignoreRoot;
            arrow.Build();
            return arrow;
        }

        void Build()
        {
            Transform t = transform;
            Vector3 Z = Vector3.forward;

            outer = NewLine("Outer Glow", t, BeamMat, Z * 0.02f, Z * (Length - 0.05f),
                Curve((0f, 0.5f), (0.5f, 1f), (1f, 0.8f)), 0.07f,
                Grad((0f, A(ArcaneViolet, 0.5f)), (0.4f, A(FireRed, 0.7f)), (1f, A(FireOrange, 0.9f))));

            core = NewLine("Core", t, CoreMat, Z * 0.03f, Z * (Length - 0.1f),
                Curve((0f, 0.4f), (0.2f, 1f), (1f, 0.8f)), 0.022f,
                Grad((0f, A(FireOrange, 0.7f)), (0.4f, FireYellow), (1f, FireWhite)));

            headGlow = NewLine("Head Glow", t, BeamMat, Z * (Length - 0.2f), Z * (Length + 0.06f),
                Curve((0f, 0.3f), (0.4f, 1f), (1f, 0f)), 0.17f,
                Grad((0f, A(FireOrange, 0f)), (0.4f, A(FireOrange, 0.7f)), (1f, A(FireYellow, 0.4f))));

            head = NewLine("Head", t, CoreMat, Z * (Length - 0.14f), Z * (Length + 0.04f),
                Curve((0f, 1f), (1f, 0f)), 0.075f,
                Grad((0f, FireOrange), (0.5f, FireYellow), (1f, FireWhite)));

            for (int i = 0; i < 4; i++)
            {
                Vector3 side = Quaternion.AngleAxis(i * 90f + 45f, Vector3.forward) * Vector3.up;
                vanes.Add(NewLine("Vane", t, BeamMat, Z * 0.2f, Z * 0.02f + side * 0.05f,
                    Curve((0f, 0.5f), (1f, 1f)), 0.014f,
                    Grad((0f, A(ArcaneViolet, 0.2f)), (1f, A(ArcanePink, 0.9f))), 4));
            }

            flames = NewPS("Flames", t, FlameMat, true, 500);
            {
                var m = flames.main;
                m.startLifetime = new MinMaxCurve(0.25f, 0.5f);
                m.startSize = new MinMaxCurve(0.04f, 0.1f);
                m.startRotation = new MinMaxCurve(0f, Mathf.PI * 2f);
                m.startSpeed = new MinMaxCurve(0f, 0.15f);
                m.gravityModifier = -0.12f;
                var sh = flames.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Box;
                sh.scale = new Vector3(0.015f, 0.015f, Length * 0.85f); sh.position = Z * (Length * 0.5f);
                sh.randomDirectionAmount = 1f;
                var col = flames.colorOverLifetime; col.enabled = true; col.color = FireOverLife;
                var sz = flames.sizeOverLifetime; sz.enabled = true; sz.size = new MinMaxCurve(1f, Curve((0f, 0.5f), (0.25f, 1f), (1f, 0.15f)));
                var rot = flames.rotationOverLifetime; rot.enabled = true; rot.z = new MinMaxCurve(-2f, 2f);
                var nz = flames.noise; nz.enabled = true; nz.strength = 0.25f; nz.frequency = 2.5f; nz.scrollSpeed = 1.5f;
            }

            spiral = NewPS("Arcane Spiral", t, SparkMat, false, 300);
            {
                const float lifeT = 0.55f;
                var m = spiral.main;
                m.startLifetime = lifeT;
                m.startSize = new MinMaxCurve(0.012f, 0.026f);
                m.startColor = RandomFrom(ArcanePalette);
                var sh = spiral.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 0.05f; sh.radiusThickness = 0f;
                var vel = spiral.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.Local;
                vel.x = 0f; vel.y = 0f; vel.z = (Length + 0.05f) / lifeT;
                vel.orbitalX = 0f; vel.orbitalY = 0f; vel.orbitalZ = 10f;
                vel.radial = -0.05f;
                var col = spiral.colorOverLifetime; col.enabled = true; col.color = FadeInOut;
            }

            embers = NewPS("Embers", t, SparkMat, true, 200);
            {
                var m = embers.main;
                m.startLifetime = new MinMaxCurve(0.6f, 1.4f);
                m.startSize = new MinMaxCurve(0.005f, 0.013f);
                m.startSpeed = new MinMaxCurve(0.1f, 0.5f);
                m.gravityModifier = 0.15f;
                m.startColor = RandomFrom(Grad((0f, FireYellow), (1f, FireOrange)));
                var sh = embers.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Box;
                sh.scale = new Vector3(0.02f, 0.02f, Length * 0.85f); sh.position = Z * (Length * 0.5f);
                sh.randomDirectionAmount = 1f;
                var nz = embers.noise; nz.enabled = true; nz.strength = 0.4f; nz.frequency = 1.5f;
                var col = embers.colorOverLifetime; col.enabled = true; col.color = FadeInOut;
            }

            tipGlow = NewPS("Tip Glow", t, GlowMat, false, 30);
            tipGlow.transform.localPosition = Z * (Length - 0.03f);
            {
                var m = tipGlow.main;
                m.startLifetime = new MinMaxCurve(0.1f, 0.18f);
                m.startSize = new MinMaxCurve(0.11f, 0.16f);
                m.startColor = A(FireYellow, 0.8f);
                var col = tipGlow.colorOverLifetime; col.enabled = true; col.color = FadeInOut;
            }
            nockGlow = NewPS("Nock Glow", t, GlowMat, false, 30);
            nockGlow.transform.localPosition = Z * 0.05f;
            {
                var m = nockGlow.main;
                m.startLifetime = new MinMaxCurve(0.12f, 0.2f);
                m.startSize = new MinMaxCurve(0.07f, 0.1f);
                m.startColor = RandomFrom(Grad((0f, A(ArcaneViolet, 0.8f)), (1f, A(ArcanePink, 0.8f))));
                var col = nockGlow.colorOverLifetime; col.enabled = true; col.color = FadeInOut;
            }

            gather = NewPS("Gather", t, SparkMat, false, 300);
            gather.transform.localPosition = Z * (Length * 0.55f);
            {
                const float lifeT = 0.35f;
                var m = gather.main;
                m.startLifetime = lifeT;
                m.startSize = new MinMaxCurve(0.01f, 0.022f);
                m.startColor = RandomFrom(ArcanePalette);
                var sh = gather.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.4f; sh.radiusThickness = 0f;
                var vel = gather.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.Local;
                vel.x = 0f; vel.y = 0f; vel.z = 0f;
                vel.radial = -0.4f / lifeT;
                var col = gather.colorOverLifetime; col.enabled = true;
                col.color = Grad((0f, A(Color.white, 0f)), (0.3f, A(Color.white, 1f)), (1f, A(Color.white, 0.6f)));
                var sz = gather.sizeOverLifetime; sz.enabled = true; sz.size = new MinMaxCurve(1f, Curve((0f, 0.5f), (1f, 1.2f)));
            }

            glowLight = NewLight(t, Z * (Length * 0.7f), new Color(1f, 0.55f, 0.22f), 1f, 0f);

            trail = NewTrail("Trail", t, BeamMat, Z * (Length - 0.05f), 0.32f, 0.09f, Curve((0f, 1f), (1f, 0f)),
                Grad((0f, A(FireYellow, 0.9f)), (0.25f, A(FireOrange, 0.8f)), (0.6f, A(ArcaneViolet, 0.5f)), (1f, A(ArcaneCyan, 0f))));
            trailCore = NewTrail("Trail Core", t, CoreMat, Z * (Length - 0.05f), 0.12f, 0.025f, Curve((0f, 1f), (1f, 0f)),
                Grad((0f, FireWhite), (1f, A(FireYellow, 0f))));

            circleRoot = new GameObject("Magic Circle").transform;
            glowR = NewQuad("Glow", circleRoot, GlowMat);
            circleGlow = glowR.transform; circleGlow.localScale = Vector3.one * 0.7f;
            outerR = NewQuad("Runes Outer", circleRoot, RuneMat);
            circleOuter = outerR.transform;
            innerR = NewQuad("Runes Inner", circleRoot, RuneMat);
            circleInner = innerR.transform; circleInner.localScale = Vector3.one * 0.58f; circleInner.localPosition = Vector3.forward * 0.01f;
            circleRoot.localScale = Vector3.zero;

            hum = gameObject.AddComponent<AudioSource>();
            hum.clip = ChargeLoopClip;
            hum.loop = true;
            hum.spatialBlend = 1f;
            hum.minDistance = 0.8f;
            hum.maxDistance = 30f;
            hum.dopplerLevel = 0f;
            hum.volume = 0f;
            hum.Play();

            ApplyLook(0f, 0f);
            flames.Play(); spiral.Play(); embers.Play(); tipGlow.Play(); nockGlow.Play(); gather.Play();
        }

        public void SetCharging(float c, Vector3 tail, Vector3 dir, Vector3 up, Vector3 front)
        {
            if (flying || dead) return;
            charge = Mathf.Clamp01(c);
            vis = Mathf.MoveTowards(vis, 1f, Time.deltaTime * 4f);
            Quaternion rot = Quaternion.LookRotation(dir, up);
            transform.SetPositionAndRotation(tail, rot);
            ApplyLook(vis, charge);

            SetRate(gather, Mathf.Lerp(30f, 110f, charge) * vis);
            UpdateCircle(front + dir * 0.06f, rot, charge, vis);

            hum.volume = Mathf.Lerp(0.12f, 0.55f, charge) * vis;
            hum.pitch = Mathf.Lerp(0.7f, 1.45f, charge);

            if (charge >= 1f && !fullChargeShown)
            {
                fullChargeShown = true;
                SpawnChargeFlash(front + dir * 0.06f, rot);
            }
        }

        void ApplyLook(float v, float c)
        {
            float flick = 0.85f + 0.3f * Mathf.PerlinNoise(Time.time * 14f, 0.37f);
            float k = v * Mathf.Lerp(0.45f, 1f, c);
            core.widthMultiplier = 0.022f * k * flick;
            outer.widthMultiplier = 0.07f * k * flick;
            head.widthMultiplier = 0.075f * k;
            headGlow.widthMultiplier = 0.17f * k * flick;
            foreach (var vane in vanes) vane.widthMultiplier = 0.014f * k;

            SetRate(flames, Mathf.Lerp(20f, 150f, c) * v);
            SetRate(spiral, Mathf.Lerp(15f, 90f, c) * v);
            SetRate(embers, Mathf.Lerp(2f, 35f, c) * v);
            SetRate(tipGlow, 45f * v);
            SetRate(nockGlow, 30f * v);
            var tm = tipGlow.main;
            float ts = Mathf.Lerp(0.5f, 1.2f, c);
            tm.startSize = new MinMaxCurve(0.11f * ts, 0.16f * ts);

            glowLight.intensity = Mathf.Lerp(0.3f, 2.5f, c) * v * flick;
            glowLight.range = Mathf.Lerp(0.8f, 3f, c);
        }

        void UpdateCircle(Vector3 pos, Quaternion rot, float c, float v)
        {
            if (circleRoot == null) return;
            circleRoot.SetPositionAndRotation(pos, rot);
            float ease = 1f - (1f - c) * (1f - c);
            float pulse = c >= 1f ? 1f + 0.07f * Mathf.Sin(Time.time * 12f) : 1f;
            circleRoot.localScale = Vector3.one * (Mathf.Lerp(0.06f, 0.34f, ease) * v * pulse);
            circleOuter.localRotation = Quaternion.Euler(0f, 0f, Time.time * (35f + 90f * c));
            circleInner.localRotation = Quaternion.Euler(0f, 0f, -Time.time * (60f + 140f * c));
            float a = Mathf.Lerp(0.25f, 1f, c) * v;
            SetColor(outerR, A(ArcaneGold, a));
            SetColor(innerR, A(Color.Lerp(ArcaneViolet, ArcanePink, 0.5f + 0.5f * Mathf.Sin(Time.time * 3f)), a));
            SetColor(glowR, A(FireOrange, 0.5f * a));
        }

        void ReleaseCircle(bool burst)
        {
            if (circleRoot == null) return;
            Transform root = circleRoot;
            circleRoot = null;
            Renderer o = outerR, i = innerR, g = glowR;
            Transform ot = circleOuter, it = circleInner;
            Vector3 s0 = root.localScale;
            float spin = ot.localEulerAngles.z;
            var fx = root.gameObject.AddComponent<FxAutoDestroy>();
            fx.life = burst ? 0.5f : 0.35f;
            fx.onUpdate = k =>
            {
                float e = 1f - (1f - k) * (1f - k);
                root.localScale = s0 * (burst ? 1f + 1.6f * e : 1f - 0.9f * e);
                ot.localRotation = Quaternion.Euler(0f, 0f, spin + e * 220f);
                it.localRotation = Quaternion.Euler(0f, 0f, -spin - e * 300f);
                float a = (1f - k) * (1f - k);
                SetColor(o, A(ArcaneGold, a));
                SetColor(i, A(ArcaneViolet, a));
                SetColor(g, A(FireOrange, 0.6f * a));
            };
        }

        public void Launch(Vector3 dir, float pwr, Vector3 front)
        {
            if (flying || dead) return;
            flying = true;
            power = Mathf.Clamp01(pwr);
            life = 0f;
            vis = 1f;
            dir.Normalize();
            transform.rotation = Quaternion.LookRotation(dir, transform.up);
            velocity = dir * Mathf.Lerp(minSpeed, maxSpeed, power);

            gather.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            var fe = flames.emission; fe.rateOverDistance = Mathf.Lerp(25f, 70f, power);
            var ee = embers.emission; ee.rateOverDistance = Mathf.Lerp(5f, 20f, power);

            trail.Clear(); trail.emitting = true;
            trailCore.Clear(); trailCore.emitting = true;
            trail.widthMultiplier = Mathf.Lerp(0.06f, 0.11f, power);

            SpawnReleaseBurst(front, dir, power);
            ReleaseCircle(true);

            hum.volume = 0.3f;
            hum.pitch = 1.6f;
            hum.dopplerLevel = 0.8f;
        }

        public void Cancel()
        {
            if (dead) return;
            dead = true;
            SpawnFizzle(transform.position + transform.forward * Length * 0.5f, transform.rotation);
            ReleaseCircle(false);
            Detach(flames, 1f);
            Detach(embers, 1.5f);
            Destroy(gameObject);
        }

        void Update()
        {
            if (!flying || dead) return;
            float dt = Time.deltaTime;
            life += dt;
            ApplyLook(1f, Mathf.Max(charge, 0.4f));

            Vector3 dir = velocity.normalized;
            Vector3 tip = transform.position + dir * Length;
            float dist = velocity.magnitude * dt;

            if (CastHit(tip, dir, dist, out Vector3 point, out Vector3 normal))
            {
                transform.position = point - dir * Length;
                Explode(point, normal, true);
                return;
            }

            transform.position += velocity * dt;
            if (life >= maxLifetime) Explode(tip + dir * dist, -dir, false);
        }

        bool CastHit(Vector3 from, Vector3 dir, float dist, out Vector3 point, out Vector3 normal)
        {
            point = normal = Vector3.zero;
            var hits = Physics.SphereCastAll(from, hitRadius, dir, dist, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            bool found = false;
            foreach (var h in hits)
            {
                if (ignoreRoot != null && h.collider.transform.IsChildOf(ignoreRoot)) continue;
                if (h.distance >= best) continue;
                best = h.distance;
                found = true;
                if (h.distance <= 0f && h.point == Vector3.zero) { point = from; normal = -dir; }
                else { point = h.point; normal = h.normal; }
            }
            return found;
        }

        void Explode(Vector3 point, Vector3 normal, bool hitSomething)
        {
            if (dead) return;
            dead = true;
            float p = Mathf.Max(power, 0.25f);
            SpawnImpact(point, normal, hitSomething ? p : p * 0.6f);
            PushBodies(point, p);
            Detach(flames, 1.5f);
            Detach(embers, 2f);
            Detach(spiral, 0.8f);
            DetachTrail(trail);
            DetachTrail(trailCore);
            Destroy(gameObject);
        }

        void PushBodies(Vector3 point, float p)
        {
            float radius = Mathf.Lerp(1.2f, 3f, p);
            float force = Mathf.Lerp(3f, 9f, p);
            var done = new HashSet<Rigidbody>();
            foreach (var c in Physics.OverlapSphere(point, radius, ~0, QueryTriggerInteraction.Ignore))
            {
                var rb = c.attachedRigidbody;
                if (rb == null || rb.isKinematic || !done.Add(rb)) continue;
                if (ignoreRoot != null && rb.transform.IsChildOf(ignoreRoot)) continue;
                rb.AddExplosionForce(force, point, radius, 0.4f, ForceMode.Impulse);
            }
        }

        static void Detach(ParticleSystem ps, float t)
        {
            if (ps == null) return;
            ps.transform.SetParent(null, true);
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            Destroy(ps.gameObject, t);
        }

        static void DetachTrail(TrailRenderer tr)
        {
            if (tr == null) return;
            tr.transform.SetParent(null, true);
            tr.emitting = false;
            Destroy(tr.gameObject, tr.time + 0.1f);
        }

        void OnDestroy()
        {
            if (circleRoot != null) Destroy(circleRoot.gameObject);
        }
    }
}
