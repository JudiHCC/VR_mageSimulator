using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using static MagicTraining.MagicFX;

namespace MagicTraining
{
    public class SpellTarget : MonoBehaviour
    {
        public Vector3 minPosition = new Vector3(-6f, 1f, 5f);
        public Vector3 maxPosition = new Vector3(6f, 3f, 10f);
        public float cooldown = 0.3f;
        public float minJump = 1.5f;
        public int hits;
        Vector3 baseScale;
        float lastHit = -10f, popT = 1f;
        Rigidbody rb;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            AttachToNamed();
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        static void OnSceneLoaded(Scene s, LoadSceneMode m) => AttachToNamed();
        static void AttachToNamed()
        {
            foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t.name != "Diana" && !t.name.StartsWith("Diana ")) continue;
                if (t.GetComponent<SpellTarget>() == null) t.gameObject.AddComponent<SpellTarget>();
            }
        }
        public static void HitCollider(Collider c)
        {
            if (c == null) return;
            var target = c.GetComponentInParent<SpellTarget>();
            if (target != null) target.Hit();
        }
        public static void HitArea(Vector3 point, float radius)
        {
            foreach (var c in Physics.OverlapSphere(point, radius, ~0, QueryTriggerInteraction.Collide))
                HitCollider(c);
        }
        void Awake()
        {
            baseScale = transform.localScale;
            rb = GetComponent<Rigidbody>();
        }
        public void Hit()
        {
            if (Time.time - lastHit < cooldown) return;
            lastHit = Time.time;
            hits++;
            Vector3 from = transform.position;
            Vector3 to = from;
            for (int i = 0; i < 10; i++)
            {
                to = new Vector3(Random.Range(minPosition.x, maxPosition.x),
                                 Random.Range(minPosition.y, maxPosition.y),
                                 Random.Range(minPosition.z, maxPosition.z));
                if (Vector3.Distance(to, from) >= minJump) break;
            }
            SpawnTeleportPoof(from, true);
            if (rb != null)
            {
                if (!rb.isKinematic) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
                rb.position = to;
            }
            transform.position = to;
            SpawnTeleportPoof(to, false);
            popT = 0f;
            transform.localScale = Vector3.zero;
        }
        void Update()
        {
            if (popT >= 1f) return;
            popT = Mathf.Min(1f, popT + Time.deltaTime / 0.25f);
            float k = popT - 1f;
            float e = 1f + 2.70158f * k * k * k + 1.70158f * k * k;
            transform.localScale = baseScale * e;
        }
        void OnDisable()
        {
            if (popT < 1f) { popT = 1f; transform.localScale = baseScale; }
        }
    }
}
