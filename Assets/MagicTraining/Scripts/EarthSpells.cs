namespace MagicTraining
{
    using UnityEngine;
    using static MagicTraining.MagicFX;

    [DefaultExecutionOrder(-70)]
    public class EarthCaster : MonoBehaviour
    {
        [Header("Barridos")]
        public float swipeSpeed = 3f;
        public float comboWindow = 0.18f;
        public float cooldown = 0.5f;

        [Header("Terremoto (manos abajo)")]
        public float slamSpeed = 2.2f;

        class Swipe { public bool active, armed; public float time, speed; public Vector3 dir; }
        readonly Swipe swL = new Swipe(), swR = new Swipe();
        float nextAllowed, slamL = -10f, slamR = -10f, hapticT;

        public static bool Armed(MagicRig.Hand h) =>
            h != null && h.trigger && h.grip && !Trident.IsHeldBy(h) && !DruidStaff.IsHeldBy(h);

        void OnDisable() => SpellLock.Set(Spell.Earth, false);

        void Update()
        {
            var rig = MagicRig.I;
            if (rig == null || !rig.Ready) return;
            var L = rig.L;
            var R = rig.R;
            bool aL = Armed(L), aR = Armed(R);
            float now = Time.time;
            SpellLock.Set(Spell.Earth, aL || aR);

            if (!aL && !aR)
            {
                swL.active = swR.active = false;
                swL.armed = swR.armed = false;
                return;
            }

            if (aL && L.velocity.y < -slamSpeed && Horizontal(L.velocity) < -L.velocity.y) slamL = now;
            if (aR && R.velocity.y < -slamSpeed && Horizontal(R.velocity) < -R.velocity.y) slamR = now;
            if (aL && aR && now >= nextAllowed && now - slamL < comboWindow && now - slamR < comboWindow)
            {
                slamL = slamR = -10f;
                nextAllowed = now + 1f;
                EarthQuake.Create(Feet(rig), rig.IgnoreRoot);
                rig.Haptic(true, 1f, 0.4f);
                rig.Haptic(false, 1f, 0.4f);
                swL.active = swR.active = false;
                swL.armed = swR.armed = false;
                return;
            }
            if (EarthQuake.Active) Rumble(rig, aL, aR, 0.5f);

            Detect(L, aL, swL, now);
            Detect(R, aR, swR, now);
            if (swL.active && swR.active && Mathf.Abs(swL.time - swR.time) <= comboWindow)
            {
                float speed = Mathf.Max(swL.speed, swR.speed);
                if (Vector3.Dot(swL.dir, swR.dir) > 0.4f) CastFissure(rig, (swL.dir + swR.dir).normalized, speed);
                else CastShockwave(rig, speed);
                swL.active = swR.active = false;
                nextAllowed = now + cooldown;
            }
            else
            {
                foreach (var (sw, left) in new[] { (swL, true), (swR, false) })
                {
                    bool otherArmed = left ? aR : aL;
                    if (!sw.active || (otherArmed && now - sw.time <= comboWindow)) continue;
                    CastLine(rig, left, sw.dir, sw.speed);
                    swL.active = swR.active = false;
                    nextAllowed = now + cooldown;
                    break;
                }
            }
        }

        static float Horizontal(Vector3 v) => new Vector2(v.x, v.z).magnitude;

        void Detect(MagicRig.Hand h, bool armedNow, Swipe sw, float now)
        {
            if (!armedNow) { sw.active = false; sw.armed = false; return; }
            Vector3 v = h.velocity;
            float hs = Horizontal(v);
            if (!sw.armed) { if (v.magnitude < 1.2f) sw.armed = true; else return; }
            if (sw.active || now < nextAllowed || hs < swipeSpeed || hs < Mathf.Abs(v.y) * 1.2f) return;
            sw.active = true;
            sw.time = now;
            sw.speed = hs;
            sw.dir = new Vector3(v.x, 0f, v.z) / hs;
        }

        static Vector3 Feet(MagicRig rig)
        {
            Transform head = rig.Head;
            float floorY = rig.IgnoreRoot != null ? rig.IgnoreRoot.position.y : head.position.y - 1.6f;
            Vector3 p = new Vector3(head.position.x, floorY, head.position.z);
            FindGround(p, rig.IgnoreRoot, floorY, out Vector3 g, out _);
            return g;
        }

        float Strength(float speed) => Mathf.InverseLerp(swipeSpeed, swipeSpeed + 3f, speed);

        void CastLine(MagicRig rig, bool left, Vector3 dir, float speed)
        {
            float k = Strength(speed);
            Vector3 feet = Feet(rig);
            MagicCircle.Flash(feet + Vector3.up * 0.04f, Quaternion.LookRotation(Vector3.up), 1.3f, MagicCircle.Element.Nature, 0.5f, 2f);
            EarthLine.Create(feet + dir * 1f, dir, Mathf.RoundToInt(Mathf.Lerp(9f, 16f, k)), 0.6f, Mathf.Lerp(1f, 1.4f, k), rig.IgnoreRoot);
            rig.Haptic(left, 0.9f, 0.2f);
        }

        void CastFissure(MagicRig rig, Vector3 dir, float speed)
        {
            float k = Strength(speed);
            Vector3 feet = Feet(rig);
            MagicCircle.Flash(feet + Vector3.up * 0.04f, Quaternion.LookRotation(Vector3.up), 2f, MagicCircle.Element.Nature, 0.6f, 2f);
            EarthFissure.Create(feet + dir * 1.2f, dir, Mathf.Lerp(8f, 13f, k), rig.IgnoreRoot);
            rig.Haptic(true, 1f, 0.3f);
            rig.Haptic(false, 1f, 0.3f);
        }

        void CastShockwave(MagicRig rig, float speed)
        {
            float k = Strength(speed);
            Vector3 feet = Feet(rig);
            MagicCircle.Flash(feet + Vector3.up * 0.04f, Quaternion.LookRotation(Vector3.up), 3f, MagicCircle.Element.Nature, 0.5f, 3f);
            EarthShockwave.Create(feet, Mathf.Lerp(6f, 9f, k), rig.IgnoreRoot);
            rig.Haptic(true, 1f, 0.3f);
            rig.Haptic(false, 1f, 0.3f);
        }

        void Rumble(MagicRig rig, bool aL, bool aR, float amp)
        {
            if ((hapticT -= Time.deltaTime) > 0f) return;
            hapticT = 0.07f;
            if (aL) rig.Haptic(true, amp * Random.Range(0.7f, 1.2f), 0.08f);
            if (aR) rig.Haptic(false, amp * Random.Range(0.7f, 1.2f), 0.08f);
        }
    }
}

namespace MagicTraining
{
    using System.Collections.Generic;
    using UnityEngine;
    using static MagicTraining.MagicFX;
    using MinMaxCurve = UnityEngine.ParticleSystem.MinMaxCurve;

    public class EarthLine : MonoBehaviour
    {
        struct Item { public float time; public Vector3 pos; public float size; public bool sound; }
        readonly List<Item> items = new List<Item>();
        Vector3 dir;
        Transform ignoreRoot;
        float t, floorY;
        int next;

        public static void Create(Vector3 start, Vector3 dir, int count, float spacing, float sizeMul, Transform ignoreRoot)
        {
            dir.y = 0f;
            dir = dir.sqrMagnitude > 0.001f ? dir.normalized : Vector3.forward;
            var go = new GameObject("Earth Line");
            var e = go.AddComponent<EarthLine>();
            e.dir = dir;
            e.ignoreRoot = ignoreRoot;
            e.floorY = start.y;
            Vector3 side = Vector3.Cross(Vector3.up, dir);
            for (int i = 0; i < count; i++)
            {
                float k = count > 1 ? i / (count - 1f) : 1f;
                e.items.Add(new Item
                {
                    time = i * 0.05f,
                    pos = start + dir * (i * spacing) + side * Random.Range(-0.25f, 0.25f),
                    size = Mathf.Lerp(0.22f, 0.5f, k) * sizeMul * Random.Range(0.85f, 1.15f),
                    sound = i % 3 == 0
                });
            }
            Vector3 end = start + dir * (count * spacing);
            EarthCrack.Create(start, end, 0.12f * sizeMul, 3.5f, ignoreRoot, false, count * 0.05f);
        }

        void Update()
        {
            t += Time.deltaTime;
            while (next < items.Count && items[next].time <= t)
            {
                var it = items[next++];
                FindGround(it.pos, ignoreRoot, floorY, out Vector3 g, out Vector3 n);
                EarthRock.Spawn(g, n, dir, it.size, 2.6f, ignoreRoot, 5f, true, it.sound);
            }
            if (next >= items.Count) Destroy(gameObject);
        }
    }

    public class EarthShockwave : MonoBehaviour
    {
        public float maxRadius = 7f;
        public float speed = 8f;
        Transform ignoreRoot;
        float r = 0.8f, spawnAcc, floorY;
        readonly HashSet<Rigidbody> hit = new HashSet<Rigidbody>();

        public static void Create(Vector3 center, float maxRadius, Transform ignoreRoot)
        {
            var go = new GameObject("Earth Shockwave");
            go.transform.position = center;
            var s = go.AddComponent<EarthShockwave>();
            s.maxRadius = maxRadius;
            s.ignoreRoot = ignoreRoot;
            s.floorY = center.y;
            SpawnEarthDust(center, 1.5f, 10, 2.5f);
            PlayAt(BoomClip, center, 0.9f, 0.55f);
            PlayAt(RockBurstClip, center, 1f, 0.7f);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            r += speed * dt;
            Vector3 c = transform.position;

            spawnAcc += dt * r * 9f;
            while (spawnAcc >= 1f)
            {
                spawnAcc -= 1f;
                float a = Random.value * Mathf.PI * 2f;
                var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                FindGround(c + d * (r + Random.Range(-0.2f, 0.2f)), ignoreRoot, floorY, out Vector3 g, out Vector3 n);
                bool big = Random.value < 0.35f;
                EarthRock.Spawn(g, n, d, Random.Range(0.12f, 0.25f) * (big ? 1.5f : 1f), 0.9f, ignoreRoot, 3f, big, false, 0.1f);
            }

            foreach (var col in Physics.OverlapSphere(c, r + 0.5f, ~0, QueryTriggerInteraction.Ignore))
            {
                var rb = col.attachedRigidbody;
                Vector3 off = col.bounds.center - c;
                off.y = 0f;
                if (Mathf.Abs(off.magnitude - r) > 0.8f) continue;
                SpellTarget.HitCollider(col);
                if (rb == null || rb.isKinematic || !hit.Add(rb)) continue;
                if (ignoreRoot != null && rb.transform.IsChildOf(ignoreRoot)) continue;
                rb.AddForce(off.normalized * 6f + Vector3.up * 4f, ForceMode.VelocityChange);
            }

            if (r >= maxRadius) Destroy(gameObject);
        }
    }

    public class EarthFissure : MonoBehaviour
    {
        public float length = 10f;
        public float openSpeed = 12f;
        public float openTime = 3.5f;

        Vector3 start, dir, side;
        Transform ignoreRoot;
        float t, opened, floorY, nextPlate = 0.3f;
        ParticleSystem embers;
        AudioSource rumble;
        bool closing;
        readonly HashSet<Rigidbody> pushed = new HashSet<Rigidbody>();

        public static void Create(Vector3 start, Vector3 dir, float length, Transform ignoreRoot)
        {
            dir.y = 0f;
            dir = dir.sqrMagnitude > 0.001f ? dir.normalized : Vector3.forward;
            var go = new GameObject("Earth Fissure");
            go.transform.SetPositionAndRotation(start, Quaternion.LookRotation(dir));
            var f = go.AddComponent<EarthFissure>();
            f.start = start;
            f.dir = dir;
            f.side = Vector3.Cross(Vector3.up, dir);
            f.length = length;
            f.ignoreRoot = ignoreRoot;
            f.floorY = start.y;
            float total = length / f.openSpeed + f.openTime + 1f;
            EarthCrack.Create(start, start + dir * length, 0.55f, total, ignoreRoot, true, length / f.openSpeed, false);
            EarthCrack.Create(start + f.side * 0.25f, start + dir * length * 0.9f + f.side * 0.4f, 0.12f, total, ignoreRoot, false, length / f.openSpeed);
            EarthCrack.Create(start - f.side * 0.25f, start + dir * length * 0.9f - f.side * 0.4f, 0.12f, total, ignoreRoot, false, length / f.openSpeed);
            f.Build();
        }

        void Build()
        {
            embers = NewPS("Embers", transform, SparkMat, true, 300);
            var m = embers.main;
            m.startLifetime = new MinMaxCurve(0.8f, 1.8f);
            m.startSpeed = new MinMaxCurve(0.5f, 2f);
            m.startSize = new MinMaxCurve(0.01f, 0.03f);
            m.gravityModifier = -0.05f;
            m.startColor = RandomFrom(Grad((0f, new Color(1f, 0.8f, 0.3f)), (1f, LavaOrange)));
            var sh = embers.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Box;
            sh.scale = new Vector3(0.3f, 0.05f, 0.1f);
            var col = embers.colorOverLifetime; col.enabled = true; col.color = FadeInOut;
            embers.Play();

            rumble = gameObject.AddComponent<AudioSource>();
            rumble.clip = EarthRumbleClip;
            rumble.loop = true;
            rumble.spatialBlend = 1f;
            rumble.minDistance = 2f;
            rumble.maxDistance = 40f;
            rumble.volume = 0.9f;
            rumble.Play();
            PlayAt(RockBurstClip, start, 1f, 0.6f);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            t += dt;
            float openDur = length / openSpeed;
            opened = Mathf.Min(length, openSpeed * t);

            float plateLife = openDur + openTime - (opened / openSpeed) + 0.6f;
            while (nextPlate < opened)
            {
                Vector3 p = start + dir * nextPlate;
                for (int s = -1; s <= 1; s += 2)
                {
                    Vector3 q = p + side * (s * Random.Range(0.35f, 0.55f));
                    FindGround(q, ignoreRoot, floorY, out Vector3 g, out Vector3 n);
                    EarthRock.Spawn(g, n, side * s, Random.Range(0.3f, 0.5f), plateLife, ignoreRoot, 3f, Random.value < 0.5f, Random.value < 0.25f, 0.18f);
                }
                if (Random.value < 0.4f) SpawnEarthDust(p, 1f, 3, 2f);
                nextPlate += Random.Range(0.35f, 0.55f);
            }

            embers.transform.position = start + dir * (opened * 0.5f);
            var sh = embers.shape; sh.scale = new Vector3(0.3f, 0.05f, Mathf.Max(0.1f, opened));
            bool open = t < openDur + openTime;
            SetRate(embers, open ? opened * 12f : 0f);

            if (open)
            {
                Vector3 c = start + dir * (opened * 0.5f);
                Vector3 half = new Vector3(0.9f, 1f, opened * 0.5f);
                foreach (var col in Physics.OverlapBox(c, half, transform.rotation, ~0, QueryTriggerInteraction.Ignore))
                {
                    var rb = col.attachedRigidbody;
                    SpellTarget.HitCollider(col);
                    if (rb == null || rb.isKinematic || !pushed.Add(rb)) continue;
                    if (ignoreRoot != null && rb.transform.IsChildOf(ignoreRoot)) continue;
                    float sgn = Vector3.Dot(rb.worldCenterOfMass - c, side) >= 0f ? 1f : -1f;
                    rb.AddForce(side * (sgn * 5f) + Vector3.up * 4f, ForceMode.VelocityChange);
                }
            }
            else if (!closing)
            {
                closing = true;
                PlayAt(RockBurstClip, start + dir * length * 0.5f, 0.8f, 0.5f);
            }
            if (closing) rumble.volume = Mathf.MoveTowards(rumble.volume, 0f, dt);
            if (t > openDur + openTime + 1.2f) { DetachPS(embers, 2f); Destroy(gameObject); }
        }
    }

    public class EarthQuake : MonoBehaviour
    {
        public float duration = 2.6f;
        public float radius = 7f;
        Transform ignoreRoot;
        float t, rockAcc, shakeT, floorY;
        AudioSource rumble;

        public static bool Active { get; private set; }
        public static float LastTime { get; private set; } = -100f;

        public static void Create(Vector3 center, Transform ignoreRoot)
        {
            var go = new GameObject("Earth Quake");
            go.transform.position = center;
            var q = go.AddComponent<EarthQuake>();
            q.ignoreRoot = ignoreRoot;
            q.floorY = center.y;
            Active = true;
            LastTime = Time.time;

            SpawnEarthDust(center, 2.2f, 16, 3f);
            SpawnPebbles(center, Vector3.up, 2f, 30);
            for (int i = 0; i < 8; i++)
            {
                float a = i / 8f * Mathf.PI * 2f + Random.Range(-0.2f, 0.2f);
                var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                EarthCrack.Create(center + d * 0.4f, center + d * Random.Range(4f, 7f), Random.Range(0.12f, 0.2f), q.duration + 2.5f, ignoreRoot, i % 2 == 0, 0.4f);
            }
            PlayAt(BoomClip, center, 1f, 0.4f);
            PlayAt(RockBurstClip, center, 1f, 0.5f);

            q.rumble = go.AddComponent<AudioSource>();
            q.rumble.clip = EarthRumbleClip;
            q.rumble.loop = true;
            q.rumble.spatialBlend = 0f;
            q.rumble.volume = 1f;
            q.rumble.Play();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            t += dt;
            float k = 1f - Mathf.Clamp01(t / duration);
            Vector3 c = transform.position;

            rockAcc += dt * 14f * k;
            while (rockAcc >= 1f)
            {
                rockAcc -= 1f;
                Vector2 p = Random.insideUnitCircle * radius;
                if (p.magnitude < 1f) p = p.normalized * 1f;
                FindGround(c + new Vector3(p.x, 0f, p.y), ignoreRoot, floorY, out Vector3 g, out Vector3 n);
                EarthRock.Spawn(g, n, new Vector3(p.x, 0f, p.y), Random.Range(0.15f, 0.4f), Random.Range(0.8f, 1.6f), ignoreRoot, 4f, true, Random.value < 0.3f, 0.12f);
            }

            shakeT -= dt;
            if (shakeT <= 0f)
            {
                shakeT = 0.22f;
                foreach (var col in Physics.OverlapSphere(c, radius + 2f, ~0, QueryTriggerInteraction.Ignore))
                {
                    var rb = col.attachedRigidbody;
                    if (rb == null || rb.isKinematic) continue;
                    if (ignoreRoot != null && rb.transform.IsChildOf(ignoreRoot)) continue;
                    rb.AddForce((Vector3.up * Random.Range(1.5f, 3f) + Random.insideUnitSphere * 1.5f) * k, ForceMode.VelocityChange);
                    rb.AddTorque(Random.insideUnitSphere * 3f * k, ForceMode.VelocityChange);
                }
            }

            rumble.volume = Mathf.Clamp01(k * 1.3f);
            if (t >= duration) { Active = false; Destroy(gameObject); }
        }

        void OnDestroy() => Active = false;
    }
}

namespace MagicTraining
{
    using System.Collections.Generic;
    using UnityEngine;
    using static MagicTraining.MagicFX;

    public class EarthRock : MonoBehaviour
    {
        Transform model;
        Transform ignoreRoot;
        Vector3 tilt;
        float life, t, sy, riseTime, pushUp;
        bool pushed;
        readonly HashSet<Rigidbody> seen = new HashSet<Rigidbody>();

        public static EarthRock Spawn(Vector3 ground, Vector3 normal, Vector3 tiltDir, float size, float life, Transform ignoreRoot,
            float pushUp = 4f, bool fx = true, bool sound = false, float riseTime = 0.14f)
        {
            Vector3 n = normal.sqrMagnitude > 0.01f ? normal.normalized : Vector3.up;
            Vector3 axis = tiltDir.sqrMagnitude > 0.01f ? Vector3.Slerp(n, tiltDir.normalized, Random.Range(0.15f, 0.45f)).normalized : n;
            var go = new GameObject("Earth Rock");
            go.transform.SetPositionAndRotation(ground, Quaternion.FromToRotation(Vector3.up, axis) * Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            var r = go.AddComponent<EarthRock>();
            r.ignoreRoot = ignoreRoot;
            r.tilt = tiltDir;
            r.life = life;
            r.pushUp = pushUp;
            r.riseTime = riseTime;
            r.model = new GameObject("Model").transform;
            r.model.SetParent(go.transform, false);
            r.model.gameObject.AddComponent<MeshFilter>().sharedMesh = RandomRockMesh;
            var mr = r.model.gameObject.AddComponent<MeshRenderer>();
            mr.sharedMaterial = RockMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            r.sy = size * Random.Range(1f, 1.7f);
            r.model.localScale = new Vector3(size * Random.Range(0.8f, 1.2f), r.sy, size * Random.Range(0.8f, 1.2f));
            r.model.localRotation = Quaternion.Euler(Random.Range(-10f, 10f), Random.Range(0f, 360f), Random.Range(-10f, 10f));
            r.model.localPosition = Vector3.down * (r.sy * 1.1f);
            if (fx) SpawnRockBurst(ground, n, size, sound);
            return r;
        }

        void Update()
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / riseTime) - 1f;
            float e = 1f + 2.70158f * k * k * k + 1.70158f * k * k;
            float y = Mathf.LerpUnclamped(-sy * 1.1f, -sy * 0.15f, e);
            if (t > life - 0.45f)
            {
                float s = Mathf.Clamp01((t - (life - 0.45f)) / 0.45f);
                y = Mathf.Lerp(-sy * 0.15f, -sy * 1.15f, s * s);
            }
            model.localPosition = new Vector3(0f, y, 0f);

            if (!pushed && t > 0.03f) { pushed = true; Push(); }
            if (t >= life) Destroy(gameObject);
        }

        void Push()
        {
            float r = Mathf.Max(model.localScale.x, model.localScale.z) * 1.3f + 0.2f;
            Vector3 c = transform.position + transform.up * (sy * 0.5f);
            foreach (var col in Physics.OverlapSphere(c, r, ~0, QueryTriggerInteraction.Ignore))
            {
                SpellTarget.HitCollider(col);
                var rb = col.attachedRigidbody;
                if (rb == null || rb.isKinematic || !seen.Add(rb)) continue;
                if (ignoreRoot != null && rb.transform.IsChildOf(ignoreRoot)) continue;
                rb.AddForce(Vector3.up * pushUp + tilt * (pushUp * 0.4f), ForceMode.VelocityChange);
                rb.AddTorque(Random.insideUnitSphere * 5f, ForceMode.VelocityChange);
            }
        }
    }

    public class EarthCrack : MonoBehaviour
    {
        LineRenderer dark, glow;
        Vector3[] pts;
        float life, growTime, width, t;

        public static EarthCrack Create(Vector3 a, Vector3 b, float width, float life, Transform ignoreRoot, bool lava = false, float growTime = 0.25f, bool branches = true)
        {
            var go = new GameObject("Earth Crack");
            var c = go.AddComponent<EarthCrack>();
            c.life = life;
            c.growTime = growTime;
            c.width = width;
            float len = Vector3.Distance(a, b);
            int n = Mathf.Clamp((int)(len * 6f) + 4, 4, 64);
            c.pts = new Vector3[n];
            Jagged(c.pts, n, a, b, width * 0.8f + len * 0.02f);
            for (int i = 0; i < n; i++)
            {
                FindGround(c.pts[i], ignoreRoot, a.y, out Vector3 g, out _);
                c.pts[i] = g + Vector3.up * 0.015f;
            }
            var w = Curve((0f, 0.3f), (0.15f, 1f), (0.8f, 0.8f), (1f, 0.1f));
            c.dark = NewBoltLine("Crack", go.transform, CrackMat, width, Grad((0f, A(new Color(0.08f, 0.05f, 0.03f), 0.95f)), (1f, A(new Color(0.08f, 0.05f, 0.03f), 0.95f))), w);
            c.dark.alignment = LineAlignment.TransformZ;
            go.transform.rotation = Quaternion.LookRotation(Vector3.down);
            if (lava)
                c.glow = NewBoltLine("Lava Glow", go.transform, BeamMat, width * 0.45f, Grad((0f, A(LavaOrange, 0.9f)), (1f, A(new Color(1f, 0.75f, 0.3f), 0.8f))), w);
            if (branches && len > 1.5f)
            {
                for (int k = 0; k < 2; k++)
                {
                    int idx = Random.Range(n / 4, n * 3 / 4);
                    Vector3 side = Vector3.Cross(Vector3.up, (b - a).normalized) * (Random.value < 0.5f ? -1f : 1f);
                    Vector3 end = c.pts[idx] + (side + (b - a).normalized * 0.6f).normalized * Random.Range(0.6f, 1.5f);
                    Create(c.pts[idx], end, width * 0.45f, life, ignoreRoot, false, growTime * 0.8f, false);
                }
            }
            return c;
        }

        void Update()
        {
            t += Time.deltaTime;
            int count = Mathf.Clamp(Mathf.CeilToInt(pts.Length * Mathf.Clamp01(t / growTime)), 2, pts.Length);
            SetLine(dark, pts, count);
            if (glow != null) SetLine(glow, pts, count);
            float fade = t > life - 1f ? Mathf.Clamp01(life - t) : 1f;
            dark.widthMultiplier = width * Mathf.Lerp(0.3f, 1f, fade);
            if (glow != null) glow.widthMultiplier = width * 0.45f * fade * (0.8f + 0.2f * Mathf.Sin(t * 8f));
            if (t >= life) Destroy(gameObject);
        }
    }
}
