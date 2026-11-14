namespace MagicTraining
{
    using UnityEngine;

    [DefaultExecutionOrder(-40)]
    public class LightningCaster : MonoBehaviour
    {
        [Header("Rayo")]
        public float startDistance = 0.3f;
        public float stopDistance = 0.5f;
        public float range = 25f;
        public float pushForce = 8f;

        [Header("Bola de trueno")]
        public float thunderChargeTime = 1.5f;
        public float cooldown = 0.5f;

        enum Mode { Idle, Beam, Charging, Cooldown }
        Mode mode = Mode.Idle;

        public static bool Active { get; private set; }

        LightningBeam beam;
        ThunderBall ball;
        MagicCircle circle;
        Vector3 dir;
        float hapticTimer, cooldownTimer;
        bool fullHapticDone;

        void OnDisable()
        {
            if (beam != null) Destroy(beam.gameObject);
            if (ball != null) Destroy(ball.gameObject);
            if (circle != null) Destroy(circle.gameObject);
            beam = null;
            ball = null;
            circle = null;
            SetMode(Mode.Idle);
        }

        void SetMode(Mode m)
        {
            mode = m;
            Active = m != Mode.Idle;
            SpellLock.Set(Spell.Lightning, m == Mode.Beam || m == Mode.Charging);
            if ((m == Mode.Idle || m == Mode.Cooldown) && circle != null)
            {
                circle.Release(m == Mode.Cooldown);
                circle = null;
            }
        }

        void Update()
        {
            var rig = MagicRig.I;
            if (rig == null || !rig.Ready) return;
            var L = rig.L;
            var R = rig.R;
            Transform head = rig.Head;

            bool grips = L.grip && R.grip && !L.trigger && !R.trigger && !IceLanceCaster.IsHolding(true) && !IceLanceCaster.IsHolding(false);
            float dist = Vector3.Distance(L.Position, R.Position);

            Vector3 target = L.AimForward + R.AimForward;
            target = target.sqrMagnitude < 0.01f ? head.forward : target.normalized;
            Vector3 aimMid = (L.aimPosition + R.aimPosition) * 0.5f;
            Vector3 handMid = (L.Position + R.Position) * 0.5f;

            bool xHeld = L.primary;
            bool stillHolding = L.grip && R.grip;

            switch (mode)
            {
                case Mode.Idle:
                {
                    bool inFront = Vector3.Dot(handMid - head.position, head.forward) > 0.12f;
                    if (grips && dist < startDistance && inFront && SpellLock.CanStart(Spell.Lightning, Spell.Fireball) && !Trident.IsTwoHanded && !DruidStaff.IsTwoHanded
                        && !rig.IsGrabbingWithXRI(L) && !rig.IsGrabbingWithXRI(R))
                    {
                        dir = target;
                        SetMode(Mode.Beam);
                        beam = LightningBeam.Create(rig.IgnoreRoot, aimMid + dir * 0.07f);
                        beam.range = range;
                        beam.pushForce = pushForce;
                        circle = MagicCircle.Create(MagicCircle.Element.Lightning);
                        rig.Haptic(true, 0.8f, 0.1f);
                        rig.Haptic(false, 0.8f, 0.1f);
                    }
                    break;
                }

                case Mode.Beam:
                {
                    if (!grips || dist > stopDistance) { StopBeam(); SetMode(Mode.Idle); break; }

                    dir = Vector3.Slerp(dir, target, Time.deltaTime * 20f).normalized;

                    if (xHeld)
                    {
                        StopBeam();
                        ball = ThunderBall.Create(rig.IgnoreRoot, BallPos(aimMid));
                        ball.chargeTime = thunderChargeTime;
                        fullHapticDone = false;
                        SetMode(Mode.Charging);
                        break;
                    }

                    beam.Tick(aimMid + dir * 0.07f, dir, L.Position, R.Position);
                    if (circle != null) circle.Follow(aimMid + dir * 0.04f, Quaternion.LookRotation(dir), 0.28f, 2.5f);
                    BeamHaptics(rig);
                    break;
                }

                case Mode.Charging:
                {
                    if (ball == null) { SetMode(Mode.Idle); break; }
                    dir = Vector3.Slerp(dir, target, Time.deltaTime * 20f).normalized;

                    if (!xHeld)
                    {
                        ball.Launch(dir);
                        ball = null;
                        rig.Haptic(true, 1f, 0.2f);
                        rig.Haptic(false, 1f, 0.2f);
                        cooldownTimer = cooldown;
                        SetMode(Mode.Cooldown);
                        break;
                    }
                    if (!stillHolding)
                    {
                        ball.Cancel();
                        ball = null;
                        SetMode(Mode.Idle);
                        break;
                    }

                    ball.Charging(BallPos(aimMid), Quaternion.LookRotation(dir), Time.deltaTime, L.Position, R.Position);
                    if (circle != null)
                        circle.Follow(BallPos(aimMid) + dir * 0.02f, Quaternion.LookRotation(dir),
                            Mathf.Lerp(0.3f, 0.6f, ball.Charge), Mathf.Lerp(2f, 5f, ball.Charge));
                    ChargeHaptics(rig, ball.Charge);
                    break;
                }

                case Mode.Cooldown:
                    cooldownTimer -= Time.deltaTime;
                    if (cooldownTimer <= 0f) SetMode(Mode.Idle);
                    break;
            }
        }

        Vector3 BallPos(Vector3 aimMid) => aimMid + dir * 0.15f;

        void StopBeam()
        {
            if (beam != null) beam.Stop();
            beam = null;
        }

        void BeamHaptics(MagicRig rig)
        {
            hapticTimer -= Time.deltaTime;
            if (hapticTimer > 0f) return;
            hapticTimer = 0.05f;
            rig.Haptic(true, Random.Range(0.25f, 0.6f), 0.06f);
            rig.Haptic(false, Random.Range(0.25f, 0.6f), 0.06f);
        }

        void ChargeHaptics(MagicRig rig, float charge)
        {
            if (charge >= 1f && !fullHapticDone)
            {
                fullHapticDone = true;
                rig.Haptic(true, 0.9f, 0.15f);
                rig.Haptic(false, 0.9f, 0.15f);
                hapticTimer = 0.2f;
                return;
            }
            hapticTimer -= Time.deltaTime;
            if (hapticTimer > 0f) return;
            hapticTimer = 0.06f;
            float amp = Mathf.Lerp(0.1f, 0.5f, charge) * Random.Range(0.7f, 1.2f);
            rig.Haptic(true, amp, 0.07f);
            rig.Haptic(false, amp, 0.07f);
        }
    }
}

namespace MagicTraining
{
    using System.Collections.Generic;
    using UnityEngine;
    using static MagicTraining.MagicFX;
    using MinMaxCurve = UnityEngine.ParticleSystem.MinMaxCurve;

    public class LightningBeam : MonoBehaviour
    {
        public float range = 25f;
        public float pushForce = 8f;
        public float chainRadius = 3.5f;

        Transform ignoreRoot;

        LineRenderer mainCore, mainGlow, second;
        readonly LineRenderer[] branches = new LineRenderer[5];
        readonly LineRenderer[] handArcs = new LineRenderer[3];
        readonly LineRenderer[] chainCore = new LineRenderer[3];
        readonly LineRenderer[] chainGlow = new LineRenderer[3];
        ParticleSystem originGlow, originSparks, hitSparks, hitGlow;
        Light originLight, hitLight;
        AudioSource buzz, hitBuzz;

        readonly Vector3[] mainPts = new Vector3[64];
        readonly Vector3[] tmp = new Vector3[64];
        int mainCount;
        float regen, age, chainTimer;
        readonly List<Rigidbody> chained = new List<Rigidbody>();

        public static LightningBeam Create(Transform ignoreRoot, Vector3 origin)
        {
            var go = new GameObject("Lightning Beam");
            go.transform.position = origin;
            var b = go.AddComponent<LightningBeam>();
            b.ignoreRoot = ignoreRoot;
            b.Build();
            return b;
        }

        void Build()
        {
            Transform t = transform;
            var mainCurve = Curve((0f, 0.7f), (0.1f, 1f), (0.9f, 1f), (1f, 0.5f));
            mainGlow = NewBoltLine("Main Glow", t, BeamMat, 0.09f, ElecGlowGradient, mainCurve);
            mainCore = NewBoltLine("Main Core", t, CoreMat, 0.018f, ElecCoreGradient, mainCurve);
            second = NewBoltLine("Secondary", t, CoreMat, 0.008f, ElecCoreGradient, Curve((0f, 0.5f), (0.5f, 1f), (1f, 0.2f)));
            for (int i = 0; i < branches.Length; i++)
                branches[i] = NewBoltLine("Branch", t, CoreMat, 0.01f, ElecCoreGradient, Curve((0f, 1f), (1f, 0f)));
            for (int i = 0; i < handArcs.Length; i++)
                handArcs[i] = NewBoltLine("Hand Arc", t, CoreMat, 0.007f, ElecCoreGradient, Curve((0f, 0.6f), (0.5f, 1f), (1f, 0.6f)));
            for (int i = 0; i < chainCore.Length; i++)
            {
                chainGlow[i] = NewBoltLine("Chain Glow", t, BeamMat, 0.05f, ElecGlowGradient, Curve((0f, 1f), (1f, 0.6f)));
                chainCore[i] = NewBoltLine("Chain Core", t, CoreMat, 0.012f, ElecCoreGradient, Curve((0f, 1f), (1f, 0.6f)));
            }

            originGlow = NewPS("Origin Glow", t, GlowMat, false, 30);
            {
                var m = originGlow.main;
                m.startLifetime = new MinMaxCurve(0.05f, 0.1f);
                m.startSize = new MinMaxCurve(0.14f, 0.24f);
                m.startColor = A(ElecCyan, 0.85f);
                var col = originGlow.colorOverLifetime; col.enabled = true; col.color = FadeOut;
                SetRate(originGlow, 70f);
            }
            originSparks = NewPS("Origin Sparks", t, SparkMat, true, 120);
            {
                var m = originSparks.main;
                m.startLifetime = new MinMaxCurve(0.1f, 0.3f);
                m.startSpeed = new MinMaxCurve(0.5f, 2.5f);
                m.startSize = new MinMaxCurve(0.006f, 0.014f);
                m.gravityModifier = 0.3f;
                m.startColor = RandomFrom(ElecPalette);
                var r = originSparks.GetComponent<ParticleSystemRenderer>();
                r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.03f; r.lengthScale = 1.2f;
                var sh = originSparks.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.07f;
                var col = originSparks.colorOverLifetime; col.enabled = true; col.color = FadeOut;
                SetRate(originSparks, 50f);
            }

            hitSparks = NewPS("Hit Sparks", t, SparkMat, true, 300);
            {
                var m = hitSparks.main;
                m.startLifetime = new MinMaxCurve(0.15f, 0.45f);
                m.startSpeed = new MinMaxCurve(1f, 5f);
                m.startSize = new MinMaxCurve(0.006f, 0.016f);
                m.gravityModifier = 0.8f;
                m.startColor = RandomFrom(ElecPalette);
                var r = hitSparks.GetComponent<ParticleSystemRenderer>();
                r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.035f; r.lengthScale = 1.2f;
                var sh = hitSparks.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 60f; sh.radius = 0.03f;
                var col = hitSparks.colorOverLifetime; col.enabled = true; col.color = FadeOut;
            }
            hitGlow = NewPS("Hit Glow", t, GlowMat, false, 30);
            {
                var m = hitGlow.main;
                m.startLifetime = new MinMaxCurve(0.05f, 0.1f);
                m.startSize = new MinMaxCurve(0.3f, 0.5f);
                m.startColor = A(ElecCyan, 0.8f);
                var col = hitGlow.colorOverLifetime; col.enabled = true; col.color = FadeOut;
            }

            originLight = NewLight(t, Vector3.zero, ElecCyan, 2.5f, 0f);
            hitLight = NewLight(t, Vector3.zero, ElecCyan, 4f, 0f);

            buzz = gameObject.AddComponent<AudioSource>();
            ConfigureLoop(buzz, 0.45f, 1f);
            hitBuzz = hitLight.gameObject.AddComponent<AudioSource>();
            ConfigureLoop(hitBuzz, 0f, 0.8f);

            originGlow.Play(); originSparks.Play(); hitSparks.Play(); hitGlow.Play();
            SpawnZapPuff(transform.position, 0.8f);
        }

        static void ConfigureLoop(AudioSource s, float volume, float pitch)
        {
            s.clip = BuzzLoopClip;
            s.loop = true;
            s.spatialBlend = 1f;
            s.minDistance = 1f;
            s.maxDistance = 35f;
            s.dopplerLevel = 0f;
            s.volume = volume;
            s.pitch = pitch;
            s.Play();
        }

        public void Tick(Vector3 origin, Vector3 dir, Vector3 lpos, Vector3 rpos)
        {
            float dt = Time.deltaTime;
            age += dt;
            transform.position = origin;

            bool hit = CastIgnoring(origin, dir, range, 0.05f, ignoreRoot, out Vector3 point, out Vector3 normal, out Collider col);
            Vector3 end = hit ? point : origin + dir * range;
            float dist = Vector3.Distance(origin, end);
            float fade = Mathf.Clamp01(age / 0.08f);

            chainTimer -= dt;
            if (chainTimer <= 0f)
            {
                chainTimer = 0.15f;
                FindChain(hit, end, col);
            }

            regen -= dt;
            if (regen <= 0f)
            {
                regen = Random.Range(0.025f, 0.05f);
                mainCount = Mathf.Clamp((int)(dist * 4f) + 8, 10, 64);
                Jagged(mainPts, mainCount, origin, end, 0.05f + dist * 0.015f);
                SetLine(mainCore, mainPts, mainCount);
                SetLine(mainGlow, mainPts, mainCount);
                Jagged(tmp, mainCount, origin, end, 0.08f + dist * 0.02f);
                SetLine(second, tmp, mainCount);

                foreach (var br in branches)
                {
                    if (Random.value > 0.7f) { br.positionCount = 0; continue; }
                    int idx = Random.Range(1, mainCount - 1);
                    Vector3 s = mainPts[idx];
                    Vector3 side = Vector3.ProjectOnPlane(Random.onUnitSphere, dir).normalized;
                    Vector3 bdir = (dir + side * Random.Range(0.4f, 1.2f)).normalized;
                    float blen = Random.Range(0.25f, 1.3f) * Mathf.Clamp(dist / 4f, 0.3f, 1f);
                    int bn = Random.Range(6, 12);
                    Jagged(tmp, bn, s, s + bdir * blen, 0.08f * blen + 0.02f);
                    SetLine(br, tmp, bn);
                }

                ArcOrHide(handArcs[0], lpos, rpos, 0.035f, 0.85f);
                ArcOrHide(handArcs[1], lpos, origin, 0.03f, 0.7f);
                ArcOrHide(handArcs[2], rpos, origin, 0.03f, 0.7f);

                for (int i = 0; i < chainCore.Length; i++)
                {
                    if (i < chained.Count && chained[i] != null)
                    {
                        Jagged(tmp, 14, end, chained[i].worldCenterOfMass, 0.15f);
                        SetLine(chainCore[i], tmp, 14);
                        SetLine(chainGlow[i], tmp, 14);
                    }
                    else { chainCore[i].positionCount = 0; chainGlow[i].positionCount = 0; }
                }
            }

            float fl = Random.Range(0.7f, 1.3f) * fade;
            mainCore.widthMultiplier = 0.018f * fl;
            mainGlow.widthMultiplier = 0.09f * fl * Random.Range(0.8f, 1.2f);
            second.widthMultiplier = 0.008f * fl;
            originLight.intensity = Random.Range(1.5f, 3f) * fade;

            hitSparks.transform.SetPositionAndRotation(end, Quaternion.LookRotation(hit ? normal : -dir));
            hitGlow.transform.position = end;
            hitLight.transform.position = end - dir * 0.1f;
            SetRate(hitSparks, hit ? 140f : 25f);
            SetRate(hitGlow, hit ? 60f : 15f);
            hitLight.intensity = (hit ? Random.Range(2f, 4.5f) : 0.8f) * fade;
            hitBuzz.volume = hit ? 0.4f : 0.08f;

            if (hit) SpellTarget.HitCollider(col);

            var direct = col != null ? col.attachedRigidbody : null;
            if (hit && direct != null && !direct.isKinematic && !IsRig(direct))
                direct.AddForceAtPosition(dir * pushForce * dt, point, ForceMode.Impulse);
            foreach (var rb in chained)
            {
                if (rb == null || rb.isKinematic) continue;
                Vector3 away = (rb.worldCenterOfMass - end).normalized;
                rb.AddForce((away * 0.5f + Vector3.up * 0.6f) * pushForce * 0.6f * dt, ForceMode.Impulse);
                rb.AddTorque(Random.insideUnitSphere * 0.5f * dt, ForceMode.Impulse);
            }
        }

        void ArcOrHide(LineRenderer lr, Vector3 a, Vector3 b, float amp, float probability)
        {
            if (Random.value < probability)
            {
                Jagged(tmp, 10, a, b, amp);
                SetLine(lr, tmp, 10);
            }
            else lr.positionCount = 0;
        }

        void FindChain(bool hit, Vector3 center, Collider direct)
        {
            chained.Clear();
            if (!hit) return;
            Rigidbody directRb = direct != null ? direct.attachedRigidbody : null;
            var seen = new HashSet<Rigidbody>();
            var cands = new List<(float d, Rigidbody rb)>();
            foreach (var c in Physics.OverlapSphere(center, chainRadius, ~0, QueryTriggerInteraction.Ignore))
            {
                var rb = c.attachedRigidbody;
                if (rb == null || rb == directRb || !seen.Add(rb) || IsRig(rb)) continue;
                cands.Add(((rb.worldCenterOfMass - center).sqrMagnitude, rb));
            }
            cands.Sort((a, b) => a.d.CompareTo(b.d));
            for (int i = 0; i < cands.Count && i < chainCore.Length; i++) chained.Add(cands[i].rb);
        }

        bool IsRig(Rigidbody rb) => ignoreRoot != null && rb.transform.IsChildOf(ignoreRoot);

        public void Stop()
        {
            SpawnZapPuff(transform.position, 0.4f);
            DetachPS(originSparks, 0.5f);
            DetachPS(hitSparks, 0.6f);
            Destroy(gameObject);
        }
    }
}

namespace MagicTraining
{
    using System.Collections.Generic;
    using UnityEngine;
    using static MagicTraining.MagicFX;

    public class ElectricArcBurst : MonoBehaviour
    {
        public float life = 0.6f;
        Vector3 origin;
        readonly List<Vector3> fixedTargets = new List<Vector3>();
        readonly List<Rigidbody> bodies = new List<Rigidbody>();
        readonly List<LineRenderer> cores = new List<LineRenderer>();
        readonly List<LineRenderer> glows = new List<LineRenderer>();
        readonly Vector3[] buf = new Vector3[20];
        float t, regen;

        public static void Spawn(Vector3 origin, Vector3 normal, int radialCount, float radius, IList<Rigidbody> targets, float life)
        {
            var go = new GameObject("Electric Arc Burst");
            go.transform.position = origin;
            var b = go.AddComponent<ElectricArcBurst>();
            b.origin = origin;
            b.life = life;

            Vector3 n = normal.sqrMagnitude > 0.001f ? normal.normalized : Vector3.up;
            Vector3 t1 = Vector3.Cross(n, Mathf.Abs(n.y) < 0.95f ? Vector3.up : Vector3.right).normalized;
            Vector3 t2 = Vector3.Cross(n, t1);
            for (int i = 0; i < radialCount; i++)
            {
                float a = (i + Random.Range(-0.3f, 0.3f)) / radialCount * Mathf.PI * 2f;
                Vector3 dir = t1 * Mathf.Cos(a) + t2 * Mathf.Sin(a);
                b.fixedTargets.Add(origin + dir * (radius * Random.Range(0.5f, 1f)) + n * 0.03f);
            }
            if (targets != null) b.bodies.AddRange(targets);

            int count = b.fixedTargets.Count + b.bodies.Count;
            for (int i = 0; i < count; i++)
            {
                bool body = i >= b.fixedTargets.Count;
                b.glows.Add(NewBoltLine("Arc Glow", go.transform, BeamMat, body ? 0.07f : 0.045f, ElecGlowGradient, Curve((0f, 1f), (1f, 0.3f))));
                b.cores.Add(NewBoltLine("Arc Core", go.transform, CoreMat, body ? 0.016f : 0.01f, ElecCoreGradient, Curve((0f, 1f), (1f, 0.2f))));
            }
        }

        void Update()
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / life);
            regen -= Time.deltaTime;
            bool reshape = regen <= 0f;
            if (reshape) regen = Random.Range(0.025f, 0.045f);

            for (int i = 0; i < cores.Count; i++)
            {
                bool isBody = i >= fixedTargets.Count;
                Vector3 target;
                if (isBody)
                {
                    var rb = bodies[i - fixedTargets.Count];
                    if (rb == null) { cores[i].positionCount = 0; glows[i].positionCount = 0; continue; }
                    target = rb.worldCenterOfMass;
                }
                else target = fixedTargets[i];

                if (reshape)
                {
                    Vector3 end = isBody ? target : Vector3.Lerp(origin, target, Mathf.Clamp01(t / 0.12f));
                    float dist = Vector3.Distance(origin, end);
                    int n = Mathf.Clamp((int)(dist * 8f) + 5, 5, buf.Length);
                    if (Random.value < 0.2f) { cores[i].positionCount = 0; glows[i].positionCount = 0; continue; }
                    Jagged(buf, n, origin, end, 0.06f + dist * 0.08f);
                    SetLine(cores[i], buf, n);
                    SetLine(glows[i], buf, n);
                }
                float w = (1f - k) * Random.Range(0.6f, 1.3f);
                cores[i].widthMultiplier = (isBody ? 0.016f : 0.01f) * w;
                glows[i].widthMultiplier = (isBody ? 0.07f : 0.045f) * w;
            }
            if (t >= life) Destroy(gameObject);
        }
    }
}

namespace MagicTraining
{
    using System.Collections.Generic;
    using UnityEngine;
    using static MagicTraining.MagicFX;
    using MinMaxCurve = UnityEngine.ParticleSystem.MinMaxCurve;

    public class ThunderBall : MonoBehaviour
    {
        public float chargeTime = 1.5f;
        public float minSpeed = 9f;
        public float maxSpeed = 20f;
        public float maxLifetime = 5f;
        public float zapRadius = 2.2f;

        Transform ignoreRoot;
        ParticleSystem core, halo, swirl, gather, shed;
        readonly LineRenderer[] shellArcs = new LineRenderer[7];
        readonly LineRenderer[] handArcs = new LineRenderer[2];
        readonly LineRenderer[] zapArcs = new LineRenderer[3];
        TrailRenderer trail, trailCore;
        Light glow;
        AudioSource buzz;

        float charge, power, life, regen, zapTimer;
        bool flying, dead, fullShown;
        Vector3 velocity, lastL, lastR;
        readonly Vector3[] buf = new Vector3[16];
        readonly List<Rigidbody> zapped = new List<Rigidbody>();

        public float Charge => charge;
        float Radius => Mathf.Lerp(0.04f, 0.13f, charge);

        public static ThunderBall Create(Transform ignoreRoot, Vector3 pos)
        {
            var go = new GameObject("Thunder Ball");
            go.transform.position = pos;
            var b = go.AddComponent<ThunderBall>();
            b.ignoreRoot = ignoreRoot;
            b.Build();
            return b;
        }

        void Build()
        {
            Transform t = transform;

            core = NewPS("Core", t, GlowMat, false, 40);
            {
                var m = core.main;
                m.startLifetime = new MinMaxCurve(0.05f, 0.1f);
                m.startSize = new MinMaxCurve(0.25f, 0.32f);
                m.startColor = A(ElecWhite, 0.95f);
                var col = core.colorOverLifetime; col.enabled = true; col.color = FadeOut;
                SetRate(core, 80f);
            }
            halo = NewPS("Halo", t, GlowMat, false, 30);
            {
                var m = halo.main;
                m.startLifetime = new MinMaxCurve(0.1f, 0.2f);
                m.startSize = new MinMaxCurve(0.45f, 0.6f);
                m.startColor = RandomFrom(Grad((0f, A(ElecBlue, 0.5f)), (1f, A(ElecViolet, 0.5f))));
                var col = halo.colorOverLifetime; col.enabled = true; col.color = FadeInOut;
                SetRate(halo, 30f);
            }
            swirl = NewPS("Swirl", t, SparkMat, false, 200);
            {
                var m = swirl.main;
                m.startLifetime = 0.4f;
                m.startSize = new MinMaxCurve(0.01f, 0.022f);
                m.startColor = RandomFrom(ElecPalette);
                var sh = swirl.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.12f; sh.radiusThickness = 0f;
                var vel = swirl.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.Local;
                vel.x = 0f; vel.y = 0f; vel.z = 0f;
                vel.orbitalX = 6f; vel.orbitalY = 9f; vel.orbitalZ = 4f;
                var col = swirl.colorOverLifetime; col.enabled = true; col.color = FadeInOut;
                SetRate(swirl, 60f);
            }
            gather = NewPS("Gather", t, SparkMat, false, 300);
            {
                const float lifeT = 0.3f;
                var m = gather.main;
                m.startLifetime = lifeT;
                m.startSize = new MinMaxCurve(0.008f, 0.018f);
                m.startColor = RandomFrom(ElecPalette);
                var sh = gather.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.5f; sh.radiusThickness = 0f;
                var vel = gather.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.Local;
                vel.x = 0f; vel.y = 0f; vel.z = 0f;
                vel.radial = -0.45f / lifeT;
                var col = gather.colorOverLifetime; col.enabled = true;
                col.color = Grad((0f, A(Color.white, 0f)), (0.3f, A(Color.white, 1f)), (1f, A(Color.white, 0.5f)));
            }
            shed = NewPS("Shed Sparks", t, SparkMat, true, 300);
            {
                var m = shed.main;
                m.startLifetime = new MinMaxCurve(0.1f, 0.35f);
                m.startSpeed = new MinMaxCurve(0.5f, 3f);
                m.startSize = new MinMaxCurve(0.006f, 0.014f);
                m.gravityModifier = 0.5f;
                m.startColor = RandomFrom(ElecPalette);
                var r = shed.GetComponent<ParticleSystemRenderer>();
                r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.03f; r.lengthScale = 1.2f;
                var sh = shed.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.1f;
                var col = shed.colorOverLifetime; col.enabled = true; col.color = FadeOut;
                SetRate(shed, 30f);
            }

            var shellCurve = Curve((0f, 0.4f), (0.5f, 1f), (1f, 0.4f));
            for (int i = 0; i < shellArcs.Length; i++)
                shellArcs[i] = NewBoltLine("Shell Arc", t, CoreMat, 0.006f, ElecCoreGradient, shellCurve);
            for (int i = 0; i < handArcs.Length; i++)
                handArcs[i] = NewBoltLine("Hand Arc", t, CoreMat, 0.009f, ElecCoreGradient, Curve((0f, 0.6f), (0.5f, 1f), (1f, 0.8f)));
            for (int i = 0; i < zapArcs.Length; i++)
                zapArcs[i] = NewBoltLine("Zap Arc", t, CoreMat, 0.012f, ElecCoreGradient, Curve((0f, 1f), (1f, 0.4f)));

            glow = NewLight(t, Vector3.zero, ElecCyan, 1.5f, 0f);

            trail = NewTrail("Trail", t, BeamMat, Vector3.zero, 0.3f, 0.2f, Curve((0f, 1f), (1f, 0f)),
                Grad((0f, A(ElecWhite, 0.9f)), (0.3f, A(ElecCyan, 0.7f)), (1f, A(ElecViolet, 0f))));
            trailCore = NewTrail("Trail Core", t, CoreMat, Vector3.zero, 0.12f, 0.05f, Curve((0f, 1f), (1f, 0f)),
                Grad((0f, ElecWhite), (1f, A(ElecCyan, 0f))));

            buzz = gameObject.AddComponent<AudioSource>();
            buzz.clip = BuzzLoopClip;
            buzz.loop = true;
            buzz.spatialBlend = 1f;
            buzz.minDistance = 0.8f;
            buzz.maxDistance = 35f;
            buzz.dopplerLevel = 0f;
            buzz.volume = 0.2f;
            buzz.Play();

            ApplyLook();
            core.Play(); halo.Play(); swirl.Play(); gather.Play(); shed.Play();
        }

        void ApplyLook()
        {
            float flick = Random.Range(0.75f, 1.25f);
            transform.localScale = Vector3.one * Mathf.Lerp(0.3f, 1f, charge);
            glow.intensity = Mathf.Lerp(1f, 4f, charge) * flick;
            glow.range = Mathf.Lerp(1.5f, 5f, charge);
            if (!flying)
            {
                buzz.volume = Mathf.Lerp(0.2f, 0.6f, charge);
                buzz.pitch = Mathf.Lerp(0.8f, 1.7f, charge);
            }
        }

        public void Charging(Vector3 pos, Quaternion rot, float dt, Vector3 lpos, Vector3 rpos)
        {
            if (flying || dead) return;
            charge = Mathf.MoveTowards(charge, 1f, dt / Mathf.Max(0.05f, chargeTime));
            transform.position = pos;
            lastL = lpos;
            lastR = rpos;
            ApplyLook();
            SetRate(gather, Mathf.Lerp(40f, 140f, charge));
            UpdateArcs(true);
            if (charge >= 1f && !fullShown)
            {
                fullShown = true;
                SpawnThunderChargeFlash(pos, rot);
            }
        }

        void UpdateArcs(bool withHands)
        {
            regen -= Time.deltaTime;
            if (regen > 0f) return;
            regen = Random.Range(0.025f, 0.045f);
            Vector3 c = transform.position;
            float r = Radius;

            foreach (var arc in shellArcs)
            {
                if (Random.value < 0.25f) { arc.positionCount = 0; continue; }
                Vector3 a = c + Random.onUnitSphere * r;
                Vector3 b = c + Random.onUnitSphere * r;
                Jagged(buf, 10, a, b, r * 0.4f);
                for (int i = 0; i < 10; i++) buf[i] = c + (buf[i] - c).normalized * (r * Random.Range(1f, 1.35f));
                SetLine(arc, buf, 10);
            }

            for (int i = 0; i < handArcs.Length; i++)
            {
                if (!withHands || Random.value < 0.15f) { handArcs[i].positionCount = 0; continue; }
                Vector3 from = i == 0 ? lastL : lastR;
                Vector3 to = c + (from - c).normalized * r;
                Jagged(buf, 12, from, to, 0.04f);
                SetLine(handArcs[i], buf, 12);
            }

            for (int i = 0; i < zapArcs.Length; i++)
            {
                if (i < zapped.Count && zapped[i] != null && Random.value < 0.8f)
                {
                    Jagged(buf, 14, c, zapped[i].worldCenterOfMass, 0.15f);
                    SetLine(zapArcs[i], buf, 14);
                }
                else zapArcs[i].positionCount = 0;
            }
        }

        public void Launch(Vector3 dir)
        {
            if (flying || dead) return;
            flying = true;
            power = Mathf.Lerp(0.35f, 1f, charge);
            velocity = dir.normalized * Mathf.Lerp(minSpeed, maxSpeed, charge);
            life = 0f;
            gather.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            foreach (var h in handArcs) h.positionCount = 0;
            trail.Clear(); trail.emitting = true;
            trailCore.Clear(); trailCore.emitting = true;
            trail.widthMultiplier = 0.2f * Mathf.Lerp(0.5f, 1f, charge);
            var em = shed.emission; em.rateOverDistance = 40f;
            buzz.pitch = 1.9f;
            buzz.volume = 0.45f;
            buzz.dopplerLevel = 0.8f;
            PlayAt(ZapClip, transform.position, 0.9f, 0.7f);
            PlayAt(WhooshClip, transform.position, 0.6f, 1.2f);
            SpawnZapPuff(transform.position, 0.3f);
        }

        public void Cancel()
        {
            if (dead) return;
            dead = true;
            SpawnZapPuff(transform.position, 0.6f);
            DetachPS(shed, 0.5f);
            Destroy(gameObject);
        }

        void Update()
        {
            if (!flying || dead) return;
            float dt = Time.deltaTime;
            life += dt;
            ApplyLook();

            Vector3 step = velocity * dt;
            float dist = step.magnitude;
            if (CastIgnoring(transform.position, step / Mathf.Max(dist, 1e-5f), dist, Radius, ignoreRoot,
                    out Vector3 point, out Vector3 normal, out _))
            {
                Explode(point, normal);
                return;
            }
            transform.position += step;

            zapTimer -= dt;
            if (zapTimer <= 0f)
            {
                zapTimer = 0.12f;
                FindZapTargets();
            }
            foreach (var rb in zapped)
                if (rb != null && !rb.isKinematic)
                    rb.AddForce(((rb.worldCenterOfMass - transform.position).normalized + Vector3.up * 0.5f) * 3f * dt, ForceMode.Impulse);
            UpdateArcs(false);

            if (life >= maxLifetime) Explode(transform.position, -velocity.normalized);
        }

        void FindZapTargets()
        {
            zapped.Clear();
            var seen = new HashSet<Rigidbody>();
            foreach (var c in Physics.OverlapSphere(transform.position, zapRadius, ~0, QueryTriggerInteraction.Ignore))
            {
                var rb = c.attachedRigidbody;
                if (rb == null || !seen.Add(rb)) continue;
                if (ignoreRoot != null && rb.transform.IsChildOf(ignoreRoot)) continue;
                zapped.Add(rb);
                if (zapped.Count >= zapArcs.Length) break;
            }
        }

        void Explode(Vector3 point, Vector3 normal)
        {
            if (dead) return;
            dead = true;
            SpawnThunderImpact(point, normal, power, ignoreRoot);
            DetachPS(shed, 0.6f);
            DetachTrail(trail);
            DetachTrail(trailCore);
            Destroy(gameObject);
        }
    }
}
