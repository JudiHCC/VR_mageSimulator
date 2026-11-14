namespace MagicTraining
{
    using UnityEngine;
    using UnityEngine.SceneManagement;
    using static MagicTraining.MagicFX;
    using MinMaxCurve = UnityEngine.ParticleSystem.MinMaxCurve;

    [DefaultExecutionOrder(-60)]
    public class Trident : MonoBehaviour
    {
        public const float Length = 1.8f;
        public float grabRadius = 0.25f;

        [Header("Lanzamiento")]
        public float throwSpeed = 1.6f;
        public float throwBoost = 2.1f;
        public float flightGravity = 2.5f;
        public float returnAfter = 8f;

        [Header("Hechizos")]
        public bool waterSpells = false;
        [Header("Golpe al suelo (campo de agua)")]
        public float minStrikeSpeed = 0.9f;
        public float fullStrikeSpeed = 3.5f;
        public float strikeHeight = 0.08f;
        public float strikeCooldown = 0.35f;
        Vector3 lastButt;
        bool strikeArmed;
        float nextStrike, stuckDuration = 0.35f;

        public static bool WaterSpells => IsHeld && Instance.waterSpells;

        public Vector3 ButtPosition => transform.position;
        Vector3 LowEnd => ButtPosition.y <= TipPosition.y ? ButtPosition : TipPosition;
        public float returnSpeed = 20f;
        enum Mode { Floating, Held, Flying, Stuck, Returning }
        Mode mode = Mode.Floating;

        public static bool IsInFlight => Instance != null && (Instance.mode == Mode.Flying || Instance.mode == Mode.Stuck || Instance.mode == Mode.Returning);
        MagicRig.Hand thrower;
        Vector3 velocity;
        float flightT, stuckT, curReturnSpeed;
        TrailRenderer trail, trailCore;
        ParticleSystem wake;
        public static Trident Instance { get; private set; }
        public static bool IsHeld => Instance != null && Instance.holder != null;
        public static bool IsHeldBy(MagicRig.Hand h) => IsHeld && h != null && (Instance.holderHand == h || Instance.secondHand == h);

        public static bool IsTwoHanded => IsHeld && Instance.secondHand != null;
        public static float Power => IsTwoHanded ? Instance.twoHandPower : 1f;
        [Header("A dos manos")]
        public float twoHandPower = 1.5f;
        MagicRig.Hand secondHand;
        float zPrimary, zSecond, twoBlend, twoHapticT;
        MagicCircle powerCircle;
        public static MagicRig.Hand HeldHand => IsHeld ? Instance.holderHand : null;
        public Vector3 TipPosition => transform.TransformPoint(0f, 0f, Length);
        public Vector3 HeadPosition => transform.TransformPoint(0f, 0f, 1.4f);
        MagicRig.Hand holderHand;
        Transform holder;
        Vector3 holdPos;
        Quaternion holdRot;
        Vector3 basePos, floorPoint;
        float t, hover, charge, regen;
        bool hoverL, hoverR;
        Renderer orbR;
        ParticleSystem swirl, bubbles, drips, orbGlow, chargeSparks;
        readonly LineRenderer[] prongGlow = new LineRenderer[3];
        readonly LineRenderer[] chargeArcs = new LineRenderer[3];
        readonly Vector3[] buf = new Vector3[12];
        Light glow;
        AudioSource hum;
        MagicCircle idleCircle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            SpawnDefault();
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        static void OnSceneLoaded(Scene s, LoadSceneMode m) => SpawnDefault();
        static void SpawnDefault()
        {
            if (Instance != null) return;
            Vector3 pos;
            var marker = GameObject.Find("Tridente");
            if (marker != null) pos = marker.transform.position;
            else
            {
                var cam = Camera.main;
                if (cam == null) return;
                Transform root = cam.transform.root;
                Vector3 f = root.forward; f.y = 0f;
                if (f.sqrMagnitude < 0.01f) f = Vector3.forward;
                f.Normalize();
                Vector3 r = Vector3.Cross(Vector3.up, f);
                pos = root.position + f * 1.1f + r * 0.7f + Vector3.up * 0.35f;
            }
            var go = new GameObject("Trident");
            go.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(Vector3.up, Vector3.forward));
            go.AddComponent<Trident>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            Build();
            basePos = transform.position;
            UpdateFloor();
            idleCircle = MagicCircle.Create(MagicCircle.Element.Water);
        }
        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (idleCircle != null) Destroy(idleCircle.gameObject);
        }
        void Build()
        {
            Transform tr = transform;
            var model = new GameObject("Model").transform;
            model.SetParent(tr, false);
            var gold = new GameObject("Gold");
            gold.transform.SetParent(model, false);
            gold.AddComponent<MeshFilter>().sharedMesh = TridentGoldMesh;
            gold.AddComponent<MeshRenderer>().sharedMaterial = GoldMat;
            var gems = new GameObject("Aquamarine Blades");
            gems.transform.SetParent(model, false);
            gems.AddComponent<MeshFilter>().sharedMesh = TridentGemMesh;
            var gr = gems.AddComponent<MeshRenderer>();
            gr.sharedMaterial = GemMat;
            gr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            orb.name = "Water Orb";
            DestroyImmediate(orb.GetComponent<Collider>());
            orb.transform.SetParent(model, false);
            orb.transform.localPosition = new Vector3(0f, 0f, 1.145f);
            orb.transform.localScale = Vector3.one * 0.075f;
            orbR = orb.GetComponent<MeshRenderer>();
            orbR.sharedMaterial = OrbMat;
            orbR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            orbGlow = NewPS("Orb Glow", model, GlowMat, false, 20);
            orbGlow.transform.localPosition = new Vector3(0f, 0f, 1.145f);
            {
                var m = orbGlow.main;
                m.startLifetime = new MinMaxCurve(0.2f, 0.35f);
                m.startSize = new MinMaxCurve(0.16f, 0.22f);
                m.startColor = RandomFrom(Grad((0f, A(WaterTeal, 0.6f)), (1f, A(WaterLight, 0.6f))));
                var col = orbGlow.colorOverLifetime; col.enabled = true; col.color = FadeInOut;
                SetRate(orbGlow, 25f);
            }

            var prongGrad = Grad((0f, A(WaterTeal, 0f)), (0.4f, A(WaterTeal, 0.6f)), (1f, A(WaterFoam, 0.9f)));
            prongGlow[0] = NewLine("Prong Glow", model, BeamMat, new Vector3(0f, 0f, 1.25f), new Vector3(0f, 0f, 1.8f), Curve((0f, 0.6f), (1f, 0.2f)), 0.03f, prongGrad);
            prongGlow[1] = NewLine("Prong Glow", model, BeamMat, new Vector3(-0.16f, 0f, 1.32f), new Vector3(-0.145f, 0f, 1.7f), Curve((0f, 0.6f), (1f, 0.2f)), 0.025f, prongGrad);
            prongGlow[2] = NewLine("Prong Glow", model, BeamMat, new Vector3(0.16f, 0f, 1.32f), new Vector3(0.145f, 0f, 1.7f), Curve((0f, 0.6f), (1f, 0.2f)), 0.025f, prongGrad);

            swirl = NewPS("Water Swirl", model, SparkMat, false, 120);
            swirl.transform.localPosition = new Vector3(0f, 0f, 1.2f);
            {
                const float L = 0.9f;
                var m = swirl.main;
                m.startLifetime = L;
                m.startSize = new MinMaxCurve(0.01f, 0.022f);
                m.startColor = RandomFrom(WaterPalette);
                var sh = swirl.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 0.11f; sh.radiusThickness = 0f;
                var vel = swirl.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.Local;
                vel.x = 0f; vel.y = 0f; vel.z = 0.3f / L;
                vel.orbitalX = 0f; vel.orbitalY = 0f; vel.orbitalZ = 6f;
                var col = swirl.colorOverLifetime; col.enabled = true; col.color = FadeInOut;
                SetRate(swirl, 30f);
            }
            bubbles = NewPS("Bubbles", model, SparkMat, false, 40);
            bubbles.transform.localPosition = new Vector3(0f, 0f, 1.145f);
            {
                var m = bubbles.main;
                m.startLifetime = new MinMaxCurve(0.4f, 0.8f);
                m.startSize = new MinMaxCurve(0.005f, 0.012f);
                m.startSpeed = new MinMaxCurve(0.02f, 0.06f);
                m.startColor = A(WaterFoam, 0.9f);
                var sh = bubbles.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.06f;
                var col = bubbles.colorOverLifetime; col.enabled = true; col.color = FadeInOut;
                SetRate(bubbles, 10f);
            }
            drips = NewPS("Drips", model, SparkMat, true, 60);
            drips.transform.localPosition = new Vector3(0f, 0f, 1.5f);
            {
                var m = drips.main;
                m.startLifetime = new MinMaxCurve(0.6f, 1f);
                m.startSize = new MinMaxCurve(0.008f, 0.015f);
                m.gravityModifier = 1f;
                m.startColor = RandomFrom(WaterPalette);
                var sh = drips.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(0.3f, 0.03f, 0.3f);
                var col = drips.colorOverLifetime; col.enabled = true; col.color = FadeOut;
                SetRate(drips, 6f);
            }

            chargeSparks = NewPS("Storm Sparks", tr, SparkMat, true, 200);
            {
                var m = chargeSparks.main;
                m.startLifetime = new MinMaxCurve(0.1f, 0.3f);
                m.startSpeed = new MinMaxCurve(0.5f, 2.5f);
                m.startSize = new MinMaxCurve(0.006f, 0.014f);
                m.startColor = RandomFrom(ElecPalette);
                var r = chargeSparks.GetComponent<ParticleSystemRenderer>();
                r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.03f; r.lengthScale = 1.2f;
                var sh = chargeSparks.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.12f;
                var col = chargeSparks.colorOverLifetime; col.enabled = true; col.color = FadeOut;
            }
            for (int i = 0; i < chargeArcs.Length; i++)
                chargeArcs[i] = NewBoltLine("Storm Arc", tr, CoreMat, 0.008f, ElecCoreGradient, Curve((0f, 0.6f), (0.5f, 1f), (1f, 0.4f)));
            glow = NewLight(model, new Vector3(0f, 0f, 1.35f), WaterTeal, 2f, 1f);

            trail = NewTrail("Trail", tr, BeamMat, new Vector3(0f, 0f, 1.45f), 0.3f, 0.16f, Curve((0f, 1f), (1f, 0f)),
                Grad((0f, A(WaterLight, 0.9f)), (0.4f, A(WaterTeal, 0.6f)), (1f, A(WaterDeep, 0f))));
            trailCore = NewTrail("Trail Core", tr, CoreMat, new Vector3(0f, 0f, 1.45f), 0.12f, 0.04f, Curve((0f, 1f), (1f, 0f)),
                Grad((0f, WaterFoam), (1f, A(WaterLight, 0f))));
            wake = NewPS("Flight Wake", tr, DropMat, true, 300);
            wake.transform.localPosition = new Vector3(0f, 0f, 1.3f);
            {
                var m = wake.main;
                m.startLifetime = new MinMaxCurve(0.4f, 0.9f);
                m.startSpeed = new MinMaxCurve(0.2f, 1f);
                m.startSize = new MinMaxCurve(0.025f, 0.06f);
                m.gravityModifier = 1f;
                m.startColor = RandomFrom(DropPalette);
                var sh = wake.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.15f;
                var col = wake.colorOverLifetime; col.enabled = true; col.color = FadeOut;
                var em = wake.emission; em.rateOverTime = 0f; em.rateOverDistance = 0f;
            }
            wake.Play();
            hum = gameObject.AddComponent<AudioSource>();
            hum.clip = WaterRushClip;
            hum.loop = true;
            hum.spatialBlend = 1f;
            hum.minDistance = 0.3f;
            hum.maxDistance = 6f;
            hum.dopplerLevel = 0f;
            hum.volume = 0.08f;
            hum.pitch = 1.4f;
            hum.Play();
            orbGlow.Play(); swirl.Play(); bubbles.Play(); drips.Play(); chargeSparks.Play();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            t += dt;
            var rig = MagicRig.I;
            switch (mode)
            {
                case Mode.Held:
                    if (rig == null || holderHand == null) { Release(); break; }
                    if (!holderHand.grip && secondHand != null && secondHand.grip)
                    {
                        holderHand = secondHand;
                        holder = secondHand.t;
                        ClearSecondHand();
                    }
                    if (!holderHand.grip)
                    {
                        ClearSecondHand();
                        if (holderHand.velocity.magnitude >= throwSpeed) Throw(rig, holderHand);
                        else Release();
                    }
                    else
                    {
                        UpdateSecondHand(rig);
                        Vector3 hp = holder.TransformPoint(holdPos);
                        Quaternion hr = holder.rotation * holdRot;
                        if (secondHand != null) TwoHandPose(ref hp, ref hr, dt);
                        transform.SetPositionAndRotation(hp, hr);
                        CheckStrike(dt, rig);
                    }
                    break;
                case Mode.Floating:
                {
                    transform.position = basePos + Vector3.up * (Mathf.Sin(t * 1.5f) * 0.03f);
                    Quaternion upright = Quaternion.AngleAxis(t * 20f, Vector3.up) * Quaternion.LookRotation(Vector3.up, Vector3.forward);
                    transform.rotation = Quaternion.Slerp(transform.rotation, upright, 1f - Mathf.Exp(-dt * 2f));
                    if (idleCircle != null) idleCircle.Follow(floorPoint + Vector3.up * 0.03f, Quaternion.LookRotation(Vector3.up), 0.9f, 0.6f);
                    bool anyNear = false;
                    if (rig != null && rig.Ready)
                    {
                        anyNear |= TryHand(rig, rig.L, ref hoverL);
                        if (holder == null) anyNear |= TryHand(rig, rig.R, ref hoverR);
                    }
                    hover = Mathf.MoveTowards(hover, anyNear ? 1f : 0f, dt * 6f);
                    break;
                }
                case Mode.Flying:
                    Fly(dt, rig);
                    break;
                case Mode.Stuck:
                    stuckT += dt;
                    if (stuckT > stuckDuration) StartReturn();
                    break;
                case Mode.Returning:
                    ReturnToHand(dt, rig);
                    break;
            }
            float held = mode == Mode.Held ? 1f : 0f;
            glow.intensity = 0.8f + 0.6f * hover + 0.6f * held + 0.3f * Mathf.Sin(t * 3f) + 2f * charge + (IsInFlight ? 1.5f : 0f)
                           + twoBlend * (1.5f + 0.5f * Mathf.Sin(t * 6f));
            glow.range = 2f + twoBlend * 1.5f;
            hum.volume = 0.08f + 0.1f * held + (IsInFlight ? 0.4f : 0f);
            SetRate(swirl, 30f + 40f * held + 60f * charge + (IsInFlight ? 80f : 0f) + 90f * twoBlend);
            SetRate(drips, 6f + 20f * twoBlend);
            SetRate(bubbles, 10f + 20f * twoBlend);
            UpdateCharge(dt);
        }
        bool TryHand(MagicRig rig, MagicRig.Hand h, ref bool wasHover)
        {
            bool near = DistanceToShaft(h.Position) < grabRadius;
            if (near && !wasHover) rig.Haptic(h.left, 0.15f, 0.04f);
            wasHover = near;
            if (near && h.gripDown && !h.gripConsumed) { Grab(rig, h); return true; }
            return near;
        }


        void UpdateSecondHand(MagicRig rig)
        {
            var other = rig.Other(holderHand);
            if (secondHand == null)
            {
                if (other.gripDown && !other.gripConsumed && !DruidStaff.IsHeldBy(other) && DistanceToShaft(other.Position) < grabRadius + 0.05f)
                {
                    other.gripConsumed = true;
                    secondHand = other;
                    zPrimary = transform.InverseTransformPoint(holder.position).z;
                    zSecond = transform.InverseTransformPoint(other.Position).z;
                    if (Mathf.Abs(zSecond - zPrimary) < 0.12f) zSecond = zPrimary + (zSecond >= zPrimary ? 0.12f : -0.12f);

                    rig.Haptic(true, 0.8f, 0.15f);
                    rig.Haptic(false, 0.8f, 0.15f);
                    SpawnSplash(HeadPosition, 0.7f, 0.6f);
                    PlayAt(ChimeClip, HeadPosition, 0.5f, 0.8f);
                    if (powerCircle == null) powerCircle = MagicCircle.Create(MagicCircle.Element.Water);
                }
                return;
            }
            if (!secondHand.grip) { ClearSecondHand(); rig.Haptic(holderHand.left, 0.3f, 0.06f); return; }
            twoHapticT -= Time.deltaTime;
            if (twoHapticT <= 0f)
            {
                twoHapticT = 0.25f;
                rig.Haptic(true, 0.08f, 0.05f);
                rig.Haptic(false, 0.08f, 0.05f);
            }
            if (powerCircle != null)
                powerCircle.Follow(HeadPosition, Quaternion.LookRotation(transform.forward), 0.55f + 0.04f * Mathf.Sin(t * 5f), 4f);
        }

        void TwoHandPose(ref Vector3 pos, ref Quaternion rot, float dt)
        {
            twoBlend = Mathf.MoveTowards(twoBlend, 1f, dt / 0.15f);
            Vector3 a = holder.position, b = secondHand.Position;
            Vector3 axis = (b - a) * Mathf.Sign(zSecond - zPrimary);
            if (axis.sqrMagnitude < 1e-4f) return;
            axis.Normalize();
            Quaternion r2 = Quaternion.FromToRotation(rot * Vector3.forward, axis) * rot;
            Vector3 p2 = a - axis * zPrimary;
            pos = Vector3.Lerp(pos, p2, twoBlend);
            rot = Quaternion.Slerp(rot, r2, twoBlend);
        }

        void ClearSecondHand()
        {
            if (secondHand == null && powerCircle == null) return;
            secondHand = null;
            twoBlend = 0f;
            if (holder != null)
            {
                holdPos = holder.InverseTransformPoint(transform.position);
                holdRot = Quaternion.Inverse(holder.rotation) * transform.rotation;
            }
            if (powerCircle != null) powerCircle.Release(false);
            powerCircle = null;
        }
        void Throw(MagicRig rig, MagicRig.Hand h)
        {
            thrower = h;
            holder = null;
            holderHand = null;
            mode = Mode.Flying;
            flightT = 0f;
            velocity = rig.ThrowVelocity(h, throwBoost, 0.2f);
            transform.rotation = Quaternion.LookRotation(velocity.normalized, transform.up);
            StartTrails();
            rig.Haptic(h.left, 0.8f, 0.12f);
            PlayAt(WhooshClip, transform.position, 0.8f, 0.85f);
            MagicCircle.Flash(HeadPosition, transform.rotation, 0.6f, MagicCircle.Element.Water, 0.15f, 4f);
        }
        void StartTrails()
        {
            trail.Clear(); trail.emitting = true;
            trailCore.Clear(); trailCore.emitting = true;
            var em = wake.emission; em.rateOverDistance = 12f;
        }
        void StopTrails()
        {
            trail.emitting = false;
            trailCore.emitting = false;
            var em = wake.emission; em.rateOverDistance = 0f;
        }
        void Fly(float dt, MagicRig rig)
        {
            flightT += dt;
            velocity += Vector3.down * flightGravity * dt;
            Vector3 dir = velocity.normalized;
            transform.rotation = Quaternion.LookRotation(dir, transform.up);
            Vector3 tip = TipPosition;
            float dist = velocity.magnitude * dt;
            Transform ignore = rig != null ? rig.IgnoreRoot : null;
            if (CastIgnoring(tip, dir, dist, 0.06f, ignore, out Vector3 point, out Vector3 normal, out Collider col))
            {
                transform.position += point - tip;
                Impact(point, normal, col, ignore);
                return;
            }
            transform.position += velocity * dt;
            bool tooFar = rig != null && rig.Head != null && Vector3.Distance(transform.position, rig.Head.position) > 35f;
            if (flightT > returnAfter || tooFar) StartReturn();
        }
        void Impact(Vector3 point, Vector3 normal, Collider col, Transform ignore)
        {
            mode = Mode.Stuck;
            stuckT = 0f;
            stuckDuration = 0.35f;
            if (normal.y > 0.6f) WaterWhirlpool.Create(point, normal, ignore);
            SpellTarget.HitCollider(col);
            SpellTarget.HitArea(point, 0.5f);
            SpawnSplash(point, 1.3f, 0.9f);
            var rb = col != null ? col.attachedRigidbody : null;
            if (rb != null && !rb.isKinematic && !(ignore != null && rb.transform.IsChildOf(ignore)))
                rb.AddForceAtPosition(velocity.normalized * 6f, point, ForceMode.VelocityChange);
            PushBodies(point, 2f, 5f, ignore);
            StopTrails();
            if (thrower != null && MagicRig.I != null) MagicRig.I.Haptic(thrower.left, 0.5f, 0.1f);
        }
        void StartReturn()
        {
            mode = Mode.Returning;
            curReturnSpeed = 2f;
            StartTrails();
            PlayAt(ChimeClip, HeadPosition, 0.5f, 1.3f);
            PlayAt(WhooshClip, HeadPosition, 0.5f, 1.1f);
            MagicCircle.Flash(HeadPosition, Quaternion.LookRotation(transform.forward), 0.8f, MagicCircle.Element.Water, 0.25f, 4f);
        }
        void ReturnToHand(float dt, MagicRig rig)
        {
            if (rig == null || !rig.Ready) { Release(); return; }
            var hand = thrower ?? rig.R;

            foreach (var h in new[] { rig.L, rig.R })
                if (h.grip && DistanceToShaft(h.Position) < 0.35f) { Catch(rig, h); return; }
            Vector3 gripPoint = transform.TransformPoint(0f, 0f, 0.6f);
            Vector3 to = hand.Position - gripPoint;
            float dist = to.magnitude;
            curReturnSpeed = Mathf.MoveTowards(curReturnSpeed, returnSpeed, dt * 30f);
            float step = Mathf.Min(curReturnSpeed * dt, dist);
            transform.position += to / Mathf.Max(dist, 0.0001f) * step;

            Quaternion far = Quaternion.LookRotation(dist > 0.01f ? -to / dist : transform.forward, Vector3.up);
            Quaternion near = Quaternion.LookRotation(hand.AimForward, hand.t.up);
            Quaternion target = Quaternion.Slerp(near, far, Mathf.Clamp01((dist - 0.3f) / 1.5f));
            transform.rotation = Quaternion.Slerp(transform.rotation, target, 1f - Mathf.Exp(-dt * 10f));
            if (dist < 0.3f)
            {
                if (hand.grip) Catch(rig, hand);
                else
                {
                    StopTrails();
                    Release();
                }
            }
        }

        void Catch(MagicRig rig, MagicRig.Hand h)
        {
            StopTrails();
            mode = Mode.Held;
            holderHand = h;
            holder = h.t;
            h.gripConsumed = true;
            Quaternion R = Quaternion.LookRotation(h.AimForward, h.t.up);
            Vector3 origin = h.Position - R * new Vector3(0f, 0f, 0.6f);
            holdPos = holder.InverseTransformPoint(origin);
            holdRot = Quaternion.Inverse(holder.rotation) * R;
            transform.SetPositionAndRotation(origin, R);
            lastButt = LowEnd;
            strikeArmed = false;
            rig.Haptic(h.left, 0.9f, 0.15f);
            SpawnSplash(HeadPosition, 0.5f, 0.5f);
            PlayAt(ChimeClip, HeadPosition, 0.5f, 1.6f);
            if (idleCircle != null) { idleCircle.Release(true); idleCircle = null; }
        }
        float DistanceToShaft(Vector3 p)
        {
            Vector3 a = transform.TransformPoint(0f, 0f, -0.1f), b = TipPosition;
            Vector3 ab = b - a;
            float k = Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector3.Distance(p, a + ab * k);
        }
        void Grab(MagicRig rig, MagicRig.Hand h)
        {
            lastButt = LowEnd;
            strikeArmed = false;
            mode = Mode.Held;
            holderHand = h;
            holder = h.t;
            h.gripConsumed = true;
            holdPos = holder.InverseTransformPoint(transform.position);
            holdRot = Quaternion.Inverse(holder.rotation) * transform.rotation;
            if (idleCircle != null) idleCircle.Release(true);
            idleCircle = null;
            hoverL = hoverR = false;
            rig.Haptic(h.left, 0.7f, 0.12f);
            SpawnSplash(HeadPosition, 0.4f, 0.4f);
            PlayAt(ChimeClip, HeadPosition, 0.4f, 1.1f);
            MagicCircle.Flash(HeadPosition, Quaternion.LookRotation(transform.forward), 0.5f, MagicCircle.Element.Water, 0.3f, 3f);
        }

        void CheckStrike(float dt, MagicRig rig)
        {
            if (dt <= 0f || waterSpells) return;
            Vector3 tip = LowEnd;
            Vector3 vel = (tip - lastButt) / dt;
            lastButt = tip;
            float floorY = rig.IgnoreRoot != null ? rig.IgnoreRoot.position.y : tip.y - 1f;
            FindGround(tip, rig.IgnoreRoot, floorY, out Vector3 g, out Vector3 n);
            float h = tip.y - g.y;
            if (!strikeArmed && h > strikeHeight + 0.1f) strikeArmed = true;
            if (h >= strikeHeight) return;
            if (strikeArmed && Time.time >= nextStrike && -vel.y >= minStrikeSpeed)
            {
                float power = Mathf.InverseLerp(minStrikeSpeed, fullStrikeSpeed, -vel.y);
                nextStrike = Time.time + strikeCooldown;
                WaterField.Create(new Vector3(tip.x, g.y, tip.z), n, power, rig.IgnoreRoot, IsTwoHanded ? 1.8f : 1f);
                rig.Haptic(holderHand.left, Mathf.Lerp(0.6f, 1f, power), 0.15f);
                if (secondHand != null) rig.Haptic(secondHand.left, 1f, 0.2f);
            }
            strikeArmed = false;
        }
        void Release()
        {
            ClearSecondHand();
            mode = Mode.Floating;
            holder = null;
            holderHand = null;
            basePos = transform.position;
            t = 0f;
            UpdateFloor();
            if (idleCircle == null) idleCircle = MagicCircle.Create(MagicCircle.Element.Water);
        }
        void UpdateFloor()
        {
            var rig = MagicRig.I;
            Transform ignore = rig != null ? rig.IgnoreRoot : null;
            float fallback = ignore != null ? ignore.position.y : basePos.y - 0.35f;
            FindGround(basePos, ignore, fallback, out floorPoint, out _);
        }

        public void SetStormCharge(float c) => charge = Mathf.Clamp01(c);
        void UpdateCharge(float dt)
        {
            chargeSparks.transform.position = TipPosition;
            SetRate(chargeSparks, charge * 140f);
            regen -= dt;
            if (regen > 0f) return;
            regen = Random.Range(0.03f, 0.06f);
            Vector3 tip = TipPosition;
            for (int i = 0; i < chargeArcs.Length; i++)
            {
                if (charge <= 0.01f || Random.value > 0.3f + 0.7f * charge) { chargeArcs[i].positionCount = 0; continue; }
                Vector3 to = tip + Random.onUnitSphere * Mathf.Lerp(0.15f, 0.45f, charge);
                Jagged(buf, 10, tip, to, 0.04f);
                SetLine(chargeArcs[i], buf, 10);
            }
        }
    }
}

namespace MagicTraining
{
    using System.Collections.Generic;
    using UnityEngine;
    using static MagicTraining.MagicFX;
    using MinMaxCurve = UnityEngine.ParticleSystem.MinMaxCurve;

    public class Tsunami : MonoBehaviour
    {
        public float speed = 6f;

        static readonly Vector2[] Swell =
        {
            new Vector2(-3f, 0f), new Vector2(-2.2f, 0.12f), new Vector2(-1.5f, 0.3f), new Vector2(-0.95f, 0.55f),
            new Vector2(-0.5f, 0.8f), new Vector2(-0.2f, 0.95f), new Vector2(0.05f, 1f), new Vector2(0.25f, 0.94f),
            new Vector2(0.4f, 0.8f), new Vector2(0.5f, 0.62f), new Vector2(0.56f, 0.46f), new Vector2(0.6f, 0.32f),
            new Vector2(0.64f, 0.2f), new Vector2(0.68f, 0.1f), new Vector2(0.72f, 0.04f), new Vector2(0.78f, 0f)
        };
        static readonly Vector2[] Breaking =
        {
            new Vector2(-3f, 0f), new Vector2(-2.2f, 0.14f), new Vector2(-1.5f, 0.34f), new Vector2(-0.95f, 0.6f),
            new Vector2(-0.5f, 0.86f), new Vector2(-0.15f, 1f), new Vector2(0.15f, 1.06f), new Vector2(0.42f, 1f),
            new Vector2(0.62f, 0.85f), new Vector2(0.7f, 0.66f), new Vector2(0.6f, 0.52f), new Vector2(0.42f, 0.45f),
            new Vector2(0.3f, 0.33f), new Vector2(0.32f, 0.18f), new Vector2(0.45f, 0.06f), new Vector2(0.62f, 0f)
        };
        const int NP = 16;
        const int NT = 10;

        bool ring, arcMode;
        float arcCenter, arcHalf, crestAcc, baseAcc;
        float width, height, travel, traveled, t, groundY, radius, radius0, radiusMax, fadeT = -1f;
        Vector3 dir;
        Transform ignoreRoot;

        int nPath;
        float[] arc, taper;
        Vector3[] pathPos, pathOut;

        Mesh waterMesh, deepMesh, foamMesh;
        Vector3[] wv, dv, fv;
        Vector2[] wuv, duv, fuv;
        Renderer waterR, deepR, foamR;
        ParticleSystem crestMist, baseMist;
        AudioSource rush;
        bool crashed;
        readonly HashSet<Rigidbody> frameSet = new HashSet<Rigidbody>();

        static Material tsunamiMat;
        static Material TsunamiMat
        {
            get
            {
                if (tsunamiMat != null) return tsunamiMat;
                tsunamiMat = new Material(WaterMat) { name = "MagicFX_Tsunami", hideFlags = HideFlags.DontSave, renderQueue = 3005 };
                tsunamiMat.SetColor("_Color", new Color(0.12f, 0.6f, 1f, 0.92f));
                tsunamiMat.SetColor("_DeepColor", new Color(0.01f, 0.16f, 0.55f, 0.98f));
                tsunamiMat.SetFloat("_FoamStart", 0.9f);
                tsunamiMat.SetFloat("_ScrollSpeed", 2f);
                tsunamiMat.SetFloat("_WaveAmp", 0.04f);
                return tsunamiMat;
            }
        }

        public static Tsunami CreateLine(Vector3 ground, Vector3 dir, float width, float height, float travel, Transform ignoreRoot)
        {
            dir.y = 0f;
            dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.forward;
            var go = new GameObject("Tsunami Wave");
            go.transform.SetPositionAndRotation(ground, Quaternion.LookRotation(dir));
            var w = go.AddComponent<Tsunami>();
            w.ring = false;
            w.dir = dir;
            w.width = width;
            w.height = height;
            w.travel = travel;
            w.ignoreRoot = ignoreRoot;
            w.groundY = ground.y;
            w.Build();
            return w;
        }

        public static Tsunami CreateRing(Vector3 center, float height, float startRadius, float maxRadius, Transform ignoreRoot)
        {
            return CreateRingInternal(center, height, startRadius, maxRadius, ignoreRoot, false, Vector3.forward, 180f);
        }

        public static Tsunami CreateArc(Vector3 center, Vector3 dir, float halfAngle, float height, float startRadius, float maxRadius, Transform ignoreRoot)
        {
            var w = CreateRingInternal(center, height, startRadius, maxRadius, ignoreRoot, true, dir, halfAngle);
            return w;
        }

        static Tsunami CreateRingInternal(Vector3 center, float height, float startRadius, float maxRadius, Transform ignoreRoot,
            bool arc, Vector3 dir, float halfAngle)
        {
            var go = new GameObject(arc ? "Tsunami Arc" : "Tsunami Ring");
            go.transform.position = center;
            var w = go.AddComponent<Tsunami>();
            w.ring = true;
            w.arcMode = arc;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;
            dir.Normalize();
            w.dir = dir;
            w.arcCenter = Mathf.Atan2(dir.z, dir.x);
            w.arcHalf = Mathf.Clamp(halfAngle, 5f, 179f) * Mathf.Deg2Rad;
            w.height = height;
            w.radius = w.radius0 = startRadius;
            w.radiusMax = maxRadius;
            w.travel = maxRadius - startRadius;
            w.ignoreRoot = ignoreRoot;
            w.groundY = center.y;
            w.speed = 5f;
            w.Build();
            return w;
        }

        void Build()
        {
            nPath = ring ? 97 : 41;
            arc = new float[nPath];
            taper = new float[nPath];
            pathPos = new Vector3[nPath];
            pathOut = new Vector3[nPath];

            waterMesh = NewSweep("Water", NP, TsunamiMat, out wv, out wuv, out waterR);
            deepMesh = NewSweep("Water Depth", NP, DeepWaterMat, out dv, out duv, out deepR);
            foamMesh = NewSweep("White Water", NT + 1, WhiteWaterMat, out fv, out fuv, out foamR);

            crestMist = NewMist("Crest Spray", new Color(0.9f, 0.96f, 1f, 0.42f), 0.6f, 1.3f, 1f, 1.8f, 0.5f, 1.5f);
            baseMist = NewMist("White Water Spray", new Color(0.95f, 0.98f, 1f, 0.55f), 0.4f, 0.9f, 0.8f, 1.4f, 0.8f, 2.2f);

            rush = gameObject.AddComponent<AudioSource>();
            rush.clip = WaterRushClip;
            rush.loop = true;
            rush.spatialBlend = 1f;
            rush.minDistance = 2f;
            rush.maxDistance = 40f;
            rush.dopplerLevel = 0f;
            rush.volume = 0.9f;
            rush.pitch = 0.6f;
            rush.Play();
            PlayAt(SplashClip, transform.position, 1f, 0.6f);

            UpdateWave(0f);
        }

        Mesh NewSweep(string name, int nProf, Material mat, out Vector3[] verts, out Vector2[] uvs, out Renderer r)
        {
            verts = new Vector3[nPath * nProf];
            uvs = new Vector2[verts.Length];
            var tris = new int[(nPath - 1) * (nProf - 1) * 6];
            int ti = 0;
            for (int i = 0; i < nPath - 1; i++)
            for (int k = 0; k < nProf - 1; k++)
            {
                int a = i * nProf + k, b = a + 1, c = a + nProf, d = c + 1;
                tris[ti++] = a; tris[ti++] = b; tris[ti++] = d;
                tris[ti++] = a; tris[ti++] = d; tris[ti++] = c;
            }
            var mesh = new Mesh { name = "Tsunami " + name, hideFlags = HideFlags.DontSave };
            mesh.MarkDynamic();
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris;

            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return mesh;
        }

        ParticleSystem NewMist(string name, Color color, float sizeMin, float sizeMax, float lifeMin, float lifeMax, float speedMin, float speedMax)
        {
            var ps = NewPS(name, transform, SmokeMat, true, 400);
            var m = ps.main;
            m.startLifetime = new MinMaxCurve(lifeMin, lifeMax);
            m.startSize = new MinMaxCurve(sizeMin * height, sizeMax * height);
            m.startSpeed = new MinMaxCurve(speedMin, speedMax);
            m.startRotation = new MinMaxCurve(0f, Mathf.PI * 2f);
            m.startColor = color;
            m.gravityModifier = -0.03f;
            var sh = ps.shape; sh.enabled = false;
            var lv = ps.limitVelocityOverLifetime; lv.enabled = true; lv.limit = 100f; lv.drag = 1.2f;
            var col = ps.colorOverLifetime; col.enabled = true; col.color = FadeInOut;
            var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new MinMaxCurve(1f, Curve((0f, 0.5f), (1f, 1.7f)));
            var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new MinMaxCurve(-0.6f, 0.6f);
            var r = ps.GetComponent<ParticleSystemRenderer>(); r.maxParticleSize = 3f;
            ps.Play();
            return ps;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            t += dt;

            if (fadeT < 0f)
            {
                traveled += speed * dt;
                if (ring) radius = radius0 + traveled;
                else
                {
                    Vector3 p = transform.position + dir * (speed * dt);
                    if (FindGround(p, ignoreRoot, groundY, out Vector3 g, out _))
                        groundY = Mathf.Lerp(groundY, g.y, 1f - Mathf.Exp(-dt * 10f));
                    p.y = groundY;
                    transform.position = p;
                }
                if (traveled >= travel) { fadeT = 0f; Crash(); }
            }
            else
            {
                fadeT += dt;
                traveled += speed * 0.35f * dt;
                if (ring) radius = radius0 + traveled;
                else transform.position += dir * (speed * 0.35f * dt);
                if (fadeT > 1.2f) { Finish(); return; }
            }

            UpdateWave(dt);
        }

        void UpdateWave(float dt)
        {
            float k = Mathf.Clamp01(traveled / travel);
            float rise = Mathf.SmoothStep(0f, 1f, t / 0.45f);
            float globalCurl = Mathf.InverseLerp(0.1f, 0.55f, k);
            float col = Mathf.InverseLerp(0.75f, 1f, k);
            float fade = fadeT < 0f ? 1f : 1f - Mathf.Clamp01(fadeT / 1.2f);
            float H = ring ? height * Mathf.Lerp(1f, arcMode ? 0.65f : 0.45f, k) : height;

            for (int i = 0; i < nPath; i++)
            {
                if (ring)
                {
                    float u = i / (nPath - 1f);
                    float a = arcMode ? arcCenter - arcHalf + u * 2f * arcHalf : u * Mathf.PI * 2f;
                    var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    pathPos[i] = d * radius;
                    pathOut[i] = d;
                    taper[i] = arcMode ? Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * u)), 0.35f) : 1f;
                    arc[i] = a * radius;
                }
                else
                {
                    float u = i / (nPath - 1f);
                    float x = (u - 0.5f) * width;
                    pathPos[i] = new Vector3(x, 0f, 0f);
                    pathOut[i] = Vector3.forward;
                    taper[i] = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * u)), 0.4f);
                    arc[i] = x;
                }
            }

            float tailLimit = ring ? radius - 0.15f : traveled + 0.4f;

            for (int i = 0; i < nPath; i++)
            {
                float s = arc[i];
                float hv = 1f + 0.1f * Mathf.Sin(s * 1.7f + t * 1.1f) + 0.06f * Mathf.Sin(s * 4.1f - t * 2.3f);
                float c = Mathf.Clamp01(globalCurl + 0.25f * Mathf.Sin(s * 1.3f + t * 2.2f) + col);
                float h = H * rise * hv * taper[i] * (1f - 0.75f * col);
                float zs = H * Mathf.Lerp(0.5f, 1f, taper[i]);
                float surge = col * 0.9f * H;

                for (int p = 0; p < NP; p++)
                {
                    Vector2 q = Vector2.Lerp(Swell[p], Breaking[p], c);
                    float z = q.x * zs;
                    if (p >= 8) z += surge * (p - 7) / 8f;
                    z = Mathf.Max(z, -tailLimit);
                    float y = q.y * h;
                    wv[i * NP + p] = pathPos[i] + pathOut[i] * z + Vector3.up * y;
                    wuv[i * NP + p] = new Vector2(s / 3f, q.y / 1.06f);
                    dv[i * NP + p] = pathPos[i] + pathOut[i] * (z - 0.05f * H) + Vector3.up * (y * 0.88f);
                    duv[i * NP + p] = wuv[i * NP + p];
                }

                float zc = Mathf.Lerp(0.6f, 0.5f, c) * zs + surge * 0.5f;
                float yc = Mathf.Lerp(0.12f, 0.22f, c) * h;
                float rr = ((0.1f + 0.14f * c) * h + 0.05f * rise) * (1f + 0.15f * Mathf.Sin(s * 3f + t * 7f));
                for (int j = 0; j <= NT; j++)
                {
                    float ang = j / (float)NT * Mathf.PI * 2f;
                    float z = zc + Mathf.Cos(ang) * rr * 1.3f;
                    float y = Mathf.Max(0f, yc + Mathf.Sin(ang) * rr);
                    fv[i * (NT + 1) + j] = pathPos[i] + pathOut[i] * z + Vector3.up * y;
                    fuv[i * (NT + 1) + j] = new Vector2(s / 2f, 0.55f + 0.45f * Mathf.Sin(ang - t * 6f));
                }
            }

            Apply(waterMesh, wv, wuv);
            Apply(deepMesh, dv, duv);
            Apply(foamMesh, fv, fuv);
            SetWater(waterR, fade);
            SetWater(deepR, fade);
            SetWater(foamR, fade * Mathf.Lerp(0.5f, 1f, c01(globalCurl + col)));

            float crestH = H * rise * (1f - 0.75f * col);
            float span = Span;
            float on = fadeT < 0f ? 1f : 0f;
            EmitAlong(crestMist, ref crestAcc, span * 1.6f * rise * globalCurl * on * dt, 0.2f * H, crestH * 0.95f);
            EmitAlong(baseMist, ref baseAcc, span * 2.2f * rise * Mathf.Max(globalCurl, 0.2f) * on * dt, 0.6f * H, 0.15f * H);
            rush.volume = 0.9f * fade;

            if (fadeT < 0f) Push(H * rise);
        }

        static float c01(float x) => Mathf.Clamp01(x);

        float Span => ring ? (arcMode ? 2f * arcHalf : 2f * Mathf.PI) * radius : width;

        void EmitAlong(ParticleSystem ps, ref float acc, float count, float forward, float y)
        {
            acc += count;
            var ep = new ParticleSystem.EmitParams();
            int guard = 0;
            while (acc >= 1f && guard++ < 60)
            {
                acc -= 1f;
                int i = Random.Range(0, nPath);
                if (Random.value > taper[i]) continue;
                Vector3 local = pathPos[i] + pathOut[i] * (forward + Random.Range(-0.15f, 0.15f) * height) + Vector3.up * y;
                Vector3 vel = pathOut[i] * Random.Range(0.6f, 1.8f) + Vector3.up * Random.Range(0.3f, 1.2f);
                ep.position = transform.TransformPoint(local);
                ep.velocity = transform.TransformDirection(vel);
                ps.Emit(ep, 1);
            }
        }

        static void Apply(Mesh m, Vector3[] v, Vector2[] uv)
        {
            m.vertices = v;
            m.uv = uv;
            m.RecalculateNormals();
            m.RecalculateBounds();
        }

        void Push(float h)
        {
            frameSet.Clear();
            Collider[] hits;
            if (ring)
                hits = Physics.OverlapSphere(transform.position, radius + height, ~0, QueryTriggerInteraction.Ignore);
            else
            {
                Vector3 center = transform.position + Vector3.up * (h * 0.5f) + dir * (0.2f * height);
                Vector3 half = new Vector3(width * 0.5f, Mathf.Max(0.3f, h * 0.5f), 0.9f * height);
                hits = Physics.OverlapBox(center, half, transform.rotation, ~0, QueryTriggerInteraction.Ignore);
            }

            foreach (var c in hits)
            {
                Vector3 pushDir = dir;
                if (ring)
                {
                    Vector3 off = c.bounds.center - transform.position;
                    off.y = 0f;
                    float d = off.magnitude;
                    if (Mathf.Abs(d - radius) > 0.9f * height) continue;
                    pushDir = d > 0.01f ? off / d : Vector3.forward;
                    if (arcMode && Vector3.Angle(pushDir, dir) > arcHalf * Mathf.Rad2Deg + 5f) continue;
                }
                SpellTarget.HitCollider(c);
                var rb = c.attachedRigidbody;
                if (rb == null || rb.isKinematic || !frameSet.Add(rb)) continue;
                if (ignoreRoot != null && rb.transform.IsChildOf(ignoreRoot)) continue;
                Vector3 target = pushDir * (speed * 1.2f) + Vector3.up * 2.5f;
                rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, target, 0.25f);
                rb.AddTorque(Random.insideUnitSphere * 0.5f, ForceMode.VelocityChange);
            }
        }

        void Crash()
        {
            if (crashed) return;
            crashed = true;
            float span = Span;
            float h = height * (ring ? (arcMode ? 0.65f : 0.45f) : 1f);
            EmitAlong(baseMist, ref baseAcc, Mathf.Clamp(span * 3f, 10f, 120f), 0.7f * h, 0.2f * h);
            EmitAlong(crestMist, ref crestAcc, Mathf.Clamp(span * 2f, 8f, 80f), 0.4f * h, 0.5f * h);
            PlayAt(SplashClip, transform.position + Vector3.up, 1f, 0.45f);
            PlayAt(BoomClip, transform.position, 0.25f, 0.4f);
        }

        void Finish()
        {
            DetachPS(crestMist, 2f);
            DetachPS(baseMist, 1.6f);
            Destroy(gameObject);
        }
    }
}

namespace MagicTraining
{
    using System.Collections.Generic;
    using UnityEngine;
    using static MagicTraining.MagicFX;
    using MinMaxCurve = UnityEngine.ParticleSystem.MinMaxCurve;

    public class WaterField : MonoBehaviour
    {
        public static int maxFields = 6;
        static readonly List<WaterField> All = new List<WaterField>();

        public float duration = 10f;

        float R, growTime, t, rippleT;
        Transform disc;
        Renderer discR, deepR;
        ParticleSystem sparkles;
        AudioSource lap;
        bool receding;

        static Mesh fieldMesh;
        static Material fieldMat;

        static Mesh FieldMesh => fieldMesh != null ? fieldMesh : (fieldMesh = LatheMesh("WaterField",
            new[] { new Vector2(0f, 0.012f), new Vector2(0.3f, 0.012f), new Vector2(0.6f, 0.011f), new Vector2(0.82f, 0.01f), new Vector2(0.94f, 0.008f), new Vector2(1f, 0.004f) },
            new[] { 0.55f, 0.55f, 0.6f, 0.75f, 0.97f, 0f }));

        static Material FieldMat
        {
            get
            {
                if (fieldMat != null) return fieldMat;
                fieldMat = new Material(WaterMat) { name = "MagicFX_WaterField", hideFlags = HideFlags.DontSave, renderQueue = 3002 };
                fieldMat.SetColor("_Color", new Color(0.15f, 0.6f, 1f, 0.8f));
                fieldMat.SetColor("_DeepColor", new Color(0.02f, 0.2f, 0.6f, 0.85f));
                fieldMat.SetFloat("_FoamStart", 0.88f);
                fieldMat.SetFloat("_ScrollSpeed", 1.2f);
                fieldMat.SetFloat("_WaveAmp", 0.008f);
                return fieldMat;
            }
        }

        public static WaterField Create(Vector3 center, Vector3 normal, float power, Transform ignoreRoot, float sizeMul = 1f)
        {
            power = Mathf.Clamp01(power);
            sizeMul = Mathf.Max(1f, sizeMul);
            while (All.Count >= maxFields) All[0].Recede();
            var go = new GameObject("Water Field");
            go.transform.position = center + Vector3.up * 0.01f;
            var f = go.AddComponent<WaterField>();
            f.R = Mathf.Lerp(2f, 3.6f, power) * sizeMul;
            f.growTime = Mathf.Lerp(0.8f, 1.3f, power) * Mathf.Lerp(1f, sizeMul, 0.5f);
            f.Build();
            All.Add(f);

            if (normal.sqrMagnitude < 0.01f) normal = Vector3.up;
            SpawnSplash(center + normal * 0.05f, Mathf.Lerp(0.8f, 1.6f, power) * Mathf.Lerp(1f, sizeMul, 0.5f), 1f);
            MagicCircle.Flash(center + normal * 0.04f, Quaternion.LookRotation(normal), Mathf.Lerp(1.2f, 2f, power) * sizeMul, MagicCircle.Element.Water, 0.7f, 1.5f);
            var root = new GameObject("Water Field Rings").transform;
            root.SetPositionAndRotation(center + normal * 0.03f, Quaternion.LookRotation(normal));
            var fx = root.gameObject.AddComponent<FxAutoDestroy>();
            fx.life = f.growTime + 0.5f;
            SpawnFieldRings(root, f.R, f.growTime);
            PlayAt(WoodThumpClip, center, 0.6f, 0.7f);
            return f;
        }

        void Build()
        {
            discR = NewWaterMesh("Water", transform, FieldMesh, FieldMat);
            disc = discR.transform;
            deepR = NewWaterMesh("Depth", disc, FieldMesh, DeepWaterMat);
            deepR.transform.localPosition = Vector3.down * 0.004f;
            deepR.transform.localScale = new Vector3(0.9f, 1f, 0.9f);
            disc.localScale = new Vector3(0.01f, 1f, 0.01f);

            sparkles = NewPS("Water Sparkles", transform, SparkMat, true, 300);
            {
                var m = sparkles.main;
                m.startLifetime = new MinMaxCurve(0.8f, 1.6f);
                m.startSize = new MinMaxCurve(0.01f, 0.025f);
                m.startColor = RandomFrom(WaterPalette);
                var sh = sparkles.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Circle;
                sh.radius = R * 0.9f; sh.radiusThickness = 1f; sh.rotation = new Vector3(90f, 0f, 0f);
                var vel = sparkles.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
                vel.x = new MinMaxCurve(0f, 0f); vel.y = new MinMaxCurve(0.05f, 0.2f); vel.z = new MinMaxCurve(0f, 0f);
                var col = sparkles.colorOverLifetime; col.enabled = true; col.color = FadeInOut;
                SetRate(sparkles, R * R * 3f);
            }
            sparkles.Play();

            lap = gameObject.AddComponent<AudioSource>();
            lap.clip = WaterRushClip;
            lap.loop = true;
            lap.spatialBlend = 1f;
            lap.minDistance = 1.5f;
            lap.maxDistance = 25f;
            lap.dopplerLevel = 0f;
            lap.volume = 0.5f;
            lap.pitch = 0.75f;
            lap.Play();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            t += dt;

            float k = Mathf.Clamp01(t / growTime);
            float r = R * (1f - (1f - k) * (1f - k) * (1f - k));
            float fade = 1f;
            if (!receding && t > duration) Recede();
            if (receding)
            {
                float rk = Mathf.Clamp01((t - duration) / 1.5f);
                r *= 1f - 0.6f * rk * rk;
                fade = 1f - rk;
                if (rk >= 1f) { Destroy(gameObject); return; }
            }
            float breathe = 1f + 0.015f * Mathf.Sin(t * 2.3f);
            disc.localScale = new Vector3(Mathf.Max(0.01f, r * breathe), 1f, Mathf.Max(0.01f, r * breathe));
            SetWater(discR, fade);
            SetWater(deepR, fade);
            lap.volume = Mathf.Lerp(0.5f, 0.2f, Mathf.Clamp01((t - growTime) / 2f)) * fade;

            rippleT -= dt;
            if (!receding && r > 0.5f && rippleT <= 0f)
            {
                rippleT = Mathf.Lerp(0.35f, 0.12f, Mathf.InverseLerp(2f, 6f, R));
                Vector2 p = Random.insideUnitCircle * (r * 0.85f);
                SpawnRipple(transform.position + new Vector3(p.x, 0.01f, p.y), Random.Range(0.4f, 0.9f));
            }
        }

        public void Recede()
        {
            All.Remove(this);
            if (receding) return;
            receding = true;
            if (t < duration) duration = t;
            sparkles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        void OnDestroy() => All.Remove(this);
    }
}

namespace MagicTraining
{
    using UnityEngine;
    using static MagicTraining.MagicFX;
    using MinMaxCurve = UnityEngine.ParticleSystem.MinMaxCurve;

    public class WaterSpear : MonoBehaviour
    {
        public float formTime = 0.18f;
        public float speed = 22f;
        public float gravity = 1f;
        public float maxLifetime = 4f;

        static Mesh spearMesh;

        Transform ignoreRoot;
        Transform model;
        Renderer outerR, coreR;
        ParticleSystem spiral, drips;
        TrailRenderer trail, trailCore;
        Light glow;
        float length, radius, t, life;
        bool flying, dead;
        Vector3 velocity;

        static Mesh SpearMesh => spearMesh != null ? spearMesh : (spearMesh = LatheMesh("WaterSpear",
            new[] { new Vector2(0f, 0f), new Vector2(0.35f, 0.08f), new Vector2(0.62f, 0.22f), new Vector2(0.7f, 0.38f),
                    new Vector2(0.55f, 0.58f), new Vector2(0.42f, 0.72f), new Vector2(0.95f, 0.76f), new Vector2(0.6f, 0.86f), new Vector2(0f, 1f) },
            new[] { 0f, 0.15f, 0.3f, 0.45f, 0.6f, 0.72f, 0.8f, 0.9f, 1f }));

        public static WaterSpear Create(Vector3 from, Vector3 to, Transform ignoreRoot, float power = 1f)
        {
            Vector3 d = to - from;
            power = Mathf.Max(1f, power);
            float len = Mathf.Clamp(d.magnitude * power, 0.3f, 3.5f * power);
            Vector3 dir = d.sqrMagnitude > 0.0001f ? d.normalized : Vector3.forward;
            var go = new GameObject("Water Spear");
            Vector3 up = Mathf.Abs(Vector3.Dot(dir, Vector3.up)) > 0.95f ? Vector3.forward : Vector3.up;
            go.transform.SetPositionAndRotation(from, Quaternion.LookRotation(dir, up));
            var s = go.AddComponent<WaterSpear>();
            s.ignoreRoot = ignoreRoot;
            s.length = len;
            s.radius = Mathf.Clamp(len * 0.07f, 0.03f, 0.22f * power) * Mathf.Lerp(1f, power, 0.5f);
            s.speed *= Mathf.Lerp(1f, power, 0.5f);
            s.Build();
            return s;
        }

        void Build()
        {
            Transform tr = transform;
            model = new GameObject("Model").transform;
            model.SetParent(tr, false);
            model.localRotation = Quaternion.Euler(90f, 0f, 0f);

            outerR = NewWaterMesh("Water", model, SpearMesh, GeyserMat);
            coreR = NewWaterMesh("Core", model, SpearMesh, DeepWaterMat);
            coreR.transform.localScale = new Vector3(0.6f, 0.92f, 0.6f);
            coreR.transform.localPosition = new Vector3(0f, 0.03f, 0f);
            model.localScale = new Vector3(radius, 0.001f, radius);

            spiral = NewPS("Water Spiral", tr, DropMat, false, 200);
            {
                const float L = 0.5f;
                var m = spiral.main;
                m.startLifetime = L;
                m.startSize = new MinMaxCurve(radius * 0.35f, radius * 0.7f);
                m.startColor = RandomFrom(DropPalette);
                var sh = spiral.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = radius * 1.2f; sh.radiusThickness = 0f;
                var vel = spiral.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.Local;
                vel.x = 0f; vel.y = 0f; vel.z = length * 0.9f / L;
                vel.orbitalX = 0f; vel.orbitalY = 0f; vel.orbitalZ = 12f;
                var col = spiral.colorOverLifetime; col.enabled = true; col.color = FadeInOut;
                SetRate(spiral, Mathf.Lerp(40f, 140f, length / 3.5f));
            }
            drips = NewPS("Drips", tr, DropMat, true, 300);
            {
                var m = drips.main;
                m.startLifetime = new MinMaxCurve(0.4f, 0.9f);
                m.startSpeed = new MinMaxCurve(0.2f, 1f);
                m.startSize = new MinMaxCurve(radius * 0.3f, radius * 0.6f);
                m.gravityModifier = 1f;
                m.startColor = RandomFrom(DropPalette);
                var sh = drips.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Box;
                sh.scale = new Vector3(radius * 1.5f, radius * 1.5f, length); sh.position = new Vector3(0f, 0f, length * 0.5f);
                var col = drips.colorOverLifetime; col.enabled = true; col.color = FadeOut;
                SetRate(drips, 15f);
            }

            glow = NewLight(tr, new Vector3(0f, 0f, length * 0.6f), WaterLight, 1.5f + length, 0f);

            float tw = radius * 2.2f;
            trail = NewTrail("Trail", tr, BeamMat, new Vector3(0f, 0f, length * 0.4f), 0.3f, tw, Curve((0f, 1f), (1f, 0f)),
                Grad((0f, A(WaterLight, 0.9f)), (0.4f, A(WaterTeal, 0.6f)), (1f, A(WaterDeep, 0f))));
            trailCore = NewTrail("Trail Core", tr, CoreMat, new Vector3(0f, 0f, length * 0.4f), 0.12f, tw * 0.3f, Curve((0f, 1f), (1f, 0f)),
                Grad((0f, WaterFoam), (1f, A(WaterLight, 0f))));

            spiral.Play();
            drips.Play();
            MagicCircle.Flash(transform.position, transform.rotation, radius * 8f + 0.2f, MagicCircle.Element.Water, 0.2f, 4f);
            PlayAt(SplashClip, transform.position, 0.4f, 1.5f);
        }

        void Update()
        {
            if (dead) return;
            float dt = Time.deltaTime;
            t += dt;

            float k = Mathf.Clamp01(t / formTime);
            float e = 1f - (1f - k) * (1f - k) * (1f - k);
            model.localScale = new Vector3(radius * (1f + 0.06f * Mathf.Sin(t * 30f)), Mathf.Max(0.001f, length * e), radius);
            glow.intensity = 1.5f * e;

            if (!flying)
            {
                if (k >= 1f) Launch();
                return;
            }

            life += dt;
            velocity += Vector3.down * gravity * dt;
            Vector3 dir = velocity.normalized;
            transform.rotation = Quaternion.LookRotation(dir, transform.up);
            Vector3 tip = transform.position + dir * length;
            float dist = velocity.magnitude * dt;
            if (CastIgnoring(tip, dir, dist, radius * 0.8f, ignoreRoot, out Vector3 point, out Vector3 normal, out Collider col))
            {
                transform.position = point - dir * length;
                Burst(point, normal, col);
                return;
            }
            transform.position += velocity * dt;
            if (life > maxLifetime) Burst(tip, -dir, null);
        }

        void Launch()
        {
            flying = true;
            velocity = transform.forward * speed;
            trail.Clear(); trail.emitting = true;
            trailCore.Clear(); trailCore.emitting = true;
            var em = drips.emission; em.rateOverDistance = 20f;
            PlayAt(WhooshClip, transform.position, 0.7f, Mathf.Lerp(1.4f, 0.8f, length / 3.5f));
        }

        void Burst(Vector3 point, Vector3 normal, Collider col)
        {
            dead = true;
            float size = Mathf.Lerp(0.5f, 1.8f, length / 3.5f);
            SpellTarget.HitCollider(col);
            SpellTarget.HitArea(point, 0.3f + radius);
            SpawnSplash(point, size, Mathf.Lerp(0.5f, 1f, size / 1.8f));
            var rb = col != null ? col.attachedRigidbody : null;
            if (rb != null && !rb.isKinematic && !(ignoreRoot != null && rb.transform.IsChildOf(ignoreRoot)))
                rb.AddForceAtPosition(transform.forward * (3f + length * 4f), point, ForceMode.VelocityChange);
            PushBodies(point, 0.8f + length * 0.6f, 1.5f + length * 2f, ignoreRoot);
            DetachPS(drips, 1f);
            DetachPS(spiral, 0.5f);
            DetachTrail(trail);
            DetachTrail(trailCore);
            Destroy(gameObject);
        }
    }
}

namespace MagicTraining
{
    using System.Collections.Generic;
    using UnityEngine;
    using static MagicTraining.MagicFX;
    using MinMaxCurve = UnityEngine.ParticleSystem.MinMaxCurve;

    public class WaterWhirlpool : MonoBehaviour
    {
        public float duration = 4f;
        public float radius = 2.2f;
        public float pullForce = 6f;

        float t;
        Transform ignoreRoot;
        Transform disc;
        Renderer discR, deepR;
        readonly LineRenderer[] arms = new LineRenderer[5];
        readonly LineRenderer[] armCores = new LineRenderer[5];
        readonly Vector3[] buf = new Vector3[32];
        ParticleSystem spout, rim;
        AudioSource rush;
        readonly HashSet<Rigidbody> seen = new HashSet<Rigidbody>();

        static Mesh poolMesh;
        static Mesh PoolMesh => poolMesh != null ? poolMesh : (poolMesh = LatheMesh("Whirlpool",
            new[] { new Vector2(0f, -0.02f), new Vector2(0.2f, 0f), new Vector2(0.5f, 0.012f), new Vector2(0.8f, 0.012f), new Vector2(0.93f, 0.01f), new Vector2(1f, 0.004f) },
            new[] { 0.2f, 0.45f, 0.6f, 0.7f, 0.97f, 0f }));

        public static WaterWhirlpool Create(Vector3 point, Vector3 normal, Transform ignoreRoot)
        {
            var go = new GameObject("Water Whirlpool");
            go.transform.position = point + Vector3.up * 0.01f;
            var w = go.AddComponent<WaterWhirlpool>();
            w.ignoreRoot = ignoreRoot;
            w.Build();
            if (normal.sqrMagnitude < 0.01f) normal = Vector3.up;
            MagicCircle.Flash(point + normal * 0.04f, Quaternion.LookRotation(normal), w.radius * 1.2f, MagicCircle.Element.Water, 0.6f, -4f);
            return w;
        }

        void Build()
        {
            discR = NewWaterMesh("Water", transform, PoolMesh, WaterMat);
            disc = discR.transform;
            deepR = NewWaterMesh("Depth", disc, PoolMesh, DeepWaterMat);
            deepR.transform.localPosition = Vector3.down * 0.006f;
            deepR.transform.localScale = new Vector3(0.85f, 1f, 0.85f);
            disc.localScale = new Vector3(0.01f, 1f, 0.01f);

            var armGrad = Grad((0f, A(WaterDeep, 0f)), (0.25f, A(WaterLight, 0.7f)), (0.8f, A(WaterFoam, 0.6f)), (1f, A(WaterFoam, 0f)));
            var coreGrad = Grad((0f, A(WaterFoam, 0f)), (0.4f, A(WaterFoam, 0.8f)), (1f, A(WaterFoam, 0f)));
            for (int i = 0; i < arms.Length; i++)
            {
                arms[i] = NewBoltLine("Spiral Arm", transform, BeamMat, 0.12f, armGrad, Curve((0f, 0.3f), (0.6f, 1f), (1f, 0.6f)));
                armCores[i] = NewBoltLine("Spiral Foam", transform, CoreMat, 0.025f, coreGrad, Curve((0f, 0.3f), (0.6f, 1f), (1f, 0.4f)));
            }

            spout = NewPS("Spout", transform, DropMat, false, 400);
            {
                const float L = 0.9f;
                var m = spout.main;
                m.startLifetime = L;
                m.startSize = new MinMaxCurve(0.03f, 0.07f);
                m.startColor = RandomFrom(DropPalette);
                var sh = spout.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Circle;
                sh.radius = 0.15f; sh.radiusThickness = 0f; sh.rotation = new Vector3(90f, 0f, 0f);
                var vel = spout.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.Local;
                vel.x = 0f; vel.y = 1.2f / L; vel.z = 0f;
                vel.orbitalX = 0f; vel.orbitalY = 9f; vel.orbitalZ = 0f;
                vel.radial = 0.5f / L;
                var col = spout.colorOverLifetime; col.enabled = true; col.color = FadeInOut;
                SetRate(spout, 140f);
            }
            rim = NewPS("Rim Foam", transform, SmokeMat, false, 200);
            {
                const float L = 1.4f;
                var m = rim.main;
                m.startLifetime = L;
                m.startSize = new MinMaxCurve(0.2f, 0.4f);
                m.startRotation = new MinMaxCurve(0f, Mathf.PI * 2f);
                m.startColor = new Color(0.92f, 0.97f, 1f, 0.4f);
                var sh = rim.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Circle;
                sh.radius = radius * 0.9f; sh.radiusThickness = 0f; sh.rotation = new Vector3(90f, 0f, 0f);
                var vel = rim.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.Local;
                vel.x = 0f; vel.y = 0.05f; vel.z = 0f;
                vel.orbitalX = 0f; vel.orbitalY = 2.5f; vel.orbitalZ = 0f;
                vel.radial = -radius * 0.4f / L;
                var col = rim.colorOverLifetime; col.enabled = true; col.color = FadeInOut;
                SetRate(rim, 45f);
            }
            spout.Play(); rim.Play();

            rush = gameObject.AddComponent<AudioSource>();
            rush.clip = WaterRushClip;
            rush.loop = true;
            rush.spatialBlend = 1f;
            rush.minDistance = 1.5f;
            rush.maxDistance = 30f;
            rush.dopplerLevel = 0f;
            rush.volume = 0.8f;
            rush.pitch = 0.9f;
            rush.Play();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            t += dt;
            float grow = Mathf.SmoothStep(0f, 1f, t / 0.5f);
            float fade = t > duration - 1f ? Mathf.Clamp01(duration - t) : 1f;
            if (t >= duration)
            {
                SpawnSplash(transform.position + Vector3.up * 0.1f, 1f, 0.6f);
                DetachPS(spout, 1f);
                DetachPS(rim, 1.5f);
                Destroy(gameObject);
                return;
            }

            float r = radius * grow * Mathf.Lerp(0.6f, 1f, fade);
            disc.localScale = new Vector3(Mathf.Max(0.01f, r), 1f, Mathf.Max(0.01f, r));
            disc.localRotation = Quaternion.Euler(0f, -t * 120f, 0f);
            SetWater(discR, fade);
            SetWater(deepR, fade);

            for (int i = 0; i < arms.Length; i++)
            {
                const int n = 28;
                float phase = -t * 4f + i * Mathf.PI * 2f / arms.Length;
                for (int k = 0; k < n; k++)
                {
                    float u = k / (n - 1f);
                    float rr = Mathf.Lerp(0.08f, r * 0.95f, u);
                    float a = phase + (1f - u) * 4.5f;
                    buf[k] = transform.position + new Vector3(Mathf.Cos(a) * rr, 0.02f + 0.01f * u, Mathf.Sin(a) * rr);
                }
                SetLine(arms[i], buf, n);
                SetLine(armCores[i], buf, n);
                arms[i].widthMultiplier = 0.12f * grow * fade;
                armCores[i].widthMultiplier = 0.025f * grow * fade;
            }
            SetRate(spout, 140f * fade);
            SetRate(rim, 45f * fade);
            rush.volume = 0.8f * fade;

            Pull(r, dt);
        }

        void Pull(float r, float dt)
        {
            seen.Clear();
            Vector3 c = transform.position;
            foreach (var col in Physics.OverlapSphere(c, r + 0.3f, ~0, QueryTriggerInteraction.Ignore))
            {
                var rb = col.attachedRigidbody;
                if (rb == null || rb.isKinematic || !seen.Add(rb)) continue;
                if (ignoreRoot != null && rb.transform.IsChildOf(ignoreRoot)) continue;
                Vector3 off = rb.worldCenterOfMass - c;
                off.y = 0f;
                float d = off.magnitude;
                if (d < 0.01f) continue;
                Vector3 inward = -off / d;
                Vector3 tang = Vector3.Cross(Vector3.up, inward);
                rb.AddForce((inward * 0.6f + tang) * pullForce * dt, ForceMode.VelocityChange);
                rb.AddTorque(Vector3.up * 4f * dt, ForceMode.VelocityChange);
            }
        }
    }
}

namespace MagicTraining
{
    using UnityEngine;
    using static MagicTraining.MagicFX;
    using MinMaxCurve = UnityEngine.ParticleSystem.MinMaxCurve;

    public class Geyser : MonoBehaviour
    {
        public float height = 2.6f;
        public float holdTime = 5f;
        public float riseTime = 0.35f;
        public float lowerTime = 0.6f;
        public float carryRadius = 0.75f;

        Transform column, inner, swirl, crown, pool, top, platform, rig, head;
        Renderer colR, innerR, swirlR, crownR, poolR;
        ParticleSystem fountain, topMist, baseFoam;
        AudioSource rush;
        Vector3 ground;
        float t;
        bool carrying = true, done;

        public static Geyser Create(Vector3 ground, Transform rigRoot, Transform head, float power = 1f)
        {
            var go = new GameObject("Geyser");
            go.transform.position = ground;
            var g = go.AddComponent<Geyser>();
            g.ground = ground;
            g.rig = rigRoot;
            g.head = head;
            power = Mathf.Max(1f, power);
            float w = Mathf.Lerp(1f, power, 0.8f);
            g.height *= power;
            g.carryRadius *= w;
            go.transform.localScale = new Vector3(w, 1f, w);
            g.Build();
            return g;
        }

        void Build()
        {
            Transform tr = transform;
            column = NewColumn("Water Column", tr, out colR);
            inner = NewColumn("Water Core", tr, out innerR);
            innerR.sharedMaterial = DeepWaterMat;
            swirl = NewColumn("Water Swirl", tr, out swirlR);
            swirlR.sharedMaterial = SwirlMat;
            crownR = NewWaterMesh("Water Crown", tr, WaterCrownMesh, GeyserMat);
            crown = crownR.transform;
            poolR = NewWaterMesh("Water Pool", tr, WaterPoolMesh, PoolMat);
            pool = poolR.transform;
            pool.localPosition = Vector3.up * 0.02f;

            top = new GameObject("Top").transform;
            top.SetParent(tr, false);
            top.localRotation = Quaternion.LookRotation(Vector3.up, Vector3.forward);

            platform = new GameObject("Platform").transform;
            platform.SetParent(tr, false);
            var box = platform.gameObject.AddComponent<BoxCollider>();
            box.size = new Vector3(1f, 0.3f, 1f);

            fountain = NewPS("Fountain", top, SparkMat, true, 600);
            {
                var m = fountain.main;
                m.startLifetime = new MinMaxCurve(0.8f, 1.4f);
                m.startSpeed = new MinMaxCurve(2.5f, 5.5f);
                m.startSize = new MinMaxCurve(0.02f, 0.05f);
                m.gravityModifier = 1f;
                m.startColor = RandomFrom(DropPalette);
                m.startSize = new MinMaxCurve(0.03f, 0.07f);
                var r = fountain.GetComponent<ParticleSystemRenderer>();
                r.sharedMaterial = DropMat;
                r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.02f; r.lengthScale = 1f;
                var sh = fountain.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 35f; sh.radius = 0.3f;
                var col = fountain.colorOverLifetime; col.enabled = true; col.color = FadeOut;
                SetRate(fountain, 160f);
            }
            topMist = NewPS("Top Mist", top, SmokeMat, true, 60);
            {
                var m = topMist.main;
                m.startLifetime = new MinMaxCurve(1f, 1.6f);
                m.startSpeed = new MinMaxCurve(1f, 2f);
                m.startSize = new MinMaxCurve(0.4f, 0.8f);
                m.startRotation = new MinMaxCurve(0f, Mathf.PI * 2f);
                m.startColor = new Color(0.92f, 0.97f, 1f, 0.35f);
                m.gravityModifier = 0.1f;
                var sh = topMist.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 45f; sh.radius = 0.3f;
                var col = topMist.colorOverLifetime; col.enabled = true; col.color = FadeInOut;
                var sz = topMist.sizeOverLifetime; sz.enabled = true; sz.size = new MinMaxCurve(1f, Curve((0f, 0.6f), (1f, 1.6f)));
                SetRate(topMist, 15f);
            }
            baseFoam = NewPS("Base Foam", tr, SmokeMat, true, 80);
            {
                var m = baseFoam.main;
                m.startLifetime = new MinMaxCurve(1f, 1.5f);
                m.startSpeed = new MinMaxCurve(0.5f, 1.2f);
                m.startSize = new MinMaxCurve(0.3f, 0.6f);
                m.startRotation = new MinMaxCurve(0f, Mathf.PI * 2f);
                m.startColor = new Color(0.9f, 0.97f, 1f, 0.4f);
                var sh = baseFoam.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Circle;
                sh.radius = 0.6f; sh.rotation = new Vector3(90f, 0f, 0f);
                var lv = baseFoam.limitVelocityOverLifetime; lv.enabled = true; lv.limit = 100f; lv.drag = 1.5f;
                var col = baseFoam.colorOverLifetime; col.enabled = true; col.color = FadeInOut;
                SetRate(baseFoam, 25f);
            }
            fountain.Play(); topMist.Play(); baseFoam.Play();

            rush = gameObject.AddComponent<AudioSource>();
            rush.clip = WaterRushClip;
            rush.loop = true;
            rush.spatialBlend = 1f;
            rush.minDistance = 1f;
            rush.maxDistance = 30f;
            rush.dopplerLevel = 0f;
            rush.volume = 0.8f;
            rush.pitch = 0.7f;
            rush.Play();

            MagicCircle.Flash(ground + Vector3.up * 0.03f, Quaternion.LookRotation(Vector3.up), 2.6f, MagicCircle.Element.Water, 0.6f, 2f);
            SpawnSplash(ground + Vector3.up * 0.05f, 1.5f, 1f);
        }

        Transform NewColumn(string name, Transform parent, out Renderer r)
        {
            var c = new GameObject(name).transform;
            c.SetParent(parent, false);
            c.gameObject.AddComponent<MeshFilter>().sharedMesh = CylinderMesh;
            r = c.gameObject.AddComponent<MeshRenderer>();
            r.sharedMaterial = GeyserMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            c.localScale = new Vector3(0.5f, 0.01f, 0.5f);
            return c;
        }

        void Update()
        {
            if (done) return;
            float dt = Time.deltaTime;
            t += dt;

            float e;
            if (t < riseTime) e = Mathf.SmoothStep(0f, 1f, t / riseTime);
            else if (t < riseTime + holdTime) e = 1f;
            else e = 1f - Mathf.SmoothStep(0f, 1f, (t - riseTime - holdTime) / lowerTime);

            float h = Mathf.Max(0.01f, height * e);
            float wob = 1f + 0.06f * Mathf.Sin(t * 13f) + 0.04f * Mathf.Sin(t * 7.3f);
            column.localScale = new Vector3(0.5f * wob, h, 0.5f * wob);
            inner.localScale = new Vector3(0.3f / wob, h + 0.05f, 0.3f / wob);
            swirl.localScale = new Vector3(0.62f * wob, h * 0.97f, 0.62f * wob);
            swirl.localRotation = Quaternion.Euler(0f, t * 140f, 0f);
            crown.localPosition = Vector3.up * h;
            crown.localScale = new Vector3(0.95f * wob, Mathf.Min(0.7f, h * 0.35f), 0.95f * wob);
            crown.localRotation = Quaternion.Euler(0f, -t * 60f, 0f);
            float pr = Mathf.Lerp(0.3f, 1.4f, Mathf.Clamp01(t / 0.5f)) * (0.95f + 0.05f * Mathf.Sin(t * 5f));
            pool.localScale = new Vector3(pr, 1f, pr);
            top.localPosition = Vector3.up * h;
            platform.localPosition = Vector3.up * (h - 0.15f);
            float fade = e < 1f && t > riseTime ? e : 1f;
            SetWater(colR, Mathf.Lerp(0.3f, 1f, fade));
            SetWater(innerR, Mathf.Lerp(0.3f, 1f, fade));
            SetWater(swirlR, Mathf.Lerp(0.2f, 1f, fade));
            SetWater(crownR, Mathf.Lerp(0f, 1f, fade));
            SetWater(poolR, Mathf.Lerp(0.3f, 1f, fade));
            SetRate(fountain, 160f * fade);
            rush.volume = 0.8f * fade;

            if (carrying && rig != null && head != null)
            {
                Vector3 hp = head.position;
                if (new Vector2(hp.x - ground.x, hp.z - ground.z).magnitude > carryRadius) carrying = false;
                else
                {
                    Vector3 p = rig.position;
                    p.y = ground.y + height * e;
                    rig.position = p;
                    Physics.SyncTransforms();
                }
            }

            if (t >= riseTime + holdTime + lowerTime) Finish();
        }

        void Finish()
        {
            done = true;
            SpawnSplash(ground + Vector3.up * 0.2f, 1.2f, 0.8f);
            DetachPS(fountain, 1.5f);
            DetachPS(topMist, 1.7f);
            DetachPS(baseFoam, 1.6f);
            Destroy(gameObject);
        }
    }
}
