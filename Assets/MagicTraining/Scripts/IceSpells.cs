namespace MagicTraining
{
    using System.Collections.Generic;
    using UnityEngine;
    using static MagicTraining.MagicFX;

    [DefaultExecutionOrder(-50)]
    public class IceLanceCaster : MonoBehaviour
    {
        public float minLength = 0.3f;
        [Range(0.5f, 1f)] public float minStraightness = 0.75f;
        public float grabRadius = 0.22f;
        public float minThrowSpeed = 1.5f;
        public float throwBoost = 1.9f;
        [Range(0f, 1f)] public float aimAssist = 0.15f;

        [Header("Líneas verticales")]
        [Range(0.5f, 0.95f)] public float verticalThreshold = 0.75f;
        public float pillarTime = 5f;
        public float rainRange = 30f;
        public float minRainDistance = 3.5f;
        class Draw
        {
            public bool active;
            public readonly List<Vector3> pts = new List<Vector3>();
            public float pathLen;
            public GameObject root;
            public LineRenderer line;
            public ParticleSystem frost;
            public MagicCircle circle;
        }
        readonly Draw dl = new Draw(), dr = new Draw();
        static IceLance heldL, heldR;
        IceLance hoverL, hoverR;
        public static bool IsHolding(bool left) => left ? heldL != null : heldR != null;
        void OnDisable()
        {
            CancelDraw(dl);
            CancelDraw(dr);
            heldL = heldR = null;
            SpellLock.Set(Spell.IceLance, false);
        }
        void Update()
        {
            var rig = MagicRig.I;
            if (rig == null || !rig.Ready) return;
            HandleHand(rig, rig.L, dl);
            HandleHand(rig, rig.R, dr);
            SpellLock.Set(Spell.IceLance, dl.active || dr.active || heldL != null || heldR != null);
        }
        void HandleHand(MagicRig rig, MagicRig.Hand h, Draw d)
        {
            IceLance held = h.left ? heldL : heldR;
            if (held != null && held.IsDead) { SetHeld(h.left, null); held = null; }
            if (held != null)
            {
                if (!h.grip)
                {
                    bool thrown = h.velocity.magnitude >= minThrowSpeed;
                    held.Release(rig.ThrowVelocity(h, throwBoost, aimAssist), thrown);
                    if (thrown) rig.Haptic(h.left, 0.7f, 0.1f);
                    SetHeld(h.left, null);
                }
            }
            else
            {
                var near = IceLance.FindNearest(h.Position, grabRadius);
                var prevHover = h.left ? hoverL : hoverR;
                if (near != null)
                {
                    near.MarkHover();
                    if (near != prevHover) rig.Haptic(h.left, 0.15f, 0.04f);
                }
                if (h.left) hoverL = near; else hoverR = near;
                if (near != null && h.gripDown && !h.gripConsumed && SpellLock.CanStart(Spell.IceLance))
                {
                    h.gripConsumed = true;
                    near.Grab(h.t);
                    SetHeld(h.left, near);
                    rig.Haptic(h.left, 0.5f, 0.08f);
                }
            }

            var other = rig.Other(h);
            if (!d.active)
            {
                if (h.triggerDown && !MagicRig.FreeGrip(h) && !other.trigger && !FireArrowCaster.IsCharging && SpellLock.CanStart(Spell.IceLance))
                {
                    StartDraw(d, h);
                    SpellLock.Set(Spell.IceLance, true);
                }
            }
            else if (other.trigger || FireArrowCaster.IsCharging || MagicRig.FreeGrip(h)) CancelDraw(d);
            else if (!h.trigger) FinishDraw(rig, h, d);
            else
            {
                AddPoint(d, TipOf(h));
                d.circle.Follow(TipOf(h), Quaternion.LookRotation(h.AimForward), 0.14f, 1.5f);
                if (Random.value < 0.15f) rig.Haptic(h.left, 0.08f, 0.03f);
            }
        }
        static void SetHeld(bool left, IceLance l)
        {
            if (left) heldL = l; else heldR = l;
        }
        void StartDraw(Draw d, MagicRig.Hand h)
        {
            d.active = true;
            d.pts.Clear();
            d.pathLen = 0f;
            d.root = new GameObject("Ice Drawing");
            d.line = NewLine("Frost Line", d.root.transform, BeamMat, Vector3.zero, Vector3.zero,
                Curve((0f, 0.4f), (0.15f, 1f), (1f, 1f)), 0.03f,
                Grad((0f, A(IceWhite, 0.3f)), (0.5f, A(IceCyan, 0.8f)), (1f, A(IceWhite, 1f))), 1);
            d.line.useWorldSpace = true;
            d.line.positionCount = 0;
            d.frost = NewFrostSparkles(d.root.transform);
            d.frost.transform.position = TipOf(h);
            d.circle = MagicCircle.Create(MagicCircle.Element.Ice);
            d.circle.Follow(TipOf(h), Quaternion.LookRotation(h.AimForward), 0.14f, 1.5f);
            AddPoint(d, TipOf(h));
        }
        static void AddPoint(Draw d, Vector3 p)
        {
            d.frost.transform.position = p;
            if (d.pts.Count > 0)
            {
                float seg = Vector3.Distance(d.pts[d.pts.Count - 1], p);
                if (seg < 0.01f) return;
                d.pathLen += seg;
            }
            if (d.pts.Count >= 300) return;
            d.pts.Add(p);
            d.line.positionCount = d.pts.Count;
            d.line.SetPosition(d.pts.Count - 1, p);
        }
        void FinishDraw(MagicRig rig, MagicRig.Hand h, Draw d)
        {
            d.active = false;
            Vector3 s = d.pts[0], e = d.pts[d.pts.Count - 1];
            float len = Vector3.Distance(s, e);
            float straight = d.pathLen > 0.001f ? len / d.pathLen : 0f;

            var line = d.line;
            float w0 = line.widthMultiplier;
            d.frost.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            var fx = d.root.AddComponent<FxAutoDestroy>();
            fx.life = 1f;
            fx.onUpdate = k => line.widthMultiplier = w0 * Mathf.Clamp01(1f - k * 3f);
            d.root = null;
            if (d.circle != null) d.circle.Release(true);
            d.circle = null;
            if (len >= minLength && straight >= minStraightness)
            {
                float vertical = (e.y - s.y) / len;
                if (vertical > verticalThreshold) CastPillar(rig, h);
                else if (vertical < -verticalThreshold) CastIcicleRain(rig, h);
                else if (Trident.IsHeldBy(h))
                {
                    WaterSpear.Create(s, e, rig.IgnoreRoot, Trident.Power);
                }
                else
                {
                    IceLance.Create(rig.IgnoreRoot, s, e);
                    MagicCircle.Flash((s + e) * 0.5f, Quaternion.LookRotation(e - s), 0.5f, MagicCircle.Element.Ice, 0.5f);
                }
                rig.Haptic(h.left, 0.4f, 0.1f);
            }
            else SpawnFrostPuff(e);
        }

        void CastPillar(MagicRig rig, MagicRig.Hand h)
        {
            Transform head = rig.Head;
            float floorY = rig.IgnoreRoot != null ? rig.IgnoreRoot.position.y : head.position.y - 1.6f;
            Vector3 feet = new Vector3(head.position.x, floorY, head.position.z);
            FindGround(feet, rig.IgnoreRoot, floorY, out Vector3 g, out _);
            if (Trident.IsHeldBy(h))
            {
                float pw = Trident.Power;
                var geyser = Geyser.Create(g, rig.PlayerRoot, head, pw);
                geyser.holdTime = pillarTime;
            }
            else
            {
                var pillar = IcePillar.Create(g, rig.PlayerRoot, head);
                pillar.holdTime = pillarTime;
            }
            rig.Haptic(true, 0.8f, 0.3f);
            rig.Haptic(false, 0.8f, 0.3f);
        }

        void CastIcicleRain(MagicRig rig, MagicRig.Hand h)
        {
            Transform head = rig.Head;
            float floorY = rig.IgnoreRoot != null ? rig.IgnoreRoot.position.y : head.position.y - 1.6f;
            bool trident = Trident.IsHeldBy(h);
            Vector3 aimDir = trident ? Trident.Instance.transform.forward : h.AimForward;
            Vector3 aimFrom = trident ? Trident.Instance.TipPosition : h.aimPosition;
            Vector3 flat = new Vector3(aimDir.x, 0f, aimDir.z);
            if (flat.sqrMagnitude < 0.04f) flat = new Vector3(head.forward.x, 0f, head.forward.z);
            flat.Normalize();

            Vector3 target;
            if (CastIgnoring(aimFrom, aimDir, rainRange, 0.05f, rig.IgnoreRoot, out Vector3 hit, out _, out _))
                target = hit;
            else
                target = new Vector3(head.position.x, floorY, head.position.z) + flat * 7f;
            Vector3 feet = new Vector3(head.position.x, floorY, head.position.z);
            Vector3 toTarget = target - feet;
            toTarget.y = 0f;
            if (toTarget.magnitude < minRainDistance) target = feet + flat * minRainDistance;
            FindGround(target, rig.IgnoreRoot, floorY, out Vector3 g, out Vector3 n);
            if (trident)
            {
                if (StormClouds.Current != null)
                {
                    var strike = StormStrike.Create(g, n, StormClouds.Current.SkyY, rig.IgnoreRoot);
                    if (Trident.IsTwoHanded) { strike.bolts = 9; strike.spread = 3f; strike.big = true; }
                }
                else SpawnSplash(TipOf(h), 0.4f, 0.3f);
            }
            else
                IcicleRain.Create(g, n, rig.IgnoreRoot);
        }

        static Vector3 TipOf(MagicRig.Hand h) => Trident.IsHeldBy(h) ? Trident.Instance.TipPosition : h.Tip;
        static void CancelDraw(Draw d)
        {
            d.active = false;
            if (d.root != null) Destroy(d.root);
            d.root = null;
            if (d.circle != null) Destroy(d.circle.gameObject);
            d.circle = null;
        }
    }
}

namespace MagicTraining
{
    using System.Collections.Generic;
    using UnityEngine;
    using static MagicTraining.MagicFX;
    using MinMaxCurve = UnityEngine.ParticleSystem.MinMaxCurve;

    public class IceLance : MonoBehaviour
    {
        public static readonly List<IceLance> All = new List<IceLance>();

        enum State { Forming, Floating, Held, Flying, Melting }

        public float gravity = 3.5f;
        public float floatLifetime = 15f;
        public float maxFlightTime = 5f;
        public float freezeTime = 4f;

        Transform ignoreRoot;
        State state;
        float length, stateTime, lastHover, glowAmt;
        bool dead;
        Vector3 velocity, basePos;
        Transform holder;
        Vector3 holdPos;
        Quaternion holdRot;

        Transform crystal;
        Renderer crystalR;
        LineRenderer core;
        ParticleSystem aura, mist, snow;
        Light glow;
        TrailRenderer trail, trailCore;

        public bool IsDead => dead;
        public bool CanGrab => !dead && (state == State.Floating || state == State.Forming);

        public static IceLance Create(Transform ignoreRoot, Vector3 from, Vector3 to)
        {
            Vector3 d = to - from;
            float drawn = d.magnitude;
            Vector3 dir = drawn > 0.001f ? d / drawn : Vector3.forward;
            float len = Mathf.Clamp(drawn * 1.1f, 0.8f, 1.6f);
            Vector3 center = (from + to) * 0.5f;
            Vector3 up = Mathf.Abs(Vector3.Dot(dir, Vector3.up)) > 0.95f ? Vector3.forward : Vector3.up;

            var go = new GameObject("Ice Lance");
            go.transform.SetPositionAndRotation(center - dir * len * 0.5f, Quaternion.LookRotation(dir, up));
            var l = go.AddComponent<IceLance>();
            l.ignoreRoot = ignoreRoot;
            l.length = len;
            l.Build();
            return l;
        }

        void Build()
        {
            Transform t = transform;
            Vector3 Z = Vector3.forward;

            crystal = new GameObject("Crystal").transform;
            crystal.SetParent(t, false);
            crystal.gameObject.AddComponent<MeshFilter>().sharedMesh = IceLanceMesh;
            crystalR = crystal.gameObject.AddComponent<MeshRenderer>();
            crystalR.sharedMaterial = IceMat;
            crystalR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            crystalR.receiveShadows = false;
            crystal.localScale = new Vector3(1f, 1f, 0.001f);

            core = NewLine("Core", t, BeamMat, Z * 0.05f, Z * (length * 0.95f),
                Curve((0f, 0.3f), (0.5f, 1f), (1f, 0.2f)), 0.025f,
                Grad((0f, A(IceCyan, 0.5f)), (1f, A(IceWhite, 0.9f))));

            aura = NewPS("Frost Aura", t, SparkMat, false, 150);
            {
                var m = aura.main;
                m.startLifetime = new MinMaxCurve(0.6f, 1.2f);
                m.startSize = new MinMaxCurve(0.008f, 0.02f);
                m.startSpeed = new MinMaxCurve(0.02f, 0.08f);
                m.startColor = RandomFrom(IcePalette);
                var sh = aura.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Box;
                sh.scale = new Vector3(0.06f, 0.06f, length); sh.position = Z * (length * 0.5f); sh.randomDirectionAmount = 1f;
                var nz = aura.noise; nz.enabled = true; nz.strength = 0.05f; nz.frequency = 2f;
                var col = aura.colorOverLifetime; col.enabled = true; col.color = FadeInOut;
                SetRate(aura, 30f);
            }

            mist = NewPS("Cold Mist", t, SmokeMat, true, 40);
            {
                var m = mist.main;
                m.startLifetime = new MinMaxCurve(1f, 1.8f);
                m.startSize = new MinMaxCurve(0.08f, 0.16f);
                m.startRotation = new MinMaxCurve(0f, Mathf.PI * 2f);
                m.startColor = new Color(0.75f, 0.9f, 1f, 0.25f);
                m.gravityModifier = 0.05f;
                var sh = mist.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Box;
                sh.scale = new Vector3(0.04f, 0.04f, length); sh.position = Z * (length * 0.5f);
                var col = mist.colorOverLifetime; col.enabled = true; col.color = FadeInOut;
                var sz = mist.sizeOverLifetime; sz.enabled = true; sz.size = new MinMaxCurve(1f, Curve((0f, 0.6f), (1f, 1.6f)));
                SetRate(mist, 10f);
            }

            snow = NewPS("Snow Trail", t, SparkMat, true, 300);
            {
                var m = snow.main;
                m.startLifetime = new MinMaxCurve(0.5f, 1.2f);
                m.startSize = new MinMaxCurve(0.006f, 0.016f);
                m.startSpeed = new MinMaxCurve(0.05f, 0.3f);
                m.gravityModifier = 0.1f;
                m.startColor = RandomFrom(IcePalette);
                var sh = snow.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.04f;
                sh.position = Z * (length * 0.6f);
                var col = snow.colorOverLifetime; col.enabled = true; col.color = FadeOut;
            }

            glow = NewLight(t, Z * (length * 0.6f), IceCyan, 1.5f, 0.8f);

            trail = NewTrail("Trail", t, BeamMat, Z * length, 0.25f, 0.07f, Curve((0f, 1f), (1f, 0f)),
                Grad((0f, A(IceWhite, 0.9f)), (0.4f, A(IceCyan, 0.6f)), (1f, A(IceBlue, 0f))));
            trailCore = NewTrail("Trail Core", t, CoreMat, Z * length, 0.1f, 0.02f, Curve((0f, 1f), (1f, 0f)),
                Grad((0f, IceWhite), (1f, A(IceCyan, 0f))));

            aura.Play(); mist.Play(); snow.Play();
            All.Add(this);
            state = State.Forming;
            SpawnIceForm(transform.position, transform.rotation, length);
        }

        public void MarkHover() => lastHover = Time.time;

        public void Grab(Transform hand)
        {
            if (!CanGrab) return;
            if (state == State.Forming) crystal.localScale = new Vector3(1f, 1f, length);
            state = State.Held;
            stateTime = 0f;
            holder = hand;
            holdPos = hand.InverseTransformPoint(transform.position);
            holdRot = Quaternion.Inverse(hand.rotation) * transform.rotation;
            PlayAt(ChimeClip, transform.position + transform.forward * length * 0.5f, 0.3f, 1.4f);
        }

        public void Release(Vector3 vel, bool thrown)
        {
            if (dead) return;
            holder = null;
            stateTime = 0f;
            if (!thrown)
            {
                state = State.Floating;
                basePos = transform.position;
                return;
            }
            state = State.Flying;
            velocity = vel;
            transform.rotation = Quaternion.LookRotation(vel.normalized, transform.up);
            trail.Clear(); trail.emitting = true;
            trailCore.Clear(); trailCore.emitting = true;
            var em = snow.emission; em.rateOverDistance = 30f;
            PlayAt(WhooshClip, transform.position, 0.6f, 1.4f);
        }

        void Update()
        {
            if (dead) return;
            float dt = Time.deltaTime;
            stateTime += dt;

            bool highlight = Time.time - lastHover < 0.1f || state == State.Held;
            glowAmt = Mathf.MoveTowards(glowAmt, highlight ? 1f : 0f, dt * 6f);
            float pulse = 0.15f + 0.1f * Mathf.Sin(Time.time * 3f);
            float fade = 1f;

            switch (state)
            {
                case State.Forming:
                {
                    float k = Mathf.Clamp01(stateTime / 0.3f);
                    float e = 1f - (1f - k) * (1f - k) * (1f - k);
                    crystal.localScale = new Vector3(1f, 1f, Mathf.Max(0.001f, length * e));
                    core.widthMultiplier = 0.025f * e;
                    if (k >= 1f) { state = State.Floating; stateTime = 0f; basePos = transform.position; }
                    break;
                }
                case State.Floating:
                    transform.position = basePos + Vector3.up * (Mathf.Sin(stateTime * 2f) * 0.012f);
                    crystal.localRotation = Quaternion.Euler(0f, 0f, stateTime * 25f);
                    if (stateTime > floatLifetime) { state = State.Melting; stateTime = 0f; }
                    break;
                case State.Held:
                    if (holder == null) { Release(Vector3.zero, false); break; }
                    transform.SetPositionAndRotation(holder.TransformPoint(holdPos), holder.rotation * holdRot);
                    break;
                case State.Flying:
                    Fly(dt);
                    if (dead) return;
                    break;
                case State.Melting:
                {
                    float k = Mathf.Clamp01(stateTime / 0.8f);
                    fade = 1f - k;
                    crystal.localScale = new Vector3(1f - 0.5f * k, 1f - 0.5f * k, length * (1f - 0.3f * k));
                    core.widthMultiplier = 0.025f * (1f - k);
                    if (k >= 1f) { Melt(); return; }
                    break;
                }
            }

            SetIce(crystalR, fade, glowAmt * 0.8f + pulse);
            glow.intensity = (0.8f + glowAmt * 1.2f) * fade;
        }

        void Fly(float dt)
        {
            velocity += Vector3.down * gravity * dt;
            Vector3 dir = velocity.normalized;
            transform.rotation = Quaternion.LookRotation(dir, transform.up);
            Vector3 tip = transform.position + dir * length;
            float dist = velocity.magnitude * dt;
            if (CastIgnoring(tip, dir, dist, 0.03f, ignoreRoot, out Vector3 point, out Vector3 normal, out Collider col))
            {
                transform.position = point - dir * length;
                Shatter(point, normal, col);
                return;
            }
            transform.position += velocity * dt;
            if (stateTime > maxFlightTime) Shatter(tip, -dir, null);
        }

        void Shatter(Vector3 point, Vector3 normal, Collider col)
        {
            if (dead) return;
            dead = true;
            All.Remove(this);
            SpawnIceImpact(point, normal, 1f);
            var rb = col != null ? col.attachedRigidbody : null;
            if (rb != null && !(ignoreRoot != null && rb.transform.IsChildOf(ignoreRoot)))
                FrozenBody.Freeze(rb, freezeTime);
            PushBodies(point, 1.2f, 2f, ignoreRoot);
            DetachPS(mist, 2f);
            DetachPS(snow, 1.3f);
            DetachTrail(trail);
            DetachTrail(trailCore);
            Destroy(gameObject);
        }

        void Melt()
        {
            if (dead) return;
            dead = true;
            All.Remove(this);
            SpawnFrostPuff(transform.position + transform.forward * length * 0.5f);
            DetachPS(mist, 2f);
            Destroy(gameObject);
        }

        void OnDestroy() => All.Remove(this);

        public float DistanceTo(Vector3 p)
        {
            Vector3 a = transform.position, b = a + transform.forward * length;
            Vector3 ab = b - a;
            float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(1e-5f, ab.sqrMagnitude));
            return Vector3.Distance(p, a + ab * t);
        }

        public static IceLance FindNearest(Vector3 p, float radius)
        {
            IceLance best = null;
            float bestD = radius;
            foreach (var l in All)
            {
                if (l == null || !l.CanGrab) continue;
                float d = l.DistanceTo(p);
                if (d < bestD) { bestD = d; best = l; }
            }
            return best;
        }
    }
}

namespace MagicTraining
{
    using UnityEngine;
    using static MagicTraining.MagicFX;
    using MinMaxCurve = UnityEngine.ParticleSystem.MinMaxCurve;

    public class IcePillar : MonoBehaviour
    {
        public float height = 2.2f;
        public float holdTime = 5f;
        public float riseTime = 0.45f;
        public float lowerTime = 0.7f;
        public float carryRadius = 0.75f;

        Transform crystal, rig, head;
        Renderer rend;
        ParticleSystem frost, mist;
        Vector3 ground;
        float t;
        bool carrying = true, done;

        public static IcePillar Create(Vector3 ground, Transform rigRoot, Transform head)
        {
            var go = new GameObject("Ice Pillar");
            go.transform.SetPositionAndRotation(ground, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            var p = go.AddComponent<IcePillar>();
            p.ground = ground;
            p.rig = rigRoot;
            p.head = head;
            p.Build();
            return p;
        }

        void Build()
        {
            crystal = new GameObject("Crystal").transform;
            crystal.SetParent(transform, false);
            crystal.gameObject.AddComponent<MeshFilter>().sharedMesh = IcePillarMesh;
            rend = crystal.gameObject.AddComponent<MeshRenderer>();
            rend.sharedMaterial = IceMat;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            crystal.localScale = new Vector3(1f, height, 1f);
            crystal.localPosition = Vector3.down * height;

            var box = crystal.gameObject.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.35f, 0f);
            box.size = new Vector3(1.05f, 1.3f, 1.05f);

            frost = NewPS("Frost", transform, SparkMat, false, 80);
            {
                var m = frost.main;
                m.startLifetime = new MinMaxCurve(0.8f, 1.5f);
                m.startSize = new MinMaxCurve(0.01f, 0.025f);
                m.startSpeed = new MinMaxCurve(0.02f, 0.1f);
                m.startColor = RandomFrom(IcePalette);
                var sh = frost.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Box;
                sh.scale = new Vector3(1.1f, height, 1.1f); sh.position = Vector3.up * (height * 0.5f); sh.randomDirectionAmount = 1f;
                var col = frost.colorOverLifetime; col.enabled = true; col.color = FadeInOut;
                SetRate(frost, 25f);
            }
            mist = NewPS("Cold Mist", transform, SmokeMat, true, 40);
            {
                var m = mist.main;
                m.startLifetime = new MinMaxCurve(1.5f, 2.5f);
                m.startSize = new MinMaxCurve(0.4f, 0.8f);
                m.startSpeed = new MinMaxCurve(0.1f, 0.4f);
                m.startRotation = new MinMaxCurve(0f, Mathf.PI * 2f);
                m.startColor = new Color(0.8f, 0.92f, 1f, 0.25f);
                m.gravityModifier = 0.02f;
                var sh = mist.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Circle;
                sh.radius = 0.8f; sh.rotation = new Vector3(90f, 0f, 0f);
                var col = mist.colorOverLifetime; col.enabled = true; col.color = FadeInOut;
                var sz = mist.sizeOverLifetime; sz.enabled = true; sz.size = new MinMaxCurve(1f, Curve((0f, 0.6f), (1f, 1.6f)));
                SetRate(mist, 6f);
            }
            frost.Play();
            mist.Play();

            MagicCircle.Flash(ground + Vector3.up * 0.03f, Quaternion.LookRotation(Vector3.up), 2.6f, MagicCircle.Element.Ice, 0.6f, 2f);
            SpawnSpikeBurst(ground, Vector3.up, 2f, false);
            for (int i = 0; i < 7; i++)
            {
                float a = i / 7f * Mathf.PI * 2f + Random.Range(-0.2f, 0.2f);
                var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                FindGround(ground + d * 1.05f, rig, ground.y, out Vector3 g, out Vector3 n);
                IceSpike.Spawn(g, n, d, Random.Range(0.35f, 0.6f), rig, false, false);
            }
            PlayAt(IceSpikeClip, ground, 1f, 0.55f);
            PlayAt(IceFormClip, ground + Vector3.up, 0.6f, 0.6f);
        }

        void Update()
        {
            if (done) return;
            t += Time.deltaTime;

            float e;
            if (t < riseTime) e = Mathf.SmoothStep(0f, 1f, t / riseTime);
            else if (t < riseTime + holdTime) e = 1f;
            else e = 1f - Mathf.SmoothStep(0f, 1f, (t - riseTime - holdTime) / lowerTime);

            crystal.localPosition = Vector3.down * (height * (1f - e));
            SetIce(rend, 1f, Mathf.Max(0f, 0.7f * (1f - t / 0.5f)) + 0.15f);
            float topY = ground.y + height * e;

            if (carrying && rig != null && head != null)
            {
                Vector3 hp = head.position;
                Vector2 off = new Vector2(hp.x - ground.x, hp.z - ground.z);
                if (off.magnitude > carryRadius) carrying = false;
                else
                {
                    Vector3 p = rig.position;
                    p.y = topY;
                    rig.position = p;
                    Physics.SyncTransforms();
                }
            }

            if (t >= riseTime + holdTime + lowerTime) Finish();
        }

        void Finish()
        {
            done = true;
            SpawnIceImpact(ground + Vector3.up * 0.4f, Vector3.up, 0.6f);
            DetachPS(mist, 2.5f);
            Destroy(gameObject);
        }
    }
}

namespace MagicTraining
{
    using UnityEngine;
    using static MagicTraining.MagicFX;

    [DefaultExecutionOrder(-20)]
    public class IceSpikeCaster : MonoBehaviour
    {
        public float swipeSpeed = 3.5f;
        public float cooldown = 0.35f;
        public float rearmSpeed = 1.2f;
        public float comboWindow = 0.15f;
        public float spacing = 0.55f;
        class Swipe { public bool active, armed; public float time, speed; public Vector3 dir; }
        readonly Swipe swL = new Swipe(), swR = new Swipe();
        float nextAllowed;
        void Update()
        {
            var rig = MagicRig.I;
            if (rig == null || !rig.Ready) return;
            float now = Time.time;

            bool buttons = AnyButton(rig.L) || AnyButton(rig.R) || IceLanceCaster.IsHolding(true) || IceLanceCaster.IsHolding(false);
            if (buttons || SpellLock.AnyBusy || FireballCaster.HoldingAny)
            {
                swL.active = swR.active = false;
                swL.armed = swR.armed = false;
                return;
            }
            Detect(rig, rig.L, swL);
            Detect(rig, rig.R, swR);
            if (swL.active && swR.active && Mathf.Abs(swL.time - swR.time) <= comboWindow)
            {
                float dot = Vector3.Dot(swL.dir, swR.dir);
                if (dot > 0.4f) CastWide(rig, (swL.dir + swR.dir).normalized, Mathf.Max(swL.speed, swR.speed));
                else CastRings(rig, Mathf.Max(swL.speed, swR.speed));
                swL.active = swR.active = false;
                nextAllowed = now + cooldown;
            }
            else
            {
                foreach (var (sw, left) in new[] { (swL, true), (swR, false) })
                {
                    if (!sw.active || now - sw.time <= comboWindow) continue;
                    CastLine(rig, left, sw.dir, sw.speed);
                    swL.active = swR.active = false;
                    nextAllowed = now + cooldown;
                    break;
                }
            }
        }
        static bool AnyButton(MagicRig.Hand h)
        {
            bool gripPressed = (h.grip || h.gripValue > 0.2f) && !MagicRig.IsHoldingItem(h);
            return h.trigger || h.primary || h.triggerValue > 0.2f || gripPressed;
        }
        void Detect(MagicRig rig, MagicRig.Hand h, Swipe sw)
        {
            float now = Time.time;
            if (Trident.IsHeld && h.Position.y > rig.Head.position.y - 0.05f) { sw.active = false; sw.armed = false; return; }
            if (Trident.IsHeldBy(h) && !Trident.WaterSpells) { sw.active = false; sw.armed = false; return; }
            Vector3 v = h.velocity;
            Vector3 hv = new Vector3(v.x, 0f, v.z);
            float speed = hv.magnitude;

            if (!sw.armed) { if (v.magnitude < rearmSpeed) sw.armed = true; else return; }
            if (sw.active || now < nextAllowed) return;
            if (speed < swipeSpeed) return;
            sw.active = true;
            sw.time = now;
            sw.speed = speed;
            sw.dir = hv / speed;
        }
        static Vector3 Feet(MagicRig rig, out float floorY)
        {
            Transform head = rig.Head;
            floorY = rig.IgnoreRoot != null ? rig.IgnoreRoot.position.y : head.position.y - 1.6f;
            return new Vector3(head.position.x, floorY, head.position.z);
        }
        static void GroundCircle(MagicRig rig, Vector3 feet, float floorY, float size)
        {
            FindGround(feet, rig.IgnoreRoot, floorY, out Vector3 g, out Vector3 n);
            var element = Trident.WaterSpells ? MagicCircle.Element.Water : MagicCircle.Element.Ice;
            MagicCircle.Flash(g + n * 0.03f, Quaternion.LookRotation(n), size, element, 0.7f, 2f);
        }

        static Vector3 LookDir(MagicRig rig)
        {
            Vector3 f = rig.Head.forward;
            f.y = 0f;
            return f.sqrMagnitude > 0.001f ? f.normalized : Vector3.forward;
        }
        static Vector3 Ground(MagicRig rig, Vector3 p, float floorY)
        {
            FindGround(p, rig.IgnoreRoot, floorY, out Vector3 g, out _);
            return g;
        }
        float Strength(float speed) => Mathf.InverseLerp(swipeSpeed, swipeSpeed + 3f, speed);

        void CastLine(MagicRig rig, bool left, Vector3 dir, float speed)
        {
            float k = Strength(speed);
            Vector3 feet = Feet(rig, out float floorY);
            GroundCircle(rig, feet, floorY, 1.3f);
            if (Trident.WaterSpells)
            {
                float pw = Trident.Power;
                Tsunami.CreateArc(Ground(rig, feet, floorY), LookDir(rig), 20f * Mathf.Lerp(1f, pw, 0.5f), Mathf.Lerp(1.3f, 1.7f, k) * pw, 1f,
                    Mathf.Lerp(10f, 13f, k) * Mathf.Lerp(1f, pw, 0.6f), rig.IgnoreRoot);
                rig.Haptic(left, 0.8f, 0.15f);
                return;
            }
            IceSpikeWave.Line(feet + dir * 0.9f, dir, Mathf.RoundToInt(Mathf.Lerp(8f, 16f, k)), spacing, Mathf.Lerp(1f, 1.4f, k), rig.IgnoreRoot);
            rig.Haptic(left, 0.8f, 0.15f);
        }

        void CastWide(MagicRig rig, Vector3 dir, float speed)
        {
            float k = Strength(speed);
            Vector3 feet = Feet(rig, out float floorY);
            GroundCircle(rig, feet, floorY, 2f);
            if (Trident.WaterSpells)
            {
                float pw = Trident.Power;
                var wave = Tsunami.CreateArc(Ground(rig, feet, floorY), LookDir(rig), 35f * Mathf.Lerp(1f, pw, 0.5f), Mathf.Lerp(2f, 2.6f, k) * pw, 1.2f,
                    Mathf.Lerp(14f, 18f, k) * Mathf.Lerp(1f, pw, 0.6f), rig.IgnoreRoot);
                wave.speed = 6f;
                rig.Haptic(true, 1f, 0.25f);
                rig.Haptic(false, 1f, 0.25f);
                return;
            }
            int count = Mathf.RoundToInt(Mathf.Lerp(14f, 22f, k));
            IceSpikeWave.WideWave(feet + dir * 1f, dir, 5, 0.6f, count, spacing, Mathf.Lerp(1.4f, 1.9f, k), rig.IgnoreRoot);
            rig.Haptic(true, 1f, 0.25f);
            rig.Haptic(false, 1f, 0.25f);
        }

        void CastRings(MagicRig rig, float speed)
        {
            float k = Strength(speed);
            Vector3 feet = Feet(rig, out float floorY);
            GroundCircle(rig, feet, floorY, 3.2f);
            if (Trident.WaterSpells)
            {
                float pw = Trident.Power;
                Tsunami.CreateRing(Ground(rig, feet, floorY), Mathf.Lerp(1.2f, 1.6f, k) * pw, 0.9f, Mathf.Lerp(8f, 11f, k) * Mathf.Lerp(1f, pw, 0.6f), rig.IgnoreRoot);
                rig.Haptic(true, 1f, 0.3f);
                rig.Haptic(false, 1f, 0.3f);
                return;
            }
            float[] radii = k > 0.5f ? new[] { 1.3f, 2.3f, 3.3f, 4.3f } : new[] { 1.3f, 2.3f, 3.3f };
            IceSpikeWave.Rings(feet, radii, spacing * 1.1f, Mathf.Lerp(1.1f, 1.5f, k), rig.IgnoreRoot);
            rig.Haptic(true, 1f, 0.3f);
            rig.Haptic(false, 1f, 0.3f);
        }
    }
}

namespace MagicTraining
{
    using System.Collections.Generic;
    using UnityEngine;
    using static MagicTraining.MagicFX;
    using MinMaxCurve = UnityEngine.ParticleSystem.MinMaxCurve;

    public class IceSpike : MonoBehaviour
    {
        public float life = 2.6f;

        Transform crystal;
        Renderer rend;
        ParticleSystem frost;
        Transform ignoreRoot;
        Vector3 dir;
        float height, t;
        bool pushed, crumbling, lite;

        public static IceSpike Spawn(Vector3 ground, Vector3 normal, Vector3 dir, float height, Transform ignoreRoot, bool sound, bool fx = true)
        {
            Vector3 n = normal.sqrMagnitude > 0.01f ? normal.normalized : Vector3.up;
            Vector3 upAxis = Vector3.Slerp(n, dir, Random.Range(0.12f, 0.3f)).normalized;
            var go = new GameObject("Ice Spike");
            go.transform.SetPositionAndRotation(ground,
                Quaternion.FromToRotation(Vector3.up, upAxis) * Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            var s = go.AddComponent<IceSpike>();
            s.height = height;
            s.dir = dir;
            s.ignoreRoot = ignoreRoot;
            s.lite = !fx;
            s.Build();
            if (fx) SpawnSpikeBurst(ground, n, height, sound);
            else if (sound) PlayAt(IceSpikeClip, ground, 0.6f, Random.Range(0.85f, 1.15f));
            return s;
        }

        void Build()
        {
            crystal = new GameObject("Crystal").transform;
            crystal.SetParent(transform, false);
            crystal.gameObject.AddComponent<MeshFilter>().sharedMesh = IceSpikeMesh;
            rend = crystal.gameObject.AddComponent<MeshRenderer>();
            rend.sharedMaterial = IceMat;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;
            float w = Mathf.Lerp(0.8f, 1.5f, Mathf.InverseLerp(0.5f, 2f, height));
            crystal.localScale = new Vector3(w, height, w);
            crystal.localPosition = Vector3.down * height;

            if (lite) return;
            frost = NewPS("Frost", transform, SparkMat, false, 40);
            var m = frost.main;
            m.startLifetime = new MinMaxCurve(0.6f, 1.2f);
            m.startSize = new MinMaxCurve(0.008f, 0.02f);
            m.startColor = RandomFrom(IcePalette);
            var sh = frost.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Box;
            sh.scale = new Vector3(0.3f * w, height, 0.3f * w); sh.position = Vector3.up * (height * 0.5f);
            var col = frost.colorOverLifetime; col.enabled = true; col.color = FadeInOut;
            SetRate(frost, 8f);
            frost.Play();
        }

        void Update()
        {
            t += Time.deltaTime;

            float rise = Mathf.Clamp01(t / 0.13f);
            float e = 1f + 2.70158f * Mathf.Pow(rise - 1f, 3f) + 1.70158f * Mathf.Pow(rise - 1f, 2f);
            float y = -height * (1f - e);
            float fade = 1f;

            if (t > life - 0.35f)
            {
                if (!crumbling) { crumbling = true; Crumble(); }
                float k = Mathf.Clamp01((t - (life - 0.35f)) / 0.35f);
                y -= height * 0.6f * k * k;
                fade = 1f - k;
            }
            crystal.localPosition = new Vector3(0f, y, 0f);
            SetIce(rend, fade, Mathf.Max(0f, 0.8f * (1f - t / 0.4f)));

            if (!pushed && t > 0.04f) { pushed = true; Push(); }
            if (t >= life) Destroy(gameObject);
        }

        void Push()
        {
            Vector3 a = transform.position;
            Vector3 b = a + transform.up * height;
            var seen = new HashSet<Rigidbody>();
            foreach (var c in Physics.OverlapCapsule(a, b, 0.45f, ~0, QueryTriggerInteraction.Ignore))
            {
                SpellTarget.HitCollider(c);
                var rb = c.attachedRigidbody;
                if (rb == null || rb.isKinematic || !seen.Add(rb)) continue;
                if (ignoreRoot != null && rb.transform.IsChildOf(ignoreRoot)) continue;
                rb.AddForce(Vector3.up * Random.Range(5f, 7f) + dir * 2f, ForceMode.VelocityChange);
                rb.AddTorque(Random.insideUnitSphere * 6f, ForceMode.VelocityChange);
            }
        }

        void Crumble()
        {
            if (frost != null) frost.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (lite && Random.value < 0.6f) return;
            SpawnSpikeCrumble(transform.position + transform.up * (height * 0.4f));
        }
    }
}

namespace MagicTraining
{
    using System.Collections.Generic;
    using UnityEngine;
    using static MagicTraining.MagicFX;

    public class IceSpikeWave : MonoBehaviour
    {
        struct Entry
        {
            public float time;
            public Vector3 pos, dir;
            public float height;
            public bool sound, fx;
        }

        readonly List<Entry> entries = new List<Entry>();
        Transform ignoreRoot;
        float floorY, t;
        int next;

        static IceSpikeWave New(Transform ignoreRoot)
        {
            var go = new GameObject("Ice Spike Wave");
            var w = go.AddComponent<IceSpikeWave>();
            w.ignoreRoot = ignoreRoot;
            w.floorY = ignoreRoot != null ? ignoreRoot.position.y : 0f;
            return w;
        }

        public static void Line(Vector3 start, Vector3 dir, int count, float spacing, float heightMul, Transform ignoreRoot,
            float interval = 0.045f, int soundEvery = 3, bool fx = true)
        {
            var w = New(ignoreRoot);
            w.AddLine(start, dir, count, spacing, heightMul, interval, soundEvery, fx, 0f);
            w.Sort();
        }

        public static void WideWave(Vector3 start, Vector3 dir, int rows, float rowGap, int count, float spacing, float heightMul, Transform ignoreRoot)
        {
            var w = New(ignoreRoot);
            dir.y = 0f;
            dir.Normalize();
            Vector3 side = Vector3.Cross(Vector3.up, dir).normalized;
            for (int r = 0; r < rows; r++)
            {
                float off = (r - (rows - 1) * 0.5f) * rowGap;
                float centerK = 1f - Mathf.Abs(off) / Mathf.Max(0.01f, (rows - 1) * 0.5f * rowGap);
                float hm = heightMul * Mathf.Lerp(0.75f, 1.15f, centerK);
                float delay = (1f - centerK) * 0.12f;
                w.AddLine(start + side * off, dir, count, spacing, hm, 0.04f, rows * 2, r % 2 == 0, delay);
            }
            w.Sort();
        }

        public static void Rings(Vector3 center, float[] radii, float spacing, float heightMul, Transform ignoreRoot, float ringInterval = 0.13f)
        {
            var w = New(ignoreRoot);
            for (int r = 0; r < radii.Length; r++)
            {
                float radius = radii[r];
                int n = Mathf.Max(6, Mathf.RoundToInt(2f * Mathf.PI * radius / spacing));
                float baseAngle = Random.Range(0f, Mathf.PI * 2f);
                float h = Mathf.Lerp(0.7f, 1.5f, radii.Length > 1 ? r / (radii.Length - 1f) : 1f) * heightMul;
                for (int i = 0; i < n; i++)
                {
                    float a = baseAngle + i / (float)n * Mathf.PI * 2f + Random.Range(-0.08f, 0.08f);
                    Vector3 outDir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    w.entries.Add(new Entry
                    {
                        time = r * ringInterval + Random.Range(0f, 0.04f),
                        pos = center + outDir * (radius + Random.Range(-0.12f, 0.12f)),
                        dir = outDir,
                        height = h * Random.Range(0.85f, 1.15f),
                        sound = i % 4 == 0,
                        fx = i % 2 == 0
                    });
                }
            }
            w.Sort();
        }

        void AddLine(Vector3 start, Vector3 dir, int count, float spacing, float heightMul, float interval, int soundEvery, bool fx, float delay)
        {
            dir.y = 0f;
            dir.Normalize();
            Vector3 side = Vector3.Cross(Vector3.up, dir).normalized;
            for (int i = 0; i < count; i++)
            {
                float t01 = count > 1 ? i / (count - 1f) : 1f;
                entries.Add(new Entry
                {
                    time = delay + i * interval,
                    pos = start + dir * (i * spacing) + side * Random.Range(-0.22f, 0.22f),
                    dir = dir,
                    height = Mathf.Lerp(0.55f, 1.5f, t01) * heightMul * Random.Range(0.85f, 1.15f),
                    sound = soundEvery > 0 && i % soundEvery == 0,
                    fx = fx || i % 2 == 0
                });
            }
        }

        void Sort() => entries.Sort((a, b) => a.time.CompareTo(b.time));

        void Update()
        {
            t += Time.deltaTime;
            while (next < entries.Count && entries[next].time <= t)
            {
                var e = entries[next++];
                FindGround(e.pos, ignoreRoot, floorY, out Vector3 g, out Vector3 n);
                IceSpike.Spawn(g, n, e.dir, e.height, ignoreRoot, e.sound, e.fx);
            }
            if (next >= entries.Count) Destroy(gameObject);
        }
    }
}

namespace MagicTraining
{
    using UnityEngine;
    using static MagicTraining.MagicFX;

    public class Icicle : MonoBehaviour
    {
        public float gravity = 25f;
        public float maxLifetime = 3f;

        Transform ignoreRoot;
        Vector3 velocity;
        TrailRenderer trail;
        bool sound;
        float life, spin;

        public static Icicle Spawn(Vector3 pos, float size, float startSpeed, Transform ignoreRoot, bool sound)
        {
            var go = new GameObject("Icicle");
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(Random.Range(-6f, 6f), Random.Range(0f, 360f), Random.Range(-6f, 6f)));
            var ic = go.AddComponent<Icicle>();
            ic.ignoreRoot = ignoreRoot;
            ic.velocity = Vector3.down * startSpeed;
            ic.sound = sound;
            ic.spin = Random.Range(-90f, 90f);
            ic.Build(size);
            return ic;
        }

        void Build(float size)
        {
            var crystal = new GameObject("Crystal").transform;
            crystal.SetParent(transform, false);
            crystal.gameObject.AddComponent<MeshFilter>().sharedMesh = IceSpikeMesh;
            var r = crystal.gameObject.AddComponent<MeshRenderer>();
            r.sharedMaterial = IceMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            float h = 0.9f * size;
            crystal.localScale = new Vector3(0.35f * size, h, 0.35f * size);
            crystal.localRotation = Quaternion.Euler(180f, 0f, 0f);
            crystal.localPosition = Vector3.up * h;

            trail = NewTrail("Trail", transform, BeamMat, Vector3.up * (h * 0.3f), 0.15f, 0.06f * size, Curve((0f, 1f), (1f, 0f)),
                Grad((0f, A(IceWhite, 0.8f)), (0.5f, A(IceCyan, 0.4f)), (1f, A(IceBlue, 0f))));
            trail.emitting = true;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            life += dt;
            velocity += Vector3.down * gravity * dt;
            transform.Rotate(0f, spin * dt, 0f, Space.World);

            Vector3 step = velocity * dt;
            float dist = step.magnitude;
            if (dist > 0f && CastIgnoring(transform.position, step / dist, dist, 0.06f, ignoreRoot,
                    out Vector3 point, out Vector3 normal, out Collider col))
            {
                Shatter(point, normal, col);
                return;
            }
            transform.position += step;
            if (life > maxLifetime) Destroy(gameObject);
        }

        void Shatter(Vector3 point, Vector3 normal, Collider col)
        {
            SpellTarget.HitCollider(col);
            SpawnIcicleImpact(point, normal, sound);
            var rb = col != null ? col.attachedRigidbody : null;
            if (rb != null && !(ignoreRoot != null && rb.transform.IsChildOf(ignoreRoot)))
            {
                if (!rb.isKinematic) rb.AddForce(Vector3.down * 2f + Random.insideUnitSphere * 2f, ForceMode.VelocityChange);
                FrozenBody.Freeze(rb, 2f);
            }
            DetachTrail(trail);
            Destroy(gameObject);
        }
    }
}

namespace MagicTraining
{
    using UnityEngine;
    using static MagicTraining.MagicFX;

    public class IcicleRain : MonoBehaviour
    {
        public float radius = 2.2f;
        public int count = 36;
        public float duration = 1.8f;
        public float cloudHeight = 9f;

        Transform ignoreRoot;
        Vector3 center;
        MagicCircle circle;
        ParticleSystem cloud, snow;
        AudioSource wind;
        float t, spawnAcc;
        int spawned;

        public static IcicleRain Create(Vector3 ground, Vector3 normal, Transform ignoreRoot)
        {
            var go = new GameObject("Icicle Rain");
            go.transform.position = ground;
            var r = go.AddComponent<IcicleRain>();
            r.center = ground;
            r.ignoreRoot = ignoreRoot;
            r.Build(normal);
            return r;
        }

        void Build(Vector3 normal)
        {
            if (normal.sqrMagnitude < 0.01f) normal = Vector3.up;
            circle = MagicCircle.Create(MagicCircle.Element.Ice);
            circle.Follow(center + normal * 0.03f, Quaternion.LookRotation(normal), radius * 2.3f, 2f);

            var sky = new GameObject("Sky").transform;
            sky.SetParent(transform, false);
            sky.localPosition = Vector3.up * cloudHeight;
            cloud = NewFrostCloud(sky, radius);
            snow = NewSnowfall(sky, radius);
            cloud.Play();
            snow.Play();

            MagicCircle.Flash(center + Vector3.up * (cloudHeight - 0.3f), Quaternion.LookRotation(Vector3.down), radius * 2f,
                MagicCircle.Element.Ice, duration + 0.4f, 1.5f);

            wind = gameObject.AddComponent<AudioSource>();
            wind.clip = WindLoopClip;
            wind.loop = true;
            wind.spatialBlend = 1f;
            wind.minDistance = 3f;
            wind.maxDistance = 40f;
            wind.volume = 0.4f;
            wind.pitch = 1.3f;
            wind.Play();

            PlayAt(IceFormClip, center + Vector3.up * 3f, 0.7f, 0.7f);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            t += dt;

            if (t > 0.35f && spawned < count)
            {
                spawnAcc += dt * count / duration;
                while (spawnAcc >= 1f && spawned < count)
                {
                    spawnAcc -= 1f;
                    Vector2 rnd = Random.insideUnitCircle * radius;
                    Vector3 p = center + new Vector3(rnd.x, cloudHeight - Random.Range(0.3f, 1.2f), rnd.y);
                    Icicle.Spawn(p, Random.Range(0.8f, 1.4f), Random.Range(4f, 8f), ignoreRoot, spawned % 3 == 0);
                    spawned++;
                }
            }

            float end = 0.35f + duration;
            if (t > end && circle != null)
            {
                circle.Release(false);
                circle = null;
                cloud.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                snow.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
            if (t > end) wind.volume = Mathf.MoveTowards(wind.volume, 0f, dt * 0.4f);
            if (t > end + 3.5f) Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (circle != null) Destroy(circle.gameObject);
        }
    }
}

namespace MagicTraining
{
    using UnityEngine;
    using static MagicTraining.MagicFX;
    using MinMaxCurve = UnityEngine.ParticleSystem.MinMaxCurve;

    public class FrozenBody : MonoBehaviour
    {
        Rigidbody rb;
        bool wasKinematic;
        float timer, t;
        Renderer shell;
        Vector3 shellScale, center;
        GameObject fxRoot;

        public static void Freeze(Rigidbody body, float duration)
        {
            if (body == null) return;
            var f = body.GetComponent<FrozenBody>();
            if (f != null) { f.timer = Mathf.Max(f.timer, duration); return; }
            f = body.gameObject.AddComponent<FrozenBody>();
            f.Init(body, duration);
        }

        void Init(Rigidbody body, float duration)
        {
            rb = body;
            timer = duration;
            wasKinematic = rb.isKinematic;
            if (!rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            rb.isKinematic = true;

            Bounds b = default;
            bool has = false;
            foreach (var r in GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds);
            }
            if (!has) b = new Bounds(transform.position, Vector3.one * 0.3f);
            center = b.center;

            var shellGo = new GameObject("Ice Shell");
            shellGo.transform.SetPositionAndRotation(center, Random.rotation);
            shellGo.AddComponent<MeshFilter>().sharedMesh = IceBlockMesh;
            shell = shellGo.AddComponent<MeshRenderer>();
            shell.sharedMaterial = IceMat;
            shell.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            float maxSide = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            shellScale = Vector3.one * (maxSide * 1.25f + 0.05f);
            shellGo.transform.localScale = shellScale * 0.5f;
            SetIce(shell, 0f, 0.3f);

            fxRoot = new GameObject("Freeze FX");
            fxRoot.transform.position = center;
            var frost = NewPS("Frost", fxRoot.transform, SparkMat, false, 80);
            {
                var m = frost.main;
                m.startLifetime = new MinMaxCurve(0.6f, 1.2f);
                m.startSize = new MinMaxCurve(0.008f, 0.02f);
                m.startColor = RandomFrom(IcePalette);
                var sh = frost.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = b.size * 1.1f;
                var col = frost.colorOverLifetime; col.enabled = true; col.color = FadeInOut;
                SetRate(frost, 20f);
                frost.Play();
            }
            var mist = NewPS("Cold Mist", fxRoot.transform, SmokeMat, true, 30);
            {
                var m = mist.main;
                m.startLifetime = new MinMaxCurve(1.2f, 2f);
                m.startSize = new MinMaxCurve(0.1f, 0.25f);
                m.startRotation = new MinMaxCurve(0f, Mathf.PI * 2f);
                m.startColor = new Color(0.8f, 0.92f, 1f, 0.22f);
                m.gravityModifier = 0.04f;
                var sh = mist.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = b.size;
                var col = mist.colorOverLifetime; col.enabled = true; col.color = FadeInOut;
                SetRate(mist, 6f);
                mist.Play();
            }
            PlayAt(IceFormClip, center, 0.6f, 0.75f);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            t += dt;
            float k = Mathf.Clamp01(t / 0.25f);
            float back = 1f + 2.2f * Mathf.Pow(k - 1f, 3f) + 1.2f * Mathf.Pow(k - 1f, 2f);
            if (shell != null)
            {
                shell.transform.localScale = shellScale * Mathf.Lerp(0.5f, 1f, back);
                SetIce(shell, k, 0.3f + 0.15f * Mathf.Sin(Time.time * 3f));
            }
            timer -= dt;
            if (timer <= 0f) Thaw();
        }

        void Thaw()
        {
            SpawnIceImpact(center, Vector3.up, 0.5f);
            if (rb != null) rb.isKinematic = wasKinematic;
            Destroy(this);
        }

        void OnDestroy()
        {
            if (shell != null) Destroy(shell.gameObject);
            if (fxRoot != null) Destroy(fxRoot);
        }
    }
}
