using System;
using System.Collections.Generic;
using Ankhu.Opening;
using Ankhu.Realms.Core;
using Ankhu.Player;
using UnityEngine;
using UnityEngine.SceneManagement;
using Random = UnityEngine.Random;

namespace Ankhu.Realms
{
    /// <summary>
    /// Zac 2026-11-25: once Anpu has given him the roll, it floats beside Ankhu and follows him. It hangs at his shoulder,
    /// trailing him on a soft spring, breathing up and down, leaning into his walk. It glows softly and sheds green and
    /// white glyphs behind it: a few when he stands still, a stream when he walks.
    ///
    /// The model is Zac's Resources/RealmCodex/RollModel when it exists, else the procedural stand-in (see
    /// <see cref="CodexGift.CreateRollModel"/>). The glyphs are the real signs written on the roll (its band
    /// pr.t m hrw and his name), plus the signs of every word and name he has written into it, drawn with
    /// the painted glow sprites of <see cref="GlyphAtlas"/>.
    ///
    /// Bootstraps itself on every scene load. It is only there when the roll has been given (codex entry lore.codex)
    /// and the scene has a player (WalkPlayer in the realms, PlayerController2D in the side-on rooms). It hides while
    /// the gift is still in his hands and while the roll is open on screen, and the open roll flies out from it.
    /// </summary>
    public sealed class CodexFamiliar : MonoBehaviour
    {
        // ------------------------------------------------------------------ tuning

        /// <summary>The model is life size here (about 0.34 m long), smaller than in the gift's close two-shot.</summary>
        public const float ModelScale = 0.9f;
        private const float Height = 1.55f;          // above his feet: shoulder height
        private const float Trail = 0.45f;           // behind him, along his travel
        private const float Side = 0.32f;            // out to his side, toward the camera
        private const float FollowTime = 0.42f;      // spring smoothing
        private const float BobAmp = 0.045f, BobHz = 0.32f;
        private const int MaxGlyphs = 28;

        private static readonly Color Green = new Color(0.46f, 1f, 0.66f, 1f);
        private static readonly Color White = new Color(1f, 0.98f, 0.92f, 1f);

        /// <summary>The signs on the roll itself: the band (pr.t m hrw) and the cartouche (Ankhu).</summary>
        private static readonly string[] RollSigns = { "O1", "D21", "X1", "D54", "G17", "O4", "G43", "N5", "S34", "N35", "Aa1", "A1" };

        private static CodexFamiliar instance;

        // ------------------------------------------------------------------ audio hooks (for the SFX thread)

        /// <summary>The floating roll, or null. Parent a looping hum AudioSource here and scale its volume by <see cref="Presence"/>.</summary>
        public static Transform Current => instance != null && instance.presence > 0.001f ? instance.transform : null;
        /// <summary>0 hidden .. 1 fully shown (fades in about 0.9 s, out about 0.25 s).</summary>
        /// <summary>Set by a room whose script hands the roll over (the First Gate) until the gift has played.</summary>
        public static bool HeldUntilGift;

        public static float Presence => instance != null ? instance.presence : 0f;
        /// <summary>Smoothed speed of Ankhu in m/s, for pitching the hum or thickening a shimmer bed.</summary>
        public static float Speed => instance != null ? instance.speed : 0f;
        /// <summary>The roll appears beside him (after the gift, after the roll closes, on entering a scene).</summary>
        public static event Action<Vector3> Appeared;
        /// <summary>The roll fades away (the roll opens on screen, a cutscene, leaving the scene).</summary>
        public static event Action<Vector3> Departed;
        /// <summary>One glyph shed: world position and its Gardiner code. Fires 0.7 to 7 times a second, so throttle.</summary>
        public static event Action<Vector3, string> GlyphShed;

        // ------------------------------------------------------------------ state

        private Transform target;
        private Transform model;
        private Renderer[] renderers;
        private MaterialPropertyBlock block;
        private FxQuad halo;
        private Light lamp;

        private Vector3 vel;
        private Vector3 lastTargetPos;
        private Vector3 travel = Vector3.back;     // smoothed direction of his movement
        private float speed;                        // smoothed m/s
        private float presence;                     // 0 hidden .. 1 shown
        private float spawnAcc;
        private float t0;
        private bool placed;
        private readonly List<string> signs = new List<string>();
        private float nextSignsCheck;
        private float nextSearch;

        private sealed class Glyph
        {
            public FxQuad Quad;
            public Vector3 Vel;
            public float Age, Life, Size, Roll, Spin;
            public Color Color;
        }

        private readonly List<Glyph> glyphs = new List<Glyph>();
        private readonly Stack<FxQuad> spare = new Stack<FxQuad>();

        // ------------------------------------------------------------------ bootstrap

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { HeldUntilGift = false; instance = null; Appeared = null; Departed = null; GlyphShed = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Ensure();
        }

        private static void OnSceneLoaded(Scene s, LoadSceneMode mode) => Ensure();

        private static void Ensure()
        {
            if (instance != null) { instance.placed = false; instance.target = null; instance.nextSearch = 0f; return; }
            var go = new GameObject("CodexFamiliar");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<CodexFamiliar>();
        }

        private void Awake()
        {
            block = new MaterialPropertyBlock();
            t0 = Random.value * 10f;
            model = CodexGift.CreateRollModel(transform);
            model.localScale = Vector3.one * ModelScale;
            renderers = model.GetComponentsInChildren<Renderer>();

            halo = new FxQuad("FamiliarHalo", GlyphAtlas.Mote, transform);
            halo.Transform.localScale = Vector3.one * 0.85f;

            var lampGo = new GameObject("FamiliarLight");
            lampGo.transform.SetParent(transform, false);
            lamp = lampGo.AddComponent<Light>();
            lamp.type = LightType.Point;
            lamp.color = new Color(0.62f, 1f, 0.78f);
            lamp.range = 1.8f;
            lamp.intensity = 0f;
            lamp.shadows = LightShadows.None;

            SetVisible(0f);
        }

        private void OnDestroy()
        {
            foreach (Glyph g in glyphs) g.Quad?.Destroy();
            glyphs.Clear();
            while (spare.Count > 0) spare.Pop()?.Destroy();
            halo?.Destroy();
            if (instance == this) instance = null;
        }

        // ------------------------------------------------------------------ who to follow

        private Transform FindTarget()
        {
            WalkPlayer walk = FindAnyObjectByType<WalkPlayer>();
            if (walk != null && walk.isActiveAndEnabled) return walk.transform;
            PlayerController2D side = FindAnyObjectByType<PlayerController2D>();
            if (side != null && side.isActiveAndEnabled) return side.transform;
            return null;
        }

        private bool Wanted()
        {
            if (target == null) return false;
            if (!CodexStore.Has("lore.codex")) return false;
            // Zac 2026-11-26: the roll floated beside Ankhu in the opening. The journal persists between runs, so a
            // replay already has lore.codex. The roll only follows from the First Gate on, and not before Anpu gives it.
            if (HeldUntilGift) return false;
            int here = PlayOrder.IndexOf(SceneManager.GetActiveScene().name), gate = PlayOrder.IndexOf(OpeningSettings.FirstGateScene);
            if (here >= 0 && gate >= 0 && here < gate) return false;
            if (CodexGift.Busy) return false;            // still in his hands
            if (CodexBook.IsOpen) return false;          // the roll is open on screen
            return true;
        }

        // ------------------------------------------------------------------ per frame

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            if ((target == null || !target.gameObject.activeInHierarchy) && Time.unscaledTime >= nextSearch)
            {
                nextSearch = Time.unscaledTime + 0.5f;
                target = FindTarget();
                placed = false;
            }
            if (target != null && !target.gameObject.activeInHierarchy) target = null;

            bool want = Wanted();
            float before = presence;
            if (before >= 0.999f && !want) Raise(Departed, transform.position);
            presence = Mathf.MoveTowards(presence, want ? 1f : 0f, dt / (want ? 0.9f : 0.25f));

            if (presence <= 0f && !want) placed = false;   // re-place when it next appears
            if (target != null)
            {
                Track(dt, want);
                if (want && before <= 0.001f && presence > 0f) Arrive();
            }

            SetVisible(presence);
            UpdateGlyphs(dt);
            if (presence > 0.01f) Shed(dt);
            PointRollOrigin();
        }

        private void Track(float dt, bool want)
        {
            Vector3 p = target.position;
            if (!placed)
            {
                lastTargetPos = p;
                travel = Forward();
            }
            Vector3 d = (p - lastTargetPos) / Mathf.Max(dt, 1e-4f);
            lastTargetPos = p;
            d.y = 0f;
            float s = d.magnitude;
            speed = Mathf.Lerp(speed, s, 1f - Mathf.Exp(-6f * dt));
            if (s > 0.15f) travel = Vector3.Slerp(travel, d / s, 1f - Mathf.Exp(-4f * dt));
            else travel = Vector3.Slerp(travel, Forward(), 1f - Mathf.Exp(-0.8f * dt));
            travel.y = 0f;
            if (travel.sqrMagnitude < 1e-4f) travel = Vector3.forward;
            travel.Normalize();

            // Out to the side that faces the camera, so he never hides it.
            Camera cam = FxQuad.ResolveCamera(Camera.main);
            Vector3 side = Vector3.Cross(Vector3.up, travel);
            if (cam != null && Vector3.Dot(side, cam.transform.position - p) < 0f) side = -side;

            float t = Time.time + t0;
            float bob = Mathf.Sin(t * Mathf.PI * 2f * BobHz) * BobAmp + Mathf.Sin(t * 1.7f) * 0.012f;
            Vector3 goal = p + Vector3.up * (Height + bob) - travel * Trail + side * Side;

            if (!placed)
            {
                Vector3 h = Vector3.zero;
                bool fromHands = want && CodexGift.ConsumeHandoff(out h);
                transform.position = fromHands ? h : goal + Vector3.up * 0.25f;
                vel = Vector3.zero;
                placed = want;
            }
            transform.position = Vector3.SmoothDamp(transform.position, goal, ref vel, FollowTime, 6f, dt);

            // Long axis across his path, leaning into the walk, a slow sway of its own.
            float lean = Mathf.Clamp(speed * 7f, 0f, 16f);
            float sway = Mathf.Sin(t * 0.9f) * 7f;
            float roll = Mathf.Sin(t * 0.6f + 1.3f) * 4f;
            Quaternion face = Quaternion.LookRotation(travel, Vector3.up);
            Quaternion aim = face * Quaternion.Euler(lean, sway, roll);
            transform.rotation = Quaternion.Slerp(transform.rotation, aim, 1f - Mathf.Exp(-4f * dt));
            // The roll spins slowly about its own long axis, like something kept aloft by breath.
            model.localRotation = Quaternion.Euler(t * 9f, 0f, 0f);
        }

        private Vector3 Forward()
        {
            if (target == null) return Vector3.forward;
            PlayerController2D side = target.GetComponent<PlayerController2D>();
            if (side != null) return side.Facing >= 0 ? Vector3.right : Vector3.left;
            Vector3 f = target.forward;
            f.y = 0f;
            return f.sqrMagnitude > 1e-4f ? f.normalized : Vector3.forward;
        }

        private static void Raise(Action<Vector3> e, Vector3 p)
        {
            try { e?.Invoke(p); } catch (Exception ex) { Debug.LogException(ex); }
        }

        private void Arrive()
        {
            Raise(Appeared, transform.position);
            MagicFx.FlareAt(transform.position, 0.45f, 0.4f, Green);
            for (int i = 0; i < 6; i++) SpawnGlyph(Random.onUnitSphere * 0.5f + Vector3.up * 0.3f);
        }

        private void SetVisible(float k)
        {
            float e = Ease.InOutSine(k);
            if (model != null)
            {
                bool on = e > 0.01f;
                if (model.gameObject.activeSelf != on) model.gameObject.SetActive(on);
                model.localScale = Vector3.one * ModelScale * Mathf.Lerp(0.6f, 1f, e);
            }
            float pulse = 0.85f + 0.15f * Mathf.Sin((Time.time + t0) * 1.6f);
            if (halo != null)
            {
                Camera cam = FxQuad.ResolveCamera(Camera.main);
                halo.Billboard(cam, (Time.time + t0) * 6f);
                halo.SetColor(Color.Lerp(Green, White, 0.35f), 0.55f * pulse, 0.32f * e);
            }
            if (lamp != null) lamp.intensity = 0.55f * e * pulse;
            if (renderers != null)
            {
                Color glow = MagicPalette.Hdr(new Color(0.55f, 1f, 0.72f), 0.18f * e * pulse);
                foreach (Renderer r in renderers)
                {
                    if (r == null) continue;
                    r.GetPropertyBlock(block);
                    block.SetColor(MagicMaterials.EmissionColorId, glow);
                    r.SetPropertyBlock(block);
                }
            }
        }

        // ------------------------------------------------------------------ glyphs

        private void Shed(float dt)
        {
            // A few when he stands still, a stream when he walks.
            float rate = Mathf.Lerp(0.7f, 7f, Mathf.Clamp01(speed / 2.2f)) * presence;
            spawnAcc += rate * dt;
            while (spawnAcc >= 1f)
            {
                spawnAcc -= 1f;
                SpawnGlyph(Vector3.zero);
            }
            // Fine motes between the glyphs.
            if (Random.value < (0.6f + speed) * dt * 6f * presence)
            {
                Vector3 jitter = Random.insideUnitSphere * 0.12f;
                Vector3 v = -travel * (0.1f + speed * 0.12f) + Vector3.up * Random.Range(0.05f, 0.25f);
                MagicFx.Mote(transform.position + jitter, v, Random.Range(0.012f, 0.022f), Random.Range(0.8f, 1.5f), Random.value < 0.6f ? Green : White);
            }
        }

        private void SpawnGlyph(Vector3 extraVel)
        {
            if (glyphs.Count >= MaxGlyphs) return;
            string code = PickSign();
            if (code == null) return;
            Texture tex = GlyphAtlas.GetGlowTexture(code);
            if (tex == null) tex = GlyphAtlas.GetTexture(code);
            if (tex == null) return;

            FxQuad q = spare.Count > 0 ? spare.Pop() : null;
            if (q == null || !q.Alive) q = new FxQuad("FamiliarGlyph", tex);
            else q.Renderer.sharedMaterial = MagicMaterials.Additive(tex);
            q.GameObject.SetActive(true);

            // Off the trailing end of the roll, drifting back along his path and rising like warm air.
            Vector3 along = transform.right * Random.Range(-0.17f, 0.17f);
            q.Transform.position = transform.position + along + Random.insideUnitSphere * 0.05f;
            var g = new Glyph
            {
                Quad = q,
                Vel = -travel * (0.12f + speed * 0.22f) + Vector3.up * Random.Range(0.1f, 0.26f) + Random.insideUnitSphere * 0.06f + extraVel,
                Life = Random.Range(2.2f, 3.4f),
                Size = Random.Range(0.075f, 0.12f),
                Roll = Random.Range(-12f, 12f),
                Spin = Random.Range(-10f, 10f),
                Color = Random.value < 0.62f ? Green : White,
            };
            glyphs.Add(g);
            try { GlyphShed?.Invoke(q.Transform.position, code); } catch (Exception ex) { Debug.LogException(ex); }
        }

        private void UpdateGlyphs(float dt)
        {
            Camera cam = FxQuad.ResolveCamera(Camera.main);
            for (int i = glyphs.Count - 1; i >= 0; i--)
            {
                Glyph g = glyphs[i];
                if (g.Quad == null || !g.Quad.Alive) { glyphs.RemoveAt(i); continue; }
                g.Age += dt;
                float k = g.Age / g.Life;
                if (k >= 1f)
                {
                    g.Quad.SetColor(g.Color, 0f, 0f);
                    g.Quad.GameObject.SetActive(false);
                    spare.Push(g.Quad);
                    glyphs.RemoveAt(i);
                    continue;
                }
                g.Vel *= Mathf.Exp(-0.9f * dt);
                g.Vel += Vector3.up * 0.04f * dt;
                g.Quad.Transform.position += g.Vel * dt;
                g.Roll += g.Spin * dt;
                g.Quad.Billboard(cam, g.Roll);
                float grow = Mathf.Lerp(0.7f, 1.15f, Ease.OutCubic(k));
                g.Quad.Transform.localScale = Vector3.one * g.Size * grow;
                float fade = Mathf.Clamp01(k / 0.12f) * (1f - Ease.InQuad(Mathf.Clamp01((k - 0.35f) / 0.65f)));
                g.Quad.SetColor(g.Color, 1.15f, fade * 0.85f);
            }
        }

        /// <summary>The roll's own signs, and the signs of every word and name he has written into it.</summary>
        private string PickSign()
        {
            if (Time.unscaledTime >= nextSignsCheck || signs.Count == 0)
            {
                nextSignsCheck = Time.unscaledTime + 3f;
                signs.Clear();
                foreach (string c in RollSigns) if (GlyphAtlas.Has(c)) signs.Add(c);
                foreach (CodexEntry e in CodexCatalog.All.Values)
                {
                    if (e == null || string.IsNullOrEmpty(e.Signs) || !CodexStore.Has(e.Id)) continue;
                    if (e.Kind != CodexKind.Word && e.Kind != CodexKind.Name) continue;
                    foreach (string c in e.Signs.Split(' '))
                        if (c.Length > 0 && GlyphAtlas.Has(c) && !signs.Contains(c)) signs.Add(c);
                }
            }
            return signs.Count > 0 ? signs[Random.Range(0, signs.Count)] : null;
        }

        /// <summary>When the roll is opened, it flies out from here.</summary>
        private void PointRollOrigin()
        {
            if (presence < 0.5f || CodexBook.IsOpen) return;
            Camera cam = FxQuad.ResolveCamera(Camera.main);
            if (cam == null) return;
            Vector3 sp = cam.WorldToScreenPoint(transform.position);
            if (sp.z > 0f) CodexBook.SetNextOpenOrigin(new Vector2(sp.x, sp.y));
        }
    }
}
