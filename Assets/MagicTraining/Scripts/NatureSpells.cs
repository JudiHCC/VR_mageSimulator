namespace MagicTraining
{
    using UnityEngine;
    using UnityEngine.SceneManagement;
    using static MagicTraining.MagicFX;
    using MinMaxCurve = UnityEngine.ParticleSystem.MinMaxCurve;

    [DefaultExecutionOrder(-60)]
    public class DruidStaff : MonoBehaviour
    {
        public const float Length = 1.75f;
        public float grabRadius = 0.2f;

        [Header("Golpe al suelo")]
        public float minStrikeSpeed = 0.9f;
        public float strikeHeight = 0.08f;
        public float fullStrikeSpeed = 3.5f;
        public float cooldown = 0.35f;

        [Header("Liana (X)")]
        public float vineRange = 6f;
        public float vineSpeed = 2.6f;
        public float vineMaxLength = 14f;

        [Header("A dos manos")]
        public float twoHandGardenSize = 1.8f;
        public float twoHandVineThickness = 2.3f;

        MagicRig.Hand secondHand;
        float yPrimary, ySecond, twoBlend, twoHapticT;
        MagicCircle powerCircle;

        public static bool IsTwoHanded => IsHeld && Instance.secondHand != null;

        StaffVine activeVine;
        MagicCircle aimCircle;
        bool aimFromTop;
        float aimHapticT;

        public static bool IsGrowingVine => Instance != null && Instance.activeVine != null;

        public static DruidStaff Instance { get; private set; }
        public static bool IsHeld => Instance != null && Instance.holder != null;
        public static bool IsHeldBy(MagicRig.Hand h) => IsHeld && h != null && Instance.holderHand == h;

        public Vector3 BottomTip => transform.position;
        public Vector3 OrbPosition => transform.TransformPoint(StaffOrbLocal);

        MagicRig.Hand holderHand;
        Transform holder;
        Vector3 holdPos;
        Quaternion holdRot;

        Vector3 basePos, floorPoint, lastTip;
        float t, hover, nextStrike, strikeGlow;
        bool hoverL, hoverR, armed;

        ParticleSystem orbGlow, orbSparkles, petals;
        Light glow;
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
            var marker = GameObject.Find("Baston");
            if (marker == null) marker = GameObject.Find("Bastón");
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
                pos = root.position + f * 1.1f - r * 0.7f + Vector3.up * 0.25f;
            }
            var go = new GameObject("Druid Staff");
            go.transform.SetPositionAndRotation(pos, Quaternion.identity);
            go.AddComponent<DruidStaff>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            Build();
            basePos = transform.position;
            UpdateFloor();
            idleCircle = MagicCircle.Create(MagicCircle.Element.Nature);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (idleCircle != null) Destroy(idleCircle.gameObject);
        }

        void Build()
        {
            var model = new GameObject("Model");
            model.transform.SetParent(transform, false);
            model.AddComponent<MeshFilter>().sharedMesh = StaffMesh;
            var r = model.AddComponent<MeshRenderer>();
            r.sharedMaterial = StaffMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            Vector3 orb = StaffOrbLocal;
            orbGlow = NewPS("Orb Glow", transform, GlowMat, false, 20);
            orbGlow.transform.localPosition = orb;
            {
                var m = orbGlow.main;
                m.startLifetime = new MinMaxCurve(0.25f, 0.4f);
                m.startSize = new MinMaxCurve(0.14f, 0.2f);
                m.startColor = RandomFrom(Grad((0f, A(NatureGreen, 0.55f)), (1f, A(NatureGold, 0.45f))));
                var col = orbGlow.colorOverLifetime; col.enabled = true; col.color = FadeInOut;
                SetRate(orbGlow, 20f);
            }
            orbSparkles = NewPS("Orb Sparkles", transform, SparkMat, false, 120);
            orbSparkles.transform.localPosition = orb;
            {
                var m = orbSparkles.main;
                m.startLifetime = 1f;
                m.startSize = new MinMaxCurve(0.008f, 0.018f);
                m.startColor = RandomFrom(NaturePalette);
                var sh = orbSparkles.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.09f; sh.radiusThickness = 0f;
                var vel = orbSparkles.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.Local;
                vel.x = 0f; vel.y = 0.05f; vel.z = 0f;
                vel.orbitalX = 0f; vel.orbitalY = 4f; vel.orbitalZ = 0f;
                var col = orbSparkles.colorOverLifetime; col.enabled = true; col.color = FadeInOut;
                SetRate(orbSparkles, 14f);
            }
            petals = NewPS("Falling Petals", transform, DropMat, true, 60);
            petals.transform.localPosition = orb + Vector3.down * 0.05f;
            {
                var m = petals.main;
                m.startLifetime = new MinMaxCurve(2f, 3f);
                m.startSize = new MinMaxCurve(0.012f, 0.022f);
                m.gravityModifier = 0.04f;
                m.startColor = RandomFrom(PetalPalette);
                var sh = petals.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.08f;
                var nz = petals.noise; nz.enabled = true; nz.strength = 0.15f; nz.frequency = 1f;
                var col = petals.colorOverLifetime; col.enabled = true; col.color = FadeInOut;
                SetRate(petals, 1.5f);
            }
            orbGlow.Play(); orbSparkles.Play(); petals.Play();

            glow = NewLight(transform, orb, NatureGreen, 1.8f, 0.8f);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            t += dt;
            var rig = MagicRig.I;

            if (holder != null)
            {
                if (holderHand != null && !holderHand.grip && secondHand != null && secondHand.grip)
                {
                    holderHand = secondHand;
                    holder = secondHand.t;
                    secondHand = null;
                    twoBlend = 0f;
                    holdPos = holder.InverseTransformPoint(transform.position);
                    holdRot = Quaternion.Inverse(holder.rotation) * transform.rotation;
                    if (powerCircle != null) powerCircle.Release(false);
                    powerCircle = null;
                }
                if (rig == null || holderHand == null || !holderHand.grip) Release();
                else
                {
                    UpdateSecondHand(rig);
                    ApplyHeldPose(dt);
                    CheckStrike(dt, rig);
                    UpdateVine(dt, rig);
                }
            }
            else
            {
                transform.position = basePos + Vector3.up * (Mathf.Sin(t * 1.3f) * 0.025f);
                Quaternion upright = Quaternion.Euler(0f, t * 15f, 0f);
                transform.rotation = Quaternion.Slerp(transform.rotation, upright, 1f - Mathf.Exp(-dt * 2f));
                if (idleCircle != null) idleCircle.Follow(floorPoint + Vector3.up * 0.03f, Quaternion.LookRotation(Vector3.up), 0.8f, 0.5f);

                bool anyNear = false;
                if (rig != null && rig.Ready)
                {
                    anyNear |= TryHand(rig, rig.L, ref hoverL);
                    if (holder == null) anyNear |= TryHand(rig, rig.R, ref hoverR);
                }
                hover = Mathf.MoveTowards(hover, anyNear ? 1f : 0f, dt * 6f);
            }

            strikeGlow = Mathf.MoveTowards(strikeGlow, 0f, dt * 2f);
            glow.intensity = 0.8f + 0.6f * hover + 0.4f * (holder != null ? 1f : 0f) + 0.25f * Mathf.Sin(t * 2.5f) + 3f * strikeGlow
                           + twoBlend * (1.5f + 0.5f * Mathf.Sin(t * 6f));
            glow.range = 1.8f + twoBlend * 1.5f;
            SetRate(orbSparkles, 14f + 50f * twoBlend);
            SetRate(orbGlow, 20f + 25f * twoBlend);
            SetRate(petals, 1.5f + 6f * twoBlend);
        }

        bool TryHand(MagicRig rig, MagicRig.Hand h, ref bool wasHover)
        {
            bool near = DistanceToShaft(h.Position) < grabRadius;
            if (near && !wasHover) rig.Haptic(h.left, 0.15f, 0.04f);
            wasHover = near;
            if (near && h.gripDown && !h.gripConsumed) { Grab(rig, h); return true; }
            return near;
        }

        float DistanceToShaft(Vector3 p)
        {
            Vector3 a = transform.position, b = transform.TransformPoint(0f, Length, 0f);
            Vector3 ab = b - a;
            float k = Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector3.Distance(p, a + ab * k);
        }

        void Grab(MagicRig rig, MagicRig.Hand h)
        {
            holderHand = h;
            holder = h.t;
            h.gripConsumed = true;
            holdPos = holder.InverseTransformPoint(transform.position);
            holdRot = Quaternion.Inverse(holder.rotation) * transform.rotation;
            lastTip = BottomTip;
            armed = false;
            if (idleCircle != null) idleCircle.Release(true);
            idleCircle = null;
            hoverL = hoverR = false;
            rig.Haptic(h.left, 0.6f, 0.1f);
            orbSparkles.Emit(25);
            PlayAt(NatureChimeClip, OrbPosition, 0.3f, 1.3f);
            MagicCircle.Flash(OrbPosition, Quaternion.LookRotation(transform.up), 0.4f, MagicCircle.Element.Nature, 0.3f, 3f);
        }

        void UpdateSecondHand(MagicRig rig)
        {
            var other = rig.Other(holderHand);
            if (secondHand == null)
            {
                if (other.gripDown && !other.gripConsumed && !Trident.IsHeldBy(other) && DistanceToShaft(other.Position) < grabRadius + 0.05f)
                {
                    other.gripConsumed = true;
                    secondHand = other;
                    yPrimary = transform.InverseTransformPoint(holder.position).y;
                    ySecond = transform.InverseTransformPoint(other.Position).y;
                    if (Mathf.Abs(ySecond - yPrimary) < 0.12f) ySecond = yPrimary + (ySecond >= yPrimary ? 0.12f : -0.12f);

                    rig.Haptic(true, 0.8f, 0.15f);
                    rig.Haptic(false, 0.8f, 0.15f);
                    orbSparkles.Emit(40);
                    SpawnVineBloom(OrbPosition);
                    PlayAt(NatureChimeClip, OrbPosition, 0.6f, 0.75f);
                    if (powerCircle == null) powerCircle = MagicCircle.Create(MagicCircle.Element.Nature);
                }
                return;
            }

            if (!secondHand.grip)
            {
                secondHand = null;
                twoBlend = 0f;
                holdPos = holder.InverseTransformPoint(transform.position);
                holdRot = Quaternion.Inverse(holder.rotation) * transform.rotation;
                if (powerCircle != null) powerCircle.Release(false);
                powerCircle = null;
                rig.Haptic(holderHand.left, 0.3f, 0.06f);
                return;
            }

            twoHapticT -= Time.deltaTime;
            if (twoHapticT <= 0f)
            {
                twoHapticT = 0.25f;
                rig.Haptic(true, 0.08f, 0.05f);
                rig.Haptic(false, 0.08f, 0.05f);
            }
            if (powerCircle != null)
                powerCircle.Follow(OrbPosition, Quaternion.LookRotation(transform.up), 0.38f + 0.03f * Mathf.Sin(t * 5f), 4f);
        }

        void ApplyHeldPose(float dt)
        {
            Vector3 pos = holder.TransformPoint(holdPos);
            Quaternion rot = holder.rotation * holdRot;
            if (secondHand != null)
            {
                twoBlend = Mathf.MoveTowards(twoBlend, 1f, dt / 0.15f);
                Vector3 a = holder.position, b = secondHand.Position;
                Vector3 axis = (b - a) * Mathf.Sign(ySecond - yPrimary);
                if (axis.sqrMagnitude > 1e-4f)
                {
                    axis.Normalize();
                    Quaternion r2 = Quaternion.FromToRotation(rot * Vector3.up, axis) * rot;
                    Vector3 p2 = a - axis * yPrimary;
                    pos = Vector3.Lerp(pos, p2, twoBlend);
                    rot = Quaternion.Slerp(rot, r2, twoBlend);
                }
            }
            transform.SetPositionAndRotation(pos, rot);
        }

        void Release()
        {
            secondHand = null;
            twoBlend = 0f;
            if (powerCircle != null) powerCircle.Release(false);
            powerCircle = null;
            StopVine();
            HideAim();
            holder = null;
            holderHand = null;
            basePos = transform.position;
            t = 0f;
            UpdateFloor();
            if (idleCircle == null) idleCircle = MagicCircle.Create(MagicCircle.Element.Nature);
        }

        void UpdateFloor()
        {
            var rig = MagicRig.I;
            Transform ignore = rig != null ? rig.IgnoreRoot : null;
            float fallback = ignore != null ? ignore.position.y : basePos.y - 0.25f;
            FindGround(basePos, ignore, fallback, out floorPoint, out _);
        }

        void AimRay(bool fromTop, out Vector3 from, out Vector3 dir)
        {
            from = fromTop ? OrbPosition : BottomTip;
            dir = fromTop ? transform.up : -transform.up;
        }

        void UpdateVine(float dt, MagicRig rig)
        {
            var X = rig.L;
            if (activeVine == null)
            {
                bool fromTop = transform.up.y < 0f;
                AimRay(fromTop, out Vector3 from, out Vector3 dir);
                bool valid = CastIgnoring(from, dir, 10f, 0.02f, rig.IgnoreRoot, out Vector3 point, out Vector3 normal, out _)
                             && normal.y > 0.5f;
                if (valid)
                {
                    Vector3 h = rig.Head.position;
                    valid = new Vector2(point.x - h.x, point.z - h.z).magnitude <= vineRange;
                }

                if (!valid) { HideAim(); return; }

                if (aimCircle == null) aimCircle = MagicCircle.Create(MagicCircle.Element.Nature);
                aimCircle.Follow(point + normal * 0.03f, Quaternion.LookRotation(normal), secondHand != null ? 0.9f : 0.45f, 2f);

                if (X.primaryDown && SpellLock.CanStart(Spell.Vine))
                {
                    HideAim();
                    aimFromTop = fromTop;
                    activeVine = StaffVine.Create(point, normal, rig.IgnoreRoot, secondHand != null ? twoHandVineThickness : 1f);
                    activeVine.speed = vineSpeed;
                    activeVine.maxLength = vineMaxLength;
                    SpellLock.Set(Spell.Vine, true);
                    rig.Haptic(holderHand.left, 0.7f, 0.12f);
                    orbSparkles.Emit(20);
                }
                return;
            }

            if (!X.primary) { StopVine(); return; }

            AimRay(aimFromTop, out Vector3 f2, out Vector3 d2);
            float reach = Vector3.Distance(f2, activeVine.HeadWorld) + 1.5f;
            activeVine.Grow(f2 + d2 * reach, dt);
            strikeGlow = Mathf.Max(strikeGlow, 0.25f);

            aimHapticT -= dt;
            if (aimHapticT <= 0f)
            {
                aimHapticT = 0.09f;
                rig.Haptic(holderHand.left, Random.Range(0.08f, 0.2f), 0.06f);
                if (secondHand != null) rig.Haptic(secondHand.left, Random.Range(0.1f, 0.25f), 0.06f);
            }
        }

        void StopVine()
        {
            if (activeVine != null) activeVine.StopGrowing();
            activeVine = null;
            SpellLock.Set(Spell.Vine, false);
        }

        void HideAim()
        {
            if (aimCircle != null) aimCircle.Release(false);
            aimCircle = null;
        }

        void CheckStrike(float dt, MagicRig rig)
        {
            if (dt <= 0f) return;
            Vector3 tip = BottomTip;
            if (activeVine != null) { lastTip = tip; armed = false; return; }
            Vector3 vel = (tip - lastTip) / dt;
            lastTip = tip;

            float floorY = rig.IgnoreRoot != null ? rig.IgnoreRoot.position.y : tip.y - 1f;
            FindGround(tip, rig.IgnoreRoot, floorY, out Vector3 g, out Vector3 n);
            float h = tip.y - g.y;

            if (!armed && h > strikeHeight + 0.1f) armed = true;
            if (h >= strikeHeight) return;

            if (armed && Time.time >= nextStrike && -vel.y >= minStrikeSpeed)
            {
                float power = Mathf.InverseLerp(minStrikeSpeed, fullStrikeSpeed, -vel.y);
                nextStrike = Time.time + cooldown;
                bool two = secondHand != null;
                GardenPatch.Create(new Vector3(tip.x, g.y, tip.z), n, power, rig.IgnoreRoot, two ? twoHandGardenSize : 1f);
                rig.Haptic(holderHand.left, Mathf.Lerp(0.6f, 1f, power), 0.15f);
                if (two)
                {
                    rig.Haptic(secondHand.left, 1f, 0.25f);
                    PlayAt(WoodThumpClip, tip, 1f, 0.6f);
                }
                orbSparkles.Emit(30);
                strikeGlow = 1f;
            }
            armed = false;
        }
    }
}

namespace MagicTraining
{
    using System.Collections.Generic;
    using UnityEngine;
    using static MagicTraining.MagicFX;
    using MinMaxCurve = UnityEngine.ParticleSystem.MinMaxCurve;

    public class GardenPatch : MonoBehaviour
    {
        public static int maxPatches = 12;
        static readonly List<GardenPatch> All = new List<GardenPatch>();

        float R, growTime, t, witherT = -1f, countMul = 1f;
        Mesh mesh;
        MeshRenderer rend;
        MaterialPropertyBlock block;
        ParticleSystem sparkles;
        bool sparklesStopped;

        public static GardenPatch Create(Vector3 center, Vector3 normal, float power, Transform ignoreRoot, float sizeMul = 1f)
        {
            power = Mathf.Clamp01(power);
            while (All.Count >= maxPatches) All[0].Wither();

            var go = new GameObject("Garden Patch");
            go.transform.position = center;
            var p = go.AddComponent<GardenPatch>();
            sizeMul = Mathf.Max(1f, sizeMul);
            p.R = Mathf.Lerp(2f, 3.6f, power) * sizeMul;
            p.growTime = Mathf.Lerp(1f, 1.6f, power) * Mathf.Lerp(1f, sizeMul, 0.5f);
            p.countMul = sizeMul * 1.4f;
            p.Build(ignoreRoot);
            All.Add(p);
            SpawnNatureStrike(center, normal, power, p.R, p.growTime);
            return p;
        }

        readonly List<Vector3> V = new List<Vector3>();
        readonly List<Vector3> N = new List<Vector3>();
        readonly List<Color> C = new List<Color>();
        readonly List<Vector4> U = new List<Vector4>();
        readonly List<int> T = new List<int>();

        void Build(Transform ignoreRoot)
        {
            Vector3 center = transform.position;
            float area = Mathf.PI * R * R;
            int grass = Mathf.Clamp(Mathf.RoundToInt(area * 24f), 80, Mathf.RoundToInt(900 * countMul));
            int flowers = Mathf.Clamp(Mathf.RoundToInt(area * 4.5f), 15, Mathf.RoundToInt(180 * countMul));
            int sprouts = Mathf.Clamp(Mathf.RoundToInt(area * 3f), 10, Mathf.RoundToInt(120 * countMul));

            var grassT = GrassTemplates;
            var flowerT = FlowerTemplates;
            float wsum = 0f;
            foreach (var w in FlowerWeights) wsum += w;

            for (int i = 0; i < grass; i++) Place(grassT[Random.Range(0, grassT.Length)], 0.8f, 1.35f, center, ignoreRoot);
            for (int i = 0; i < sprouts; i++) Place(SproutTemplate, 0.8f, 1.4f, center, ignoreRoot);
            for (int i = 0; i < flowers; i++)
            {
                float r = Random.value * wsum;
                int k = 0;
                while (k < FlowerWeights.Length - 1 && r > FlowerWeights[k]) { r -= FlowerWeights[k]; k++; }
                Place(flowerT[k], 0.85f, 1.3f, center, ignoreRoot);
            }

            mesh = new Mesh { name = "Garden", hideFlags = HideFlags.DontSave, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(V);
            mesh.SetNormals(N);
            mesh.SetColors(C);
            mesh.SetUVs(1, U);
            mesh.SetTriangles(T, 0);
            mesh.RecalculateBounds();
            V.Clear(); N.Clear(); C.Clear(); U.Clear(); T.Clear();

            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            rend = gameObject.AddComponent<MeshRenderer>();
            rend.sharedMaterial = GardenMat;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;
            block = new MaterialPropertyBlock();
            ApplyBlock(0f, 0f);

            sparkles = NewPS("Garden Sparkles", transform, SparkMat, true, 400);
            {
                var m = sparkles.main;
                m.startLifetime = new MinMaxCurve(1.2f, 2.4f);
                m.startSize = new MinMaxCurve(0.01f, 0.025f);
                m.startColor = RandomFrom(NaturePalette);
                var sh = sparkles.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Circle;
                sh.radius = R; sh.radiusThickness = 1f; sh.rotation = new Vector3(90f, 0f, 0f);
                var vel = sparkles.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
                vel.x = new MinMaxCurve(0f, 0f); vel.y = new MinMaxCurve(0.08f, 0.25f); vel.z = new MinMaxCurve(0f, 0f);
                var nz = sparkles.noise; nz.enabled = true; nz.strength = 0.2f; nz.frequency = 1f;
                var col = sparkles.colorOverLifetime; col.enabled = true; col.color = FadeInOut;
                SetRate(sparkles, area * 5f);
            }
            sparkles.Play();
        }

        void Place(PlantTemplate tp, float sMin, float sMax, Vector3 center, Transform ignoreRoot)
        {
            for (int attempt = 0; attempt < 4; attempt++)
            {
                float r = R * Mathf.Sqrt(Random.value);
                float edge = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.7f * R, R, r));
                if (Random.value > edge) continue;
                float a = Random.value * Mathf.PI * 2f;
                Vector3 world = center + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                FindGround(world, ignoreRoot, center.y, out Vector3 g, out Vector3 n);
                if (Vector3.Angle(n, Vector3.up) > 50f || Mathf.Abs(g.y - center.y) > 0.8f) continue;

                Vector3 lp = g - center;
                Quaternion rot = Quaternion.FromToRotation(Vector3.up, n)
                               * Quaternion.Euler(Random.Range(-8f, 8f), Random.Range(0f, 360f), Random.Range(-8f, 8f));
                float s = Random.Range(sMin, sMax);
                float birth = r + Random.Range(0f, 0.12f);
                float tint = Random.Range(0.85f, 1.15f);
                AddPlant(tp, lp, rot, s, birth, tint);
                return;
            }
        }

        void AddPlant(PlantTemplate tp, Vector3 pos, Quaternion rot, float s, float birth, float tint)
        {
            int b = V.Count;
            var u = new Vector4(pos.x, pos.y, pos.z, birth);
            for (int i = 0; i < tp.v.Length; i++)
            {
                V.Add(pos + rot * (tp.v[i] * s));
                N.Add(rot * tp.n[i]);
                Color c = tp.c[i];
                C.Add(new Color(c.r * tint, c.g * tint, c.b * tint, c.a));
                U.Add(u);
            }
            for (int i = 0; i < tp.t.Length; i++) T.Add(b + tp.t[i]);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            t += dt;

            float front;
            if (t < growTime)
            {
                float k = t / growTime;
                front = (R + 0.5f) * (1f - (1f - k) * (1f - k));
            }
            else front = t > growTime + 0.6f ? 10000f : R + 0.5f + (t - growTime) * 2f;

            float wither = 0f;
            if (witherT >= 0f)
            {
                witherT += dt;
                wither = Mathf.SmoothStep(0f, 1f, witherT / 1.5f);
                if (witherT > 1.6f) { Destroy(gameObject); return; }
            }
            ApplyBlock(front, wither);

            if (!sparklesStopped && t > growTime + 2.5f)
            {
                sparklesStopped = true;
                sparkles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        void ApplyBlock(float front, float wither)
        {
            block.SetFloat("_Front", front);
            block.SetFloat("_Wither", wither);
            rend.SetPropertyBlock(block);
        }

        public void Wither()
        {
            All.Remove(this);
            if (witherT >= 0f) return;
            witherT = 0f;
            if (sparkles != null) sparkles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        void OnDestroy()
        {
            All.Remove(this);
            if (mesh != null) Destroy(mesh);
        }
    }
}

namespace MagicTraining
{
    using System.Collections.Generic;
    using UnityEngine;
    using static MagicTraining.MagicFX;
    using MinMaxCurve = UnityEngine.ParticleSystem.MinMaxCurve;

    public class StaffVine : MonoBehaviour
    {
        public static int maxVines = 8;
        static readonly List<StaffVine> All = new List<StaffVine>();

        public float speed = 2.6f;
        public float maxLength = 14f;
        public float turnRate = 200f;
        public float baseRadius = 0.06f;
        public float tipRadius = 0.01f;

        const float Seg = 0.06f;
        const int Sides = 7;

        readonly List<Vector3> pts = new List<Vector3>();
        readonly List<float> arc = new List<float>();
        Vector3 head, heading;
        float length, groundY, nextLeaf = 0.15f, nextFlower = 0.9f, nextTendril = 0.55f, front, stopT = -1f, witherT = -1f;
        int leafSide;
        float thick = 1f;
        bool growing = true, dirty = true;
        Transform ignoreRoot;

        Mesh mesh;
        MeshRenderer rend;
        MaterialPropertyBlock block;
        ParticleSystem headSparkles;
        Light headLight;
        AudioSource creak;

        readonly List<Vector3> dV = new List<Vector3>();
        readonly List<Color> dC = new List<Color>();
        readonly List<Vector4> dU = new List<Vector4>();
        readonly List<int> dT = new List<int>();

        public float Length => length;
        public Vector3 HeadWorld => transform.TransformPoint(head);

        public static StaffVine Create(Vector3 ground, Vector3 normal, Transform ignoreRoot, float thickness = 1f)
        {
            while (All.Count >= maxVines) All[0].Wither();
            var go = new GameObject("Vine");
            go.transform.position = ground;
            var v = go.AddComponent<StaffVine>();
            v.ignoreRoot = ignoreRoot;
            v.groundY = ground.y;
            v.thick = Mathf.Max(1f, thickness);
            v.baseRadius *= v.thick;
            v.tipRadius *= Mathf.Lerp(1f, v.thick, 0.6f);
            v.heading = normal.sqrMagnitude > 0.01f ? normal.normalized : Vector3.up;
            v.Build();
            All.Add(v);
            SpawnVineSprout(ground, normal);
            return v;
        }

        void Build()
        {
            mesh = new Mesh { name = "Vine", hideFlags = HideFlags.DontSave };
            mesh.MarkDynamic();
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            rend = gameObject.AddComponent<MeshRenderer>();
            rend.sharedMaterial = VineMat;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;
            block = new MaterialPropertyBlock();

            pts.Add(Vector3.zero);
            arc.Add(0f);
            head = Vector3.zero;

            headSparkles = NewPS("Head Sparkles", transform, SparkMat, true, 300);
            {
                var m = headSparkles.main;
                m.startLifetime = new MinMaxCurve(0.6f, 1.2f);
                m.startSize = new MinMaxCurve(0.012f, 0.028f);
                m.startSpeed = new MinMaxCurve(0.05f, 0.3f);
                m.startColor = RandomFrom(NaturePalette);
                var sh = headSparkles.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.05f * thick;
                var nz = headSparkles.noise; nz.enabled = true; nz.strength = 0.2f; nz.frequency = 1.5f;
                var col = headSparkles.colorOverLifetime; col.enabled = true; col.color = FadeInOut;
                var em = headSparkles.emission; em.rateOverTime = 15f; em.rateOverDistance = 35f;
            }
            headSparkles.Play();
            headLight = NewLight(headSparkles.transform, Vector3.zero, NatureGreen, 1.6f, 1.2f);

            creak = gameObject.AddComponent<AudioSource>();
            creak.clip = VineLoopClip;
            creak.loop = true;
            creak.spatialBlend = 1f;
            creak.minDistance = 1f;
            creak.maxDistance = 25f;
            creak.dopplerLevel = 0f;
            creak.volume = 0f;
            creak.Play();
            Rebuild();
        }

        public void Grow(Vector3 targetWorld, float dt)
        {
            if (!growing) return;
            Vector3 target = transform.InverseTransformPoint(targetWorld);
            Vector3 desired = target - head;
            if (desired.sqrMagnitude > 0.0001f)
                heading = Vector3.RotateTowards(heading, desired.normalized, turnRate * Mathf.Deg2Rad * dt, 0f).normalized;

            Vector3 hw = transform.TransformPoint(head);
            FindGround(hw, ignoreRoot, groundY, out Vector3 g, out _);
            if (hw.y - g.y < 0.08f && heading.y < 0.25f) { heading.y = 0.25f; heading.Normalize(); }

            float step = Mathf.Min(speed * dt, maxLength - length);
            bool moving = desired.magnitude > 0.12f && step > 0f;
            creak.volume = Mathf.MoveTowards(creak.volume, moving ? 0.55f : 0.15f, dt * 3f);
            if (!moving) return;

            Vector3 side = Vector3.Cross(heading, Mathf.Abs(heading.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            Vector3 wiggle = side * (Mathf.Sin(length * 2.3f) * 0.12f) + Vector3.Cross(heading, side) * (Mathf.Sin(length * 1.7f + 1f) * 0.08f);
            head += (heading + wiggle).normalized * step;
            length += step;

            if (Vector3.Distance(head, pts[pts.Count - 1]) >= Seg)
            {
                pts.Add(head);
                arc.Add(length);
                Decorate();
            }
            dirty = true;
            headSparkles.transform.localPosition = head;
            if (length >= maxLength) StopGrowing();
        }

        public void StopGrowing()
        {
            if (!growing) return;
            growing = false;
            stopT = 0f;
            if (Vector3.Distance(head, pts[pts.Count - 1]) > 0.005f) { pts.Add(head); arc.Add(length); }
            dirty = true;
            headSparkles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            SpawnVineBloom(HeadWorld);
        }

        static readonly Color[] LeafCols = { new Color(0.24f, 0.62f, 0.18f, 0f), new Color(0.3f, 0.72f, 0.22f, 0f), new Color(0.2f, 0.55f, 0.16f, 0f) };
        static readonly Color[] PetalCols = { new Color(1f, 0.6f, 0.8f, 0.55f), new Color(1f, 1f, 0.95f, 0.45f), new Color(1f, 0.85f, 0.25f, 0.55f), new Color(0.78f, 0.5f, 1f, 0.55f) };

        void Decorate()
        {
            int i = pts.Count - 1;
            Vector3 c = pts[i];
            Vector3 tg = (pts[i] - pts[i - 1]).normalized;
            Vector3 n1 = Vector3.Cross(tg, Mathf.Abs(tg.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            Vector3 n2 = Vector3.Cross(tg, n1);
            float big = Mathf.Lerp(1f, thick, 0.7f);
            float off = 0.03f * thick;

            while (length >= nextLeaf)
            {
                nextLeaf += Random.Range(0.16f, 0.28f) * Mathf.Lerp(1f, thick, 0.4f);
                leafSide++;
                float a = leafSide * Mathf.PI + Random.Range(-0.6f, 0.6f);
                Vector3 outw = n1 * Mathf.Cos(a) + n2 * Mathf.Sin(a);
                Vector3 basePos = c + outw * off;
                Vector3 dir = outw * 0.9f + tg * 0.45f - Vector3.up * 0.1f;
                var tmp = new VMesh();
                float len = Random.Range(0.09f, 0.15f) * big;
                tmp.Tube(new[] { c, basePos + outw * (0.01f * big) }, new[] { 0.004f * big, 0.003f * big }, new[] { LeafCols[0], LeafCols[0] }, 3, false, false);
                tmp.Leaf(basePos + outw * (0.01f * big), dir, tg, len, len * 0.7f, LeafCols[Random.Range(0, LeafCols.Length)]);
                AddDecor(tmp, basePos, length);
            }
            if (length >= nextFlower)
            {
                nextFlower += Random.Range(0.8f, 1.5f) / Mathf.Lerp(1f, thick, 0.3f);
                float a = Random.value * Mathf.PI * 2f;
                Vector3 outw = n1 * Mathf.Cos(a) + n2 * Mathf.Sin(a);
                var tmp = new VMesh();
                Color pc = PetalCols[Random.Range(0, PetalCols.Length)];
                Vector3 fp = c + outw * (off + 0.01f * big);
                tmp.FlowerHead(fp, outw + Vector3.up * 0.4f, Random.Range(5, 8), 0.035f * big, 0.02f * big, pc, new Color(1f, 0.85f, 0.3f, 0.5f), 0.35f, a, 0.01f * big);
                AddDecor(tmp, fp, length);
            }
            if (length >= nextTendril)
            {
                nextTendril += Random.Range(0.7f, 1.2f);
                float a = Random.value * Mathf.PI * 2f;
                Vector3 outw = n1 * Mathf.Cos(a) + n2 * Mathf.Sin(a);
                Vector3 axis = (outw + tg * 0.3f).normalized;
                Vector3 p1 = Vector3.Cross(axis, Mathf.Abs(axis.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
                Vector3 p2 = Vector3.Cross(axis, p1);
                var hp = new List<Vector3>();
                var hr = new List<float>();
                var hc = new List<Color>();
                const int HN = 18;
                Vector3 start = c + outw * (off * 0.85f);
                for (int k = 0; k <= HN; k++)
                {
                    float u = k / (float)HN;
                    float ang = u * 2.6f * Mathf.PI * 2f;
                    float rr = Mathf.Lerp(0.035f, 0.012f, u) * big;
                    hp.Add(start + axis * (u * 0.16f * big) + (p1 * Mathf.Cos(ang) + p2 * Mathf.Sin(ang)) * rr * Mathf.Min(1f, u * 4f));
                    hr.Add(Mathf.Lerp(0.004f, 0.0015f, u) * big);
                    hc.Add(Color.Lerp(LeafCols[2], LeafCols[1], u));
                }
                var tmp = new VMesh();
                tmp.Tube(hp, hr, hc, 3, false, true);
                AddDecor(tmp, start, length);
            }
        }

        void AddDecor(VMesh tmp, Vector3 origin, float birth)
        {
            int b = dV.Count;
            var u = new Vector4(origin.x, origin.y, origin.z, birth);
            for (int k = 0; k < tmp.V.Count; k++) { dV.Add(tmp.V[k]); dC.Add(tmp.C[k]); dU.Add(u); }
            for (int k = 0; k < tmp.T.Count; k++) dT.Add(b + tmp.T[k]);
        }

        readonly List<Vector3> aV = new List<Vector3>();
        readonly List<Color> aC = new List<Color>();
        readonly List<Vector4> aU = new List<Vector4>();
        readonly List<int> aT = new List<int>();
        readonly List<Vector3> tubeP = new List<Vector3>();
        readonly List<float> tubeR = new List<float>();
        readonly List<Color> tubeC = new List<Color>();

        void Rebuild()
        {
            dirty = false;
            tubeP.Clear(); tubeR.Clear(); tubeC.Clear();
            var dark = new Color(0.2f, 0.3f, 0.1f, 0f);
            var lightC = new Color(0.42f, 0.72f, 0.26f, 0f);
            for (int i = 0; i < pts.Count; i++)
            {
                float s = arc[i];
                float r = tipRadius + (baseRadius - tipRadius) * Mathf.Pow(Mathf.Clamp01((length - s) / 3f), 0.7f);
                float rootLen = 0.15f * thick;
                if (s < rootLen) r *= 1f + (rootLen - s) / rootLen * 0.7f;
                tubeP.Add(pts[i]);
                tubeR.Add(r);
                Color c = Color.Lerp(dark, lightC, 1f - Mathf.Clamp01((length - s) / 2f)) * (0.88f + 0.12f * Mathf.Sin(s * 25f));
                c.a = 0f;
                tubeC.Add(c);
            }
            Vector3 tip = head + heading * (0.05f * thick);
            tubeP.Add(tip);
            tubeR.Add(0.002f * thick);
            tubeC.Add(lightC);
            if (tubeP.Count < 2) return;

            var tube = new VMesh();
            tube.Tube(tubeP, tubeR, tubeC, Sides, false, true);

            aV.Clear(); aC.Clear(); aU.Clear(); aT.Clear();
            int ringVerts = tubeP.Count * Sides;
            for (int k = 0; k < tube.V.Count; k++)
            {
                aV.Add(tube.V[k]);
                aC.Add(tube.C[k]);
                Vector3 org = k < ringVerts ? tubeP[k / Sides] : tubeP[tubeP.Count - 1];
                aU.Add(new Vector4(org.x, org.y, org.z, -100f));
            }
            aT.AddRange(tube.T);
            int b = aV.Count;
            aV.AddRange(dV); aC.AddRange(dC); aU.AddRange(dU);
            for (int k = 0; k < dT.Count; k++) aT.Add(b + dT[k]);

            mesh.Clear();
            if (aV.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(aV);
            mesh.SetColors(aC);
            mesh.SetUVs(1, aU);
            mesh.SetTriangles(aT, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dirty) Rebuild();

            if (growing) front = length;
            else
            {
                stopT += dt;
                front = stopT < 0.6f ? length + stopT * 3f : 10000f;
                creak.volume = Mathf.MoveTowards(creak.volume, 0f, dt);
                headLight.intensity = Mathf.MoveTowards(headLight.intensity, 0f, dt * 2f);
            }

            float wither = 0f;
            if (witherT >= 0f)
            {
                witherT += dt;
                wither = Mathf.SmoothStep(0f, 1f, witherT / 1.5f);
                if (witherT > 1.6f) { Destroy(gameObject); return; }
            }
            block.SetFloat("_Front", front);
            block.SetFloat("_Wither", wither);
            rend.SetPropertyBlock(block);
        }

        public void Wither()
        {
            All.Remove(this);
            if (witherT >= 0f) return;
            if (growing) StopGrowing();
            witherT = 0f;
        }

        void OnDestroy()
        {
            All.Remove(this);
            if (mesh != null) Destroy(mesh);
        }
    }
}
