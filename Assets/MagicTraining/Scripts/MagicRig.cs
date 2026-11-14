namespace MagicTraining
{
    using System.Collections.Generic;
    using System.Reflection;
    using UnityEngine;
    using UnityEngine.InputSystem;
    using UnityEngine.InputSystem.XR;

    [DefaultExecutionOrder(-100)]
    public class MagicRig : MonoBehaviour
    {
        public class Hand
        {
            public readonly bool left;
            public Transform t;
            public float triggerValue, gripValue;
            public bool trigger, triggerDown, triggerUp;
            public bool grip, gripDown, gripUp;
            public bool primary, primaryDown, primaryUp;
            public Vector3 aimPosition;
            public Quaternion aimRotation = Quaternion.identity;
            public bool gripConsumed;
            public Vector3 velocity;

            internal InputAction triggerAction, gripAction, primaryAction, aimPosAction, aimRotAction;
            internal const int HistSize = 16;
            internal readonly Vector3[] posHist = new Vector3[HistSize];
            internal readonly float[] timeHist = new float[HistSize];
            internal int histCount, histHead;

            public Hand(bool left) { this.left = left; }
            public Vector3 Position => t.position;
            public Quaternion Rotation => t.rotation;
            public Vector3 Forward => t.forward;
            public Vector3 AimForward => aimRotation * Vector3.forward;
            public Vector3 Tip => t.position + t.forward * 0.07f;
            public Vector3 Palm => t.position + t.up * 0.07f + t.forward * 0.03f;
        }

        public static MagicRig I { get; private set; }
        public static bool autoCreate = true;

        [Header("Referencias (opcional: se buscan solas)")]
        public Transform head;
        public Transform leftHand;
        public Transform rightHand;

        [Header("Botones")]
        [Range(0.1f, 1f)] public float pressThreshold = 0.6f;
        [Range(0f, 1f)] public float releaseThreshold = 0.35f;

        [Header("Vibración")]
        public bool haptics = true;

        public Hand L { get; } = new Hand(true);
        public Hand R { get; } = new Hand(false);
        public Hand Get(bool left) => left ? L : R;
        public Hand Other(Hand h) => h.left ? R : L;
        public Transform Head => head;
        public Transform IgnoreRoot => head != null ? head.root : null;

        public static bool IsHoldingItem(Hand h) => h != null && (Trident.IsHeldBy(h) || DruidStaff.IsHeldBy(h));
        public static bool FreeGrip(Hand h) => h != null && h.grip && !IsHoldingItem(h);

        Transform playerRoot, playerRootHead;
        public Transform PlayerRoot
        {
            get
            {
                if (head == null) return null;
                if (playerRoot != null && playerRootHead == head) return playerRoot;
                playerRootHead = head;
                playerRoot = null;
                for (Transform t = head.parent; t != null && playerRoot == null; t = t.parent)
                    foreach (var c in t.GetComponents<Component>())
                        if (c != null && (c.GetType().Name == "XROrigin" || c is CharacterController)) { playerRoot = t; break; }
                if (playerRoot == null) playerRoot = head.root;
                return playerRoot;
            }
        }
        public bool Ready => head != null && L.t != null && R.t != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (!autoCreate || FindAnyObjectByType<MagicRig>() != null) return;
            var go = new GameObject("Magic System");
            go.AddComponent<MagicRig>();
            go.AddComponent<StormCaster>();
            go.AddComponent<TornadoCaster>();
            go.AddComponent<AirCaster>();
            go.AddComponent<IceLanceCaster>();
            go.AddComponent<LightningCaster>();
            go.AddComponent<FireballCaster>();
            go.AddComponent<FireArrowCaster>();
            go.AddComponent<IceSpikeCaster>();
            go.AddComponent<EarthCaster>();
            DontDestroyOnLoad(go);
        }

        void Awake()
        {
            I = this;
            L.triggerAction = new InputAction("Magic L Trigger", InputActionType.Value, "<XRController>{LeftHand}/trigger", expectedControlType: "Axis");
            R.triggerAction = new InputAction("Magic R Trigger", InputActionType.Value, "<XRController>{RightHand}/trigger", expectedControlType: "Axis");
            L.gripAction = new InputAction("Magic L Grip", InputActionType.Value, "<XRController>{LeftHand}/grip", expectedControlType: "Axis");
            R.gripAction = new InputAction("Magic R Grip", InputActionType.Value, "<XRController>{RightHand}/grip", expectedControlType: "Axis");
            L.primaryAction = new InputAction("Magic L Primary", InputActionType.Value, "<XRController>{LeftHand}/primaryButton");
            R.primaryAction = new InputAction("Magic R Primary", InputActionType.Value, "<XRController>{RightHand}/primaryButton");
            L.aimPosAction = new InputAction("Magic L Aim Pos", InputActionType.Value, "<XRController>{LeftHand}/pointerPosition", expectedControlType: "Vector3");
            R.aimPosAction = new InputAction("Magic R Aim Pos", InputActionType.Value, "<XRController>{RightHand}/pointerPosition", expectedControlType: "Vector3");
            L.aimRotAction = new InputAction("Magic L Aim Rot", InputActionType.Value, "<XRController>{LeftHand}/pointerRotation", expectedControlType: "Quaternion");
            R.aimRotAction = new InputAction("Magic R Aim Rot", InputActionType.Value, "<XRController>{RightHand}/pointerRotation", expectedControlType: "Quaternion");
        }

        void OnEnable()
        {
            if (I == null) I = this;
            foreach (var a in AllActions()) a?.Enable();
        }

        void OnDisable()
        {
            foreach (var a in AllActions()) a?.Disable();
        }

        void OnDestroy()
        {
            if (I == this) I = null;
            foreach (var a in AllActions()) a?.Dispose();
        }

        IEnumerable<InputAction> AllActions()
        {
            foreach (var h in new[] { L, R })
            {
                yield return h.triggerAction;
                yield return h.gripAction;
                yield return h.primaryAction;
                yield return h.aimPosAction;
                yield return h.aimRotAction;
            }
        }

        void Update()
        {
            if (!EnsureRig()) return;
            Read(L);
            Read(R);
        }

        bool Held(float value, bool wasHeld) => wasHeld ? value > releaseThreshold : value >= pressThreshold;

        void Read(Hand h)
        {
            h.triggerValue = h.triggerAction.ReadValue<float>();
            h.gripValue = h.gripAction.ReadValue<float>();
            bool tr = Held(h.triggerValue, h.trigger);
            bool gr = Held(h.gripValue, h.grip);
            h.triggerDown = tr && !h.trigger; h.triggerUp = !tr && h.trigger; h.trigger = tr;
            h.gripDown = gr && !h.grip; h.gripUp = !gr && h.grip; h.grip = gr;
            if (!h.grip) h.gripConsumed = false;
            bool pr = h.primaryAction.ReadValue<float>() > 0.5f;
            h.primaryDown = pr && !h.primary; h.primaryUp = !pr && h.primary; h.primary = pr;

            bool aimOk = false;
            if (h.aimRotAction.controls.Count > 0)
            {
                Quaternion q = h.aimRotAction.ReadValue<Quaternion>();
                if (q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w > 0.5f)
                {
                    Transform space = h.t.parent != null ? h.t.parent : (head != null ? head.parent : null);
                    Vector3 p = h.aimPosAction.ReadValue<Vector3>();
                    h.aimRotation = space != null ? space.rotation * q : q;
                    h.aimPosition = space != null ? space.TransformPoint(p) : p;
                    aimOk = true;
                }
            }
            if (!aimOk) { h.aimRotation = h.t.rotation; h.aimPosition = h.t.position; }

            float now = Time.time;
            Transform rigSpace = PlayerRoot;
            Vector3 pos = rigSpace != null ? rigSpace.InverseTransformPoint(h.t.position) : h.t.position;
            h.histHead = (h.histHead + 1) % Hand.HistSize;
            h.posHist[h.histHead] = pos;
            h.timeHist[h.histHead] = now;
            h.histCount = Mathf.Min(h.histCount + 1, Hand.HistSize);
            Vector3 oldP = pos;
            float oldT = now;
            for (int k = 1; k < h.histCount; k++)
            {
                int j = (h.histHead - k + Hand.HistSize) % Hand.HistSize;
                if (now - h.timeHist[j] > 0.1f) break;
                oldP = h.posHist[j];
                oldT = h.timeHist[j];
            }
            Vector3 localVel = now - oldT > 0.0001f ? (pos - oldP) / (now - oldT) : Vector3.zero;
            h.velocity = rigSpace != null ? rigSpace.TransformVector(localVel) : localVel;
        }

        public Vector3 ThrowVelocity(Hand h, float boost, float aimAssist)
        {
            Vector3 v = h.velocity * boost;
            float speed = v.magnitude;
            if (speed < 0.01f || head == null) return v;
            Vector3 dir = v / speed;
            if (Vector3.Angle(dir, head.forward) < 35f) dir = Vector3.Slerp(dir, head.forward, aimAssist).normalized;
            return dir * speed;
        }

        public void Haptic(bool left, float amp, float dur)
        {
            if (!haptics) return;
            var dev = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(left ? UnityEngine.XR.XRNode.LeftHand : UnityEngine.XR.XRNode.RightHand);
            if (dev.isValid) dev.SendHapticImpulse(0u, Mathf.Clamp01(amp), dur);
        }

        readonly Dictionary<Transform, List<(Behaviour b, PropertyInfo p)>> selectCache = new Dictionary<Transform, List<(Behaviour, PropertyInfo)>>();

        public bool IsGrabbingWithXRI(Hand h)
        {
            if (h.t == null) return false;
            if (!selectCache.TryGetValue(h.t, out var list))
            {
                list = new List<(Behaviour, PropertyInfo)>();
                foreach (var mb in h.t.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    var p = mb.GetType().GetProperty("hasSelection", BindingFlags.Public | BindingFlags.Instance);
                    if (p != null && p.PropertyType == typeof(bool)) list.Add((mb, p));
                }
                selectCache[h.t] = list;
            }
            foreach (var (b, p) in list)
                if (b != null && b.isActiveAndEnabled && (bool)p.GetValue(b)) return true;
            return false;
        }

        bool EnsureRig()
        {
            if (head == null)
            {
                var cam = Camera.main;
                if (cam == null) return false;
                head = cam.transform;
            }
            if (leftHand == null || !leftHand.gameObject.activeInHierarchy) leftHand = FindOrCreateHand(true);
            if (rightHand == null || !rightHand.gameObject.activeInHierarchy) rightHand = FindOrCreateHand(false);
            if (L.t != leftHand) { L.t = leftHand; L.histCount = 0; }
            if (R.t != rightHand) { R.t = rightHand; R.histCount = 0; }
            return Ready;
        }

        Transform FindOrCreateHand(bool left)
        {
            string[] names = left
                ? new[] { "Left Controller", "LeftHand Controller", "Left Hand Controller" }
                : new[] { "Right Controller", "RightHand Controller", "Right Hand Controller" };
            var all = FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var n in names)
                foreach (var t in all)
                    if (t.name == n) return t;

            string fallbackName = left ? "Magic Hand L" : "Magic Hand R";
            foreach (var t in all) if (t.name == fallbackName) return t;

            string side = left ? "LeftHand" : "RightHand";
            var go = new GameObject(fallbackName);
            go.transform.SetParent(head.parent, false);
            var tpd = go.AddComponent<TrackedPoseDriver>();
            tpd.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
            tpd.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;
            tpd.ignoreTrackingState = true;
            tpd.positionInput = new InputActionProperty(new InputAction(binding: $"<XRController>{{{side}}}/devicePosition", expectedControlType: "Vector3"));
            tpd.rotationInput = new InputActionProperty(new InputAction(binding: $"<XRController>{{{side}}}/deviceRotation", expectedControlType: "Quaternion"));
            tpd.positionInput.action?.Enable();
            tpd.rotationInput.action?.Enable();
            return go.transform;
        }
    }
}

namespace MagicTraining
{
    using UnityEngine;

    public enum Spell { FireArrow, Fireball, Lightning, IceLance, Tornado, IceSpikes, Storm, Vine, Earth }

    public static class SpellLock
    {
        const int Count = 9;
        static readonly bool[] busy = new bool[Count];

        public static float LastActiveTime { get; private set; } = -100f;

        public static bool AnyBusy
        {
            get
            {
                for (int i = 0; i < Count; i++) if (busy[i]) return true;
                return false;
            }
        }

        public static void Set(Spell s, bool isBusy)
        {
            busy[(int)s] = isBusy;
            if (isBusy) LastActiveTime = Time.time;
        }
        public static bool IsBusy(Spell s) => busy[(int)s];

        public static bool CanStart(Spell s)
        {
            for (int i = 0; i < Count; i++)
                if (i != (int)s && busy[i]) return false;
            return true;
        }

        public static bool CanStart(Spell s, Spell canReplace)
        {
            for (int i = 0; i < Count; i++)
                if (i != (int)s && i != (int)canReplace && busy[i]) return false;
            return true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetAll()
        {
            for (int i = 0; i < Count; i++) busy[i] = false;
            LastActiveTime = -100f;
        }
    }
}

namespace MagicTraining
{
    using UnityEngine;
    using static MagicTraining.MagicFX;

    public class MagicCircle : MonoBehaviour
    {
        public enum Element { Fire, Ice, Lightning, Wind, Water, Nature }

        Renderer outerR, innerR, glowR;
        Transform outerT, innerT;
        Color outerC, innerC, glowC;
        float size = 0.2f, spin = 1f, alpha, t, autoRelease = -1f, releaseExpand;
        bool releasing;

        public static MagicCircle Create(Element e)
        {
            var go = new GameObject("Magic Circle");
            var c = go.AddComponent<MagicCircle>();
            c.glowR = NewQuad("Glow", go.transform, GlowMat);
            c.glowR.transform.localScale = Vector3.one * 0.7f;
            c.outerR = NewQuad("Runes Outer", go.transform, RuneMat);
            c.outerT = c.outerR.transform;
            c.innerR = NewQuad("Runes Inner", go.transform, RuneMat);
            c.innerT = c.innerR.transform;
            c.innerT.localScale = Vector3.one * 0.58f;
            c.innerT.localPosition = Vector3.forward * 0.005f;
            switch (e)
            {
                case Element.Fire:      c.outerC = ArcaneGold; c.innerC = FireOrange;   c.glowC = FireOrange; break;
                case Element.Ice:       c.outerC = IceWhite;   c.innerC = IceCyan;      c.glowC = IceBlue;    break;
                case Element.Lightning: c.outerC = ElecCyan;   c.innerC = ElecViolet;   c.glowC = ElecBlue;   break;
                case Element.Nature:    c.outerC = NatureGold; c.innerC = NatureGreen;  c.glowC = NatureLeaf; break;
                case Element.Water:     c.outerC = WaterLight; c.innerC = WaterTeal;    c.glowC = WaterDeep;  break;
                default:                c.outerC = WindWhite;  c.innerC = WindTeal;     c.glowC = WindCyan;   break;
            }
            go.transform.localScale = Vector3.zero;
            c.Apply();
            return c;
        }

        public static MagicCircle Flash(Vector3 pos, Quaternion rot, float size, Element e, float hold = 0.4f, float spin = 1.5f)
        {
            var c = Create(e);
            c.Follow(pos, rot, size, spin);
            c.autoRelease = hold;
            return c;
        }

        public void Follow(Vector3 pos, Quaternion rot, float newSize, float spinSpeed = 1f)
        {
            if (releasing) return;
            transform.SetPositionAndRotation(pos, rot);
            size = newSize;
            spin = spinSpeed;
        }

        public void Release(bool burst)
        {
            if (releasing) return;
            releasing = true;
            releaseExpand = burst ? 1.4f : -0.6f;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            t += dt;
            if (autoRelease >= 0f && t >= autoRelease) Release(true);

            alpha = Mathf.MoveTowards(alpha, releasing ? 0f : 1f, dt * (releasing ? 2.5f : 5f));
            float ease = 1f - (1f - alpha) * (1f - alpha);
            float scale = releasing ? size * (1f + (1f - alpha) * releaseExpand) : size * Mathf.Lerp(0.3f, 1f, ease);
            transform.localScale = Vector3.one * Mathf.Max(0.0001f, scale);
            outerT.localRotation = Quaternion.Euler(0f, 0f, t * 40f * spin);
            innerT.localRotation = Quaternion.Euler(0f, 0f, -t * 70f * spin);
            Apply();

            if (releasing && alpha <= 0f) Destroy(gameObject);
        }

        void Apply()
        {
            float pulse = 0.85f + 0.15f * Mathf.Sin(t * 6f);
            SetColor(outerR, A(outerC, alpha));
            SetColor(innerR, A(innerC, alpha * pulse));
            SetColor(glowR, A(glowC, 0.45f * alpha));
        }
    }
}

namespace MagicTraining
{
    using System;
    using UnityEngine;

    public class FxAutoDestroy : MonoBehaviour
    {
        public float life = 2f;
        public Action<float> onUpdate;
        float t;

        void Update()
        {
            t += Time.deltaTime;
            onUpdate?.Invoke(Mathf.Clamp01(t / Mathf.Max(0.0001f, life)));
            if (t >= life) Destroy(gameObject);
        }
    }
}
