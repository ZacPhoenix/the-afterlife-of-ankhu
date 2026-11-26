using UnityEngine;

namespace Ankhu.Realms.EditorTools
{
    /// <summary>
    /// Rooms A, B and C under docs/PAINTED_ROOM_SPEC.md: the live primitives that Cardify leaves (water sheets, wet
    /// beds, shoots, the shadoof, the bridge, threads, bolts, glows) lose their renderers and wear a painted skin from
    /// the abc_ props atlas or a painted fx sprite. A skin is a child of the primitive it replaces, so every pose that
    /// shows, hides, moves or turns the primitive carries the paint with it. The skin undoes the primitive's scale,
    /// so sizes here are in world units.
    /// </summary>
    public static partial class RealmSceneBuilder
    {
        private static readonly Color Cyan = new Color(0.55f, 1f, 1.1f, 1f);

        /// <summary>Returns 1 when t was a Room A, B or C piece and is now painted.</summary>
        private static int PaintABC(Transform t, string pn)
        {
            string n = t.name;
            Renderer r = t.GetComponent<Renderer>();
            string gp = t.parent != null && t.parent.parent != null ? t.parent.parent.name : "";

            // ---------------------------------------------------------------- water, mud and flood
            if (r != null && (n == "ChannelWater" || n == "TroughWater" || n == "Run" || n == "DoorStream"))
            {
                Hide(t);
                Skin(t, WaterMat(), new Vector2(t.lossyScale.x, t.lossyScale.z), Vector3.up * Top(r, t), flat: true);
                return 1;
            }
            if (r != null && pn == "WaterBody" && (n == "Rippled" || n == "Mirror"))
            {
                Hide(t);
                Material m = n == "Mirror"
                    ? FlipMat("flip_abc_mirror", SpriteTex("abc_flood", out _), new Color(0.8f, 0.9f, 1.1f, 1f), 1.15f, 0.06f, 0.6f, 0f, false)
                    : FlipMat("flip_abc_ripple", SpriteTex("abc_flood", out _), Color.white, 1f, 0.12f, 1.6f, 0.05f, false);
                Skin(t, m, new Vector2(t.lossyScale.x, t.lossyScale.z), Vector3.up * Top(r, t), flat: true);
                return 1;
            }
            if (r != null && n == "ThreadShadow")
            {
                Hide(t);
                Skin(t, FlipMat("flip_abc_threadshadow", SpriteTex("abc_thread", out _), new Color(0f, 0f, 0f, 0.55f), 1f, 0f, 1f, 0f, false),
                    new Vector2(3.2f, 0.22f), Vector3.up * 0.012f, flat: true, local: true);
                return 1;
            }
            if (r != null && n == "Pour")
            {
                Hide(t);
                Skin(t, WaterMat(), new Vector2(t.lossyScale.y, 0.2f), Vector3.zero, spin: 90f);
                return 1;
            }
            if (r != null && n == "Bed" && pn == "Wet")
            {
                Hide(t);
                Skin(t, FlipMat("flip_abc_mud", SpriteTex("abc_mud", out _), new Color(1f, 1f, 1f, 0.82f), 1f, 0.05f, 0.8f, 0f, false),
                    new Vector2(t.lossyScale.x, t.lossyScale.z), Vector3.up * Top(r, t), flat: true);
                return 1;
            }
            if (r != null && n == "Sheet" && pn == "Flood")
            {
                Hide(t);
                Skin(t, FlipMat("flip_abc_flood", SpriteTex("abc_flood", out _), new Color(1f, 1f, 1f, 0.88f), 1.05f, 0.12f, 1.3f, 0.05f, false),
                    new Vector2(t.lossyScale.x, t.lossyScale.z), Vector3.up * Top(r, t), flat: true);
                return 1;
            }
            if (r != null && n.StartsWith("Drowned") && pn == "Flood")
            {
                Hide(t);
                int i = n.Length > 7 && int.TryParse(n.Substring(7), out int k) ? k : 0;
                Skin(t, SpriteMat("abc_reed_dead", out _), new Vector2(0.55f, 0.12f), Vector3.up * 0.015f, flat: true, spin: (i % 3 - 1) * 18f);
                return 1;
            }
            if (r != null && n == "Crest" && pn == "Swell")
            {
                // The surge reads from the front as a standing wall of water and from above as a moving sheet.
                Hide(t);
                Vector3 s = t.lossyScale;
                Skin(t, WaterMat(), new Vector2(s.x, s.z), Vector3.up * (s.y * 0.5f + 0.01f), flat: true);
                Skin(t, WaterMat(), new Vector2(s.x, s.y * 1.3f), new Vector3(0f, 0f, -s.z * 0.5f - 0.01f));
                return 1;
            }

            // ---------------------------------------------------------------- shoots and leaves
            if (r != null && n == "Leaf" && pn.StartsWith("Blade"))
            {
                Hide(t);
                // On the blade, not the thin leaf box, so the tuft stands upright whatever way the blade was spun.
                Skin(t.parent, FlipMat("flip_abc_sprout", SpriteTex("abc_sprout", out _), Color.white, 1f, 0f, 1f, 0f, false, 0.05f),
                    new Vector2(0.25f, 0.34f), Vector3.up * 0.15f);
                return 1;
            }
            if (r != null && n == "Blade" && pn.StartsWith("Leaf") && gp == "Leaves")
            {
                Hide(t);
                Skin(t, FlipMat("flip_abc_leaf", SpriteTex("abc_petal", out _), new Color(0.5f, 0.85f, 0.4f, 1f), 1f, 0f, 1f, 0f, false, 0.02f),
                    new Vector2(0.16f, 0.32f), Vector3.zero, flat: true, local: true);
                return 1;
            }
            if (r != null && n == "Leaf" && pn.StartsWith("Petal"))
            {
                Hide(t);
                Skin(t, FlipMat("flip_abc_petal", SpriteTex("abc_petal", out _), Color.white, 1.05f, 0.08f, 1.1f, 0f, false),
                    new Vector2(0.15f, 0.3f), Vector3.zero, local: true);
                return 1;
            }

            // ---------------------------------------------------------------- shadoof
            if (r != null && n == "Beam" && pn == "BeamPivot")
            {
                Hide(t);
                Skin(t, SpriteMat("abc_beam", out _), new Vector2(t.lossyScale.x, 0.36f), Vector3.zero, local: true);
                return 1;
            }
            if (r != null && n == "Plate" && pn == "Footplate")
            {
                Hide(t);
                Skin(t, SpriteMat("abc_footplate", out _), new Vector2(0.95f, 0.4f), Vector3.up * 0.1f, local: true);
                return 1;
            }
            if (r != null && n == "Pot" && pn == "Hang")
            {
                Hide(t);
                // The pot sprite carries its own neck rope, so it stands taller than the clay sphere and sits on its base.
                Skin(t, SpriteMat("abc_pot", out float a), new Vector2(0.5f, 0.5f / a), new Vector3(0f, 0.5f / a * 0.5f - 0.21f, 0f));
                return 1;
            }
            if (r != null && n == "Rope" && (pn == "Hang" || pn == "Arms"))
            {
                Hide(t);
                Skin(t, SpriteMat("abc_rope", out _), new Vector2(t.lossyScale.y, 0.1f), Vector3.zero, spin: 90f, local: true);
                return 1;
            }
            if (r != null && pn == "Binding" && (n == "Knot" || n == "Thread"))
            {
                Hide(t);
                if (n == "Knot") Skin(t, FxMat("fx_glow", Cyan, 1.4f, 0.3f, 1.6f, 0.08f), new Vector2(0.7f, 0.7f), new Vector3(0f, 0f, -0.1f));
                else Skin(t, ThreadMat(), new Vector2(t.lossyScale.y * 2f, 0.12f), Vector3.zero, spin: 90f, local: true);
                return 1;
            }
            if (r != null && n == "Pole" && pn == "Yoke")
            {
                Hide(t);
                Skin(t, SpriteMat("abc_pole", out _), new Vector2(1.45f, 0.13f), Vector3.zero, local: true);
                return 1;
            }

            // ---------------------------------------------------------------- falcon toys and shabti eyes
            if (t.Find("Body") != null && t.Find("Beak") != null && t.Find("Base") != null)
            {
                foreach (Renderer c in t.GetComponentsInChildren<Renderer>(true)) c.enabled = false;
                Skin(t, SpriteMat("abc_falcon", out float a), new Vector2(0.36f * a, 0.36f), new Vector3(0f, 0.17f, -0.08f));
                return 1;
            }
            if (r != null && (n == "EyeL" || n == "EyeR") && pn == "Eyes")
            {
                Hide(t);
                Skin(t, FxMat("fx_glint", new Color(1f, 0.85f, 0.5f, 1f), 1.6f, 0.25f, 2.0f, 0.15f), new Vector2(0.13f, 0.13f), new Vector3(0f, 0f, -0.02f));
                return 1;
            }

            // ---------------------------------------------------------------- bridge and holding threads
            if (r != null && pn == "Deck")
            {
                if (n.StartsWith("Plank"))
                {
                    Hide(t);
                    Skin(t, SpriteMat("abc_plank", out _), new Vector2(1.4f, 0.46f), Vector3.up * 0.045f, flat: true, spin: 90f, local: true);
                    return 1;
                }
                if (n.StartsWith("Stringer"))
                {
                    Hide(t);
                    Skin(t, SpriteMat("abc_beam", out _), new Vector2(t.lossyScale.x, 0.14f), Vector3.zero, local: true);
                    return 1;
                }
                if (n == "RopeRail")
                {
                    Hide(t);
                    Skin(t, SpriteMat("abc_rope", out _), new Vector2(t.lossyScale.x, 0.09f), Vector3.zero, local: true);
                    return 1;
                }
            }
            if (r != null && pn == "Threads" && (n.StartsWith("Thread") || n.StartsWith("Knot")))
            {
                Hide(t);
                if (n.StartsWith("Knot")) Skin(t, FxMat("fx_glow", Cyan, 1.4f, 0.3f, 1.6f, 0.08f), new Vector2(0.5f, 0.5f), new Vector3(0f, 0f, -0.05f));
                else Skin(t, ThreadMat(), new Vector2(t.lossyScale.x, 0.12f), Vector3.zero);
                return 1;
            }

            // ---------------------------------------------------------------- Room C door pieces (the doorway faces along x)
            if (r != null && pn == "Thread" && (n == "Strand" || n == "Knot"))
            {
                Hide(t);
                if (n == "Knot") Skin(t, FxMat("fx_glow", Cyan, 1.4f, 0.3f, 1.6f, 0.08f), new Vector2(0.45f, 0.45f), Vector3.zero);
                else Skin(t, ThreadMat(), new Vector2(t.lossyScale.z, 0.12f), Vector3.zero, yaw: 90f);
                return 1;
            }
            if (r != null && n == "Bar" && pn == "Bolt")
            {
                Hide(t);
                Skin(t, FlipMat("flip_abc_bolt", SpriteTex("abc_bolt", out _), Color.white, 1.1f, 0.12f, 1.4f, 0f, false), new Vector2(0.95f, 0.18f), Vector3.zero, yaw: 90f);
                return 1;
            }
            if (r != null && n == "Seal" && pn == "SealMark")
            {
                Hide(t);
                Skin(t, FlipMat("flip_abc_seal", SpriteTex("abc_seal", out _), Color.white, 1.1f, 0.18f, 1.2f, 0.04f, false), new Vector2(0.52f, 0.52f), new Vector3(-0.02f, 0f, 0f), yaw: 90f);
                return 1;
            }
            if (r != null && n == "Glow" && pn == "Niche")
            {
                Hide(t);
                Skin(t, FlipMat("fx_glow_amber", FxTex("fx_glow"), new Color(1f, 0.75f, 0.42f, 1f), 1.3f, 0.2f, 1.2f, 0.06f, true),new Vector2(1.5f, 2.6f), Vector3.zero, local: true);
                return 1;
            }
            return 0;
        }

        private static Material WaterMat() =>
            FlipMat("flip_abc_water", SpriteTex("abc_water", out _), Color.white, 1.05f, 0.12f, 1.3f, 0.05f, false);

        private static Material ThreadMat() =>
            FlipMat("flip_abc_thread", SpriteTex("abc_thread", out _), Cyan, 1.5f, 0.25f, 1.6f, 0.05f, false);

        /// <summary>Height of the renderer's top face above the transform's origin, plus a hair.</summary>
        private static float Top(Renderer r, Transform t) => r.bounds.max.y - t.position.y + 0.012f;

        /// <summary>
        /// A painted quad under t that cancels t's scale. Upright quads face the room camera (down +z), flat ones lie
        /// face up. Local skins take t's own rotation, so they follow a plank's yaw or a petal's tilt in its own plane.
        /// spin turns the sprite inside its plane, yaw turns an upright quad about world up (Room C's doorway faces x).
        /// </summary>
        private static Transform Skin(Transform t, Material m, Vector2 size, Vector3 worldOffset, bool flat = false, float spin = 0f, bool local = false, float yaw = 0f)
        {
            if (t == null || m == null) return null;
            var h = new GameObject("Skin").transform;
            h.SetParent(t, false);
            Quaternion rot = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(flat ? 90f : 0f, 0f, 0f) * Quaternion.Euler(0f, 0f, spin);
            if (local) h.localRotation = rot; else h.rotation = rot;
            h.position = t.position + worldOffset;
            h.localScale = Vector3.one;
            Vector3 ls = h.lossyScale;
            h.localScale = new Vector3(Inv(ls.x), Inv(ls.y), Inv(ls.z));
            Quad(h, m, Vector3.zero, size);
            return h;
        }

        private static float Inv(float v) => Mathf.Abs(v) < 1e-4f ? 1f : 1f / v;
    }
}
