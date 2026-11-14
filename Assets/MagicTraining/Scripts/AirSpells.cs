namespace MagicTraining
{
    using System.Collections.Generic;
    using UnityEngine;
    using static MagicTraining.MagicFX;

    [DefaultExecutionOrder(-44)]
    public class AirCaster : MonoBehaviour
    {
        [Header("Tajos")]
        public float slashSpeed = 3f;
        public float slashCooldown = 0.25f;

        [Header("Bola de aire")]
        public float armExtended = 0.48f;
        public float ballChargeTime = 1f;

        [Header("Dash")]
        public float dashHandSpeed = 2.6f;
        public float dashDistance = 4f;
        public float dashTime = 0.22f;

        [Header("Salto")]
        public float jumpHandSpeed = 2.6f;
        public float jumpVelocity = 6f;
        public float jumpGravity = 12f;

        class HandState { public float lastExt, cooldown, stillT; public bool armed = true; }
        readonly HandState hL = new HandState(), hR = new HandState();
        float moveCooldown;
        AirBall ball;
        MagicRig.Hand ballHand;
        TornadoCaster tornado;

        enum Move { None, Dash, Jump }
        Move move;
        Vector3 dashDir;
        float moveT, vy;
        bool airDash;
        CharacterController cc;
        readonly List<Behaviour> disabledGravity = new List<Behaviour>();

        bool XFree(MagicRig rig) =>
            !LightningCaster.Active && !FireballCaster.HoldingAny && !DruidStaff.IsHeld && !EarthCaster.Armed(rig.L);

        void Awake() => tornado = GetComponent<TornadoCaster>();

        void OnDisable()
        {
            if (ball != null) ball.Cancel();
            ball = null;
            EndMove();
        }

        void Update()
        {
            var rig = MagicRig.I;
            if (rig == null || !rig.Ready) return;
            float dt = Time.deltaTime;
            moveCooldown -= dt;
            UpdateMove(rig, dt);

            bool x = rig.L.primary && XFree(rig);
            if (!x)
            {
                if (ball != null) { ball.Cancel(); ball = null; }
                hL.armed = hR.armed = true;
                hL.stillT = hR.stillT = 0f;
                return;
            }

            HandleHand(rig, rig.L, hL, dt);
            HandleHand(rig, rig.R, hR, dt);
        }

        Vector3 Shoulder(MagicRig rig, bool left)
        {
            Transform head = rig.Head;
            Vector3 right = head.right; right.y = 0f;
            right = right.sqrMagnitude > 0.001f ? right.normalized : Vector3.right;
            return head.position + Vector3.down * 0.18f + right * (left ? -0.17f : 0.17f);
        }

        void HandleHand(MagicRig rig, MagicRig.Hand h, HandState st, float dt)
        {
            Transform head = rig.Head;
            Vector3 fwd = head.forward; fwd.y = 0f;
            fwd = fwd.sqrMagnitude > 0.001f ? fwd.normalized : Vector3.forward;
            Vector3 shoulder = Shoulder(rig, h.left);
            Vector3 arm = h.Position - shoulder;
            float ext = arm.magnitude;
            float extRate = dt > 0f ? (ext - st.lastExt) / dt : 0f;
            st.lastExt = ext;
            st.cooldown -= dt;

            Vector3 v = h.velocity;
            float speed = v.magnitude;
            if (!st.armed && speed < 1.4f) st.armed = true;

            if (st.armed && st.cooldown <= 0f && speed > Mathf.Min(slashSpeed, Mathf.Min(dashHandSpeed, jumpHandSpeed)))
            {
                Vector3 flat = new Vector3(v.x, 0f, v.z);
                float fwdComp = Vector3.Dot(v, fwd);

                bool jumpGesture = v.y > jumpHandSpeed && v.y > speed * 0.75f;
                bool dashGesture = fwdComp > dashHandSpeed && fwdComp > speed * 0.75f && extRate > 1.2f;
                if (jumpGesture)
                {
                    Fired(st, rig);
                    if (moveCooldown <= 0f && move == Move.None) StartJump(rig);
                    return;
                }
                if (dashGesture)
                {
                    Fired(st, rig);
                    if (move != Move.Dash && (moveCooldown <= 0f || move == Move.Jump))
                        StartDash(rig, flat.sqrMagnitude > 0.01f ? flat.normalized : fwd);
                    return;
                }
                if (speed > slashSpeed)
                {
                    Fired(st, rig);
                    Vector3 travel = head.forward;
                    AirSlash.Create(h.Position + travel * 0.3f, travel, v, Mathf.Lerp(0.6f, 1.1f, Mathf.InverseLerp(slashSpeed, slashSpeed + 4f, speed)), rig.IgnoreRoot);
                    rig.Haptic(h.left, 0.6f, 0.08f);
                    st.cooldown = slashCooldown;
                    return;
                }
            }

            bool extended = ext > armExtended && Vector3.Dot(arm / Mathf.Max(ext, 0.001f), head.forward) > 0.6f && speed < 0.7f;
            if (ball != null && ballHand != h) return;
            if (extended)
            {
                st.stillT += dt;
                if (st.stillT > 0.15f && ball == null && move == Move.None)
                {
                    ball = AirBall.Create(h.Position + arm.normalized * 0.15f, rig.IgnoreRoot);
                    ballHand = h;
                    if (tornado != null) tornado.Cancel();
                }
                if (ball != null)
                {
                    float c = Mathf.Clamp01((st.stillT - 0.15f) / Mathf.Max(0.05f, ballChargeTime - 0.15f));
                    ball.Charging(h.Position + arm.normalized * (0.15f + 0.25f * c), c);
                    if (Random.value < 0.3f) rig.Haptic(h.left, 0.05f + 0.2f * c, 0.05f);
                    if (st.stillT >= ballChargeTime)
                    {
                        if (tornado != null) tornado.Cancel();
                        ball.Launch(arm.normalized);
                        ball = null;
                        ballHand = null;
                        st.stillT = 0f;
                        st.armed = false;
                        rig.Haptic(h.left, 0.8f, 0.15f);
                    }
                }
            }
            else
            {
                st.stillT = 0f;
                if (ball != null && ballHand == h) { ball.Cancel(); ball = null; ballHand = null; }
            }
        }

        void Fired(HandState st, MagicRig rig)
        {
            st.armed = false;
            st.stillT = 0f;
            if (tornado != null) tornado.Cancel();
            if (ball != null) { ball.Cancel(); ball = null; ballHand = null; }
        }

        void StartDash(MagicRig rig, Vector3 dir)
        {
            airDash = move == Move.Jump;
            if (!airDash) DisableGravityIfAirborne(rig);
            move = Move.Dash;
            dashDir = dir;
            moveT = 0f;
            moveCooldown = 0.6f;
            Vector3 feet = rig.PlayerRoot.position;
            SpawnWindPulse(feet + Vector3.up * 0.1f, 1.5f);
            SpawnAirBurst(rig.Head.position - dir * 0.4f, -dir, 0.7f);
            PlayAt(WhooshClip, rig.Head.position, 0.9f, 1.2f);
            rig.Haptic(true, 0.7f, 0.15f);
            rig.Haptic(false, 0.7f, 0.15f);
        }

        void StartJump(MagicRig rig)
        {
            move = Move.Jump;
            vy = jumpVelocity;
            moveT = 0f;
            moveCooldown = 0.8f;
            DisableGravity(rig);
            Vector3 feet = rig.PlayerRoot.position;
            SpawnWindPulse(feet + Vector3.up * 0.05f, 2.2f);
            SpawnAirBurst(feet, Vector3.up, 1.2f);
            PlayAt(WhooshClip, feet, 1f, 0.8f);
            rig.Haptic(true, 0.8f, 0.2f);
            rig.Haptic(false, 0.8f, 0.2f);
        }

        void UpdateMove(MagicRig rig, float dt)
        {
            if (move == Move.None || rig.PlayerRoot == null) return;
            Transform root = rig.PlayerRoot;
            if (cc == null || cc.transform != root) cc = root.GetComponent<CharacterController>();
            moveT += dt;

            if (move == Move.Dash)
            {
                float k = Mathf.Clamp01(moveT / dashTime);
                float spd = dashDistance / dashTime * 1.5f * (1f - k);
                Displace(root, dashDir * spd * dt);
                if (k >= 1f)
                {
                    if (airDash || disabledGravity.Count > 0)
                    {
                        move = Move.Jump;
                        vy = 0f;
                        moveT = 0f;
                        airDash = false;
                    }
                    else EndMove();
                }
                return;
            }

            vy -= jumpGravity * dt;
            Vector3 fwd = rig.Head.forward; fwd.y = 0f;
            CollisionFlags flags = Displace(root, Vector3.up * (vy * dt) + fwd.normalized * (0.8f * dt));
            bool landed = vy < 0f && ((flags & CollisionFlags.Below) != 0 || (cc == null && Grounded(rig)));
            if (landed || moveT > 4f)
            {
                SpawnWindPulse(root.position + Vector3.up * 0.05f, 1.5f);
                PlayAt(WhooshClip, root.position, 0.5f, 0.6f);
                EndMove();
            }
        }

        CollisionFlags Displace(Transform root, Vector3 delta)
        {
            if (cc != null && cc.enabled) return cc.Move(delta);
            root.position += delta;
            return CollisionFlags.None;
        }

        bool Grounded(MagicRig rig)
        {
            Transform root = rig.PlayerRoot;
            Vector3 p = root.position;
            FindGround(p, rig.IgnoreRoot, p.y - 10f, out Vector3 g, out _);
            if (p.y <= g.y + 0.02f) { root.position = new Vector3(p.x, g.y, p.z); return true; }
            return false;
        }

        void DisableGravity(MagicRig rig)
        {
            disabledGravity.Clear();
            foreach (var b in rig.PlayerRoot.GetComponentsInChildren<Behaviour>())
                if (b != null && b.enabled && b.GetType().Name == "GravityProvider") { b.enabled = false; disabledGravity.Add(b); }
        }

        void DisableGravityIfAirborne(MagicRig rig)
        {
            if (cc == null || cc.transform != rig.PlayerRoot) cc = rig.PlayerRoot.GetComponent<CharacterController>();
            if (cc != null && !cc.isGrounded) DisableGravity(rig);
        }

        void EndMove()
        {
            move = Move.None;
            foreach (var b in disabledGravity) if (b != null) b.enabled = true;
            disabledGravity.Clear();
        }
    }
}

namespace MagicTraining
{
    using System.Collections.Generic;
    using UnityEngine;
    using static MagicTraining.MagicFX;
    using MinMaxCurve = UnityEngine.ParticleSystem.MinMaxCurve;

    public class AirSlash : MonoBehaviour
    {
        public float speed = 18f;
        public float range = 22f;

        Transform ignoreRoot;
        LineRenderer glow, core, inner;
        TrailRenderer trailA, trailB;
        ParticleSystem streaks;
        float traveled, size, t;
        bool dead;
        readonly Vector3[] arc = new Vector3[17];

        public static AirSlash Create(Vector3 pos, Vector3 travelDir, Vector3 slashDir, float size, Transform ignoreRoot)
        {
            travelDir = travelDir.normalized;
            Vector3 across = Vector3.ProjectOnPlane(slashDir, travelDir);
            if (across.sqrMagnitude < 0.01f) across = Vector3.Cross(Vector3.up, travelDir);
            across.Normalize();
            Vector3 bulge = Vector3.Cross(travelDir, across);
            var go = new GameObject("Air Slash");
            go.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(travelDir, bulge));
            var s = go.AddComponent<AirSlash>();
            s.ignoreRoot = ignoreRoot;
            s.size = size;
            s.Build();
            PlayAt(WhooshClip, pos, 0.8f, Random.Range(1.5f, 1.8f));
            return s;
        }

        void Build()
        {
            Transform tr = transform;
            for (int i = 0; i < arc.Length; i++)
            {
                float a = Mathf.Lerp(-70f, 70f, i / (arc.Length - 1f)) * Mathf.Deg2Rad;
                arc[i] = new Vector3(Mathf.Sin(a), Mathf.Cos(a) - 1f, -(1f - Mathf.Cos(a)) * 0.6f);
            }
            var w = Curve((0f, 0f), (0.25f, 0.7f), (0.5f, 1f), (0.75f, 0.7f), (1f, 0f));
            glow = MakeArc("Glow", tr, BeamMat, 0.22f, Grad((0f, A(WindCyan, 0f)), (0.5f, A(WindCyan, 0.55f)), (1f, A(WindCyan, 0f))), w);
            core = MakeArc("Core", tr, CoreMat, 0.06f, Grad((0f, A(Color.white, 0f)), (0.5f, A(Color.white, 0.95f)), (1f, A(Color.white, 0f))), w);
            inner = MakeArc("Inner", tr, BeamMat, 0.1f, Grad((0f, A(WindWhite, 0f)), (0.5f, A(WindWhite, 0.35f)), (1f, A(WindWhite, 0f))), w);
            inner.transform.localPosition = new Vector3(0f, -0.08f, -0.12f);

            var tg = Grad((0f, A(WindWhite, 0.6f)), (1f, A(WindCyan, 0f)));
            trailA = NewTrail("Trail A", tr, BeamMat, Vector3.zero, 0.25f, 0.05f, Curve((0f, 1f), (1f, 0f)), tg);
            trailB = NewTrail("Trail B", tr, BeamMat, Vector3.zero, 0.25f, 0.05f, Curve((0f, 1f), (1f, 0f)), tg);
            trailA.emitting = trailB.emitting = true;

            streaks = NewPS("Streaks", tr, SparkMat, true, 200);
            var m = streaks.main;
            m.startLifetime = new MinMaxCurve(0.2f, 0.45f);
            m.startSize = new MinMaxCurve(0.01f, 0.025f);
            m.startColor = RandomFrom(WindPalette);
            var r = streaks.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.05f; r.lengthScale = 2f;
            var sh = streaks.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(1.6f, 0.2f, 0.1f);
            var col = streaks.colorOverLifetime; col.enabled = true; col.color = FadeOut;
            var em = streaks.emission; em.rateOverDistance = 8f;
            streaks.Play();
            Apply();
        }

        LineRenderer MakeArc(string name, Transform parent, Material mat, float width, Gradient g, AnimationCurve w)
        {
            var lr = NewLine(name, parent, mat, Vector3.zero, Vector3.zero, w, width, g, 1);
            lr.alignment = LineAlignment.TransformZ;
            lr.positionCount = arc.Length;
            return lr;
        }

        void Apply()
        {
            float s = size * Mathf.Lerp(1f, 1.5f, traveled / range);
            float fade = 1f - Mathf.Clamp01((traveled - range * 0.7f) / (range * 0.3f));
            for (int i = 0; i < arc.Length; i++)
            {
                Vector3 p = arc[i] * s;
                glow.SetPosition(i, p);
                core.SetPosition(i, p);
                inner.SetPosition(i, p * 0.85f);
            }
            float flick = 0.85f + 0.15f * Mathf.Sin(t * 40f);
            glow.widthMultiplier = 0.22f * s * fade * flick;
            core.widthMultiplier = 0.06f * s * fade;
            inner.widthMultiplier = 0.1f * s * fade;
            trailA.transform.localPosition = arc[2] * s;
            trailB.transform.localPosition = arc[arc.Length - 3] * s;
            var sh = streaks.shape; sh.scale = new Vector3(1.6f * s, 0.2f, 0.1f);
        }

        void Update()
        {
            if (dead) return;
            float dt = Time.deltaTime;
            t += dt;
            float step = speed * dt;
            Vector3 dir = transform.forward;
            float s = size * Mathf.Lerp(1f, 1.5f, traveled / range);
            if (CastIgnoring(transform.position, dir, step, s * 0.6f, ignoreRoot, out Vector3 point, out Vector3 normal, out Collider col))
            {
                Hit(point, normal, col, dir);
                return;
            }
            transform.position += dir * step;
            traveled += step;
            CutThrough(s, dir);
            Apply();
            if (traveled >= range) Finish(false, transform.position, -dir);
        }

        void CutThrough(float s, Vector3 dir)
        {
            foreach (var c in Physics.OverlapSphere(transform.position, s * 0.9f, ~0, QueryTriggerInteraction.Ignore))
            {
                var rb = c.attachedRigidbody;
                if (rb == null || rb.isKinematic) continue;
                if (ignoreRoot != null && rb.transform.IsChildOf(ignoreRoot)) continue;
                SpellTarget.HitCollider(c);
                rb.AddForce(dir * 9f + Vector3.up * 2f, ForceMode.VelocityChange);
                rb.AddTorque(Random.insideUnitSphere * 6f, ForceMode.VelocityChange);
            }
        }

        void Hit(Vector3 point, Vector3 normal, Collider col, Vector3 dir)
        {
            SpellTarget.HitCollider(col);
            var rb = col != null ? col.attachedRigidbody : null;
            if (rb != null && !rb.isKinematic && !(ignoreRoot != null && rb.transform.IsChildOf(ignoreRoot)))
                rb.AddForceAtPosition(dir * 10f + Vector3.up * 2f, point, ForceMode.VelocityChange);
            PushBodies(point, 1.5f, 4f, ignoreRoot);
            Finish(true, point, normal);
        }

        void Finish(bool impact, Vector3 point, Vector3 normal)
        {
            dead = true;
            SpawnAirBurst(point, normal, impact ? 1f : 0.5f);
            DetachTrail(trailA);
            DetachTrail(trailB);
            DetachPS(streaks, 0.6f);
            Destroy(gameObject);
        }
    }

    public class AirBall : MonoBehaviour
    {
        public float speed = 9f;
        public float range = 25f;
        public float grabRadius = 1.6f;

        Transform ignoreRoot;
        readonly LineRenderer[] bands = new LineRenderer[4];
        readonly Vector3[] buf = new Vector3[24];
        Renderer glowR;
        ParticleSystem swirl, trailMotes;
        AudioSource wind;
        float charge, traveled, t;
        bool flying, dead;
        Vector3 dir;
        readonly HashSet<Rigidbody> carried = new HashSet<Rigidbody>();

        float Radius => Mathf.Lerp(0.08f, 0.32f, charge);

        public static AirBall Create(Vector3 pos, Transform ignoreRoot)
        {
            var go = new GameObject("Air Ball");
            go.transform.position = pos;
            var b = go.AddComponent<AirBall>();
            b.ignoreRoot = ignoreRoot;
            b.Build();
            return b;
        }

        void Build()
        {
            Transform tr = transform;
            glowR = NewQuad("Glow", tr, GlowMat);
            SetColor(glowR, A(WindWhite, 0.25f));
            var g = Grad((0f, A(WindWhite, 0f)), (0.3f, A(WindCyan, 0.7f)), (0.7f, A(WindWhite, 0.6f)), (1f, A(WindWhite, 0f)));
            for (int i = 0; i < bands.Length; i++) bands[i] = NewBoltLine("Wind Band", tr, BeamMat, 0.02f, g, Curve((0f, 0.3f), (0.5f, 1f), (1f, 0.3f)));

            swirl = NewPS("Swirl", tr, SparkMat, false, 200);
            {
                var m = swirl.main;
                m.startLifetime = 0.5f;
                m.startSize = new MinMaxCurve(0.008f, 0.02f);
                m.startColor = RandomFrom(WindPalette);
                var sh = swirl.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.3f; sh.radiusThickness = 0f;
                var vel = swirl.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.Local;
                vel.x = 0f; vel.y = 0f; vel.z = 0f;
                vel.orbitalX = 5f; vel.orbitalY = 9f; vel.orbitalZ = 3f;
                vel.radial = -0.2f;
                var col = swirl.colorOverLifetime; col.enabled = true; col.color = FadeInOut;
                SetRate(swirl, 60f);
            }
            trailMotes = NewPS("Trail", tr, SparkMat, true, 300);
            {
                var m = trailMotes.main;
                m.startLifetime = new MinMaxCurve(0.4f, 0.9f);
                m.startSize = new MinMaxCurve(0.01f, 0.025f);
                m.startSpeed = new MinMaxCurve(0.2f, 0.8f);
                m.startColor = RandomFrom(WindPalette);
                var sh = trailMotes.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.25f;
                var col = trailMotes.colorOverLifetime; col.enabled = true; col.color = FadeInOut;
            }
            swirl.Play(); trailMotes.Play();

            wind = gameObject.AddComponent<AudioSource>();
            wind.clip = WindLoopClip;
            wind.loop = true;
            wind.spatialBlend = 1f;
            wind.minDistance = 1f;
            wind.maxDistance = 30f;
            wind.dopplerLevel = 0f;
            wind.volume = 0.2f;
            wind.pitch = 1.3f;
            wind.Play();
        }

        public void Charging(Vector3 pos, float c)
        {
            if (flying || dead) return;
            charge = Mathf.Clamp01(c);
            transform.position = pos;
            wind.volume = Mathf.Lerp(0.15f, 0.5f, charge);
            wind.pitch = Mathf.Lerp(1.6f, 1.1f, charge);
        }

        public void Launch(Vector3 direction)
        {
            if (flying || dead) return;
            flying = true;
            charge = 1f;
            dir = direction.normalized;
            var em = trailMotes.emission; em.rateOverDistance = 25f;
            PlayAt(WhooshClip, transform.position, 0.9f, 0.9f);
            SpawnWindPulse(transform.position, 1.2f);
        }

        public void Cancel()
        {
            if (dead) return;
            dead = true;
            SpawnWindPuff(transform.position);
            DetachPS(trailMotes, 1f);
            Destroy(gameObject);
        }

        void Update()
        {
            if (dead) return;
            float dt = Time.deltaTime;
            t += dt;
            float r = Radius;

            var cam = Camera.main;
            if (cam != null) glowR.transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
            glowR.transform.localScale = Vector3.one * r * 3.5f;
            for (int i = 0; i < bands.Length; i++)
            {
                Vector3 axis = Quaternion.Euler(i * 47f + t * 90f, i * 71f + t * 130f, 0f) * Vector3.up;
                Vector3 p1 = Vector3.Cross(axis, Vector3.right).normalized;
                if (p1.sqrMagnitude < 0.1f) p1 = Vector3.Cross(axis, Vector3.forward).normalized;
                Vector3 p2 = Vector3.Cross(axis, p1);
                for (int k = 0; k < 20; k++)
                {
                    float a = k / 19f * Mathf.PI * 1.6f + t * 8f + i;
                    buf[k] = transform.position + (p1 * Mathf.Cos(a) + p2 * Mathf.Sin(a)) * r * (1f + 0.15f * Mathf.Sin(a * 3f));
                }
                SetLine(bands[i], buf, 20);
                bands[i].widthMultiplier = 0.02f + r * 0.08f;
            }
            var sh = swirl.shape; sh.radius = r;

            if (!flying) return;

            float step = speed * dt;
            if (CastIgnoring(transform.position, dir, step, r, ignoreRoot, out Vector3 point, out Vector3 normal, out Collider col))
            {
                SpellTarget.HitCollider(col);
                Burst(point, normal);
                return;
            }
            transform.position += dir * step;
            traveled += step;
            Drag(dt);
            if (traveled >= range) Burst(transform.position, -dir);
        }

        void Drag(float dt)
        {
            Vector3 c = transform.position;
            foreach (var col in Physics.OverlapSphere(c, grabRadius, ~0, QueryTriggerInteraction.Ignore))
            {
                var rb = col.attachedRigidbody;
                if (rb == null || rb.isKinematic) continue;
                if (ignoreRoot != null && rb.transform.IsChildOf(ignoreRoot)) continue;
                if (carried.Add(rb)) SpellTarget.HitCollider(col);
            }
            foreach (var rb in carried)
            {
                if (rb == null || rb.isKinematic) continue;
                Vector3 to = c - rb.worldCenterOfMass;
                Vector3 target = dir * speed + to * 6f;
                rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, target, 1f - Mathf.Exp(-dt * 8f));
                rb.AddTorque(Random.insideUnitSphere * 8f * dt, ForceMode.VelocityChange);
            }
        }

        void Burst(Vector3 point, Vector3 normal)
        {
            dead = true;
            foreach (var rb in carried)
            {
                if (rb == null || rb.isKinematic) continue;
                Vector3 away = (rb.worldCenterOfMass - point).normalized;
                rb.AddForce(away * 5f + dir * 4f + Vector3.up * 3f, ForceMode.VelocityChange);
            }
            PushBodies(point, 2f, 5f, ignoreRoot);
            SpawnAirBurst(point, normal, 1.3f);
            DetachPS(trailMotes, 1f);
            Destroy(gameObject);
        }
    }
}

namespace MagicTraining
{
    using UnityEngine;
    using static MagicTraining.MagicFX;

    [DefaultExecutionOrder(-45)]
    public class TornadoCaster : MonoBehaviour
    {
        public float circlesForFull = 2.5f;
        [Range(0f, 1f)] public float minCharge = 0.15f;

        public static bool Charging { get; private set; }

        WindVortex vortex;
        MagicCircle circle;
        Vector3 acc, prevDir, center;
        bool hasPrev;
        float charge;

        static void SetCharging(bool v)
        {
            Charging = v;
            SpellLock.Set(Spell.Tornado, v);
        }

        void OnDisable()
        {
            SetCharging(false);
            if (vortex != null) Destroy(vortex.gameObject);
            if (circle != null) Destroy(circle.gameObject);
            vortex = null;
            circle = null;
        }

        void Update()
        {
            var rig = MagicRig.I;
            if (rig == null || !rig.Ready) return;
            var L = rig.L;

            if (!Charging)
            {
                if (L.primaryDown && SpellLock.CanStart(Spell.Tornado) && !DruidStaff.IsHeld) Begin(L);
                return;
            }

            if (!L.primary) { Release(rig, L); return; }

            Vector3 v = L.velocity;
            if (v.magnitude > 0.35f)
            {
                Vector3 d = v.normalized;
                if (hasPrev) acc += Vector3.Cross(prevDir, d);
                prevDir = d;
                hasPrev = true;
            }
            float newCharge = Mathf.Clamp01(acc.magnitude / (Mathf.PI * 2f * circlesForFull));

            if (Mathf.Floor(newCharge * circlesForFull * 4f) > Mathf.Floor(charge * circlesForFull * 4f))
                rig.Haptic(true, Mathf.Lerp(0.2f, 0.7f, newCharge), 0.05f);
            if (newCharge >= 1f && charge < 1f)
            {
                rig.Haptic(true, 0.9f, 0.15f);
                SpawnWindPulse(center, 0.8f);
                PlayAt(ChimeClip, center, 0.4f, 0.9f);
            }
            charge = newCharge;

            center = Vector3.Lerp(center, L.Position, 1f - Mathf.Exp(-Time.deltaTime * 3f));
            if (vortex == null && charge > 0.12f)
            {
                vortex = WindVortex.Create(center + Vector3.up * 0.12f);
                circle = MagicCircle.Create(MagicCircle.Element.Wind);
            }
            if (vortex != null)
            {
                vortex.Tick(center + Vector3.up * 0.12f, charge);
                circle.Follow(center + Vector3.up * 0.02f, Quaternion.LookRotation(Vector3.up),
                    Mathf.Lerp(0.18f, 0.45f, charge), Mathf.Lerp(1f, 4f, charge));
            }
        }

        void Begin(MagicRig.Hand L)
        {
            SetCharging(true);
            acc = Vector3.zero;
            hasPrev = false;
            charge = 0f;
            center = L.Position;
        }

        public void Cancel()
        {
            if (!Charging) return;
            SetCharging(false);
            if (vortex != null) Destroy(vortex.gameObject);
            vortex = null;
            if (circle != null) circle.Release(false);
            circle = null;
            charge = 0f;
        }

        void Release(MagicRig rig, MagicRig.Hand L)
        {
            SetCharging(false);
            if (vortex != null) vortex.Release();
            vortex = null;
            bool ok = charge >= minCharge;
            if (circle != null) circle.Release(ok);
            circle = null;
            if (!ok) return;

            Vector3 aim = L.AimForward;
            Vector3 flat = new Vector3(aim.x, 0f, aim.z);
            if (flat.sqrMagnitude < 0.04f) flat = new Vector3(rig.Head.forward.x, 0f, rig.Head.forward.z);
            flat.Normalize();

            Transform head = rig.Head;
            float floorY = rig.IgnoreRoot != null ? rig.IgnoreRoot.position.y : head.position.y - 1.6f;
            Vector3 start = new Vector3(head.position.x, floorY, head.position.z) + flat * 1.6f;
            FindGround(start, rig.IgnoreRoot, floorY, out Vector3 g, out Vector3 n);
            var tornado = Tornado.Create(g, flat, charge, rig.IgnoreRoot);

            MagicCircle.Flash(g + n * 0.03f, Quaternion.LookRotation(n), tornado.radius * 2.8f, MagicCircle.Element.Wind, 0.8f, 3f);
            rig.Haptic(true, 1f, 0.2f);
        }
    }
}

namespace MagicTraining
{
    using System.Collections.Generic;
    using UnityEngine;
    using static MagicTraining.MagicFX;
    using MinMaxCurve = UnityEngine.ParticleSystem.MinMaxCurve;

    public class Tornado : MonoBehaviour
    {
        public float height, radius, duration, speed;

        float power, t, seed, endT = -1f, groundY, phase;
        Vector3 dir;
        Transform ignoreRoot;

        ParticleSystem body, streaks, sparkles, dust;
        readonly LineRenderer[] bands = new LineRenderer[4];
        readonly Vector3[] buf = new Vector3[40];
        Light glow;
        AudioSource wind;
        readonly HashSet<Rigidbody> caught = new HashSet<Rigidbody>();
        readonly HashSet<Rigidbody> frameSet = new HashSet<Rigidbody>();

        public static Tornado Create(Vector3 ground, Vector3 dir, float power, Transform ignoreRoot)
        {
            var go = new GameObject("Tornado");
            go.transform.position = ground;
            var tn = go.AddComponent<Tornado>();
            tn.dir = dir.normalized;
            tn.power = Mathf.Clamp01(power);
            tn.ignoreRoot = ignoreRoot;
            tn.groundY = ground.y;
            tn.seed = Random.value * 100f;
            tn.height = Mathf.Lerp(2f, 5.5f, tn.power);
            tn.radius = Mathf.Lerp(0.5f, 1.6f, tn.power);
            tn.duration = Mathf.Lerp(4f, 9f, tn.power);
            tn.speed = Mathf.Lerp(2.5f, 4f, tn.power);
            tn.Build();
            return tn;
        }

        void Build()
        {
            Transform tr = transform;
            float H = height, R = radius;

            body = NewPS("Funnel", tr, SmokeMat, false, 500);
            {
                const float L = 1.6f;
                var m = body.main;
                m.startLifetime = L;
                m.startSize = new MinMaxCurve(0.3f * R + 0.1f, 0.6f * R + 0.15f);
                m.startRotation = new MinMaxCurve(0f, Mathf.PI * 2f);
                m.startColor = new Color(0.78f, 0.86f, 0.92f, 0.35f);
                var sh = body.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Circle;
                sh.radius = R * 0.18f; sh.rotation = new Vector3(90f, 0f, 0f);
                var vel = body.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.Local;
                vel.x = 0f; vel.y = H / L; vel.z = 0f;
                vel.orbitalX = 0f; vel.orbitalY = Mathf.Lerp(5f, 3.5f, power); vel.orbitalZ = 0f;
                vel.radial = R * 0.7f / L;
                var col = body.colorOverLifetime; col.enabled = true; col.color = FadeInOut;
                var sz = body.sizeOverLifetime; sz.enabled = true; sz.size = new MinMaxCurve(1f, Curve((0f, 0.5f), (1f, 1.6f)));
                var rot = body.rotationOverLifetime; rot.enabled = true; rot.z = new MinMaxCurve(-1f, 1f);
                SetRate(body, Mathf.Lerp(80f, 200f, power));
            }

            streaks = NewPS("Wind Streaks", tr, SparkMat, false, 400);
            {
                const float L = 1.2f;
                var m = streaks.main;
                m.startLifetime = L;
                m.startSize = new MinMaxCurve(0.015f, 0.035f);
                m.startColor = RandomFrom(WindPalette);
                var sh = streaks.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Circle;
                sh.radius = R * 0.25f; sh.rotation = new Vector3(90f, 0f, 0f);
                var vel = streaks.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.Local;
                vel.x = 0f; vel.y = H / L; vel.z = 0f;
                vel.orbitalX = 0f; vel.orbitalY = 7f; vel.orbitalZ = 0f;
                vel.radial = R * 0.8f / L;
                var r = streaks.GetComponent<ParticleSystemRenderer>();
                r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.06f; r.lengthScale = 2f;
                var col = streaks.colorOverLifetime; col.enabled = true; col.color = FadeInOut;
                SetRate(streaks, Mathf.Lerp(60f, 160f, power));
            }

            sparkles = NewPS("Sparkles", tr, SparkMat, false, 200);
            {
                const float L = 2f;
                var m = sparkles.main;
                m.startLifetime = L;
                m.startSize = new MinMaxCurve(0.02f, 0.045f);
                m.startColor = RandomFrom(WindPalette);
                var sh = sparkles.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Circle;
                sh.radius = R * 0.3f; sh.rotation = new Vector3(90f, 0f, 0f);
                var vel = sparkles.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.Local;
                vel.x = 0f; vel.y = H / L; vel.z = 0f;
                vel.orbitalX = 0f; vel.orbitalY = 4.5f; vel.orbitalZ = 0f;
                vel.radial = R * 0.6f / L;
                var nz = sparkles.noise; nz.enabled = true; nz.strength = 0.2f; nz.frequency = 1.5f;
                var col = sparkles.colorOverLifetime; col.enabled = true; col.color = FadeInOut;
                SetRate(sparkles, 30f);
            }

            dust = NewPS("Dust", tr, SmokeMat, true, 200);
            {
                var m = dust.main;
                m.startLifetime = new MinMaxCurve(0.8f, 1.5f);
                m.startSpeed = new MinMaxCurve(1f, 3f);
                m.startSize = new MinMaxCurve(0.3f * R, 0.7f * R);
                m.startRotation = new MinMaxCurve(0f, Mathf.PI * 2f);
                m.startColor = new Color(0.6f, 0.55f, 0.5f, 0.35f);
                m.gravityModifier = -0.05f;
                var sh = dust.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Circle;
                sh.radius = R * 0.5f; sh.rotation = new Vector3(90f, 0f, 0f);
                var lv = dust.limitVelocityOverLifetime; lv.enabled = true; lv.limit = 100f; lv.drag = 1.5f;
                var col = dust.colorOverLifetime; col.enabled = true; col.color = FadeInOut;
                var sz = dust.sizeOverLifetime; sz.enabled = true; sz.size = new MinMaxCurve(1f, Curve((0f, 0.6f), (1f, 1.5f)));
                SetRate(dust, Mathf.Lerp(25f, 60f, power));
            }

            var bandGrad = Grad((0f, A(WindWhite, 0f)), (0.2f, A(WindCyan, 0.45f)), (0.8f, A(WindWhite, 0.35f)), (1f, A(WindWhite, 0f)));
            for (int i = 0; i < bands.Length; i++)
                bands[i] = NewBoltLine("Wind Band", tr, BeamMat, 0.06f, bandGrad, Curve((0f, 0.4f), (1f, 1.2f)));

            glow = NewLight(tr, Vector3.up * (H * 0.4f), WindCyan, R * 3f, 0.8f);

            wind = gameObject.AddComponent<AudioSource>();
            wind.clip = WindLoopClip;
            wind.loop = true;
            wind.spatialBlend = 1f;
            wind.minDistance = 2f;
            wind.maxDistance = 40f;
            wind.dopplerLevel = 0f;
            wind.volume = 0f;
            wind.pitch = Mathf.Lerp(1.1f, 0.75f, power);
            wind.Play();

            transform.localScale = Vector3.one * 0.01f;
            body.Play(); streaks.Play(); sparkles.Play(); dust.Play();
            PlayAt(WhooshClip, transform.position, 0.8f, 0.5f);
            SpawnWindPulse(transform.position + Vector3.up * 0.1f, R * 2.5f);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            t += dt;
            float grow = Mathf.SmoothStep(0f, 1f, t / 0.6f);
            float fade = 1f;
            if (endT < 0f && t >= duration) Dissipate();
            if (endT >= 0f)
            {
                fade = 1f - Mathf.Clamp01((t - endT) / 1.2f);
                if (t - endT > 2f) { Destroy(gameObject); return; }
            }
            float scale = Mathf.Max(0.01f, grow * Mathf.Lerp(0.6f, 1f, fade));
            transform.localScale = Vector3.one * scale;

            float yaw = (Mathf.PerlinNoise(seed, t * 0.4f) - 0.5f) * 70f * dt;
            dir = Quaternion.AngleAxis(yaw, Vector3.up) * dir;
            Vector3 p = transform.position + dir * (speed * dt * (endT < 0f ? 1f : 0.4f));
            if (FindGround(p, ignoreRoot, groundY, out Vector3 g, out _))
                groundY = Mathf.Lerp(groundY, g.y, 1f - Mathf.Exp(-dt * 8f));
            p.y = groundY;
            transform.position = p;

            float H = height * scale, R = radius * scale;
            phase += dt * Mathf.Lerp(7f, 4f, power);
            for (int i = 0; i < bands.Length; i++)
            {
                Helix(bands[i], buf, 30, p, Vector3.up, H, R * 0.12f, R, 2.2f, -phase + i * Mathf.PI * 0.5f);
                bands[i].widthMultiplier = 0.06f * Mathf.Lerp(0.7f, 1.4f, power) * scale * fade;
            }

            if (endT < 0f) Suck(dt, H, R);

            glow.intensity = 0.8f * fade * grow;
            wind.volume = Mathf.Lerp(0.3f, 0.7f, power) * grow * fade;
        }

        void Suck(float dt, float H, float R)
        {
            if (H < 0.1f) return;
            Vector3 basePos = transform.position;
            frameSet.Clear();
            foreach (var c in Physics.OverlapCapsule(basePos, basePos + Vector3.up * H, R * 1.4f, ~0, QueryTriggerInteraction.Ignore))
            {
                SpellTarget.HitCollider(c);
                var rb = c.attachedRigidbody;
                if (rb == null || rb.isKinematic || !frameSet.Add(rb)) continue;
                if (ignoreRoot != null && rb.transform.IsChildOf(ignoreRoot)) continue;
                caught.Add(rb);

                Vector3 off = rb.worldCenterOfMass - basePos;
                float y = Mathf.Clamp(off.y, 0f, H);
                Vector3 hv = new Vector3(off.x, 0f, off.z);
                float dist = hv.magnitude;
                Vector3 hn = dist > 0.01f ? hv / dist : Vector3.right;
                Vector3 tang = Vector3.Cross(Vector3.up, hn);
                float targetR = Mathf.Lerp(R * 0.25f, R * 0.8f, y / H);

                Vector3 accel = tang * Mathf.Lerp(7f, 13f, power)
                              - hn * ((dist - targetR) * 5f)
                              + Vector3.up * (9.81f + Mathf.Clamp(H * 0.65f - off.y, -2f, 4f) * 2.5f);
                rb.AddForce(accel * dt, ForceMode.VelocityChange);
                rb.linearVelocity *= 1f - Mathf.Min(1f, 0.8f * dt);
                rb.AddTorque(Random.insideUnitSphere * (6f * dt), ForceMode.VelocityChange);
            }
        }

        void Dissipate()
        {
            endT = t;
            body.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            streaks.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            sparkles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            dust.Stop(true, ParticleSystemStopBehavior.StopEmitting);

            Vector3 basePos = transform.position;
            foreach (var rb in caught)
            {
                if (rb == null || rb.isKinematic) continue;
                Vector3 hv = rb.worldCenterOfMass - basePos;
                hv.y = 0f;
                Vector3 hn = hv.sqrMagnitude > 0.0001f ? hv.normalized : Random.insideUnitSphere;
                rb.AddForce(hn * Random.Range(3f, 6f) + Vector3.up * Random.Range(2f, 4f), ForceMode.VelocityChange);
            }
            caught.Clear();
            SpawnWindPuff(basePos + Vector3.up * (height * 0.4f));
        }
    }
}

namespace MagicTraining
{
    using UnityEngine;
    using static MagicTraining.MagicFX;
    using MinMaxCurve = UnityEngine.ParticleSystem.MinMaxCurve;

    public class WindVortex : MonoBehaviour
    {
        ParticleSystem swirl, sparkles;
        readonly LineRenderer[] bands = new LineRenderer[3];
        readonly Vector3[] buf = new Vector3[24];
        AudioSource wind;
        float phase;

        public static WindVortex Create(Vector3 pos)
        {
            var go = new GameObject("Wind Vortex");
            go.transform.position = pos;
            var v = go.AddComponent<WindVortex>();
            v.Build();
            return v;
        }

        void Build()
        {
            Transform t = transform;

            swirl = NewPS("Swirl", t, SparkMat, false, 300);
            {
                const float L = 0.5f;
                var m = swirl.main;
                m.startLifetime = L;
                m.startSize = new MinMaxCurve(0.01f, 0.02f);
                m.startColor = RandomFrom(WindPalette);
                var sh = swirl.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Circle;
                sh.radius = 0.03f; sh.radiusThickness = 0f; sh.rotation = new Vector3(90f, 0f, 0f);
                var vel = swirl.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.Local;
                vel.x = 0f; vel.y = 0.3f / L; vel.z = 0f;
                vel.orbitalX = 0f; vel.orbitalY = 10f; vel.orbitalZ = 0f;
                vel.radial = 0.1f / L;
                var r = swirl.GetComponent<ParticleSystemRenderer>();
                r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.05f; r.lengthScale = 2f;
                var col = swirl.colorOverLifetime; col.enabled = true; col.color = FadeInOut;
            }
            sparkles = NewPS("Sparkles", t, SparkMat, false, 80);
            {
                var m = sparkles.main;
                m.startLifetime = 0.6f;
                m.startSize = new MinMaxCurve(0.012f, 0.025f);
                m.startColor = RandomFrom(WindPalette);
                var sh = sparkles.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.1f; sh.radiusThickness = 0f;
                var vel = sparkles.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.Local;
                vel.x = 0f; vel.y = 0.1f; vel.z = 0f;
                vel.orbitalX = 0f; vel.orbitalY = 6f; vel.orbitalZ = 0f;
                var col = sparkles.colorOverLifetime; col.enabled = true; col.color = FadeInOut;
            }

            var bandGrad = Grad((0f, A(WindWhite, 0f)), (0.3f, A(WindCyan, 0.6f)), (1f, A(WindWhite, 0f)));
            for (int i = 0; i < bands.Length; i++)
                bands[i] = NewBoltLine("Wind Band", t, BeamMat, 0.02f, bandGrad, Curve((0f, 0.4f), (1f, 1f)));

            wind = gameObject.AddComponent<AudioSource>();
            wind.clip = WindLoopClip;
            wind.loop = true;
            wind.spatialBlend = 1f;
            wind.minDistance = 0.6f;
            wind.maxDistance = 25f;
            wind.dopplerLevel = 0f;
            wind.volume = 0.1f;
            wind.Play();

            swirl.Play();
            sparkles.Play();
        }

        public void Tick(Vector3 pos, float charge)
        {
            transform.position = pos;
            float s = Mathf.Lerp(0.6f, 1.6f, charge);
            transform.localScale = Vector3.one * s;
            phase += Time.deltaTime * Mathf.Lerp(8f, 20f, charge);
            for (int i = 0; i < bands.Length; i++)
            {
                Helix(bands[i], buf, 20, pos - Vector3.up * (0.1f * s), Vector3.up, 0.3f * s, 0.02f * s, 0.12f * s, 1.5f,
                    -phase + i * Mathf.PI * 2f / bands.Length);
                bands[i].widthMultiplier = 0.02f * s;
            }
            SetRate(swirl, Mathf.Lerp(30f, 120f, charge));
            SetRate(sparkles, Mathf.Lerp(5f, 30f, charge));
            wind.volume = Mathf.Lerp(0.1f, 0.5f, charge);
            wind.pitch = Mathf.Lerp(0.8f, 1.5f, charge);
        }

        public void Release()
        {
            SpawnWindPuff(transform.position);
            DetachPS(swirl, 0.6f);
            DetachPS(sparkles, 0.7f);
            Destroy(gameObject);
        }
    }
}
