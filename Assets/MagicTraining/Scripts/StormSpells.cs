namespace MagicTraining
{
    using UnityEngine;
    using static MagicTraining.MagicFX;

    [DefaultExecutionOrder(-55)]
    public class StormCaster : MonoBehaviour
    {
        public float turnsToSummon = 1.5f;
        public float raiseMargin = 0.05f;

        Vector3 acc, prevDir;
        bool hasPrev;
        float charge;
        MagicCircle circle;

        void OnDisable()
        {
            ResetCharge();
            if (StormClouds.Current != null) StormClouds.Current.Dissipate();
        }

        void ResetCharge()
        {
            acc = Vector3.zero;
            hasPrev = false;
            charge = 0f;
            SpellLock.Set(Spell.Storm, false);
            if (Trident.Instance != null) Trident.Instance.SetStormCharge(0f);
            if (circle != null) circle.Release(false);
            circle = null;
        }

        void Update()
        {
            var rig = MagicRig.I;
            if (rig == null || !rig.Ready) return;

            if (!Trident.IsHeld)
            {
                if (charge > 0f || circle != null) ResetCharge();
                if (StormClouds.Current != null && !Trident.IsInFlight) StormClouds.Current.Dissipate();
                return;
            }

            var h = Trident.HeldHand;
            bool raised = h.Position.y > rig.Head.position.y + raiseMargin;
            bool otherButtons = h.trigger || h.primary;
            if (!raised || otherButtons || StormClouds.Current != null || (charge <= 0f && !SpellLock.CanStart(Spell.Storm)))
            {
                if (charge > 0f || circle != null) ResetCharge();
                return;
            }

            Vector3 v = h.velocity;
            if (v.magnitude > 0.35f)
            {
                Vector3 d = v.normalized;
                if (hasPrev) acc += Vector3.Cross(prevDir, d);
                prevDir = d;
                hasPrev = true;
            }
            float turns = Trident.IsTwoHanded ? turnsToSummon * 0.6f : turnsToSummon;
            float newCharge = Mathf.Clamp01(acc.magnitude / (Mathf.PI * 2f * turns));
            if (newCharge > 0.05f)
            {
                SpellLock.Set(Spell.Storm, true);
                if (circle == null) circle = MagicCircle.Create(MagicCircle.Element.Lightning);
            }
            if (Mathf.Floor(newCharge * turns * 4f) > Mathf.Floor(charge * turns * 4f))
                rig.Haptic(h.left, Mathf.Lerp(0.2f, 0.7f, newCharge), 0.05f);
            charge = newCharge;

            var trident = Trident.Instance;
            trident.SetStormCharge(charge);
            if (circle != null)
                circle.Follow(trident.TipPosition + Vector3.up * 0.15f, Quaternion.LookRotation(Vector3.up),
                    Mathf.Lerp(0.3f, 1.2f, charge), Mathf.Lerp(1f, 5f, charge));

            if (charge >= 1f)
            {
                Transform head = rig.Head;
                float floorY = rig.IgnoreRoot != null ? rig.IgnoreRoot.position.y : head.position.y - 1.6f;
                var storm = StormClouds.Create(head, floorY);
                StormBolt.Spawn(new Vector3(trident.TipPosition.x, storm.SkyY, trident.TipPosition.z), trident.TipPosition);
                SpawnZapPuff(trident.TipPosition, 0.8f);
                rig.Haptic(h.left, 1f, 0.3f);
                ResetCharge();
            }
        }
    }
}

namespace MagicTraining
{
    using UnityEngine;
    using static MagicTraining.MagicFX;
    using MinMaxCurve = UnityEngine.ParticleSystem.MinMaxCurve;

    public class StormClouds : MonoBehaviour
    {
        public static StormClouds Current { get; private set; }

        public float height = 10f;
        public float radius = 14f;
        public float sunDim = 0.35f;

        Transform follow;
        float floorY, t, fade, nextFlash, flashT = -1f;
        bool dissipating;
        ParticleSystem clouds, cloudGlow, rain;
        Light flashLight, sun;
        float sunOrig = -1f;
        AudioSource rainAudio;

        public float SkyY => transform.position.y;

        public static StormClouds Create(Transform head, float floorY)
        {
            if (Current != null) return Current;
            var go = new GameObject("Storm Clouds");
            var s = go.AddComponent<StormClouds>();
            Current = s;
            s.follow = head;
            s.floorY = floorY;
            go.transform.position = new Vector3(head.position.x, floorY + s.height, head.position.z);
            s.Build();
            return s;
        }

        void Build()
        {
            Transform tr = transform;
            clouds = NewPS("Clouds", tr, SmokeMat, false, 260);
            {
                var m = clouds.main;
                m.startLifetime = new MinMaxCurve(6f, 9f);
                m.startSize = new MinMaxCurve(5f, 9f);
                m.startRotation = new MinMaxCurve(0f, Mathf.PI * 2f);
                m.startColor = RandomFrom(Grad((0f, new Color(0.13f, 0.14f, 0.19f, 0.85f)), (1f, new Color(0.22f, 0.23f, 0.3f, 0.8f))));
                var sh = clouds.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Circle;
                sh.radius = radius; sh.rotation = new Vector3(90f, 0f, 0f);
                var vel = clouds.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.Local;
                vel.x = 0f; vel.y = 0f; vel.z = 0f;
                vel.orbitalX = 0f; vel.orbitalY = 0.06f; vel.orbitalZ = 0f;
                var col = clouds.colorOverLifetime; col.enabled = true; col.color = FadeInOut;
                var rot = clouds.rotationOverLifetime; rot.enabled = true; rot.z = new MinMaxCurve(-0.1f, 0.1f);
                var r = clouds.GetComponent<ParticleSystemRenderer>(); r.maxParticleSize = 3f;
                SetRate(clouds, 40f);
            }
            cloudGlow = NewPS("Cloud Lightning", tr, GlowMat, false, 60);
            {
                var m = cloudGlow.main;
                m.startLifetime = new MinMaxCurve(0.1f, 0.25f);
                m.startSize = new MinMaxCurve(4f, 8f);
                m.startColor = RandomFrom(Grad((0f, A(ElecWhite, 0.5f)), (1f, A(ElecViolet, 0.4f))));
                var sh = cloudGlow.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Circle;
                sh.radius = radius * 0.8f; sh.rotation = new Vector3(90f, 0f, 0f);
                var col = cloudGlow.colorOverLifetime; col.enabled = true; col.color = FadeOut;
                var r = cloudGlow.GetComponent<ParticleSystemRenderer>(); r.maxParticleSize = 3f;
            }
            rain = NewPS("Rain", tr, SparkMat, true, 1500);
            rain.transform.localPosition = Vector3.down * 0.5f;
            {
                var m = rain.main;
                m.startLifetime = 0.9f;
                m.startSpeed = 0f;
                m.startSize = new MinMaxCurve(0.008f, 0.014f);
                m.gravityModifier = 2.5f;
                m.startColor = new Color(0.7f, 0.85f, 1f, 0.5f);
                var r = rain.GetComponent<ParticleSystemRenderer>();
                r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.03f; r.lengthScale = 1f;
                var sh = rain.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Circle;
                sh.radius = radius * 0.7f; sh.rotation = new Vector3(90f, 0f, 0f);
            }
            clouds.Play(); cloudGlow.Play(); rain.Play();

            flashLight = NewLight(tr, Vector3.down * 2f, new Color(0.75f, 0.8f, 1f), 60f, 0f);

            sun = RenderSettings.sun;
            if (sun == null)
                foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                    if (l.type == LightType.Directional) { sun = l; break; }
            if (sun != null) sunOrig = sun.intensity;

            rainAudio = gameObject.AddComponent<AudioSource>();
            rainAudio.clip = RainLoopClip;
            rainAudio.loop = true;
            rainAudio.spatialBlend = 0f;
            rainAudio.volume = 0f;
            rainAudio.Play();

            nextFlash = 1.2f;
            PlayAt(ThunderClip, transform.position, 0.8f, 0.65f);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            t += dt;
            fade = Mathf.MoveTowards(fade, dissipating ? 0f : 1f, dt / (dissipating ? 3f : 2f));

            if (follow != null)
            {
                Vector3 target = new Vector3(follow.position.x, floorY + height, follow.position.z);
                transform.position = Vector3.Lerp(transform.position, target, 1f - Mathf.Exp(-dt * 0.5f));
            }

            SetRate(rain, 350f * fade);
            rainAudio.volume = 0.35f * fade;
            if (sun != null && sunOrig >= 0f) sun.intensity = Mathf.Lerp(sunOrig, sunOrig * sunDim, fade);

            if (!dissipating && t >= nextFlash)
            {
                nextFlash = t + Random.Range(1.5f, 4f);
                Flash(Random.value < 0.5f);
            }
            if (flashT >= 0f)
            {
                flashT += dt;
                float k = flashT < 0.06f ? 1f : flashT < 0.12f ? 0.2f : flashT < 0.2f ? 0.8f : Mathf.Max(0f, 0.8f - (flashT - 0.2f) * 4f);
                flashLight.intensity = k * 3f;
                if (flashT > 0.5f) { flashT = -1f; flashLight.intensity = 0f; }
            }

            if (dissipating && fade <= 0f) Destroy(gameObject);
        }

        public void Flash(bool withThunder)
        {
            flashT = 0f;
            cloudGlow.Emit(Random.Range(2, 5));
            if (withThunder)
                PlayAt(ThunderClip, transform.position + Random.insideUnitSphere * radius * 0.5f, 0.35f, Random.Range(0.55f, 0.8f));
        }

        public void Dissipate()
        {
            if (dissipating) return;
            dissipating = true;
            clouds.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (Current == this) Current = null;
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
            if (sun != null && sunOrig >= 0f) sun.intensity = sunOrig;
        }
    }
}

namespace MagicTraining
{
    using UnityEngine;
    using static MagicTraining.MagicFX;

    public class StormBolt : MonoBehaviour
    {
        public float life = 0.35f;

        Vector3 from, to;
        LineRenderer core, glow;
        readonly LineRenderer[] branches = new LineRenderer[4];
        readonly Vector3[] pts = new Vector3[48];
        readonly Vector3[] tmp = new Vector3[16];
        float t, regen;

        public static StormBolt Spawn(Vector3 sky, Vector3 ground)
        {
            var go = new GameObject("Storm Bolt");
            var b = go.AddComponent<StormBolt>();
            b.from = sky;
            b.to = ground;
            var mainCurve = Curve((0f, 0.6f), (0.5f, 1f), (1f, 1f));
            b.glow = NewBoltLine("Bolt Glow", go.transform, BeamMat, 0.45f, ElecGlowGradient, mainCurve);
            b.core = NewBoltLine("Bolt Core", go.transform, CoreMat, 0.07f, ElecCoreGradient, mainCurve);
            for (int i = 0; i < b.branches.Length; i++)
                b.branches[i] = NewBoltLine("Bolt Branch", go.transform, CoreMat, 0.03f, ElecCoreGradient, Curve((0f, 1f), (1f, 0f)));
            b.Reshape();
            return b;
        }

        void Reshape()
        {
            float dist = Vector3.Distance(from, to);
            Jagged(pts, pts.Length, from, to, dist * 0.04f);
            SetLine(core, pts, pts.Length);
            SetLine(glow, pts, pts.Length);
            Vector3 down = (to - from).normalized;
            foreach (var br in branches)
            {
                if (Random.value < 0.25f) { br.positionCount = 0; continue; }
                int idx = Random.Range(4, pts.Length - 8);
                Vector3 s = pts[idx];
                Vector3 side = Vector3.ProjectOnPlane(Random.onUnitSphere, down).normalized;
                Vector3 e = s + (down + side * Random.Range(0.5f, 1.2f)).normalized * Random.Range(1.5f, 4f);
                int n = Random.Range(8, 14);
                Jagged(tmp, n, s, e, 0.3f);
                SetLine(br, tmp, n);
            }
        }

        void Update()
        {
            t += Time.deltaTime;
            regen -= Time.deltaTime;
            if (regen <= 0f && t < life * 0.6f) { regen = 0.05f; Reshape(); }
            float k = Mathf.Clamp01(t / life);
            float w = (1f - k) * Random.Range(0.7f, 1.3f);
            core.widthMultiplier = 0.07f * w;
            glow.widthMultiplier = 0.45f * w;
            foreach (var br in branches) br.widthMultiplier = 0.03f * w;
            if (t >= life) Destroy(gameObject);
        }
    }
}

namespace MagicTraining
{
    using UnityEngine;
    using static MagicTraining.MagicFX;

    public class StormStrike : MonoBehaviour
    {
        public int bolts = 4;
        public float spread = 1.6f;

        static readonly float[] times = { 0.3f, 0.45f, 0.6f, 0.72f, 0.84f, 0.95f, 1.05f, 1.15f, 1.25f };
        public bool big;

        Vector3 center;
        float skyY, t, floorY;
        int fired;
        Transform ignoreRoot;

        public static StormStrike Create(Vector3 ground, Vector3 normal, float skyY, Transform ignoreRoot)
        {
            var go = new GameObject("Storm Strike");
            go.transform.position = ground;
            var s = go.AddComponent<StormStrike>();
            s.center = ground;
            s.skyY = skyY;
            s.ignoreRoot = ignoreRoot;
            s.floorY = ground.y;
            if (normal.sqrMagnitude < 0.01f) normal = Vector3.up;
            MagicCircle.Flash(ground + normal * 0.03f, Quaternion.LookRotation(normal), 3.2f, MagicCircle.Element.Lightning, 0.9f, 4f);
            PlayAt(ZapClip, ground, 0.5f, 0.6f);
            return s;
        }

        void Update()
        {
            t += Time.deltaTime;
            while (fired < bolts && fired < times.Length && t >= times[fired])
            {
                Vector3 p = center;
                if (fired > 0)
                {
                    Vector2 r = Random.insideUnitCircle * spread;
                    p += new Vector3(r.x, 0f, r.y);
                }
                FindGround(p, ignoreRoot, floorY, out Vector3 g, out Vector3 n);
                Vector3 sky = new Vector3(g.x + Random.Range(-1.5f, 1.5f), skyY, g.z + Random.Range(-1.5f, 1.5f));
                StormBolt.Spawn(sky, g);
                SpawnThunderImpact(g, n, big ? 1f : (fired == 0 ? 0.9f : 0.65f), ignoreRoot);
                if (StormClouds.Current != null) StormClouds.Current.Flash(false);
                fired++;
            }
            if (fired >= bolts || fired >= times.Length) Destroy(gameObject, 0.1f);
        }
    }
}
