namespace MagicTraining
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEngine.Rendering;
    using MinMaxCurve = UnityEngine.ParticleSystem.MinMaxCurve;

    public static partial class MagicFX
    {
        public static readonly Color FireWhite    = new Color(1f, 0.96f, 0.85f);
        public static readonly Color FireYellow   = new Color(1f, 0.78f, 0.28f);
        public static readonly Color FireOrange   = new Color(1f, 0.42f, 0.07f);
        public static readonly Color FireRed      = new Color(0.85f, 0.12f, 0.04f);
        public static readonly Color ArcaneViolet = new Color(0.72f, 0.32f, 1f);
        public static readonly Color ArcanePink   = new Color(1f, 0.35f, 0.8f);
        public static readonly Color ArcaneCyan   = new Color(0.35f, 0.9f, 1f);
        public static readonly Color ArcaneGold   = new Color(1f, 0.86f, 0.45f);

        public static Color A(Color c, float a) { c.a = a; return c; }

        static Shader shader;
        static Shader GlowShader
        {
            get
            {
                if (shader != null) return shader;
                shader = Resources.Load<Shader>("MagicGlow");
                if (shader == null) shader = Shader.Find("MagicTraining/MagicGlow");
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                return shader;
            }
        }

        static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();

        static Material Mat(string key, Func<Texture2D> tex, float intensity, bool additive)
        {
            if (mats.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(GlowShader) { name = "MagicFX_" + key, hideFlags = HideFlags.DontSave };
            var t = tex();
            m.SetTexture("_MainTex", t);
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", t);
            m.SetFloat("_Intensity", intensity);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            m.renderQueue = additive ? 3010 : 3000;
            mats[key] = m;
            return m;
        }

        public static Material GlowMat  => Mat("Glow",  () => GlowTex,  2f,   true);
        public static Material CoreMat  => Mat("Core",  () => BeamTex,  4f,   true);
        public static Material BeamMat  => Mat("Beam",  () => BeamTex,  1.8f, true);
        public static Material FlameMat => Mat("Flame", () => FlameTex, 1.7f, true);
        public static Material SparkMat => Mat("Spark", () => SparkTex, 3f,   true);
        public static Material SmokeMat => Mat("Smoke", () => SmokeTex, 1f,   false);
        public static Material RuneMat  => Mat("Rune",  () => RuneTex,  2.4f, true);
        public static Material RingMat  => Mat("Ring",  () => RingTex,  2.2f, true);

        static readonly Dictionary<string, Texture2D> texs = new Dictionary<string, Texture2D>();
        static float S(float x) => Mathf.Clamp01(x);

        static Texture2D Tex(string key, int size, Func<float, float, float> alpha)
        {
            if (texs.TryGetValue(key, out var t) && t != null) return t;
            t = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = "MagicFX_" + key, wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear, hideFlags = HideFlags.DontSave
            };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f;
                float v = (y + 0.5f) / size * 2f - 1f;
                px[y * size + x] = new Color32(255, 255, 255, (byte)(S(alpha(u, v)) * 255f));
            }
            t.SetPixels32(px);
            t.Apply(true, true);
            texs[key] = t;
            return t;
        }

        public static Texture2D GlowTex => Tex("Glow", 64, (u, v) =>
        {
            float r = Mathf.Sqrt(u * u + v * v);
            return Mathf.Pow(S(1 - r), 2.4f) + 0.5f * Mathf.Pow(S(1 - r * 2.5f), 2f);
        });

        public static Texture2D BeamTex => Tex("Beam", 64, (u, v) =>
        {
            float across = Mathf.Pow(S(1 - Mathf.Abs(v)), 2.2f);
            float u01 = (u + 1f) * 0.5f;
            float ends = Mathf.SmoothStep(0, 1, u01 / 0.08f) * Mathf.SmoothStep(0, 1, (1 - u01) / 0.08f);
            return across * ends;
        });

        public static Texture2D SparkTex => Tex("Spark", 64, (u, v) =>
        {
            float r = Mathf.Sqrt(u * u + v * v);
            float core = Mathf.Pow(S(1 - r), 6f) * 1.2f + Mathf.Pow(S(1 - r), 2.5f) * 0.35f;
            float cross = (Mathf.Pow(S(1 - Mathf.Abs(u) * 9f), 2f) * S(1 - Mathf.Abs(v)) +
                           Mathf.Pow(S(1 - Mathf.Abs(v) * 9f), 2f) * S(1 - Mathf.Abs(u))) * 0.6f;
            return core + cross;
        });

        public static Texture2D FlameTex => Tex("Flame", 64, (u, v) =>
        {
            float r = Mathf.Sqrt(u * u + v * v);
            float n = Mathf.PerlinNoise(u * 2.5f + 11.3f, v * 2.5f + 5.7f);
            return Mathf.Pow(S(1 - r * (0.75f + 0.6f * n)), 1.6f);
        });

        public static Texture2D SmokeTex => Tex("Smoke", 64, (u, v) =>
        {
            float r = Mathf.Sqrt(u * u + v * v);
            float n = Mathf.PerlinNoise(u * 3f + 3.1f, v * 3f + 8.2f) * 0.65f +
                      Mathf.PerlinNoise(u * 7f + 1.7f, v * 7f + 2.9f) * 0.35f;
            return Mathf.Pow(S(1 - r), 1.4f) * (0.35f + 0.9f * n);
        });

        public static Texture2D RingTex => Tex("Ring", 128, (u, v) =>
        {
            float r = Mathf.Sqrt(u * u + v * v);
            if (r > 1f) return 0f;
            float d = r - 0.8f;
            return Mathf.Exp(-(d * d) / 0.0025f) + 0.45f * Mathf.Exp(-(d * d) / 0.03f) * (d < 0 ? 1f : 0.6f);
        });

        public static Texture2D RuneTex
        {
            get
            {
                if (texs.TryGetValue("Rune", out var cached) && cached != null) return cached;

                var segs = new List<Vector4>();
                var rnd = new System.Random(1337);
                const int glyphs = 14;
                for (int i = 0; i < glyphs; i++)
                {
                    float ang = i / (float)glyphs * Mathf.PI * 2f;
                    var rd = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                    var td = new Vector2(-rd.y, rd.x);
                    int strokes = 2 + rnd.Next(3);
                    for (int s = 0; s < strokes; s++)
                    {
                        int ax = rnd.Next(3) - 1, ay = rnd.Next(3);
                        int bx = Mathf.Clamp(ax + rnd.Next(3) - 1, -1, 1);
                        int by = Mathf.Clamp(ay + rnd.Next(3) - 1, 0, 2);
                        if (ax == bx && ay == by) by = ay == 2 ? 0 : 2;
                        Vector2 a = rd * (0.755f + ay * 0.06f) + td * (ax * 0.04f);
                        Vector2 b = rd * (0.755f + by * 0.06f) + td * (bx * 0.04f);
                        segs.Add(new Vector4(a.x, a.y, b.x, b.y));
                    }
                }
                for (int tri = 0; tri < 2; tri++)
                for (int k = 0; k < 3; k++)
                {
                    float a0 = Mathf.PI * 0.5f + tri * Mathf.PI + k * Mathf.PI * 2f / 3f;
                    float a1 = a0 + Mathf.PI * 2f / 3f;
                    segs.Add(new Vector4(Mathf.Cos(a0) * 0.68f, Mathf.Sin(a0) * 0.68f, Mathf.Cos(a1) * 0.68f, Mathf.Sin(a1) * 0.68f));
                }
                for (int k = 0; k < 48; k++)
                {
                    float a = k / 48f * Mathf.PI * 2f;
                    float r0 = k % 4 == 0 ? 0.885f : 0.905f;
                    segs.Add(new Vector4(Mathf.Cos(a) * r0, Mathf.Sin(a) * r0, Mathf.Cos(a) * 0.925f, Mathf.Sin(a) * 0.925f));
                }
                float[] rings = { 0.955f, 0.925f, 0.715f, 0.68f, 0.26f };
                var segArr = segs.ToArray();
                const int size = 256;
                return Tex("Rune", size, (u, v) =>
                {
                    float r = Mathf.Sqrt(u * u + v * v);
                    if (r > 1f) return 0f;
                    float d = 10f;
                    foreach (var rr in rings) d = Mathf.Min(d, Mathf.Abs(r - rr));
                    for (int i = 0; i < segArr.Length; i++) d = Mathf.Min(d, SegDist(u, v, segArr[i]));
                    float pxd = d * size * 0.5f;
                    float line = S(1.6f - pxd);
                    float glow = 0.4f * Mathf.Exp(-pxd / 4f);
                    return Mathf.Max(line, glow);
                });
            }
        }

        static float SegDist(float px, float py, Vector4 s)
        {
            float dx = s.z - s.x, dy = s.w - s.y;
            float t = Mathf.Clamp01(((px - s.x) * dx + (py - s.y) * dy) / (dx * dx + dy * dy + 1e-6f));
            float ex = s.x + dx * t - px, ey = s.y + dy * t - py;
            return Mathf.Sqrt(ex * ex + ey * ey);
        }

        public static AnimationCurve Curve(params (float t, float v)[] k)
        {
            var keys = new Keyframe[k.Length];
            for (int i = 0; i < k.Length; i++) keys[i] = new Keyframe(k[i].t, k[i].v);
            var c = new AnimationCurve(keys);
            for (int i = 0; i < keys.Length; i++) c.SmoothTangents(i, 0f);
            return c;
        }

        public static Gradient Grad(params (float t, Color c)[] k)
        {
            var g = new Gradient();
            var ck = new GradientColorKey[k.Length];
            var ak = new GradientAlphaKey[k.Length];
            for (int i = 0; i < k.Length; i++)
            {
                ck[i] = new GradientColorKey(k[i].c, k[i].t);
                ak[i] = new GradientAlphaKey(k[i].c.a, k[i].t);
            }
            g.SetKeys(ck, ak);
            return g;
        }

        public static Gradient FireOverLife => Grad(
            (0f, A(FireWhite, 0f)), (0.07f, A(FireYellow, 1f)), (0.3f, A(FireOrange, 0.9f)),
            (0.6f, A(FireRed, 0.55f)), (1f, A(ArcaneViolet, 0f)));

        public static Gradient FadeInOut => Grad(
            (0f, A(Color.white, 0f)), (0.15f, A(Color.white, 1f)), (0.7f, A(Color.white, 0.8f)), (1f, A(Color.white, 0f)));

        public static Gradient FadeOut => Grad((0f, A(Color.white, 1f)), (0.6f, A(Color.white, 0.85f)), (1f, A(Color.white, 0f)));

        public static Gradient ArcanePalette => Grad((0f, ArcaneGold), (0.35f, ArcanePink), (0.65f, ArcaneViolet), (1f, ArcaneCyan));
        public static Gradient FirePalette => Grad((0f, FireWhite), (0.5f, FireYellow), (1f, FireOrange));

        public static ParticleSystem.MinMaxGradient RandomFrom(Gradient g)
        {
            var m = new ParticleSystem.MinMaxGradient(g);
            m.mode = ParticleSystemGradientMode.RandomColor;
            return m;
        }

        public static ParticleSystem NewPS(string name, Transform parent, Material mat, bool worldSpace, int max = 300)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 1f;
            main.simulationSpace = worldSpace ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = max;
            main.startSpeed = 0f;
            main.startColor = Color.white;
            var sh = ps.shape; sh.enabled = false;
            var em = ps.emission; em.rateOverTime = 0f;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.maxParticleSize = 2f;
            return ps;
        }

        public static ParticleSystem OneShot(ParticleSystem ps, int count, float duration = 1f)
        {
            var main = ps.main; main.loop = false; main.duration = duration;
            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
            return ps;
        }

        public static void SetRate(ParticleSystem ps, float rate)
        {
            var em = ps.emission; em.rateOverTime = rate;
        }

        public static LineRenderer NewLine(string name, Transform parent, Material mat, Vector3 a, Vector3 b,
            AnimationCurve width, float widthMul, Gradient color, int segments = 8)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.positionCount = segments + 1;
            for (int i = 0; i <= segments; i++) lr.SetPosition(i, Vector3.Lerp(a, b, i / (float)segments));
            lr.widthCurve = width;
            lr.widthMultiplier = widthMul;
            lr.colorGradient = color;
            lr.sharedMaterial = mat;
            lr.alignment = LineAlignment.View;
            lr.textureMode = LineTextureMode.Stretch;
            lr.numCapVertices = 0;
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.receiveShadows = false;
            return lr;
        }

        public static TrailRenderer NewTrail(string name, Transform parent, Material mat, Vector3 localPos,
            float time, float widthMul, AnimationCurve width, Gradient color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var tr = go.AddComponent<TrailRenderer>();
            tr.time = time;
            tr.widthCurve = width;
            tr.widthMultiplier = widthMul;
            tr.colorGradient = color;
            tr.sharedMaterial = mat;
            tr.minVertexDistance = 0.03f;
            tr.alignment = LineAlignment.View;
            tr.textureMode = LineTextureMode.Stretch;
            tr.numCapVertices = 2;
            tr.shadowCastingMode = ShadowCastingMode.Off;
            tr.receiveShadows = false;
            tr.emitting = false;
            tr.autodestruct = false;
            return tr;
        }

        public static Renderer NewQuad(string name, Transform parent, Material mat)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = name;
            UnityEngine.Object.DestroyImmediate(q.GetComponent<Collider>());
            q.transform.SetParent(parent, false);
            var r = q.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return r;
        }

        static MaterialPropertyBlock mpb;
        public static void SetColor(Renderer r, Color c)
        {
            if (r == null) return;
            mpb ??= new MaterialPropertyBlock();
            r.GetPropertyBlock(mpb);
            mpb.SetColor("_Color", c);
            r.SetPropertyBlock(mpb);
        }

        public static Light NewLight(Transform parent, Vector3 localPos, Color c, float range, float intensity)
        {
            var go = new GameObject("Light");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = c;
            l.range = range;
            l.intensity = intensity;
            l.shadows = LightShadows.None;
            return l;
        }

        static Transform FxRoot(string name, Vector3 pos, Quaternion rot, float life)
        {
            var go = new GameObject(name);
            go.transform.SetPositionAndRotation(pos, rot);
            var fx = go.AddComponent<FxAutoDestroy>();
            fx.life = life;
            return go.transform;
        }

        static void FlashLight(Transform root, Color c, float range, float intensity, float life)
        {
            var l = NewLight(root, Vector3.zero, c, range, intensity);
            var fx = l.gameObject.AddComponent<FxAutoDestroy>();
            fx.life = life;
            fx.onUpdate = k => l.intensity = intensity * (1 - k) * (1 - k);
        }

        static void AnimatedRing(Transform root, Color color, float from, float to, float life, float delay = 0f)
        {
            var r = NewQuad("Shockwave", root, RingMat);
            r.transform.localScale = Vector3.one * from;
            SetColor(r, A(color, 0f));
            var fx = r.gameObject.AddComponent<FxAutoDestroy>();
            fx.life = life + delay;
            fx.onUpdate = k =>
            {
                float t = Mathf.Clamp01((k * (life + delay) - delay) / life);
                float e = 1f - (1f - t) * (1f - t) * (1f - t);
                r.transform.localScale = Vector3.one * Mathf.Lerp(from, to, e);
                SetColor(r, A(color, t <= 0f ? 0f : (1f - t) * (1f - t)));
            };
        }

        static ParticleSystem Flash(Transform root, Color c, float size, float life)
        {
            var ps = OneShot(NewPS("Flash", root, GlowMat, true, 4), 1, life + 0.1f);
            var m = ps.main;
            m.startLifetime = life;
            m.startSize = size;
            m.startColor = c;
            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = Grad((0f, A(Color.white, 1f)), (0.4f, A(Color.white, 0.6f)), (1f, A(Color.white, 0f)));
            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.size = new MinMaxCurve(1f, Curve((0f, 0.4f), (0.3f, 1f), (1f, 1.2f)));
            return ps;
        }

        static ParticleSystem Sparks(Transform root, int count, float speedMin, float speedMax, float lifeMin, float lifeMax, float gravity, bool collide, Gradient palette = null)
        {
            var ps = OneShot(NewPS("Sparks", root, SparkMat, true, count + 10), count);
            var m = ps.main;
            m.startLifetime = new MinMaxCurve(lifeMin, lifeMax);
            m.startSpeed = new MinMaxCurve(speedMin, speedMax);
            m.startSize = new MinMaxCurve(0.008f, 0.02f);
            m.gravityModifier = gravity;
            m.startColor = RandomFrom(palette ?? FirePalette);
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.035f;
            r.lengthScale = 1.2f;
            var lv = ps.limitVelocityOverLifetime; lv.enabled = true; lv.limit = 100f; lv.drag = 1.2f;
            var col = ps.colorOverLifetime; col.enabled = true; col.color = FadeOut;
            if (collide)
            {
                var co = ps.collision;
                co.enabled = true;
                co.type = ParticleSystemCollisionType.World;
                co.mode = ParticleSystemCollisionMode.Collision3D;
                co.bounce = 0.35f;
                co.dampen = 0.3f;
                co.lifetimeLoss = 0.15f;
                co.radiusScale = 0.5f;
                co.quality = ParticleSystemCollisionQuality.Medium;
            }
            return ps;
        }

        static ParticleSystem Motes(Transform root, int count, float speedMin, float speedMax, float lifeMin, float lifeMax, Gradient palette = null)
        {
            var ps = OneShot(NewPS("Arcane Motes", root, SparkMat, true, count + 10), count, 2f);
            var m = ps.main;
            m.startLifetime = new MinMaxCurve(lifeMin, lifeMax);
            m.startSpeed = new MinMaxCurve(speedMin, speedMax);
            m.startSize = new MinMaxCurve(0.014f, 0.034f);
            m.gravityModifier = -0.04f;
            m.startColor = RandomFrom(palette ?? ArcanePalette);
            var lv = ps.limitVelocityOverLifetime; lv.enabled = true; lv.limit = 100f; lv.drag = 2.2f;
            var nz = ps.noise; nz.enabled = true; nz.strength = 0.5f; nz.frequency = 1.5f; nz.scrollSpeed = 0.8f;
            var col = ps.colorOverLifetime; col.enabled = true; col.color = FadeInOut;
            var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new MinMaxCurve(1f, Curve((0f, 0.6f), (0.2f, 1.2f), (1f, 0.3f)));
            return ps;
        }

        public static void SpawnChargeFlash(Vector3 pos, Quaternion rot)
        {
            var root = FxRoot("Full Charge FX", pos, rot, 1.6f);
            AnimatedRing(root, ArcaneGold, 0.1f, 0.6f, 0.35f);
            AnimatedRing(root, ArcaneViolet, 0.05f, 0.45f, 0.4f, 0.08f);
            Flash(root, A(ArcaneGold, 0.9f), 0.45f, 0.18f).Play();
            var motes = Motes(root, 26, 0.6f, 1.8f, 0.6f, 1.2f);
            var sh = motes.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 0.12f; sh.radiusThickness = 0f;
            motes.Play();
            PlayAt(ChimeClip, pos, 0.6f, 1f);
        }

        public static void SpawnReleaseBurst(Vector3 pos, Vector3 dir, float power)
        {
            var root = FxRoot("Release FX", pos, Quaternion.LookRotation(dir), 1.5f);
            AnimatedRing(root, ArcaneGold, 0.08f, Mathf.Lerp(0.5f, 1f, power), 0.3f);
            AnimatedRing(root, FireOrange, 0.05f, Mathf.Lerp(0.35f, 0.7f, power), 0.35f, 0.05f);
            Flash(root, A(FireYellow, 0.9f), Mathf.Lerp(0.3f, 0.6f, power), 0.15f).Play();

            var sparks = Sparks(root, (int)Mathf.Lerp(20, 50, power), 2f, 7f, 0.2f, 0.5f, 0.3f, false);
            var sh = sparks.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 25f; sh.radius = 0.02f;
            sparks.Play();

            var motes = Motes(root, (int)Mathf.Lerp(12, 30, power), 1f, 2.5f, 0.5f, 1f);
            var msh = motes.shape; msh.enabled = true; msh.shapeType = ParticleSystemShapeType.Circle; msh.radius = 0.05f; msh.radiusThickness = 0f;
            motes.Play();

            FlashLight(root, new Color(1f, 0.6f, 0.3f), 3f, 2.5f, 0.25f);
            PlayAt(WhooshClip, pos, Mathf.Lerp(0.5f, 0.9f, power), Mathf.Lerp(1.1f, 0.9f, power));
        }

        public static void SpawnImpact(Vector3 pos, Vector3 normal, float power, float sizeMul = 1f)
        {
            power = Mathf.Clamp01(power);
            sizeMul = Mathf.Max(1f, sizeMul);
            float s = Mathf.Lerp(0.6f, 1.5f, power) * sizeMul;
            float cnt = Mathf.Lerp(0.6f, 1.5f, power) * Mathf.Sqrt(sizeMul);
            SpellTarget.HitArea(pos, 0.35f * sizeMul);
            if (normal.sqrMagnitude < 0.001f) normal = Vector3.up;
            var root = FxRoot("Fire Impact", pos + normal * 0.03f, Quaternion.LookRotation(normal), 3.5f);

            Flash(root, A(FireWhite, 1f), 1.6f * s, 0.22f).Play();

            var fire = OneShot(NewPS("Fireball", root, FlameMat, true, 400), (int)(45 * cnt));
            {
                var m = fire.main;
                m.startLifetime = new MinMaxCurve(0.35f, 0.85f);
                m.startSpeed = new MinMaxCurve(0.6f * s, 3.2f * s);
                m.startSize = new MinMaxCurve(0.12f * s, 0.32f * s);
                m.startRotation = new MinMaxCurve(0f, Mathf.PI * 2f);
                m.gravityModifier = -0.25f;
                var sh = fire.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Hemisphere; sh.radius = 0.08f;
                var lv = fire.limitVelocityOverLifetime; lv.enabled = true; lv.limit = 100f; lv.drag = 3f;
                var nz = fire.noise; nz.enabled = true; nz.strength = 0.4f; nz.frequency = 1.8f; nz.scrollSpeed = 1f;
                var col = fire.colorOverLifetime; col.enabled = true; col.color = FireOverLife;
                var sz = fire.sizeOverLifetime; sz.enabled = true; sz.size = new MinMaxCurve(1f, Curve((0f, 0.5f), (0.25f, 1f), (1f, 1.4f)));
                var rot = fire.rotationOverLifetime; rot.enabled = true; rot.z = new MinMaxCurve(-1.5f, 1.5f);
            }
            fire.Play();

            var sparks = Sparks(root, (int)(70 * cnt), 2.5f * s, 9f * s, 0.35f, 1.0f, 1.2f, true);
            { var sh = sparks.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Hemisphere; sh.radius = 0.05f; }
            sparks.Play();

            var motes = Motes(root, (int)(30 * cnt), 0.5f * Mathf.Sqrt(sizeMul), 2f * Mathf.Sqrt(sizeMul), 1f, 1.8f);
            { var sh = motes.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Hemisphere; sh.radius = 0.1f; }
            motes.Play();

            var smoke = OneShot(NewPS("Smoke", root, SmokeMat, true, 80), (int)(10 * cnt), 2f);
            {
                var m = smoke.main;
                m.startDelay = 0.05f;
                m.startLifetime = new MinMaxCurve(1.4f, 2.4f);
                m.startSpeed = new MinMaxCurve(0.3f, 1f);
                m.startSize = new MinMaxCurve(0.3f * s, 0.6f * s);
                m.startRotation = new MinMaxCurve(0f, Mathf.PI * 2f);
                m.startColor = new Color(0.2f, 0.15f, 0.2f, 0.55f);
                m.gravityModifier = -0.06f;
                var sh = smoke.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Hemisphere; sh.radius = 0.15f;
                var lv = smoke.limitVelocityOverLifetime; lv.enabled = true; lv.limit = 100f; lv.drag = 1.5f;
                var col = smoke.colorOverLifetime; col.enabled = true;
                col.color = Grad((0f, A(Color.white, 0f)), (0.2f, A(Color.white, 1f)), (1f, A(Color.white, 0f)));
                var sz = smoke.sizeOverLifetime; sz.enabled = true; sz.size = new MinMaxCurve(1f, Curve((0f, 0.6f), (1f, 1.8f)));
                var rot = smoke.rotationOverLifetime; rot.enabled = true; rot.z = new MinMaxCurve(-0.5f, 0.5f);
            }
            smoke.Play();

            var embers = OneShot(NewPS("Embers", root, SparkMat, true, 200), (int)(35 * cnt), 2f);
            {
                var m = embers.main;
                m.startLifetime = new MinMaxCurve(1.5f, 3f);
                m.startSpeed = new MinMaxCurve(0.5f, 2.5f);
                m.startSize = new MinMaxCurve(0.006f, 0.014f);
                m.gravityModifier = -0.03f;
                m.startColor = RandomFrom(Grad((0f, FireYellow), (1f, FireOrange)));
                var sh = embers.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Hemisphere; sh.radius = 0.1f;
                var lv = embers.limitVelocityOverLifetime; lv.enabled = true; lv.limit = 100f; lv.drag = 1.5f;
                var nz = embers.noise; nz.enabled = true; nz.strength = 0.8f; nz.frequency = 1.2f; nz.scrollSpeed = 0.6f;
                var col = embers.colorOverLifetime; col.enabled = true; col.color = FadeInOut;
            }
            embers.Play();

            AnimatedRing(root, FireOrange, 0.1f, 2.6f * s, 0.45f);
            AnimatedRing(root, ArcaneViolet, 0.1f, 1.8f * s, 0.6f, 0.08f);
            AnimatedRing(root, ArcaneGold, 0.05f, 1.1f * s, 0.35f, 0.03f);

            FlashLight(root, new Color(1f, 0.55f, 0.25f), 6f * s, 8f * Mathf.Min(s, 3f), 0.6f + 0.15f * (sizeMul - 1f));
            PlayAt(BoomClip, pos, Mathf.Lerp(0.6f, 1f, power), Mathf.Lerp(1.15f, 0.85f, power) / (1f + 0.1f * (sizeMul - 1f)));
            if (sizeMul > 1.5f) PlayAt(BoomClip, pos, 0.8f, 0.5f);
        }

        public static void SpawnFizzle(Vector3 pos, Quaternion rot)
        {
            var root = FxRoot("Fizzle FX", pos, rot, 2.5f);
            var sparks = Sparks(root, 18, 0.3f, 1.5f, 0.3f, 0.8f, 0.8f, false);
            { var sh = sparks.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.15f; }
            sparks.Play();
            var motes = Motes(root, 18, 0.2f, 0.8f, 0.6f, 1.2f);
            { var sh = motes.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.2f; }
            motes.Play();
            var smoke = OneShot(NewPS("Smoke", root, SmokeMat, true, 10), 6, 1.5f);
            {
                var m = smoke.main;
                m.startLifetime = new MinMaxCurve(0.8f, 1.4f);
                m.startSpeed = new MinMaxCurve(0.1f, 0.3f);
                m.startSize = new MinMaxCurve(0.12f, 0.25f);
                m.startColor = new Color(0.35f, 0.3f, 0.4f, 0.4f);
                m.gravityModifier = -0.05f;
                var sh = smoke.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.15f;
                var col = smoke.colorOverLifetime; col.enabled = true; col.color = FadeInOut;
                var sz = smoke.sizeOverLifetime; sz.enabled = true; sz.size = new MinMaxCurve(1f, Curve((0f, 0.6f), (1f, 1.6f)));
            }
            smoke.Play();
            PlayAt(FizzleClip, pos, 0.5f, 1f);
        }

        static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        const int SR = 44100;

        static AudioClip Synth(string name, float seconds, Func<float, float> f)
        {
            if (clips.TryGetValue(name, out var c) && c != null) return c;
            int n = (int)(SR * seconds);
            var data = new float[n];
            float peak = 0.0001f;
            for (int i = 0; i < n; i++) { data[i] = f(i / (float)SR); peak = Mathf.Max(peak, Mathf.Abs(data[i])); }
            float norm = 0.9f / peak;
            for (int i = 0; i < n; i++) data[i] *= norm;
            c = AudioClip.Create(name, n, 1, SR, false);
            c.SetData(data, 0);
            c.hideFlags = HideFlags.DontSave;
            clips[name] = c;
            return c;
        }

        const float TAU = Mathf.PI * 2f;

        public static AudioClip ChargeLoopClip
        {
            get
            {
                var rnd = new System.Random(3);
                float lp = 0f, crack = 0f;
                return Synth("MagicCharge", 2f, t =>
                {
                    float hum = 0.5f * Mathf.Sin(TAU * 110f * t)
                              + 0.25f * Mathf.Sin(TAU * 220f * t + 0.8f * Mathf.Sin(TAU * 2f * t))
                              + 0.18f * Mathf.Sin(TAU * 330f * t) * (0.6f + 0.4f * Mathf.Sin(TAU * 4f * t));
                    float n = (float)rnd.NextDouble() * 2f - 1f;
                    lp += (n - lp) * 0.06f;
                    if (rnd.NextDouble() < 0.0015) crack = 1f;
                    crack *= 0.92f;
                    float cr = crack * ((float)rnd.NextDouble() * 2f - 1f) * 0.4f;
                    return hum * 0.55f + lp * 1.6f + cr;
                });
            }
        }

        public static AudioClip WhooshClip
        {
            get
            {
                var rnd = new System.Random(5);
                float lp1 = 0f, lp2 = 0f, ph = 0f;
                const float len = 0.8f;
                return Synth("MagicWhoosh", len, t =>
                {
                    float c = Mathf.Lerp(0.35f, 0.03f, t / len);
                    float n = (float)rnd.NextDouble() * 2f - 1f;
                    lp1 += (n - lp1) * c;
                    lp2 += (lp1 - lp2) * c;
                    float env = S(t / 0.03f) * Mathf.Exp(-t * 4.5f);
                    ph += TAU * (500f + 1400f * t / len) / SR;
                    float zing = Mathf.Sin(ph) * 0.25f * Mathf.Exp(-t * 5f);
                    return (lp1 - lp2) * 2.5f * env + zing;
                });
            }
        }

        public static AudioClip BoomClip
        {
            get
            {
                var rnd = new System.Random(7);
                float lp = 0f, ph = 0f, crack = 0f;
                return Synth("MagicBoom", 1.6f, t =>
                {
                    float f = 35f + 80f * Mathf.Exp(-t * 2.5f);
                    ph += TAU * f / SR;
                    float body = Mathf.Sin(ph) * Mathf.Exp(-t * 3f);
                    float n = (float)rnd.NextDouble() * 2f - 1f;
                    lp += (n - lp) * 0.08f;
                    float rumble = lp * Mathf.Exp(-t * 4f) * 2.2f;
                    if (rnd.NextDouble() < 0.004 * Mathf.Exp(-t * 2f)) crack = 1f;
                    crack *= 0.93f;
                    float cr = crack * ((float)rnd.NextDouble() * 2f - 1f) * 0.5f;
                    return S(t / 0.005f) * (body * 0.9f + rumble + cr);
                });
            }
        }

        public static AudioClip FizzleClip
        {
            get
            {
                var rnd = new System.Random(11);
                float lp = 0f, ph = 0f;
                const float len = 0.5f;
                return Synth("MagicFizzle", len, t =>
                {
                    float n = (float)rnd.NextDouble() * 2f - 1f;
                    lp += (n - lp) * 0.1f;
                    float hiss = (n - lp) * Mathf.Exp(-t * 8f) * 0.4f;
                    ph += TAU * (900f - 600f * t / len) / SR;
                    return hiss + Mathf.Sin(ph) * 0.2f * Mathf.Exp(-t * 6f);
                });
            }
        }

        public static AudioClip ChimeClip => Synth("MagicChime", 1.4f, t =>
        {
            float vib = 1f + 0.004f * Mathf.Sin(TAU * 6f * t);
            return S(t / 0.004f) * (
                0.5f * Mathf.Sin(TAU * 1046.5f * vib * t) * Mathf.Exp(-t * 3f) +
                0.35f * Mathf.Sin(TAU * 1568f * vib * t) * Mathf.Exp(-t * 3.5f) +
                0.25f * Mathf.Sin(TAU * 2093f * vib * t) * Mathf.Exp(-t * 4.5f) +
                0.15f * Mathf.Sin(TAU * 2637f * vib * t) * Mathf.Exp(-t * 6f));
        });

        public static AudioSource PlayAt(AudioClip clip, Vector3 pos, float volume, float pitch)
        {
            var go = new GameObject("SFX " + clip.name);
            go.transform.position = pos;
            var s = go.AddComponent<AudioSource>();
            s.clip = clip;
            s.volume = volume;
            s.pitch = pitch;
            s.spatialBlend = 1f;
            s.minDistance = 1.5f;
            s.maxDistance = 40f;
            s.dopplerLevel = 0f;
            s.Play();
            UnityEngine.Object.Destroy(go, clip.length / Mathf.Max(0.1f, pitch) + 0.1f);
            return s;
        }
    }
}

namespace MagicTraining
{
    using System.Collections.Generic;
    using UnityEngine;

    public static partial class MagicFX
    {
        public static bool CastIgnoring(Vector3 from, Vector3 dir, float dist, float radius, Transform ignoreRoot,
            out Vector3 point, out Vector3 normal, out Collider collider)
        {
            point = normal = Vector3.zero;
            collider = null;
            if (dist <= 0f) return false;
            var hits = Physics.SphereCastAll(from, radius, dir, dist, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            foreach (var h in hits)
            {
                if (ignoreRoot != null && h.collider.transform.IsChildOf(ignoreRoot)) continue;
                if (h.distance >= best) continue;
                best = h.distance;
                collider = h.collider;
                if (h.distance <= 0f && h.point == Vector3.zero) { point = from; normal = -dir; }
                else { point = h.point; normal = h.normal; }
            }
            return collider != null;
        }

        public static bool FindGround(Vector3 p, Transform ignoreRoot, float fallbackY, out Vector3 point, out Vector3 normal)
        {
            point = new Vector3(p.x, fallbackY, p.z);
            normal = Vector3.up;
            var hits = Physics.RaycastAll(p + Vector3.up * 1.5f, Vector3.down, 5f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            bool found = false;
            foreach (var h in hits)
            {
                if (ignoreRoot != null && h.collider.transform.IsChildOf(ignoreRoot)) continue;
                if (h.rigidbody != null && !h.rigidbody.isKinematic) continue;
                if (h.distance >= best) continue;
                best = h.distance;
                point = h.point;
                normal = h.normal;
                found = true;
            }
            return found;
        }

        public static void PushBodies(Vector3 point, float radius, float force, Transform ignoreRoot)
        {
            var done = new HashSet<Rigidbody>();
            foreach (var c in Physics.OverlapSphere(point, radius, ~0, QueryTriggerInteraction.Ignore))
            {
                var rb = c.attachedRigidbody;
                if (rb == null || rb.isKinematic || !done.Add(rb)) continue;
                if (ignoreRoot != null && rb.transform.IsChildOf(ignoreRoot)) continue;
                rb.AddExplosionForce(force, point, radius, 0.4f, ForceMode.Impulse);
            }
        }

        public static void DetachPS(ParticleSystem ps, float destroyAfter)
        {
            if (ps == null) return;
            ps.transform.SetParent(null, true);
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            Object.Destroy(ps.gameObject, destroyAfter);
        }

        public static void DetachTrail(TrailRenderer tr)
        {
            if (tr == null) return;
            tr.transform.SetParent(null, true);
            tr.emitting = false;
            Object.Destroy(tr.gameObject, tr.time + 0.1f);
        }

        public static AudioClip FireLoopClip
        {
            get
            {
                var rnd = new System.Random(13);
                float lp1 = 0f, lp2 = 0f, crack = 0f;
                return Synth("FireLoop", 2f, t =>
                {
                    float n = (float)rnd.NextDouble() * 2f - 1f;
                    lp1 += (n - lp1) * 0.05f;
                    lp2 += (lp1 - lp2) * 0.05f;
                    float roar = lp1 * 2.2f + lp2 * 1.5f;
                    float low = 0.22f * Mathf.Sin(TAU * 70f * t) * (0.7f + 0.3f * Mathf.Sin(TAU * 3f * t));
                    if (rnd.NextDouble() < 0.002) crack = 1f;
                    crack *= 0.9f;
                    return roar + low + crack * n * 0.5f;
                });
            }
        }
    }
}

namespace MagicTraining
{
    using System.Collections.Generic;
    using UnityEngine;
    using MinMaxCurve = UnityEngine.ParticleSystem.MinMaxCurve;

    public static partial class MagicFX
    {
        public static readonly Color WaterFoam  = new Color(0.9f, 0.97f, 1f);
        public static readonly Color WaterLight = new Color(0.45f, 0.85f, 1f);
        public static readonly Color WaterTeal  = new Color(0.2f, 0.9f, 0.85f);
        public static readonly Color WaterDeep  = new Color(0.1f, 0.45f, 0.9f);
        public static readonly Color Gold       = new Color(1f, 0.78f, 0.34f);

        public static Gradient WaterPalette => Grad((0f, WaterFoam), (0.4f, WaterLight), (0.75f, WaterTeal), (1f, WaterDeep));

        static Shader waterShader;
        static Material waterMat, geyserMat, orbMat, gemMat, goldMat, deepWaterMat, sheetMat, poolMat, swirlMat, curtainMat, whiteWaterMat;

        static Shader WaterShader
        {
            get
            {
                if (waterShader != null) return waterShader;
                waterShader = Resources.Load<Shader>("MagicWater");
                if (waterShader == null) waterShader = Shader.Find("MagicTraining/Water");
                return waterShader;
            }
        }

        static Material NewWaterMat(string name, float foamStart, float scroll, Color shallow, Color deep, float wobble = 0.02f, int queue = 3004)
        {
            if (WaterShader == null) return IceMat;
            var m = new Material(WaterShader) { name = name, hideFlags = HideFlags.DontSave, renderQueue = queue };
            m.SetFloat("_WaveAmp", wobble);
            m.SetFloat("_FoamStart", foamStart);
            m.SetFloat("_ScrollSpeed", scroll);
            m.SetColor("_Color", shallow);
            m.SetColor("_DeepColor", deep);
            return m;
        }

        public static Material WaterMat => waterMat != null ? waterMat :
            (waterMat = NewWaterMat("MagicFX_Water", 0.66f, 1.5f, new Color(0.15f, 0.62f, 0.98f, 0.8f), new Color(0.02f, 0.18f, 0.55f, 0.92f), 0.03f, 3005));
        public static Material DeepWaterMat => deepWaterMat != null ? deepWaterMat :
            (deepWaterMat = NewWaterMat("MagicFX_DeepWater", 2f, 1f, new Color(0.05f, 0.35f, 0.85f, 0.8f), new Color(0.01f, 0.08f, 0.35f, 0.95f), 0.02f, 3003));
        public static Material SheetMat => sheetMat != null ? sheetMat :
            (sheetMat = NewWaterMat("MagicFX_WaterSheet", 0.88f, 2.5f, new Color(0.2f, 0.65f, 1f, 0.75f), new Color(0.04f, 0.25f, 0.65f, 0.6f), 0.015f, 3002));
        public static Material PoolMat => poolMat != null ? poolMat :
            (poolMat = NewWaterMat("MagicFX_WaterPool", 0.8f, 2f, new Color(0.2f, 0.68f, 1f, 0.8f), new Color(0.04f, 0.25f, 0.65f, 0.55f), 0.015f, 3002));
        public static Material GeyserMat => geyserMat != null ? geyserMat :
            (geyserMat = NewWaterMat("MagicFX_Geyser", 0.95f, 4f, new Color(0.25f, 0.72f, 1f, 0.75f), new Color(0.03f, 0.25f, 0.7f, 0.9f), 0.04f, 3005));
        public static Material SwirlMat => swirlMat != null ? swirlMat :
            (swirlMat = NewWaterMat("MagicFX_WaterSwirl", 0.9f, 6f, new Color(0.45f, 0.85f, 1f, 0.45f), new Color(0.1f, 0.45f, 0.95f, 0.35f), 0.05f, 3006));
        public static Material OrbMat => orbMat != null ? orbMat :
            (orbMat = NewWaterMat("MagicFX_WaterOrb", 2f, 1f, new Color(0.3f, 0.9f, 1f, 0.75f), new Color(0.03f, 0.3f, 0.8f, 0.92f), 0.004f));
        public static Material CurtainMat => curtainMat != null ? curtainMat :
            (curtainMat = NewWaterMat("MagicFX_WaterCurtain", 0.62f, 5f, new Color(0.3f, 0.78f, 1f, 0.7f), new Color(0.08f, 0.4f, 0.9f, 0.55f), 0.03f, 3006));
        public static Material WhiteWaterMat
        {
            get
            {
                if (whiteWaterMat != null) return whiteWaterMat;
                whiteWaterMat = NewWaterMat("MagicFX_WhiteWater", 0.3f, 3f, new Color(0.6f, 0.9f, 1f, 0.75f), new Color(0.2f, 0.6f, 1f, 0.5f), 0.04f, 3007);
                whiteWaterMat.SetColor("_FoamColor", new Color(0.95f, 0.99f, 1f, 0.92f));
                return whiteWaterMat;
            }
        }

        public static Material DropMat => Mat("Drop", () => GlowTex, 1.3f, false);
        public static Gradient DropPalette => Grad((0f, WaterFoam), (0.3f, WaterLight), (0.7f, new Color(0.15f, 0.55f, 1f)), (1f, WaterDeep));

        public static Material GemMat
        {
            get
            {
                if (gemMat != null) return gemMat;
                var sh = Resources.Load<Shader>("MagicIce");
                if (sh == null) sh = Shader.Find("MagicTraining/Ice");
                if (sh == null) return GlowMat;
                gemMat = new Material(sh) { name = "MagicFX_Aquamarine", hideFlags = HideFlags.DontSave, renderQueue = 3005 };
                gemMat.SetColor("_Color", new Color(0.2f, 0.85f, 0.9f, 0.45f));
                gemMat.SetColor("_RimColor", new Color(0.6f, 1f, 0.95f, 1f));
                gemMat.SetFloat("_Intensity", 2f);
                return gemMat;
            }
        }

        public static Material GoldMat
        {
            get
            {
                if (goldMat != null) return goldMat;
                var sh = Shader.Find("Universal Render Pipeline/Lit");
                if (sh == null) sh = Shader.Find("Universal Render Pipeline/Simple Lit");
                if (sh == null) return GlowMat;
                goldMat = new Material(sh) { name = "MagicFX_Gold", hideFlags = HideFlags.DontSave };
                goldMat.SetColor("_BaseColor", Gold);
                if (goldMat.HasProperty("_Metallic")) goldMat.SetFloat("_Metallic", 1f);
                if (goldMat.HasProperty("_Smoothness")) goldMat.SetFloat("_Smoothness", 0.78f);
                return goldMat;
            }
        }

        public static void SetWater(Renderer r, float fade)
        {
            if (r == null) return;
            mpb ??= new MaterialPropertyBlock();
            r.GetPropertyBlock(mpb);
            mpb.SetFloat("_Fade", fade);
            r.SetPropertyBlock(mpb);
        }

        static Mesh waveMesh, cylinderMesh, tridentGold, tridentGems;

        public static Mesh WaveMesh
        {
            get
            {
                if (waveMesh != null) return waveMesh;
                Vector2[] prof =
                {
                    new Vector2(-1.0f, 0f), new Vector2(-0.75f, 0.08f), new Vector2(-0.5f, 0.25f), new Vector2(-0.25f, 0.5f),
                    new Vector2(-0.05f, 0.75f), new Vector2(0.1f, 0.92f), new Vector2(0.25f, 1f), new Vector2(0.38f, 0.97f),
                    new Vector2(0.45f, 0.88f), new Vector2(0.42f, 0.76f), new Vector2(0.33f, 0.68f)
                };
                const int nx = 18;
                int np = prof.Length;
                var verts = new Vector3[(nx + 1) * np];
                var uvs = new Vector2[verts.Length];
                var tris = new List<int>();
                for (int i = 0; i <= nx; i++)
                {
                    float u = i / (float)nx;
                    float x = u - 0.5f;
                    float f = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * u)), 0.5f);
                    for (int k = 0; k < np; k++)
                    {
                        Vector2 p = prof[k];
                        verts[i * np + k] = new Vector3(x, p.y * f, p.x * Mathf.Lerp(0.5f, 1f, f));
                        uvs[i * np + k] = new Vector2(u, k / (np - 1f));
                    }
                }
                for (int i = 0; i < nx; i++)
                for (int k = 0; k < np - 1; k++)
                {
                    int a = i * np + k, b = a + 1, c = a + np, d = c + 1;
                    tris.Add(a); tris.Add(b); tris.Add(d);
                    tris.Add(a); tris.Add(d); tris.Add(c);
                }
                waveMesh = new Mesh { name = "WaterWave", hideFlags = HideFlags.DontSave };
                waveMesh.vertices = verts;
                waveMesh.uv = uvs;
                waveMesh.SetTriangles(tris, 0);
                waveMesh.RecalculateNormals();
                waveMesh.RecalculateBounds();
                return waveMesh;
            }
        }

        public static Mesh CylinderMesh
        {
            get
            {
                if (cylinderMesh != null) return cylinderMesh;
                const int seg = 24, rows = 6;
                var verts = new List<Vector3>();
                var norms = new List<Vector3>();
                var uvs = new List<Vector2>();
                var tris = new List<int>();
                for (int r = 0; r <= rows; r++)
                for (int j = 0; j <= seg; j++)
                {
                    float a = j / (float)seg * Mathf.PI * 2f;
                    float y = r / (float)rows;
                    var n = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    verts.Add(n + Vector3.up * y);
                    norms.Add(n);
                    uvs.Add(new Vector2(j / (float)seg * 3f, y));
                }
                for (int r = 0; r < rows; r++)
                for (int j = 0; j < seg; j++)
                {
                    int a = r * (seg + 1) + j, b = a + 1, c = a + seg + 1, d = c + 1;
                    tris.Add(a); tris.Add(c); tris.Add(d);
                    tris.Add(a); tris.Add(d); tris.Add(b);
                }
                cylinderMesh = new Mesh { name = "WaterColumn", hideFlags = HideFlags.DontSave };
                cylinderMesh.SetVertices(verts);
                cylinderMesh.SetNormals(norms);
                cylinderMesh.SetUVs(0, uvs);
                cylinderMesh.SetTriangles(tris, 0);
                cylinderMesh.RecalculateBounds();
                return cylinderMesh;
            }
        }

        public static Mesh TridentGoldMesh
        {
            get
            {
                if (tridentGold != null) return tridentGold;
                var mb = new MeshBuilder();
                Vector3 Z(float z) => new Vector3(0f, 0f, z);
                Vector3 X(float x, float z) => new Vector3(x, 0f, z);

                mb.Spike(Z(0.02f), Z(-0.1f), 0.03f);
                mb.Prism(Z(0f), Z(0.05f), 0.03f, 0.022f, 6);
                mb.Prism(Z(0.05f), Z(1.08f), 0.016f, 0.016f, 8);
                for (int i = 0; i < 4; i++) mb.Prism(Z(0.42f + i * 0.09f), Z(0.45f + i * 0.09f), 0.021f, 0.021f, 6, 0.3f);
                mb.Prism(Z(1.06f), Z(1.09f), 0.03f, 0.045f, 6);
                mb.Prism(Z(1.19f), Z(1.22f), 0.045f, 0.03f, 6);
                for (int k = 0; k < 4; k++)
                {
                    float a = k * Mathf.PI * 0.5f + Mathf.PI * 0.25f;
                    var d = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                    mb.Prism(Z(1.08f) + d * 0.035f, Z(1.2f) + d * 0.035f, 0.007f, 0.007f, 4);
                }
                mb.Prism(Z(1.22f), Z(1.27f), 0.03f, 0.022f, 6);
                mb.Prism(X(-0.14f, 1.27f), X(0.14f, 1.27f), 0.018f, 0.018f, 6);
                mb.Prism(X(-0.14f, 1.27f), X(-0.16f, 1.34f), 0.018f, 0.015f, 6);
                mb.Prism(X(0.14f, 1.27f), X(0.16f, 1.34f), 0.018f, 0.015f, 6);
                mb.Spike(X(-0.13f, 1.27f), X(-0.2f, 1.24f), 0.012f);
                mb.Spike(X(0.13f, 1.27f), X(0.2f, 1.24f), 0.012f);
                mb.Prism(Z(1.26f), Z(1.55f), 0.014f, 0.012f, 6);
                mb.Prism(X(-0.16f, 1.34f), X(-0.155f, 1.5f), 0.014f, 0.012f, 6);
                mb.Prism(X(0.16f, 1.34f), X(0.155f, 1.5f), 0.014f, 0.012f, 6);
                mb.Spike(Z(1.53f), X(-0.05f, 1.47f), 0.008f);
                mb.Spike(Z(1.53f), X(0.05f, 1.47f), 0.008f);
                mb.Spike(X(-0.155f, 1.49f), X(-0.11f, 1.44f), 0.007f);
                mb.Spike(X(0.155f, 1.49f), X(0.11f, 1.44f), 0.007f);
                tridentGold = mb.Build("TridentGold");
                return tridentGold;
            }
        }

        public static Mesh TridentGemMesh
        {
            get
            {
                if (tridentGems != null) return tridentGems;
                var mb = new MeshBuilder();
                mb.Prism(new Vector3(0f, 0f, 1.52f), new Vector3(0f, 0f, 1.58f), 0.012f, 0.026f, 4);
                mb.Spike(new Vector3(0f, 0f, 1.58f), new Vector3(0f, 0f, 1.8f), 0.026f);
                for (int s = -1; s <= 1; s += 2)
                {
                    mb.Prism(new Vector3(0.155f * s, 0f, 1.48f), new Vector3(0.155f * s, 0f, 1.53f), 0.011f, 0.022f, 4);
                    mb.Spike(new Vector3(0.155f * s, 0f, 1.53f), new Vector3(0.145f * s, 0f, 1.7f), 0.022f);
                }
                tridentGems = mb.Build("TridentGems");
                return tridentGems;
            }
        }

        static Mesh sheetMesh, crownMesh, poolMesh, curtainMesh, skirtMesh;

        public static Mesh WaveCurtainMesh => curtainMesh != null ? curtainMesh : (curtainMesh = Extrude("WaveCurtain",
            new[] { new Vector2(0.34f, 0.72f), new Vector2(0.43f, 0.55f), new Vector2(0.5f, 0.35f), new Vector2(0.56f, 0.16f), new Vector2(0.6f, 0f) },
            new[] { 0.07f, 0.3f, 0.55f, 0.8f, 1f }));

        public static Mesh WaveSkirtMesh => skirtMesh != null ? skirtMesh : (skirtMesh = Extrude("WaveSkirt",
            new[] { new Vector2(0.38f, 0f), new Vector2(0.46f, 0.13f), new Vector2(0.58f, 0.2f), new Vector2(0.72f, 0.12f), new Vector2(0.8f, 0f) },
            new[] { 0f, 0.55f, 1f, 0.55f, 0f }));

        static Mesh Extrude(string name, Vector2[] prof, float[] v)
        {
            const int nx = 18;
            int np = prof.Length;
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            for (int i = 0; i <= nx; i++)
            {
                float u = i / (float)nx;
                float f = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * u)), 0.5f);
                for (int k = 0; k < np; k++)
                {
                    verts.Add(new Vector3(u - 0.5f, prof[k].y * f, prof[k].x * Mathf.Lerp(0.5f, 1f, f)));
                    uvs.Add(new Vector2(u * 2f, v[k]));
                }
            }
            for (int i = 0; i < nx; i++)
            for (int k = 0; k < np - 1; k++)
            {
                int a = i * np + k, b = a + 1, c = a + np, d = c + 1;
                tris.Add(a); tris.Add(b); tris.Add(d);
                tris.Add(a); tris.Add(d); tris.Add(c);
            }
            return BuildMesh(name, verts, uvs, tris);
        }

        public static Mesh WaterSheetMesh
        {
            get
            {
                if (sheetMesh != null) return sheetMesh;
                const int nx = 8, nz = 14;
                var verts = new List<Vector3>();
                var uvs = new List<Vector2>();
                var tris = new List<int>();
                for (int k = 0; k <= nz; k++)
                for (int i = 0; i <= nx; i++)
                {
                    float u = i / (float)nx, zt = k / (float)nz;
                    float edge = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * u)), 0.3f);
                    verts.Add(new Vector3(u - 0.5f, 0.02f * edge, -zt));
                    uvs.Add(new Vector2(u * 2f, (1f - zt) * Mathf.Lerp(0.5f, 1f, edge)));
                }
                for (int k = 0; k < nz; k++)
                for (int i = 0; i < nx; i++)
                {
                    int a = k * (nx + 1) + i, b = a + 1, c = a + nx + 1, d = c + 1;
                    tris.Add(a); tris.Add(b); tris.Add(d);
                    tris.Add(a); tris.Add(d); tris.Add(c);
                }
                sheetMesh = BuildMesh("WaterSheet", verts, uvs, tris);
                return sheetMesh;
            }
        }

        public static Mesh WaterCrownMesh => crownMesh != null ? crownMesh : (crownMesh = Lathe("WaterCrown",
            new[] { new Vector2(0f, 0.15f), new Vector2(0.3f, 0.25f), new Vector2(0.6f, 0.2f), new Vector2(0.85f, 0f), new Vector2(1f, -0.4f), new Vector2(1.05f, -1f) },
            new[] { 1f, 0.95f, 0.85f, 0.6f, 0.3f, 0f }));

        public static Mesh WaterPoolMesh => poolMesh != null ? poolMesh : (poolMesh = Lathe("WaterPool",
            new[] { new Vector2(0f, 0.01f), new Vector2(0.2f, 0.01f), new Vector2(0.45f, 0.01f), new Vector2(0.7f, 0.008f), new Vector2(0.88f, 0.005f), new Vector2(1f, 0f) },
            new[] { 1f, 0.85f, 0.65f, 0.4f, 0.18f, 0f }));

        public static Mesh LatheMesh(string name, Vector2[] profile, float[] v) => Lathe(name, profile, v);

        static Mesh Lathe(string name, Vector2[] profile, float[] v)
        {
            const int seg = 32;
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            int np = profile.Length;
            for (int k = 0; k < np; k++)
            for (int j = 0; j <= seg; j++)
            {
                float a = j / (float)seg * Mathf.PI * 2f;
                verts.Add(new Vector3(Mathf.Cos(a) * profile[k].x, profile[k].y, Mathf.Sin(a) * profile[k].x));
                uvs.Add(new Vector2(j / (float)seg * 4f, v[k]));
            }
            for (int k = 0; k < np - 1; k++)
            for (int j = 0; j < seg; j++)
            {
                int a = k * (seg + 1) + j, b = a + 1, c = a + seg + 1, d = c + 1;
                tris.Add(a); tris.Add(c); tris.Add(d);
                tris.Add(a); tris.Add(d); tris.Add(b);
            }
            return BuildMesh(name, verts, uvs, tris);
        }

        static Mesh BuildMesh(string name, List<Vector3> verts, List<Vector2> uvs, List<int> tris)
        {
            var m = new Mesh { name = name, hideFlags = HideFlags.DontSave };
            m.SetVertices(verts);
            m.SetUVs(0, uvs);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        public static Renderer NewWaterMesh(string name, Transform parent, Mesh mesh, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return r;
        }

        public static ParticleSystem WaterDrops(Transform root, int count, float speedMin, float speedMax, float sizeMin, float sizeMax)
        {
            var drops = Sparks(root, count, speedMin, speedMax, 0.5f, 1.2f, 1f, true, DropPalette);
            var m = drops.main; m.startSize = new MinMaxCurve(sizeMin, sizeMax);
            drops.GetComponent<ParticleSystemRenderer>().sharedMaterial = DropMat;
            return drops;
        }

        public static void SpawnSplash(Vector3 pos, float size, float volume = 0.8f)
        {
            var root = FxRoot("Splash", pos, Quaternion.LookRotation(Vector3.up), 3f);
            var drops = WaterDrops(root, (int)(40 * size) + 10, 1.5f * size, 5f * size, 0.025f, 0.06f);
            { var sh = drops.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 40f; sh.radius = 0.2f * size; }
            drops.Play();

            var crownR = NewWaterMesh("Splash Crown", null, CylinderMesh, GeyserMat);
            crownR.transform.position = pos;
            var poolR = NewWaterMesh("Splash Pool", null, WaterPoolMesh, PoolMat);
            poolR.transform.position = pos + Vector3.up * 0.02f;
            crownR.transform.SetParent(root, true);
            poolR.transform.SetParent(root, true);
            var anim = crownR.gameObject.AddComponent<FxAutoDestroy>();
            anim.life = 0.9f;
            anim.onUpdate = k =>
            {
                float e = 1f - (1f - k) * (1f - k);
                float r = Mathf.Lerp(0.25f, 1.3f, e) * size;
                float h = Mathf.Sin(Mathf.Min(1f, k * 1.6f) * Mathf.PI) * 0.7f * size;
                crownR.transform.localScale = new Vector3(r, Mathf.Max(0.01f, h), r);
                SetWater(crownR, 1f - k * k);
                float pr = Mathf.Lerp(0.2f, 1.6f, Mathf.Min(1f, k * 1.5f)) * size;
                poolR.transform.localScale = new Vector3(pr, 1f, pr);
                SetWater(poolR, k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f);
            };

            var foam = OneShot(NewPS("Foam", root, SmokeMat, true, 40), (int)(10 * size) + 4, 2f);
            {
                var m = foam.main;
                m.startLifetime = new MinMaxCurve(0.8f, 1.6f);
                m.startSpeed = new MinMaxCurve(0.5f * size, 2f * size);
                m.startSize = new MinMaxCurve(0.25f * size, 0.6f * size);
                m.startRotation = new MinMaxCurve(0f, TAU);
                m.startColor = new Color(0.92f, 0.97f, 1f, 0.45f);
                m.gravityModifier = 0.2f;
                var sh = foam.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 60f; sh.radius = 0.2f * size;
                var lv = foam.limitVelocityOverLifetime; lv.enabled = true; lv.limit = 100f; lv.drag = 2f;
                var col = foam.colorOverLifetime; col.enabled = true; col.color = FadeInOut;
                var sz = foam.sizeOverLifetime; sz.enabled = true; sz.size = new MinMaxCurve(1f, Curve((0f, 0.6f), (1f, 1.6f)));
            }
            foam.Play();

            AnimatedRing(root, WaterLight, 0.1f, 2f * size, 0.45f);
            AnimatedRing(root, WaterFoam, 0.05f, 1.3f * size, 0.35f, 0.05f);
            PlayAt(SplashClip, pos, volume, Random.Range(0.85f, 1.15f) / Mathf.Max(0.6f, Mathf.Sqrt(size)));
        }

        public static void SpawnFieldRings(Transform root, float radius, float growTime)
        {
            AnimatedRing(root, WaterLight, 0.3f, radius * 2.5f, growTime);
            AnimatedRing(root, WaterFoam, 0.2f, radius * 2.3f, growTime * 1.1f, 0.08f);
            AnimatedRing(root, WaterTeal, 0.1f, radius * 1.5f, growTime * 0.8f, 0.15f);
        }

        public static void SpawnRipple(Vector3 pos, float size)
        {
            var root = FxRoot("Ripple", pos + Vector3.up * 0.02f, Quaternion.LookRotation(Vector3.up), 1.2f);
            var pool = NewWaterMesh("Ripple Pool", root, WaterPoolMesh, PoolMat);
            pool.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            var anim = pool.gameObject.AddComponent<FxAutoDestroy>();
            anim.life = 1.1f;
            anim.onUpdate = k =>
            {
                float r = Mathf.Lerp(0.2f, 1f, 1f - (1f - k) * (1f - k)) * size;
                pool.transform.localScale = new Vector3(r, 1f, r);
                SetWater(pool, (1f - k) * 0.9f);
            };
            AnimatedRing(root, WaterFoam, 0.1f * size, 1.2f * size, 0.6f);
        }

        public static AudioClip WaterRushClip
        {
            get
            {
                var rnd = new System.Random(47);
                float lp1 = 0f, lp2 = 0f, bub = 0f, bf = 400f, bph = 0f;
                return Synth("WaterRush", 2f, t =>
                {
                    float n = (float)rnd.NextDouble() * 2f - 1f;
                    lp1 += (n - lp1) * 0.08f;
                    lp2 += (lp1 - lp2) * 0.02f;
                    float rush = (lp1 - lp2) * 3f * (0.75f + 0.25f * Mathf.Sin(TAU * 2f * t)) + lp2 * 1.2f;
                    if (rnd.NextDouble() < 0.003) { bub = 1f; bf = 300f + (float)rnd.NextDouble() * 500f; }
                    bub *= 0.995f;
                    bph += TAU * bf * (1f + (1f - bub) * 1.5f) / SR;
                    return rush + Mathf.Sin(bph) * bub * 0.15f;
                });
            }
        }

        public static AudioClip SplashClip
        {
            get
            {
                var rnd = new System.Random(53);
                float lp1 = 0f, lp2 = 0f, bub = 0f, bf = 400f, bph = 0f;
                return Synth("Splash", 1.3f, t =>
                {
                    float n = (float)rnd.NextDouble() * 2f - 1f;
                    lp1 += (n - lp1) * 0.25f;
                    lp2 += (lp1 - lp2) * 0.04f;
                    float burst = (lp1 - lp2) * 2.2f * S(t / 0.01f) * Mathf.Exp(-t * 5f);
                    float thump = Mathf.Sin(TAU * 80f * t) * Mathf.Exp(-t * 14f) * 0.6f;
                    if (t < 0.9f && rnd.NextDouble() < 0.006) { bub = 1f; bf = 250f + (float)rnd.NextDouble() * 700f; }
                    bub *= 0.993f;
                    bph += TAU * bf * (1f + (1f - bub) * 1.5f) / SR;
                    return burst + thump + Mathf.Sin(bph) * bub * 0.2f;
                });
            }
        }

        public static AudioClip RainLoopClip
        {
            get
            {
                var rnd = new System.Random(59);
                float lp = 0f, click = 0f;
                return Synth("Rain", 2f, t =>
                {
                    float n = (float)rnd.NextDouble() * 2f - 1f;
                    lp += (n - lp) * 0.3f;
                    if (rnd.NextDouble() < 0.02) click = 1f;
                    click *= 0.6f;
                    return (n - lp) * 0.45f + n * click * 0.6f + lp * 0.3f;
                });
            }
        }
    }
}

namespace MagicTraining
{
    using System.Collections.Generic;
    using UnityEngine;
    using MinMaxCurve = UnityEngine.ParticleSystem.MinMaxCurve;

    public static partial class MagicFX
    {
        public static readonly Color IceWhite = new Color(0.9f, 0.97f, 1f);
        public static readonly Color IceCyan  = new Color(0.55f, 0.9f, 1f);
        public static readonly Color IceBlue  = new Color(0.3f, 0.6f, 1f);

        public static Gradient IcePalette => Grad((0f, IceWhite), (0.5f, IceCyan), (1f, IceBlue));

        static Material iceMat;
        public static Material IceMat
        {
            get
            {
                if (iceMat != null) return iceMat;
                var sh = Resources.Load<Shader>("MagicIce");
                if (sh == null) sh = Shader.Find("MagicTraining/Ice");
                if (sh == null) return GlowMat;
                iceMat = new Material(sh) { name = "MagicFX_Ice", hideFlags = HideFlags.DontSave, renderQueue = 3005 };
                return iceMat;
            }
        }

        public static void SetIce(Renderer r, float fade, float glow)
        {
            if (r == null) return;
            mpb ??= new MaterialPropertyBlock();
            r.GetPropertyBlock(mpb);
            mpb.SetFloat("_Fade", fade);
            mpb.SetFloat("_Glow", glow);
            r.SetPropertyBlock(mpb);
        }

        class MeshBuilder
        {
            readonly List<Vector3> v = new List<Vector3>();
            readonly List<Vector3> n = new List<Vector3>();
            readonly List<int> tri = new List<int>();

            public void Tri(Vector3 a, Vector3 b, Vector3 c, Vector3 outward)
            {
                Vector3 nn = Vector3.Cross(b - a, c - a);
                if (nn.sqrMagnitude < 1e-12f) return;
                if (Vector3.Dot(nn, outward) < 0f) { var t = b; b = c; c = t; nn = -nn; }
                nn.Normalize();
                int i = v.Count;
                v.Add(a); v.Add(b); v.Add(c);
                n.Add(nn); n.Add(nn); n.Add(nn);
                tri.Add(i); tri.Add(i + 1); tri.Add(i + 2);
            }

            public void Cone(Vector3[] ring, Vector3 tip, Vector3 axisStart)
            {
                Vector3 axis = (tip - axisStart).normalized;
                for (int j = 0; j < ring.Length; j++)
                {
                    Vector3 a = ring[j], b = ring[(j + 1) % ring.Length];
                    Vector3 c = (a + b + tip) / 3f;
                    Vector3 onAxis = axisStart + axis * Vector3.Dot(c - axisStart, axis);
                    Tri(a, b, tip, c - onAxis);
                }
            }

            public void Cap(Vector3[] ring, Vector3 outward)
            {
                for (int j = 1; j < ring.Length - 1; j++) Tri(ring[0], ring[j], ring[j + 1], outward);
            }

            public void Spike(Vector3 baseP, Vector3 tip, float r)
            {
                Vector3 ax = (tip - baseP).normalized;
                Vector3 p1 = Vector3.Cross(ax, Mathf.Abs(ax.y) < 0.95f ? Vector3.up : Vector3.right).normalized;
                Vector3 p2 = Vector3.Cross(ax, p1);
                var ring = new[] { baseP + p1 * r, baseP + p2 * r, baseP - p1 * r, baseP - p2 * r };
                Cone(ring, tip, baseP);
                Cap(ring, -ax);
            }

            public void Prism(Vector3 a, Vector3 b, float ra, float rb, int sides, float angle0 = 0f)
            {
                Vector3 ax = (b - a).normalized;
                Vector3 p1 = Vector3.Cross(ax, Mathf.Abs(ax.y) < 0.95f ? Vector3.up : Vector3.right).normalized;
                Vector3 p2 = Vector3.Cross(ax, p1);
                var ringA = new Vector3[sides];
                var ringB = new Vector3[sides];
                for (int j = 0; j < sides; j++)
                {
                    float ang = angle0 + j / (float)sides * Mathf.PI * 2f;
                    Vector3 d = p1 * Mathf.Cos(ang) + p2 * Mathf.Sin(ang);
                    ringA[j] = a + d * ra;
                    ringB[j] = b + d * rb;
                }
                for (int j = 0; j < sides; j++)
                {
                    int n = (j + 1) % sides;
                    Vector3 mid = (ringA[j] + ringA[n] + ringB[j] + ringB[n]) * 0.25f;
                    Vector3 onAxis = a + ax * Vector3.Dot(mid - a, ax);
                    Vector3 outw = mid - onAxis;
                    Tri(ringA[j], ringA[n], ringB[n], outw);
                    Tri(ringA[j], ringB[n], ringB[j], outw);
                }
                Cap(ringA, -ax);
                Cap(ringB, ax);
            }

            public Mesh Build(string name)
            {
                var m = new Mesh { name = name, hideFlags = HideFlags.DontSave };
                m.SetVertices(v);
                m.SetNormals(n);
                m.SetTriangles(tri, 0);
                m.RecalculateBounds();
                return m;
            }
        }

        static Vector3 Polar(float z, float r, float a) => new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, z);

        static Mesh lanceMesh, shardMesh, blockMesh;

        public static Mesh IceLanceMesh
        {
            get
            {
                if (lanceMesh != null) return lanceMesh;
                var mb = new MeshBuilder();
                (float z, float r)[] prof =
                {
                    (0f, 0f), (0.05f, 0.028f), (0.09f, 0.018f), (0.6f, 0.018f),
                    (0.64f, 0.04f), (0.68f, 0.03f), (0.74f, 0.055f), (1f, 0f)
                };
                const int sides = 6;
                for (int i = 0; i < prof.Length - 1; i++)
                for (int j = 0; j < sides; j++)
                {
                    float a0 = j / (float)sides * TAU, a1 = (j + 1) / (float)sides * TAU;
                    Vector3 A0 = Polar(prof[i].z, prof[i].r, a0), A1 = Polar(prof[i].z, prof[i].r, a1);
                    Vector3 B0 = Polar(prof[i + 1].z, prof[i + 1].r, a0), B1 = Polar(prof[i + 1].z, prof[i + 1].r, a1);
                    Vector3 mid = (A0 + A1 + B0 + B1) * 0.25f;
                    Vector3 outw = new Vector3(mid.x, mid.y, 0f);
                    mb.Tri(A0, A1, B1, outw);
                    mb.Tri(A0, B1, B0, outw);
                }
                for (int k = 0; k < 3; k++)
                {
                    float a = k / 3f * TAU + 0.5f;
                    mb.Spike(Polar(0.70f, 0.028f, a), Polar(0.58f, 0.075f, a), 0.012f);
                    mb.Spike(Polar(0.07f, 0.02f, a + 1f), Polar(0.0f, 0.05f, a + 1f), 0.008f);
                }
                lanceMesh = mb.Build("IceLance");
                return lanceMesh;
            }
        }

        public static Mesh IceShardMesh
        {
            get
            {
                if (shardMesh != null) return shardMesh;
                var mb = new MeshBuilder();
                var ring = new[] { Polar(0f, 0.3f, 0f), Polar(0f, 0.25f, 2.2f), Polar(0f, 0.35f, 4.3f) };
                mb.Cone(ring, new Vector3(0f, 0f, 1f), Vector3.zero);
                mb.Cone(ring, new Vector3(0f, 0f, -0.45f), Vector3.zero);
                shardMesh = mb.Build("IceShard");
                return shardMesh;
            }
        }

        public static Mesh IceBlockMesh
        {
            get
            {
                if (blockMesh != null) return blockMesh;
                float p = (1f + Mathf.Sqrt(5f)) * 0.5f;
                var vs = new[]
                {
                    new Vector3(-1, p, 0), new Vector3(1, p, 0), new Vector3(-1, -p, 0), new Vector3(1, -p, 0),
                    new Vector3(0, -1, p), new Vector3(0, 1, p), new Vector3(0, -1, -p), new Vector3(0, 1, -p),
                    new Vector3(p, 0, -1), new Vector3(p, 0, 1), new Vector3(-p, 0, -1), new Vector3(-p, 0, 1)
                };
                var rnd = new System.Random(5);
                for (int i = 0; i < vs.Length; i++) vs[i] = vs[i].normalized * 0.62f * (0.9f + 0.2f * (float)rnd.NextDouble());
                int[] f =
                {
                    0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
                    3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1
                };
                var mb = new MeshBuilder();
                for (int i = 0; i < f.Length; i += 3)
                {
                    Vector3 a = vs[f[i]], b = vs[f[i + 1]], c = vs[f[i + 2]];
                    mb.Tri(a, b, c, (a + b + c) / 3f);
                }
                blockMesh = mb.Build("IceBlock");
                return blockMesh;
            }
        }

        public static ParticleSystem NewFrostSparkles(Transform parent)
        {
            var ps = NewPS("Frost Sparkles", parent, SparkMat, true, 400);
            var m = ps.main;
            m.startLifetime = new MinMaxCurve(0.4f, 0.9f);
            m.startSize = new MinMaxCurve(0.006f, 0.016f);
            m.startSpeed = new MinMaxCurve(0.02f, 0.1f);
            m.gravityModifier = 0.05f;
            m.startColor = RandomFrom(IcePalette);
            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.015f;
            var em = ps.emission; em.rateOverTime = 10f; em.rateOverDistance = 120f;
            var nz = ps.noise; nz.enabled = true; nz.strength = 0.1f; nz.frequency = 2f;
            var col = ps.colorOverLifetime; col.enabled = true; col.color = FadeOut;
            ps.Play();
            return ps;
        }

        public static void SpawnIceForm(Vector3 tail, Quaternion rot, float length)
        {
            var root = FxRoot("Ice Form FX", tail, rot, 2.5f);
            var motes = Motes(root, 45, 0.1f, 0.6f, 0.6f, 1.3f, IcePalette);
            var sh = motes.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Box;
            sh.scale = new Vector3(0.08f, 0.08f, length); sh.position = new Vector3(0f, 0f, length * 0.5f);
            var m = motes.main; m.gravityModifier = 0.03f;
            motes.Play();
            var flash = Flash(root, A(IceCyan, 0.8f), 0.5f, 0.2f);
            flash.transform.localPosition = new Vector3(0f, 0f, length * 0.5f);
            flash.Play();
            PlayAt(IceFormClip, tail + rot * Vector3.forward * (length * 0.5f), 0.6f, 1f);
        }

        public static void SpawnFrostPuff(Vector3 pos)
        {
            var root = FxRoot("Frost Puff", pos, Quaternion.identity, 2f);
            var motes = Motes(root, 16, 0.1f, 0.6f, 0.5f, 1.1f, IcePalette);
            var sh = motes.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.08f;
            motes.Play();
            IceMist(root, 5, 0.1f, 0.2f, 0.8f, 1.3f).Play();
            PlayAt(ChimeClip, pos, 0.25f, 1.6f);
        }

        static ParticleSystem IceMist(Transform root, int count, float sizeMin, float sizeMax, float lifeMin, float lifeMax)
        {
            var mist = OneShot(NewPS("Ice Mist", root, SmokeMat, true, count + 5), count, 2f);
            var m = mist.main;
            m.startLifetime = new MinMaxCurve(lifeMin, lifeMax);
            m.startSpeed = new MinMaxCurve(0.1f, 0.6f);
            m.startSize = new MinMaxCurve(sizeMin, sizeMax);
            m.startRotation = new MinMaxCurve(0f, TAU);
            m.startColor = new Color(0.8f, 0.92f, 1f, 0.35f);
            m.gravityModifier = 0.04f;
            var sh = mist.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.1f;
            var lv = mist.limitVelocityOverLifetime; lv.enabled = true; lv.limit = 100f; lv.drag = 1.5f;
            var col = mist.colorOverLifetime; col.enabled = true; col.color = FadeInOut;
            var sz = mist.sizeOverLifetime; sz.enabled = true; sz.size = new MinMaxCurve(1f, Curve((0f, 0.6f), (1f, 1.8f)));
            return mist;
        }

        public static void SpawnIceImpact(Vector3 pos, Vector3 normal, float power)
        {
            power = Mathf.Clamp01(power);
            float s = Mathf.Lerp(0.6f, 1.3f, power);
            SpellTarget.HitArea(pos, 0.35f);
            if (normal.sqrMagnitude < 0.001f) normal = Vector3.up;
            var root = FxRoot("Ice Impact", pos + normal * 0.02f, Quaternion.LookRotation(normal), 3.5f);

            Flash(root, A(IceWhite, 1f), 1.1f * s, 0.2f).Play();

            var shards = OneShot(NewPS("Ice Shards", root, IceMat, true, 60), (int)(26 * s), 1.5f);
            {
                var m = shards.main;
                m.startLifetime = new MinMaxCurve(0.8f, 1.6f);
                m.startSpeed = new MinMaxCurve(1.5f * s, 5.5f * s);
                m.startSize = new MinMaxCurve(0.03f, 0.08f);
                m.startRotation3D = true;
                m.startRotationX = new MinMaxCurve(0f, TAU);
                m.startRotationY = new MinMaxCurve(0f, TAU);
                m.startRotationZ = new MinMaxCurve(0f, TAU);
                m.gravityModifier = 1f;
                var r = shards.GetComponent<ParticleSystemRenderer>();
                r.renderMode = ParticleSystemRenderMode.Mesh;
                r.mesh = IceShardMesh;
                r.alignment = ParticleSystemRenderSpace.World;
                var sh = shards.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Hemisphere; sh.radius = 0.06f;
                var rot = shards.rotationOverLifetime; rot.enabled = true; rot.separateAxes = true;
                rot.x = new MinMaxCurve(-6f, 6f); rot.y = new MinMaxCurve(-6f, 6f); rot.z = new MinMaxCurve(-6f, 6f);
                var co = shards.collision;
                co.enabled = true;
                co.type = ParticleSystemCollisionType.World;
                co.mode = ParticleSystemCollisionMode.Collision3D;
                co.bounce = 0.3f;
                co.dampen = 0.4f;
                co.radiusScale = 0.6f;
                co.quality = ParticleSystemCollisionQuality.Medium;
                var col = shards.colorOverLifetime; col.enabled = true;
                col.color = Grad((0f, A(Color.white, 1f)), (0.75f, A(Color.white, 1f)), (1f, A(Color.white, 0f)));
            }
            shards.Play();

            var sparks = Sparks(root, (int)(45 * s), 2f * s, 7f * s, 0.3f, 0.8f, 0.6f, false, IcePalette);
            { var sh = sparks.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Hemisphere; sh.radius = 0.05f; }
            sparks.Play();

            var snow = Motes(root, (int)(30 * s), 0.3f, 1.5f, 1.2f, 2.2f, IcePalette);
            { var sh = snow.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Hemisphere; sh.radius = 0.1f; var m = snow.main; m.gravityModifier = 0.05f; }
            snow.Play();

            IceMist(root, (int)(9 * s), 0.25f * s, 0.5f * s, 1.2f, 2.2f).Play();

            AnimatedRing(root, IceCyan, 0.1f, 2f * s, 0.4f);
            AnimatedRing(root, IceWhite, 0.05f, 1.1f * s, 0.3f, 0.04f);
            FlashLight(root, IceCyan, 5f * s, 5f * s, 0.5f);
            PlayAt(ShatterClip, pos, Mathf.Lerp(0.6f, 1f, power), Random.Range(0.92f, 1.08f));
        }

        public static AudioClip IceFormClip
        {
            get
            {
                var rnd = new System.Random(21);
                const int P = 14;
                float[] on = new float[P], fr = new float[P], de = new float[P], am = new float[P];
                for (int i = 0; i < P; i++)
                {
                    on[i] = (float)rnd.NextDouble() * 0.5f;
                    fr[i] = 2200f + (float)rnd.NextDouble() * 3300f;
                    de[i] = 6f + (float)rnd.NextDouble() * 6f;
                    am[i] = 0.15f + (float)rnd.NextDouble() * 0.2f;
                }
                float lp = 0f;
                return Synth("IceForm", 1.2f, t =>
                {
                    float s = 0f;
                    for (int i = 0; i < P; i++)
                    {
                        float dt = t - on[i];
                        if (dt < 0f) continue;
                        s += am[i] * Mathf.Sin(TAU * fr[i] * dt) * Mathf.Exp(-dt * de[i]);
                    }
                    float n = (float)rnd.NextDouble() * 2f - 1f;
                    lp += (n - lp) * 0.2f;
                    return s + (n - lp) * 0.25f * S(t / 0.3f) * Mathf.Exp(-t * 4f);
                });
            }
        }

        public static AudioClip ShatterClip
        {
            get
            {
                var rnd = new System.Random(23);
                const int P = 24;
                float[] on = new float[P], fr = new float[P], de = new float[P], am = new float[P];
                for (int i = 0; i < P; i++)
                {
                    on[i] = (float)rnd.NextDouble() * 0.25f;
                    fr[i] = 1800f + (float)rnd.NextDouble() * 5200f;
                    de[i] = 8f + (float)rnd.NextDouble() * 12f;
                    am[i] = 0.1f + (float)rnd.NextDouble() * 0.2f;
                }
                float lp = 0f;
                return Synth("IceShatter", 1.3f, t =>
                {
                    float n = (float)rnd.NextDouble() * 2f - 1f;
                    lp += (n - lp) * 0.15f;
                    float s = (n - lp) * Mathf.Exp(-t * 14f) * 0.9f;
                    s += Mathf.Sin(TAU * 90f * t) * Mathf.Exp(-t * 12f) * 0.4f;
                    for (int i = 0; i < P; i++)
                    {
                        float dt = t - on[i];
                        if (dt < 0f) continue;
                        s += am[i] * Mathf.Sin(TAU * fr[i] * dt) * Mathf.Exp(-dt * de[i]);
                    }
                    return s;
                });
            }
        }
    }
}

namespace MagicTraining
{
    using UnityEngine;
    using MinMaxCurve = UnityEngine.ParticleSystem.MinMaxCurve;

    public static partial class MagicFX
    {
        static Mesh pillarMesh;

        public static Mesh IcePillarMesh
        {
            get
            {
                if (pillarMesh != null) return pillarMesh;
                var mb = new MeshBuilder();
                const int sides = 7;
                var rnd = new System.Random(43);
                var r0 = new Vector3[sides];
                var r1 = new Vector3[sides];
                var r2 = new Vector3[sides];
                for (int j = 0; j < sides; j++)
                {
                    float a = j / (float)sides * TAU;
                    float b = a + TAU / (sides * 2f);
                    float k0 = 0.62f * (0.9f + 0.2f * (float)rnd.NextDouble());
                    float k1 = 0.52f * (0.9f + 0.2f * (float)rnd.NextDouble());
                    r0[j] = new Vector3(Mathf.Cos(a) * k0, -0.3f, Mathf.Sin(a) * k0);
                    r1[j] = new Vector3(Mathf.Cos(b) * k1, 0.55f, Mathf.Sin(b) * k1);
                    r2[j] = new Vector3(Mathf.Cos(b) * 0.58f, 1f, Mathf.Sin(b) * 0.58f);
                }
                for (int j = 0; j < sides; j++)
                {
                    int n = (j + 1) % sides;
                    Vector3 mid = (r0[j] + r0[n] + r1[j]) / 3f;
                    mb.Tri(r0[j], r0[n], r1[j], new Vector3(mid.x, 0f, mid.z));
                    mid = (r0[n] + r1[n] + r1[j]) / 3f;
                    mb.Tri(r0[n], r1[n], r1[j], new Vector3(mid.x, 0f, mid.z));
                    mid = (r1[j] + r1[n] + r2[n] + r2[j]) * 0.25f;
                    mb.Tri(r1[j], r1[n], r2[n], new Vector3(mid.x, 0f, mid.z));
                    mb.Tri(r1[j], r2[n], r2[j], new Vector3(mid.x, 0f, mid.z));
                }
                mb.Cap(r2, Vector3.up);
                for (int k = 0; k < 5; k++)
                {
                    float a = k / 5f * TAU + 0.3f;
                    var dirH = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    mb.Spike(dirH * 0.5f + Vector3.up * 0.9f, dirH * 0.78f + Vector3.up * 1.22f, 0.06f);
                    mb.Spike(dirH * 0.5f + Vector3.up * 0.25f, dirH * 0.95f + Vector3.up * 0.5f, 0.08f);
                }
                pillarMesh = mb.Build("IcePillar");
                return pillarMesh;
            }
        }

        public static void SpawnIcicleImpact(Vector3 pos, Vector3 normal, bool sound)
        {
            if (normal.sqrMagnitude < 0.001f) normal = Vector3.up;
            var root = FxRoot("Icicle Impact", pos + normal * 0.02f, Quaternion.LookRotation(normal), 2f);
            IceShardBurst(root, 7, 1f, 3.5f, 0.025f, 0.05f).Play();
            var motes = Motes(root, 8, 0.3f, 1.2f, 0.4f, 0.9f, IcePalette);
            { var sh = motes.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Hemisphere; sh.radius = 0.1f; }
            motes.Play();
            IceMist(root, 2, 0.15f, 0.3f, 0.6f, 1.1f).Play();
            AnimatedRing(root, IceCyan, 0.05f, 0.6f, 0.25f);
            if (sound) PlayAt(ShatterClip, pos, 0.35f, Random.Range(1.2f, 1.6f));
        }

        public static ParticleSystem NewFrostCloud(Transform parent, float radius)
        {
            var cloud = NewPS("Frost Cloud", parent, SmokeMat, false, 120);
            var m = cloud.main;
            m.startLifetime = new MinMaxCurve(2f, 3f);
            m.startSize = new MinMaxCurve(radius * 0.8f, radius * 1.5f);
            m.startRotation = new MinMaxCurve(0f, TAU);
            m.startColor = new Color(0.72f, 0.82f, 0.95f, 0.45f);
            var sh = cloud.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Circle;
            sh.radius = radius * 1.1f; sh.rotation = new Vector3(90f, 0f, 0f);
            var vel = cloud.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.Local;
            vel.x = 0f; vel.y = 0f; vel.z = 0f;
            vel.orbitalX = 0f; vel.orbitalY = 0.5f; vel.orbitalZ = 0f;
            var col = cloud.colorOverLifetime; col.enabled = true; col.color = FadeInOut;
            var rot = cloud.rotationOverLifetime; rot.enabled = true; rot.z = new MinMaxCurve(-0.3f, 0.3f);
            SetRate(cloud, 18f);
            return cloud;
        }

        public static ParticleSystem NewSnowfall(Transform parent, float radius)
        {
            var snow = NewPS("Snowfall", parent, SparkMat, true, 300);
            var m = snow.main;
            m.startLifetime = new MinMaxCurve(2.5f, 3.5f);
            m.startSize = new MinMaxCurve(0.012f, 0.03f);
            m.startSpeed = 0f;
            m.gravityModifier = 0.25f;
            m.startColor = RandomFrom(IcePalette);
            var sh = snow.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Circle;
            sh.radius = radius; sh.rotation = new Vector3(90f, 0f, 0f);
            var nz = snow.noise; nz.enabled = true; nz.strength = 0.4f; nz.frequency = 0.8f;
            var col = snow.colorOverLifetime; col.enabled = true; col.color = FadeInOut;
            SetRate(snow, 70f);
            return snow;
        }
    }
}

namespace MagicTraining
{
    using UnityEngine;
    using MinMaxCurve = UnityEngine.ParticleSystem.MinMaxCurve;

    public static partial class MagicFX
    {
        static Mesh spikeMesh;

        public static Mesh IceSpikeMesh
        {
            get
            {
                if (spikeMesh != null) return spikeMesh;
                var mb = new MeshBuilder();
                const int sides = 6;
                var rnd = new System.Random(31);
                var ring0 = new Vector3[sides];
                var ring1 = new Vector3[sides];
                for (int j = 0; j < sides; j++)
                {
                    float a = j / (float)sides * TAU;
                    float r0 = 0.16f * (0.85f + 0.3f * (float)rnd.NextDouble());
                    float r1 = 0.11f * (0.85f + 0.3f * (float)rnd.NextDouble());
                    ring0[j] = new Vector3(Mathf.Cos(a) * r0, -0.25f, Mathf.Sin(a) * r0);
                    float b = a + TAU / (sides * 2f);
                    ring1[j] = new Vector3(Mathf.Cos(b) * r1, 0.35f, Mathf.Sin(b) * r1);
                }
                for (int j = 0; j < sides; j++)
                {
                    Vector3 a0 = ring0[j], a1 = ring0[(j + 1) % sides], b0 = ring1[j], b1 = ring1[(j + 1) % sides];
                    Vector3 mid = (a0 + a1 + b0) / 3f;
                    mb.Tri(a0, a1, b0, new Vector3(mid.x, 0f, mid.z));
                    mid = (a1 + b1 + b0) / 3f;
                    mb.Tri(a1, b1, b0, new Vector3(mid.x, 0f, mid.z));
                }
                mb.Cone(ring1, new Vector3(0.02f, 1f, 0f), new Vector3(0f, 0.35f, 0f));
                mb.Spike(new Vector3(0.09f, 0.05f, 0.03f), new Vector3(0.28f, 0.45f, 0.06f), 0.05f);
                mb.Spike(new Vector3(-0.07f, 0.0f, -0.08f), new Vector3(-0.2f, 0.33f, -0.24f), 0.04f);
                spikeMesh = mb.Build("IceSpike");
                return spikeMesh;
            }
        }

        static ParticleSystem IceShardBurst(Transform root, int count, float speedMin, float speedMax, float sizeMin, float sizeMax)
        {
            var shards = OneShot(NewPS("Ice Shards", root, IceMat, true, count + 5), count, 1.2f);
            var m = shards.main;
            m.startLifetime = new MinMaxCurve(0.5f, 1.1f);
            m.startSpeed = new MinMaxCurve(speedMin, speedMax);
            m.startSize = new MinMaxCurve(sizeMin, sizeMax);
            m.startRotation3D = true;
            m.startRotationX = new MinMaxCurve(0f, TAU);
            m.startRotationY = new MinMaxCurve(0f, TAU);
            m.startRotationZ = new MinMaxCurve(0f, TAU);
            m.gravityModifier = 1f;
            var r = shards.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Mesh;
            r.mesh = IceShardMesh;
            r.alignment = ParticleSystemRenderSpace.World;
            var sh = shards.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Hemisphere; sh.radius = 0.08f;
            var rot = shards.rotationOverLifetime; rot.enabled = true; rot.separateAxes = true;
            rot.x = new MinMaxCurve(-6f, 6f); rot.y = new MinMaxCurve(-6f, 6f); rot.z = new MinMaxCurve(-6f, 6f);
            var col = shards.colorOverLifetime; col.enabled = true;
            col.color = Grad((0f, A(Color.white, 1f)), (0.75f, A(Color.white, 1f)), (1f, A(Color.white, 0f)));
            return shards;
        }

        public static void SpawnSpikeBurst(Vector3 ground, Vector3 normal, float height, bool sound)
        {
            if (normal.sqrMagnitude < 0.001f) normal = Vector3.up;
            var root = FxRoot("Spike Burst", ground + normal * 0.02f, Quaternion.LookRotation(normal), 2f);
            IceShardBurst(root, 8, 1.5f, 4f, 0.03f, 0.06f).Play();
            var motes = Motes(root, 12, 0.3f, 1.5f, 0.5f, 1f, IcePalette);
            { var sh = motes.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Hemisphere; sh.radius = 0.15f; }
            motes.Play();
            IceMist(root, 3, 0.2f, 0.4f, 0.8f, 1.4f).Play();
            AnimatedRing(root, IceCyan, 0.1f, 0.9f * height, 0.3f);
            if (sound) PlayAt(IceSpikeClip, ground, 0.7f, Random.Range(0.85f, 1.15f));
        }

        public static void SpawnSpikeCrumble(Vector3 p)
        {
            var root = FxRoot("Spike Crumble", p, Quaternion.identity, 1.5f);
            var motes = Motes(root, 8, 0.2f, 0.8f, 0.4f, 0.8f, IcePalette);
            { var sh = motes.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.15f; }
            motes.Play();
            IceShardBurst(root, 4, 0.5f, 2f, 0.02f, 0.04f).Play();
            if (Random.value < 0.3f) PlayAt(ShatterClip, p, 0.15f, 1.4f);
        }

        public static AudioClip IceSpikeClip
        {
            get
            {
                var rnd = new System.Random(37);
                const int P = 6;
                float[] fr = new float[P], de = new float[P];
                for (int i = 0; i < P; i++) { fr[i] = 2000f + (float)rnd.NextDouble() * 4000f; de[i] = 10f + (float)rnd.NextDouble() * 10f; }
                float ph = 0f, lp = 0f;
                return Synth("IceSpike", 0.7f, t =>
                {
                    ph += TAU * (40f + 40f * Mathf.Exp(-t * 15f)) / SR;
                    float thump = Mathf.Sin(ph) * Mathf.Exp(-t * 10f) * 0.8f;
                    float n = (float)rnd.NextDouble() * 2f - 1f;
                    lp += (n - lp) * 0.3f;
                    float crack = (n - lp * 0.5f) * Mathf.Exp(-t * 25f) * 0.8f;
                    float s = 0f;
                    for (int i = 0; i < P; i++) s += 0.12f * Mathf.Sin(TAU * fr[i] * t) * Mathf.Exp(-t * de[i]);
                    return thump + crack + s;
                });
            }
        }
    }
}

namespace MagicTraining
{
    using System.Collections.Generic;
    using UnityEngine;
    using MinMaxCurve = UnityEngine.ParticleSystem.MinMaxCurve;

    public static partial class MagicFX
    {
        public static readonly Color ElecWhite  = new Color(0.92f, 0.97f, 1f);
        public static readonly Color ElecCyan   = new Color(0.4f, 0.85f, 1f);
        public static readonly Color ElecBlue   = new Color(0.3f, 0.5f, 1f);
        public static readonly Color ElecViolet = new Color(0.68f, 0.42f, 1f);

        public static Gradient ElecCoreGradient => Grad((0f, ElecWhite), (0.6f, ElecWhite), (1f, A(ElecCyan, 0.9f)));
        public static Gradient ElecGlowGradient => Grad((0f, A(ElecCyan, 0.75f)), (0.5f, A(ElecBlue, 0.6f)), (1f, A(ElecViolet, 0.45f)));
        public static Gradient ElecPalette => Grad((0f, ElecWhite), (0.4f, ElecCyan), (0.75f, ElecBlue), (1f, ElecViolet));

        static readonly float[] walkX = new float[64];
        static readonly float[] walkY = new float[64];

        public static void Jagged(Vector3[] buf, int n, Vector3 a, Vector3 b, float amp)
        {
            n = Mathf.Clamp(n, 2, Mathf.Min(buf.Length, walkX.Length));
            Vector3 d = b - a;
            float len = d.magnitude;
            if (len < 1e-4f) { for (int i = 0; i < n; i++) buf[i] = a; return; }
            Vector3 f = d / len;
            Vector3 p1 = Vector3.Cross(f, Mathf.Abs(f.y) < 0.95f ? Vector3.up : Vector3.right).normalized;
            Vector3 p2 = Vector3.Cross(f, p1);
            float step = amp * 2f / Mathf.Sqrt(n);
            float wx = 0f, wy = 0f;
            for (int i = 0; i < n; i++)
            {
                walkX[i] = wx; walkY[i] = wy;
                wx += Random.Range(-1f, 1f) * step;
                wy += Random.Range(-1f, 1f) * step;
            }
            float lx = walkX[n - 1], ly = walkY[n - 1];
            for (int i = 0; i < n; i++)
            {
                float t = i / (n - 1f);
                buf[i] = a + d * t + p1 * (walkX[i] - lx * t) + p2 * (walkY[i] - ly * t);
            }
        }

        public static void SetLine(LineRenderer lr, Vector3[] pts, int n)
        {
            lr.positionCount = n;
            for (int i = 0; i < n; i++) lr.SetPosition(i, pts[i]);
        }

        public static LineRenderer NewBoltLine(string name, Transform parent, Material mat, float width, Gradient color, AnimationCurve curve)
        {
            var lr = NewLine(name, parent, mat, Vector3.zero, Vector3.zero, curve, width, color, 1);
            lr.useWorldSpace = true;
            lr.positionCount = 0;
            return lr;
        }

        public static void SpawnZapPuff(Vector3 pos, float volume = 0.5f)
        {
            var root = FxRoot("Zap FX", pos, Quaternion.identity, 1.5f);
            Flash(root, A(ElecCyan, 0.9f), 0.35f, 0.12f).Play();
            var sparks = Sparks(root, 24, 1f, 4f, 0.15f, 0.4f, 0.5f, false, ElecPalette);
            var sh = sparks.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.05f;
            sparks.Play();
            FlashLight(root, ElecCyan, 2.5f, 2.5f, 0.15f);
            PlayAt(ZapClip, pos, volume, Random.Range(1.2f, 1.5f));
        }

        public static AudioClip BuzzLoopClip
        {
            get
            {
                var rnd = new System.Random(17);
                float lp = 0f, crack = 0f;
                return Synth("ElectricBuzz", 1f, t =>
                {
                    float saw = 2f * ((t * 60f) % 1f) - 1f;
                    float saw2 = 2f * ((t * 120f + 0.3f) % 1f) - 1f;
                    float n = (float)rnd.NextDouble() * 2f - 1f;
                    lp += (n - lp) * 0.3f;
                    if (rnd.NextDouble() < 0.004) crack = 1f;
                    crack *= 0.9f;
                    float am = 0.6f + 0.4f * Mathf.Sin(TAU * 8f * t);
                    return (saw * 0.35f + saw2 * 0.2f) * am + (n - lp) * 0.25f + crack * n * 0.8f;
                });
            }
        }

        public static AudioClip ZapClip
        {
            get
            {
                var rnd = new System.Random(19);
                float ph = 0f, crack = 0f;
                const float len = 0.6f;
                return Synth("ElectricZap", len, t =>
                {
                    float n = (float)rnd.NextDouble() * 2f - 1f;
                    if (rnd.NextDouble() < 0.02 * Mathf.Exp(-t * 6f)) crack = 1f;
                    crack *= 0.85f;
                    float burst = n * Mathf.Exp(-t * 10f) * 0.6f + crack * n;
                    ph += TAU * Mathf.Lerp(1800f, 150f, t / len) / SR;
                    return burst + Mathf.Sin(ph) * 0.4f * Mathf.Exp(-t * 6f);
                });
            }
        }

        public static void SpawnThunderChargeFlash(Vector3 pos, Quaternion rot)
        {
            var root = FxRoot("Thunder Full Charge", pos, rot, 1.5f);
            AnimatedRing(root, ElecCyan, 0.15f, 0.8f, 0.3f);
            AnimatedRing(root, ElecViolet, 0.1f, 0.6f, 0.35f, 0.06f);
            Flash(root, A(ElecWhite, 1f), 0.6f, 0.15f).Play();
            var sparks = Sparks(root, 40, 1.5f, 5f, 0.2f, 0.5f, 0.4f, false, ElecPalette);
            var sh = sparks.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.12f;
            sparks.Play();
            PlayAt(ZapClip, pos, 0.7f, 0.8f);
            PlayAt(ChimeClip, pos, 0.4f, 0.75f);
        }

        public static void SpawnThunderImpact(Vector3 pos, Vector3 normal, float power, Transform ignoreRoot)
        {
            power = Mathf.Clamp01(power);
            float s = Mathf.Lerp(0.6f, 1.5f, power);
            SpellTarget.HitArea(pos, 0.4f);
            if (normal.sqrMagnitude < 0.001f) normal = Vector3.up;
            var root = FxRoot("Thunder Impact", pos + normal * 0.03f, Quaternion.LookRotation(normal), 3f);

            Flash(root, A(ElecWhite, 1f), 1.8f * s, 0.18f).Play();
            Flash(root, A(ElecViolet, 0.7f), 2.6f * s, 0.35f).Play();

            var sparks = Sparks(root, (int)(90 * s), 3f * s, 11f * s, 0.25f, 0.8f, 1f, true, ElecPalette);
            { var sh = sparks.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Hemisphere; sh.radius = 0.05f; }
            sparks.Play();

            var motes = Motes(root, (int)(30 * s), 0.5f, 2.5f, 0.6f, 1.2f, ElecPalette);
            { var sh = motes.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Hemisphere; sh.radius = 0.1f; }
            motes.Play();

            AnimatedRing(root, ElecCyan, 0.1f, 3f * s, 0.35f);
            AnimatedRing(root, ElecViolet, 0.1f, 2f * s, 0.5f, 0.05f);
            AnimatedRing(root, ElecWhite, 0.05f, 1.2f * s, 0.25f);
            FlashLight(root, ElecCyan, 8f * s, 10f * s, 0.5f);

            var targets = new List<Rigidbody>();
            var seen = new HashSet<Rigidbody>();
            var cands = new List<(float d, Rigidbody rb)>();
            foreach (var c in Physics.OverlapSphere(pos, 5f * s, ~0, QueryTriggerInteraction.Ignore))
            {
                var rb = c.attachedRigidbody;
                if (rb == null || !seen.Add(rb)) continue;
                if (ignoreRoot != null && rb.transform.IsChildOf(ignoreRoot)) continue;
                cands.Add(((rb.worldCenterOfMass - pos).sqrMagnitude, rb));
            }
            cands.Sort((a, b) => a.d.CompareTo(b.d));
            for (int i = 0; i < cands.Count && i < 5; i++) targets.Add(cands[i].rb);

            ElectricArcBurst.Spawn(pos + normal * 0.03f, normal, 9, Mathf.Lerp(0.8f, 2.5f, power), targets, 0.6f);
            PushBodies(pos, 3.5f * s, 10f * s, ignoreRoot);

            PlayAt(ThunderClip, pos, 1f, Random.Range(0.95f, 1.05f));
            PlayAt(ZapClip, pos, 0.8f, 0.7f);
        }

        public static AudioClip ThunderClip
        {
            get
            {
                var rnd = new System.Random(29);
                float lp1 = 0f, lp2 = 0f, crack = 0f;
                return Synth("Thunder", 2.8f, t =>
                {
                    float n = (float)rnd.NextDouble() * 2f - 1f;
                    float snap = n * Mathf.Exp(-t * 30f) * 1.2f;
                    if (rnd.NextDouble() < 0.01 * Mathf.Exp(-t * 5f)) crack = 1f;
                    crack *= 0.88f;
                    lp1 += (n - lp1) * 0.012f;
                    lp2 += (lp1 - lp2) * 0.02f;
                    float env = S(t / 0.05f) * Mathf.Exp(-t * 1.4f) * (0.6f + 0.4f * Mathf.Sin(TAU * 3f * t + 1.3f));
                    float rumble = (lp1 * 9f + lp2 * 6f) * env;
                    float low = Mathf.Sin(TAU * 45f * t) * Mathf.Exp(-t * 2f) * 0.5f;
                    return snap + crack * n * 0.9f + rumble + low;
                });
            }
        }
    }
}

namespace MagicTraining
{
    using UnityEngine;
    using MinMaxCurve = UnityEngine.ParticleSystem.MinMaxCurve;

    public static partial class MagicFX
    {
        public static readonly Color WindWhite = new Color(0.92f, 0.97f, 1f);
        public static readonly Color WindCyan  = new Color(0.6f, 0.95f, 1f);
        public static readonly Color WindTeal  = new Color(0.4f, 0.9f, 0.8f);

        public static Gradient WindPalette => Grad((0f, WindWhite), (0.4f, WindCyan), (0.75f, WindTeal), (1f, ArcaneViolet));

        public static void Helix(LineRenderer lr, Vector3[] buf, int n, Vector3 basePos, Vector3 up, float height,
            float r0, float r1, float turns, float phase)
        {
            n = Mathf.Clamp(n, 2, buf.Length);
            Vector3 right = Vector3.Cross(up, Vector3.forward);
            if (right.sqrMagnitude < 0.01f) right = Vector3.right;
            right.Normalize();
            Vector3 fwd = Vector3.Cross(right, up);
            for (int i = 0; i < n; i++)
            {
                float t = i / (n - 1f);
                float r = Mathf.Lerp(r0, r1, Mathf.Pow(t, 1.3f));
                float a = phase + t * turns * TAU;
                buf[i] = basePos + up * (height * t) + (right * Mathf.Cos(a) + fwd * Mathf.Sin(a)) * r;
            }
            SetLine(lr, buf, n);
        }

        public static void SpawnWindPuff(Vector3 pos)
        {
            var root = FxRoot("Wind Puff", pos, Quaternion.identity, 2f);
            var motes = Motes(root, 20, 0.5f, 2f, 0.5f, 1.1f, WindPalette);
            { var sh = motes.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.15f; }
            motes.Play();
            PlayAt(WhooshClip, pos, 0.35f, 0.6f);
        }

        public static void SpawnWindPulse(Vector3 pos, float size)
        {
            var root = FxRoot("Wind Pulse", pos, Quaternion.LookRotation(Vector3.up), 1.5f);
            AnimatedRing(root, WindCyan, 0.1f, size, 0.4f);
            AnimatedRing(root, WindWhite, 0.05f, size * 0.7f, 0.35f, 0.06f);
            var motes = Motes(root, 24, 1f, 3f, 0.5f, 1f, WindPalette);
            { var sh = motes.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 0.1f; sh.radiusThickness = 0f; }
            motes.Play();
        }

        public static AudioClip WindLoopClip
        {
            get
            {
                var rnd = new System.Random(41);
                float lp1 = 0f, lp2 = 0f, ph = 0f;
                return Synth("WindLoop", 3f, t =>
                {
                    float sweep = 0.5f + 0.5f * Mathf.Sin(TAU * t / 1.5f);
                    float c = 0.01f + 0.05f * sweep;
                    float n = (float)rnd.NextDouble() * 2f - 1f;
                    lp1 += (n - lp1) * c;
                    lp2 += (lp1 - lp2) * c;
                    float band = (lp1 - lp2) * (3f + 4f * sweep);
                    ph += TAU * (180f + 60f * sweep) / SR;
                    float howl = Mathf.Sin(ph) * 0.06f * sweep;
                    return band + lp2 * 1.5f + howl;
                });
            }
        }

        public static void SpawnAirBurst(Vector3 pos, Vector3 normal, float size)
        {
            if (normal.sqrMagnitude < 0.01f) normal = Vector3.up;
            var root = FxRoot("Air Burst", pos, Quaternion.LookRotation(normal), 1.8f);
            AnimatedRing(root, WindWhite, 0.1f, 2.4f * size, 0.35f);
            AnimatedRing(root, WindCyan, 0.05f, 1.6f * size, 0.3f, 0.05f);
            Flash(root, A(WindWhite, 0.6f), 0.6f * size, 0.15f).Play();
            var streaks = Sparks(root, (int)(30 * size), 2f * size, 6f * size, 0.2f, 0.5f, 0f, false, WindPalette);
            { var sh = streaks.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Hemisphere; sh.radius = 0.1f; }
            streaks.Play();
            var motes = Motes(root, (int)(20 * size), 0.5f, 2f, 0.5f, 1f, WindPalette);
            { var sh = motes.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Hemisphere; sh.radius = 0.15f; }
            motes.Play();
            PlayAt(WhooshClip, pos, 0.6f * Mathf.Min(1f, size), 0.6f);
        }
    }
}

namespace MagicTraining
{
    using UnityEngine;
    using MinMaxCurve = UnityEngine.ParticleSystem.MinMaxCurve;

    public static partial class MagicFX
    {
        public static readonly Color EarthDark  = new Color(0.22f, 0.16f, 0.11f);
        public static readonly Color EarthBrown = new Color(0.42f, 0.31f, 0.2f);
        public static readonly Color EarthLight = new Color(0.62f, 0.5f, 0.36f);
        public static readonly Color StoneGrey  = new Color(0.47f, 0.45f, 0.42f);
        public static readonly Color LavaOrange = new Color(1f, 0.45f, 0.1f);

        static Material rockMat;
        public static Material RockMat => rockMat != null ? rockMat : (rockMat = NewFoliageMat("MagicFX_Rock", 0f, 0f));
        public static Material CrackMat => Mat("Crack", () => BeamTex, 1f, false);

        static Mesh[] rockMeshes;

        public static Mesh RandomRockMesh
        {
            get
            {
                if (rockMeshes == null)
                {
                    rockMeshes = new Mesh[5];
                    for (int i = 0; i < rockMeshes.Length; i++) rockMeshes[i] = MakeRock(101 + i);
                }
                return rockMeshes[Random.Range(0, rockMeshes.Length)];
            }
        }

        static Mesh MakeRock(int seed)
        {
            var rnd = new System.Random(seed);
            float p = (1f + Mathf.Sqrt(5f)) * 0.5f;
            var vs = new[]
            {
                new Vector3(-1, p, 0), new Vector3(1, p, 0), new Vector3(-1, -p, 0), new Vector3(1, -p, 0),
                new Vector3(0, -1, p), new Vector3(0, 1, p), new Vector3(0, -1, -p), new Vector3(0, 1, -p),
                new Vector3(p, 0, -1), new Vector3(p, 0, 1), new Vector3(-p, 0, -1), new Vector3(-p, 0, 1)
            };
            for (int i = 0; i < vs.Length; i++)
            {
                Vector3 v = vs[i].normalized * (0.75f + 0.5f * (float)rnd.NextDouble());
                vs[i] = new Vector3(v.x * 1.05f, v.y * 0.85f, v.z);
            }
            int[] f =
            {
                0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
                3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1
            };
            var m = new VMesh();
            for (int i = 0; i < f.Length; i += 3)
            {
                Vector3 a = vs[f[i]], b = vs[f[i + 1]], c = vs[f[i + 2]];
                float k = (float)rnd.NextDouble();
                Color col = Color.Lerp(Color.Lerp(EarthBrown, StoneGrey, k), EarthLight, Mathf.Clamp01((a.y + b.y + c.y) / 3f) * 0.35f);
                col *= 0.85f + 0.3f * (float)rnd.NextDouble();
                col.a = 0f;
                int ia = m.Add(a, col), ib = m.Add(b, col), ic = m.Add(c, col);
                m.Tri(ia, ib, ic);
            }
            return m.Build("Rock", true);
        }

        public static ParticleSystem SpawnEarthDust(Vector3 p, float size, int count, float life = 2.5f)
        {
            var root = FxRoot("Earth Dust", p, Quaternion.LookRotation(Vector3.up), life + 0.5f);
            var dust = OneShot(NewPS("Dust", root, SmokeMat, true, count + 5), count, 2f);
            var m = dust.main;
            m.startLifetime = new MinMaxCurve(life * 0.5f, life);
            m.startSpeed = new MinMaxCurve(0.5f * size, 2f * size);
            m.startSize = new MinMaxCurve(0.4f * size, 0.9f * size);
            m.startRotation = new MinMaxCurve(0f, TAU);
            m.startColor = RandomFrom(Grad((0f, new Color(0.5f, 0.42f, 0.32f, 0.55f)), (1f, new Color(0.62f, 0.55f, 0.45f, 0.45f))));
            m.gravityModifier = -0.02f;
            var sh = dust.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Hemisphere; sh.radius = 0.2f * size;
            var lv = dust.limitVelocityOverLifetime; lv.enabled = true; lv.limit = 100f; lv.drag = 1.8f;
            var col = dust.colorOverLifetime; col.enabled = true; col.color = FadeInOut;
            var sz = dust.sizeOverLifetime; sz.enabled = true; sz.size = new MinMaxCurve(1f, Curve((0f, 0.5f), (1f, 1.7f)));
            var rot = dust.rotationOverLifetime; rot.enabled = true; rot.z = new MinMaxCurve(-0.5f, 0.5f);
            dust.Play();
            return dust;
        }

        public static void SpawnPebbles(Vector3 p, Vector3 n, float size, int count)
        {
            var root = FxRoot("Pebbles", p, Quaternion.LookRotation(n.sqrMagnitude > 0.01f ? n : Vector3.up), 2.5f);
            var ps = OneShot(NewPS("Pebbles", root, RockMat, true, count + 5), count, 1.5f);
            var m = ps.main;
            m.startLifetime = new MinMaxCurve(0.8f, 1.6f);
            m.startSpeed = new MinMaxCurve(1.5f * size, 5f * size);
            m.startSize = new MinMaxCurve(0.025f * size, 0.07f * size);
            m.startRotation3D = true;
            m.startRotationX = new MinMaxCurve(0f, TAU);
            m.startRotationY = new MinMaxCurve(0f, TAU);
            m.startRotationZ = new MinMaxCurve(0f, TAU);
            m.startColor = RandomFrom(Grad((0f, EarthBrown), (1f, StoneGrey)));
            m.gravityModifier = 1f;
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Mesh;
            r.mesh = RandomRockMesh;
            r.alignment = ParticleSystemRenderSpace.World;
            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 35f; sh.radius = 0.1f * size;
            var rot = ps.rotationOverLifetime; rot.enabled = true; rot.separateAxes = true;
            rot.x = new MinMaxCurve(-6f, 6f); rot.y = new MinMaxCurve(-6f, 6f); rot.z = new MinMaxCurve(-6f, 6f);
            var co = ps.collision; co.enabled = true; co.type = ParticleSystemCollisionType.World; co.mode = ParticleSystemCollisionMode.Collision3D;
            co.bounce = 0.25f; co.dampen = 0.4f; co.radiusScale = 0.5f; co.quality = ParticleSystemCollisionQuality.Medium;
            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = Grad((0f, A(Color.white, 1f)), (0.8f, A(Color.white, 1f)), (1f, A(Color.white, 0f)));
            ps.Play();
        }

        public static void SpawnRockBurst(Vector3 p, Vector3 n, float size, bool sound)
        {
            SpawnEarthDust(p, Mathf.Max(0.5f, size * 1.6f), 3, 2f);
            SpawnPebbles(p, n, Mathf.Max(0.6f, size * 1.5f), 8);
            if (sound) PlayAt(RockBurstClip, p, 0.7f, Random.Range(0.85f, 1.15f));
        }

        public static AudioClip RockBurstClip
        {
            get
            {
                var rnd = new System.Random(223);
                float lp = 0f, crack = 0f, ph = 0f;
                return Synth("RockBurst", 0.9f, t =>
                {
                    float n = (float)rnd.NextDouble() * 2f - 1f;
                    lp += (n - lp) * 0.12f;
                    ph += TAU * (45f + 70f * Mathf.Exp(-t * 10f)) / SR;
                    float thump = Mathf.Sin(ph) * Mathf.Exp(-t * 7f);
                    if (rnd.NextDouble() < 0.012 * Mathf.Exp(-t * 6f)) crack = 1f;
                    crack *= 0.85f;
                    return thump + lp * Mathf.Exp(-t * 5f) * 2f + crack * n * 0.8f;
                });
            }
        }

        public static AudioClip EarthRumbleClip
        {
            get
            {
                var rnd = new System.Random(227);
                float lp1 = 0f, lp2 = 0f, crack = 0f;
                return Synth("EarthRumble", 3f, t =>
                {
                    float n = (float)rnd.NextDouble() * 2f - 1f;
                    lp1 += (n - lp1) * 0.01f;
                    lp2 += (lp1 - lp2) * 0.02f;
                    if (rnd.NextDouble() < 0.0015) crack = 1f;
                    crack *= 0.9f;
                    float am = 0.7f + 0.3f * Mathf.Sin(TAU * 1.7f * t) * Mathf.Sin(TAU * 0.6f * t + 1f);
                    return (lp1 * 10f + lp2 * 8f) * am + Mathf.Sin(TAU * 38f * t) * 0.25f * am + crack * n * 0.5f;
                });
            }
        }
    }
}

namespace MagicTraining
{
    using System.Collections.Generic;
    using UnityEngine;
    using MinMaxCurve = UnityEngine.ParticleSystem.MinMaxCurve;

    public static partial class MagicFX
    {
        public static readonly Color NatureGreen = new Color(0.45f, 1f, 0.4f);
        public static readonly Color NatureLeaf  = new Color(0.22f, 0.75f, 0.22f);
        public static readonly Color NatureGold  = new Color(1f, 0.88f, 0.45f);
        public static readonly Color PetalPink   = new Color(1f, 0.6f, 0.8f);

        public static Gradient NaturePalette => Grad((0f, NatureGold), (0.35f, new Color(0.85f, 1f, 0.6f)), (0.7f, NatureGreen), (1f, PetalPink));
        public static Gradient PetalPalette => Grad((0f, Color.white), (0.3f, PetalPink), (0.6f, new Color(1f, 0.85f, 0.3f)), (1f, new Color(0.75f, 0.55f, 1f)));

        static Shader foliageShader;
        static Material gardenMat, staffMat;

        static Material NewFoliageMat(string name, float wind, float glow)
        {
            if (foliageShader == null)
            {
                foliageShader = Resources.Load<Shader>("MagicFoliage");
                if (foliageShader == null) foliageShader = Shader.Find("MagicTraining/Foliage");
            }
            if (foliageShader == null) return GlowMat;
            var m = new Material(foliageShader) { name = name, hideFlags = HideFlags.DontSave };
            m.SetFloat("_Wind", wind);
            m.SetFloat("_Glow", glow);
            return m;
        }

        public static Material GardenMat => gardenMat != null ? gardenMat : (gardenMat = NewFoliageMat("MagicFX_Garden", 1f, 0.7f));
        public static Material StaffMat => staffMat != null ? staffMat : (staffMat = NewFoliageMat("MagicFX_Staff", 0f, 1.4f));

        public class VMesh
        {
            public readonly List<Vector3> V = new List<Vector3>();
            public readonly List<Color> C = new List<Color>();
            public readonly List<int> T = new List<int>();

            public int Add(Vector3 p, Color c) { V.Add(p); C.Add(c); return V.Count - 1; }
            public void Tri(int a, int b, int c) { T.Add(a); T.Add(b); T.Add(c); }

            public void Tube(IList<Vector3> pts, IList<float> radii, IList<Color> cols, int sides, bool capStart, bool capEnd)
            {
                int n = pts.Count;
                var ring = new int[n];
                Vector3 prevN = Vector3.zero;
                for (int i = 0; i < n; i++)
                {
                    Vector3 tg = (pts[Mathf.Min(i + 1, n - 1)] - pts[Mathf.Max(i - 1, 0)]).normalized;
                    Vector3 N = i == 0 ? Vector3.zero : Vector3.ProjectOnPlane(prevN, tg);
                    if (N.sqrMagnitude < 1e-8f) N = Vector3.Cross(tg, Mathf.Abs(tg.y) < 0.9f ? Vector3.up : Vector3.right);
                    N.Normalize();
                    Vector3 B = Vector3.Cross(tg, N);
                    prevN = N;
                    ring[i] = V.Count;
                    for (int s = 0; s < sides; s++)
                    {
                        float a = s / (float)sides * TAU;
                        Add(pts[i] + (N * Mathf.Cos(a) + B * Mathf.Sin(a)) * radii[i], cols[i]);
                    }
                }
                for (int i = 0; i < n - 1; i++)
                for (int s = 0; s < sides; s++)
                {
                    int a = ring[i] + s, b = ring[i] + (s + 1) % sides, c = ring[i + 1] + s, d = ring[i + 1] + (s + 1) % sides;
                    Tri(a, c, d);
                    Tri(a, d, b);
                }
                if (capStart) { int c0 = Add(pts[0], cols[0]); for (int s = 0; s < sides; s++) Tri(c0, ring[0] + (s + 1) % sides, ring[0] + s); }
                if (capEnd) { int c1 = Add(pts[n - 1], cols[n - 1]); for (int s = 0; s < sides; s++) Tri(c1, ring[n - 1] + s, ring[n - 1] + (s + 1) % sides); }
            }

            public void Blade(Vector3 p, Vector3 lean, float h, float w, float bend, Color cb, Color ct)
            {
                Vector3 side = Vector3.Cross(Vector3.up, lean);
                if (side.sqrMagnitude < 1e-6f) side = Vector3.right;
                side.Normalize();
                const int S = 4;
                int pl = -1, pr = -1;
                for (int s = 0; s < S; s++)
                {
                    float t = s / (float)S;
                    Vector3 c = p + Vector3.up * (h * t) + lean * (bend * h * t * t);
                    float ww = w * (1f - t * 0.85f);
                    Color col = Color.Lerp(cb, ct, t);
                    int l = Add(c - side * (ww * 0.5f), col), r = Add(c + side * (ww * 0.5f), col);
                    if (pl >= 0) { Tri(pl, l, r); Tri(pl, r, pr); }
                    pl = l; pr = r;
                }
                int tip = Add(p + Vector3.up * h + lean * (bend * h), ct);
                Tri(pl, tip, pr);
            }

            public void Leaf(Vector3 p, Vector3 dir, Vector3 hint, float len, float wid, Color c)
            {
                dir.Normalize();
                Vector3 side = Vector3.Cross(hint, dir);
                if (side.sqrMagnitude < 1e-6f) side = Vector3.Cross(Vector3.up, dir);
                if (side.sqrMagnitude < 1e-6f) side = Vector3.right;
                side.Normalize();
                Vector3 fold = Vector3.Cross(dir, side);
                Color tipC = c * 1.15f; tipC.a = c.a;
                int v0 = Add(p, c * 0.85f);
                int vl = Add(p + dir * (len * 0.4f) + side * (wid * 0.5f), c);
                int vm = Add(p + dir * (len * 0.45f) + fold * (len * 0.08f), tipC);
                int vr = Add(p + dir * (len * 0.4f) - side * (wid * 0.5f), c);
                int vt = Add(p + dir * len, tipC);
                Tri(v0, vl, vm); Tri(vm, vl, vt); Tri(v0, vm, vr); Tri(vm, vt, vr);
            }

            public void FlowerHead(Vector3 c, Vector3 up, int petals, float len, float wid, Color pc, Color cc, float cup, float rot0, float centerR)
            {
                up.Normalize();
                Vector3 a = Vector3.Cross(up, Mathf.Abs(up.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
                Vector3 b = Vector3.Cross(up, a);
                int ci = Add(c + up * (centerR * 0.7f), cc);
                int r0 = V.Count;
                for (int j = 0; j < 6; j++)
                {
                    float an = j / 6f * TAU;
                    Color dc = cc * 0.85f; dc.a = cc.a;
                    Add(c + (a * Mathf.Cos(an) + b * Mathf.Sin(an)) * centerR, dc);
                }
                for (int j = 0; j < 6; j++) Tri(ci, r0 + j, r0 + (j + 1) % 6);
                for (int k = 0; k < petals; k++)
                {
                    float an = rot0 + k / (float)petals * TAU;
                    Vector3 d = a * Mathf.Cos(an) + b * Mathf.Sin(an);
                    Vector3 perp = Vector3.Cross(up, d);
                    Color pb = pc * 0.85f; pb.a = pc.a;
                    int v0 = Add(c + d * (centerR * 0.6f), pb);
                    int vl = Add(c + d * (centerR + len * 0.5f) + up * (cup * len * 0.4f) - perp * (wid * 0.5f), pc);
                    int vr = Add(c + d * (centerR + len * 0.5f) + up * (cup * len * 0.4f) + perp * (wid * 0.5f), pc);
                    int vt = Add(c + d * (centerR + len) + up * (cup * len), pc);
                    Tri(v0, vl, vt);
                    Tri(v0, vt, vr);
                }
            }

            public void Sphere(Vector3 c, float r, Color col, int seg = 10, int rings = 7)
            {
                int start = V.Count;
                for (int i = 0; i <= rings; i++)
                {
                    float th = Mathf.PI * i / rings;
                    for (int j = 0; j <= seg; j++)
                    {
                        float ph = TAU * j / seg;
                        Add(c + new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Sin(ph)) * r, col);
                    }
                }
                for (int i = 0; i < rings; i++)
                for (int j = 0; j < seg; j++)
                {
                    int a = start + i * (seg + 1) + j, b2 = a + seg + 1;
                    Tri(a, b2, a + 1);
                    Tri(a + 1, b2, b2 + 1);
                }
            }

            public Mesh Build(string name, bool staticUV)
            {
                var mesh = new Mesh { name = name, hideFlags = HideFlags.DontSave };
                if (V.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.SetVertices(V);
                mesh.SetColors(C);
                mesh.SetTriangles(T, 0);
                if (staticUV)
                {
                    var u = new List<Vector4>(V.Count);
                    for (int i = 0; i < V.Count; i++) u.Add(new Vector4(0f, 0f, 0f, -100f));
                    mesh.SetUVs(1, u);
                }
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                return mesh;
            }
        }

        public class PlantTemplate
        {
            public Vector3[] v;
            public Vector3[] n;
            public Color[] c;
            public int[] t;
        }

        static PlantTemplate ToTemplate(VMesh vm)
        {
            var m = vm.Build("PlantTemplate", false);
            var tp = new PlantTemplate { v = m.vertices, n = m.normals, c = m.colors, t = m.triangles };
            Object.Destroy(m);
            return tp;
        }

        static float R01(System.Random r) => (float)r.NextDouble();

        static PlantTemplate[] grassTemplates, flowerTemplates;
        static PlantTemplate sproutTemplate;

        public static PlantTemplate[] GrassTemplates => grassTemplates ?? (grassTemplates = new[]
        {
            MakeTuft(1, 9, 0.18f, 0.34f, 0.05f),
            MakeTuft(2, 14, 0.07f, 0.15f, 0.07f),
            MakeTuft(3, 11, 0.12f, 0.24f, 0.06f)
        });

        public static PlantTemplate[] FlowerTemplates => flowerTemplates ?? (flowerTemplates = new[]
        {
            MakeFlower(11, new Color(1f, 1f, 0.95f, 0.35f), new Color(1f, 0.82f, 0.2f, 0.4f), 10, 0.045f, 0.012f, 0.2f, 0.25f, 1),
            MakeFlower(12, new Color(1f, 0.55f, 0.75f, 0.5f), new Color(1f, 0.92f, 0.55f, 0.4f), 6, 0.04f, 0.024f, 0.16f, 0.35f, 1),
            MakeFlower(13, new Color(1f, 0.85f, 0.2f, 0.5f), new Color(1f, 0.55f, 0.1f, 0.4f), 5, 0.03f, 0.026f, 0.13f, 0.6f, 1),
            MakeFlower(14, new Color(0.45f, 0.62f, 1f, 0.6f), new Color(1f, 0.95f, 0.7f, 0.5f), 5, 0.02f, 0.016f, 0.11f, 0.3f, 3),
            MakeFlower(15, new Color(0.78f, 0.48f, 1f, 0.55f), new Color(1f, 0.95f, 0.6f, 0.4f), 8, 0.04f, 0.016f, 0.22f, 0.3f, 1),
            MakeFlower(16, new Color(0.5f, 1f, 0.9f, 1f), new Color(1f, 1f, 0.8f, 1f), 6, 0.05f, 0.022f, 0.26f, 0.45f, 1)
        });
        public static readonly float[] FlowerWeights = { 25f, 20f, 20f, 15f, 15f, 5f };

        public static PlantTemplate SproutTemplate
        {
            get
            {
                if (sproutTemplate != null) return sproutTemplate;
                var m = new VMesh();
                var leafC = new Color(0.28f, 0.72f, 0.22f, 0f);
                var stemC = new Color(0.2f, 0.5f, 0.12f, 0f);
                m.Tube(new[] { Vector3.zero, Vector3.up * 0.03f }, new[] { 0.003f, 0.0025f }, new[] { stemC, stemC }, 4, false, false);
                for (int k = 0; k < 4; k++)
                {
                    float a = k / 4f * TAU + 0.3f;
                    var d = new Vector3(Mathf.Cos(a), 0.55f, Mathf.Sin(a));
                    m.Leaf(Vector3.up * 0.028f, d, Vector3.up, 0.045f, 0.035f, leafC);
                }
                sproutTemplate = ToTemplate(m);
                return sproutTemplate;
            }
        }

        static PlantTemplate MakeTuft(int seed, int blades, float hMin, float hMax, float spread)
        {
            var rnd = new System.Random(seed);
            var m = new VMesh();
            var baseC = new Color(0.1f, 0.3f, 0.07f, 0f);
            for (int b = 0; b < blades; b++)
            {
                float a = R01(rnd) * TAU;
                float r = spread * Mathf.Sqrt(R01(rnd));
                var p = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                float la = a + (R01(rnd) - 0.5f) * 1.6f;
                var lean = new Vector3(Mathf.Cos(la), 0f, Mathf.Sin(la));
                var tip = Color.Lerp(new Color(0.35f, 0.8f, 0.2f, 0f), new Color(0.65f, 0.9f, 0.3f, 0f), R01(rnd) * 0.6f);
                m.Blade(p, lean, Mathf.Lerp(hMin, hMax, R01(rnd)), Mathf.Lerp(0.01f, 0.018f, R01(rnd)), Mathf.Lerp(0.2f, 0.55f, R01(rnd)), baseC, tip);
            }
            return ToTemplate(m);
        }

        static PlantTemplate MakeFlower(int seed, Color petal, Color center, int petals, float len, float wid, float height, float cup, int heads)
        {
            var rnd = new System.Random(seed);
            var m = new VMesh();
            var stemC = new Color(0.2f, 0.5f, 0.12f, 0f);
            var leafC = new Color(0.26f, 0.66f, 0.2f, 0f);
            for (int hd = 0; hd < heads; hd++)
            {
                float a = R01(rnd) * TAU;
                var lean = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 off = heads > 1 ? lean * (0.015f + 0.02f * R01(rnd)) : Vector3.zero;
                float hh = height * (1f - 0.22f * hd);
                var p0 = off;
                var p1 = off + lean * 0.015f + Vector3.up * (hh * 0.5f);
                var p2 = off + lean * 0.025f + Vector3.up * hh;
                m.Tube(new[] { p0, p1, p2 }, new[] { 0.0035f, 0.003f, 0.0025f }, new[] { stemC, stemC, stemC }, 4, false, false);
                m.Leaf(p0 + Vector3.up * (hh * 0.22f), lean * -1f + Vector3.up * 0.9f, Vector3.up, hh * 0.35f, hh * 0.12f, leafC);
                if (hh > 0.15f) m.Leaf(p0 + Vector3.up * (hh * 0.45f), lean + Vector3.up * 0.7f, Vector3.up, hh * 0.28f, hh * 0.1f, leafC);
                m.FlowerHead(p2, Vector3.up + lean * 0.3f, petals, len, wid, petal, center, cup, R01(rnd) * TAU, len * 0.28f);
            }
            return ToTemplate(m);
        }

        static Mesh staffMesh;

        static float Bump(float t, float c, float w) { float x = (t - c) / w; return Mathf.Exp(-x * x); }
        static Vector3 ShaftCenter(float t) => new Vector3(0.012f * Mathf.Sin(t * 9f + 0.5f) + 0.005f * Mathf.Sin(t * 23f),
            0.03f + t * 1.42f, 0.01f * Mathf.Sin(t * 7f + 2f) + 0.004f * Mathf.Sin(t * 19f + 1f));
        static float ShaftRadius(float t) => Mathf.Lerp(0.016f, 0.024f, t) * (1f + 0.35f * Bump(t, 0.3f, 0.025f) + 0.3f * Bump(t, 0.62f, 0.03f) + 0.25f * Bump(t, 0.85f, 0.025f));

        public static Vector3 StaffOrbLocal => ShaftCenter(1f) + Vector3.up * 0.14f;

        public static Mesh StaffMesh
        {
            get
            {
                if (staffMesh != null) return staffMesh;
                var m = new VMesh();
                var rnd = new System.Random(71);
                var dark = new Color(0.28f, 0.17f, 0.09f, 0f);
                var light = new Color(0.46f, 0.31f, 0.17f, 0f);
                var leafC = new Color(0.3f, 0.7f, 0.22f, 0f);
                var vineC = new Color(0.18f, 0.45f, 0.12f, 0f);

                var pts = new List<Vector3> { Vector3.zero };
                var rad = new List<float> { 0.004f };
                var col = new List<Color> { dark };
                const int N = 26;
                for (int i = 0; i <= N; i++)
                {
                    float t = i / (float)N;
                    pts.Add(ShaftCenter(t));
                    rad.Add(ShaftRadius(t));
                    Color c = Color.Lerp(dark, light, 0.5f + 0.5f * Mathf.Sin(t * 37f)) * (0.9f + 0.2f * R01(rnd));
                    c.a = 0f;
                    col.Add(c);
                }
                m.Tube(pts, rad, col, 7, false, true);
                Vector3 top = ShaftCenter(1f);

                Color[] petalCols = { new Color(1f, 0.6f, 0.8f, 0.5f), new Color(1f, 1f, 0.95f, 0.4f), new Color(1f, 0.85f, 0.25f, 0.5f) };
                for (int k = 0; k < 3; k++)
                {
                    float a = k * TAU / 3f + 0.4f;
                    var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    var side = Vector3.Cross(Vector3.up, d);
                    var bp = new List<Vector3>
                    {
                        top + Vector3.down * 0.03f,
                        top + d * 0.03f + Vector3.up * 0.05f,
                        top + d * 0.06f + side * 0.012f + Vector3.up * 0.13f,
                        top + d * 0.045f + side * 0.02f + Vector3.up * 0.22f,
                        top + d * 0.005f + side * 0.01f + Vector3.up * 0.27f,
                        top - d * 0.02f + Vector3.up * 0.265f
                    };
                    var br = new List<float> { 0.016f, 0.014f, 0.011f, 0.008f, 0.006f, 0.003f };
                    var bc = new List<Color>();
                    for (int i = 0; i < bp.Count; i++) bc.Add(Color.Lerp(light, dark, i / (bp.Count - 1f)));
                    m.Tube(bp, br, bc, 6, false, true);

                    m.Leaf(bp[1], d + Vector3.up * 0.6f, side, 0.045f, 0.022f, leafC);
                    m.Leaf(bp[3], d - side * 0.5f + Vector3.up * 0.2f, side, 0.035f, 0.018f, leafC);
                    m.FlowerHead(bp[2] + d * 0.012f, d + Vector3.up, 5, 0.016f, 0.012f, petalCols[k], new Color(1f, 0.85f, 0.3f, 0.5f), 0.3f, a, 0.005f);
                }

                m.Sphere(StaffOrbLocal, 0.035f, new Color(0.55f, 1f, 0.6f, 1f), 10, 7);

                var vp = new List<Vector3>();
                var vr = new List<float>();
                var vc = new List<Color>();
                const int VN = 34;
                for (int i = 0; i <= VN; i++)
                {
                    float u = i / (float)VN;
                    float ts = Mathf.Lerp(0.58f, 0.98f, u);
                    float ang = u * 3.2f * TAU;
                    var dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                    vp.Add(ShaftCenter(ts) + dir * (ShaftRadius(ts) + 0.004f));
                    vr.Add(0.0035f);
                    vc.Add(vineC);
                    if (i % 5 == 2) m.Leaf(vp[i], dir + Vector3.up * 0.4f, Vector3.up, 0.03f, 0.018f, leafC);
                    if (i == 17 || i == 31)
                        m.FlowerHead(vp[i] + dir * 0.008f, dir + Vector3.up * 0.5f, 5, 0.012f, 0.01f, petalCols[i == 17 ? 0 : 2], new Color(1f, 0.9f, 0.4f, 0.5f), 0.3f, ang, 0.004f);
                }
                m.Tube(vp, vr, vc, 4, false, false);

                staffMesh = m.Build("DruidStaff", true);
                return staffMesh;
            }
        }

        public static void SpawnNatureStrike(Vector3 p, Vector3 n, float power, float radius, float growTime)
        {
            if (n.sqrMagnitude < 0.01f) n = Vector3.up;
            var root = FxRoot("Nature Strike", p + n * 0.03f, Quaternion.LookRotation(n), growTime + 3f);

            MagicCircle.Flash(p + n * 0.04f, Quaternion.LookRotation(n), Mathf.Lerp(1.2f, 2f, power), MagicCircle.Element.Nature, 0.7f, 1.5f);
            AnimatedRing(root, NatureGreen, 0.3f, radius * 2.5f, growTime);
            AnimatedRing(root, NatureGold, 0.2f, radius * 1.6f, growTime * 0.7f, 0.05f);
            Flash(root, A(NatureGold, 0.8f), 0.7f, 0.2f).Play();

            var dust = OneShot(NewPS("Dust", root, SmokeMat, true, 20), 8, 1.5f);
            {
                var m = dust.main;
                m.startLifetime = new MinMaxCurve(0.6f, 1.2f);
                m.startSpeed = new MinMaxCurve(0.3f, 1f);
                m.startSize = new MinMaxCurve(0.15f, 0.35f);
                m.startRotation = new MinMaxCurve(0f, TAU);
                m.startColor = new Color(0.55f, 0.45f, 0.32f, 0.4f);
                var sh = dust.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Hemisphere; sh.radius = 0.1f;
                var lv = dust.limitVelocityOverLifetime; lv.enabled = true; lv.limit = 100f; lv.drag = 2f;
                var col = dust.colorOverLifetime; col.enabled = true; col.color = FadeInOut;
                var sz = dust.sizeOverLifetime; sz.enabled = true; sz.size = new MinMaxCurve(1f, Curve((0f, 0.6f), (1f, 1.6f)));
            }
            dust.Play();

            var petals = OneShot(NewPS("Petals", root, DropMat, true, 80), (int)Mathf.Lerp(30f, 60f, power), 2f);
            {
                var m = petals.main;
                m.startLifetime = new MinMaxCurve(1.5f, 2.8f);
                m.startSpeed = new MinMaxCurve(1f, 3.5f);
                m.startSize = new MinMaxCurve(0.02f, 0.04f);
                m.gravityModifier = 0.12f;
                m.startColor = RandomFrom(PetalPalette);
                var sh = petals.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Hemisphere; sh.radius = 0.15f;
                var lv = petals.limitVelocityOverLifetime; lv.enabled = true; lv.limit = 100f; lv.drag = 1.5f;
                var nz = petals.noise; nz.enabled = true; nz.strength = 0.6f; nz.frequency = 1.2f;
                var col = petals.colorOverLifetime; col.enabled = true; col.color = FadeInOut;
            }
            petals.Play();

            var motes = Motes(root, (int)Mathf.Lerp(25f, 45f, power), 0.5f, 2f, 1f, 1.8f, NaturePalette);
            { var sh = motes.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Hemisphere; sh.radius = 0.15f; }
            motes.Play();

            FlashLight(root, NatureGreen, 4f, 3f, 0.5f);
            PlayAt(WoodThumpClip, p, 0.9f, Random.Range(0.9f, 1.1f));
            PlayAt(NatureChimeClip, p + n * 0.5f, 0.55f, Random.Range(0.97f, 1.03f));
            PlayAt(RustleClip, p, 0.6f, 1f);
        }

        public static AudioClip WoodThumpClip
        {
            get
            {
                var rnd = new System.Random(73);
                float lp = 0f;
                return Synth("WoodThump", 0.6f, t =>
                {
                    float n = (float)rnd.NextDouble() * 2f - 1f;
                    lp += (n - lp) * 0.35f;
                    float knock = Mathf.Sin(TAU * 150f * t) * Mathf.Exp(-t * 28f)
                                + 0.7f * Mathf.Sin(TAU * 95f * t) * Mathf.Exp(-t * 14f)
                                + 0.3f * Mathf.Sin(TAU * 420f * t) * Mathf.Exp(-t * 60f);
                    return knock + lp * Mathf.Exp(-t * 80f) * 1.5f;
                });
            }
        }

        static readonly float[] chimeNotes = { 1046.5f, 1174.7f, 1318.5f, 1568f, 1760f, 2093f };

        public static AudioClip NatureChimeClip => Synth("NatureChime", 2.4f, t =>
        {
            float s = 0f;
            for (int i = 0; i < chimeNotes.Length; i++)
            {
                float dt = t - i * 0.07f;
                if (dt < 0f) continue;
                s += Mathf.Sin(TAU * chimeNotes[i] * dt) * Mathf.Exp(-dt * 2.5f) * 0.25f
                   + Mathf.Sin(TAU * chimeNotes[i] * 2f * dt) * Mathf.Exp(-dt * 5f) * 0.06f;
            }
            return s;
        });

        public static AudioClip RustleClip
        {
            get
            {
                var rnd = new System.Random(79);
                float lp = 0f, flutter = 0f;
                return Synth("Rustle", 2.2f, t =>
                {
                    float n = (float)rnd.NextDouble() * 2f - 1f;
                    lp += (n - lp) * 0.5f;
                    if (rnd.NextDouble() < 0.01) flutter = 1f;
                    flutter *= 0.995f;
                    float env = S(t / 0.15f) * Mathf.Exp(-t * 1.3f);
                    return (n - lp) * env * (0.35f + 0.65f * flutter);
                });
            }
        }

        static Material vineMat;
        public static Material VineMat => vineMat != null ? vineMat : (vineMat = NewFoliageMat("MagicFX_Vine", 0.6f, 0.7f));

        public static void SpawnVineSprout(Vector3 p, Vector3 n)
        {
            if (n.sqrMagnitude < 0.01f) n = Vector3.up;
            var root = FxRoot("Vine Sprout", p + n * 0.03f, Quaternion.LookRotation(n), 2.5f);
            MagicCircle.Flash(p + n * 0.04f, Quaternion.LookRotation(n), 1.1f, MagicCircle.Element.Nature, 0.5f, 2.5f);
            AnimatedRing(root, NatureGreen, 0.1f, 2.2f, 0.45f);
            Flash(root, A(NatureGold, 0.7f), 0.5f, 0.18f).Play();

            var dust = OneShot(NewPS("Dust", root, SmokeMat, true, 12), 6, 1.5f);
            {
                var m = dust.main;
                m.startLifetime = new MinMaxCurve(0.6f, 1.1f);
                m.startSpeed = new MinMaxCurve(0.3f, 0.9f);
                m.startSize = new MinMaxCurve(0.12f, 0.28f);
                m.startRotation = new MinMaxCurve(0f, TAU);
                m.startColor = new Color(0.55f, 0.45f, 0.32f, 0.4f);
                var sh = dust.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Hemisphere; sh.radius = 0.08f;
                var lv = dust.limitVelocityOverLifetime; lv.enabled = true; lv.limit = 100f; lv.drag = 2f;
                var col = dust.colorOverLifetime; col.enabled = true; col.color = FadeInOut;
            }
            dust.Play();
            var motes = Motes(root, 25, 0.4f, 1.6f, 0.8f, 1.5f, NaturePalette);
            { var sh = motes.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Hemisphere; sh.radius = 0.12f; }
            motes.Play();
            PlayAt(WoodThumpClip, p, 0.7f, 0.8f);
            PlayAt(NatureChimeClip, p, 0.35f, 0.85f);
        }

        public static void SpawnVineBloom(Vector3 p)
        {
            var root = FxRoot("Vine Bloom", p, Quaternion.identity, 3f);
            var petals = OneShot(NewPS("Petals", root, DropMat, true, 40), 22, 2f);
            {
                var m = petals.main;
                m.startLifetime = new MinMaxCurve(1.5f, 2.6f);
                m.startSpeed = new MinMaxCurve(0.4f, 1.4f);
                m.startSize = new MinMaxCurve(0.02f, 0.035f);
                m.gravityModifier = 0.08f;
                m.startColor = RandomFrom(PetalPalette);
                var sh = petals.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.1f;
                var nz = petals.noise; nz.enabled = true; nz.strength = 0.5f; nz.frequency = 1.2f;
                var col = petals.colorOverLifetime; col.enabled = true; col.color = FadeInOut;
            }
            petals.Play();
            var motes = Motes(root, 18, 0.3f, 1f, 0.8f, 1.4f, NaturePalette);
            { var sh = motes.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.1f; }
            motes.Play();
            PlayAt(NatureChimeClip, p, 0.4f, 1.15f);
        }

        public static AudioClip VineLoopClip
        {
            get
            {
                var rnd = new System.Random(83);
                float lp = 0f, flutter = 0f, ph = 0f;
                return Synth("VineLoop", 2f, t =>
                {
                    float n = (float)rnd.NextDouble() * 2f - 1f;
                    lp += (n - lp) * 0.45f;
                    if (rnd.NextDouble() < 0.006) flutter = 1f;
                    flutter *= 0.996f;
                    float rustle = (n - lp) * (0.25f + 0.6f * flutter);
                    ph += TAU * (70f + 25f * Mathf.Sin(TAU * 0.7f * t) + 10f * Mathf.Sin(TAU * 5f * t)) / SR;
                    float creak = Mathf.Sin(ph) * 0.12f * (0.5f + 0.5f * Mathf.Sin(TAU * 1.5f * t));
                    return rustle + creak + lp * 0.3f;
                });
            }
        }
    }
}

namespace MagicTraining
{
    using UnityEngine;

    public static partial class MagicFX
    {
        public static void SpawnTeleportPoof(Vector3 pos, bool vanish)
        {
            var cam = Camera.main;
            Vector3 toCam = cam != null ? cam.transform.position - pos : Vector3.back;
            Quaternion facing = Quaternion.LookRotation(toCam.sqrMagnitude > 0.001f ? toCam : Vector3.back);
            var root = FxRoot(vanish ? "Target Vanish" : "Target Appear", pos, facing, 2f);

            Flash(root, A(vanish ? ArcaneGold : ArcaneViolet, 0.9f), vanish ? 1.2f : 0.9f, 0.2f).Play();
            var motes = Motes(root, vanish ? 40 : 25, vanish ? 1f : 0.3f, vanish ? 3f : 1.2f, 0.5f, 1.1f);
            var sh = motes.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.3f;
            motes.Play();
            AnimatedRing(root, vanish ? ArcaneGold : ArcaneViolet, 0.1f, vanish ? 1.8f : 1.2f, 0.35f);

            if (!vanish) MagicCircle.Flash(pos, facing, 1.3f, MagicCircle.Element.Fire, 0.35f, 3f);
            PlayAt(ChimeClip, pos, vanish ? 0.7f : 0.4f, vanish ? 1.2f : 1.5f);
        }
    }
}
