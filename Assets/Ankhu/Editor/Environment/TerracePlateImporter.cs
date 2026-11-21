using System.IO;
using Ankhu.Environment;
using UnityEditor;
using UnityEngine;

namespace Ankhu.EditorTools.Environment
{
    /// <summary>
    /// Import settings for environment plates under Assets/Art/Environments/**/plates_4k/.
    /// Plates are authored at 2160 px frame height. PPU 168 makes the frame 12.86 world units tall,
    /// so Ankhu (1.8 m) is 14 percent of frame height, matching the art bible and the character pipeline.
    /// Every plate has a bottom-left pivot and sits at the frame bottom (cropped layers stay bottom-anchored).
    /// Adapted from the environment pipeline's draft (art/environments/unity).
    /// </summary>
    public sealed class TerracePlateImporter : AssetPostprocessor
    {
        public const float PixelsPerUnit = 168f;
        public const float FrameHeightPixels = 2160f;
        public const float FrameHeight = FrameHeightPixels / PixelsPerUnit;
        /// <summary>Feet line as a fraction of frame height, measured from the bottom.</summary>
        public const float WalkLineFraction = 0.265f;

        public struct LayerSpec
        {
            public string Name;
            public float Factor;
            public bool Tileable;
            public int SortingOrder;
            public float Z;

            public LayerSpec(string name, float factor, bool tileable, int sortingOrder, float z)
            {
                Name = name;
                Factor = factor;
                Tileable = tileable;
                SortingOrder = sortingOrder;
                Z = z;
            }
        }

        public static readonly LayerSpec[] TerraceLayers =
        {
            new LayerSpec("terrace_sky", 0.04f, true, -500, 50f),
            new LayerSpec("terrace_far", 0.15f, true, -400, 40f),
            new LayerSpec("terrace_mid", 0.40f, false, -300, 30f),
            new LayerSpec("terrace_play", 1.00f, true, -100, 0.5f),
            new LayerSpec("terrace_fg", 1.30f, true, 500, -5f),
        };

        private void OnPreprocessTexture()
        {
            string p = assetPath.Replace('\\', '/');
            if (!p.Contains("/Environments/") || !p.Contains("/plates_4k/"))
            {
                return;
            }
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = PixelsPerUnit;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.wrapMode = TextureWrapMode.Repeat;
            ti.filterMode = FilterMode.Bilinear;
            ti.maxTextureSize = 16384;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
            ti.npotScale = TextureImporterNPOTScale.None;
            var s = new TextureImporterSettings();
            ti.ReadTextureSettings(s);
            s.spriteMeshType = SpriteMeshType.FullRect;
            s.spriteAlignment = (int)SpriteAlignment.BottomLeft;
            ti.SetTextureSettings(s);
        }

        [MenuItem("Ankhu/Environment/Build Terrace Parallax Rig")]
        private static void BuildRigFromMenu()
        {
            GameObject root = BuildRig(0f);
            Selection.activeGameObject = root;
        }

        /// <summary>
        /// Builds the five terrace layers under a new root in the open scene. The frame bottom sits at
        /// <paramref name="walkLineY"/> minus the walk line height, so Ankhu's feet at walkLineY land on
        /// the painted walk line. A camera at <see cref="CameraYForWalkLine"/> with orthographic size
        /// FrameHeight / 2 frames it exactly.
        /// </summary>
        public static GameObject BuildRig(float walkLineY)
        {
            string[] guids = AssetDatabase.FindAssets("terrace_ t:Sprite");
            var root = new GameObject("TerraceParallax");
            float frameBottom = walkLineY - FrameHeight * WalkLineFraction;
            foreach (LayerSpec l in TerraceLayers)
            {
                Sprite sprite = null;
                foreach (string g in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(g);
                    if (path.Contains("/plates_4k/") && Path.GetFileNameWithoutExtension(path) == l.Name)
                    {
                        sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    }
                }
                if (sprite == null)
                {
                    Debug.LogWarning("Missing plate " + l.Name);
                    continue;
                }
                var go = new GameObject(l.Name);
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = new Vector3(-sprite.bounds.size.x * 0.5f, frameBottom, l.Z);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.sortingOrder = l.SortingOrder;
                go.AddComponent<ParallaxLayer>().Configure(l.Factor, l.Tileable);
            }
            return root;
        }

        public static float CameraYForWalkLine(float walkLineY)
        {
            return walkLineY - FrameHeight * WalkLineFraction + FrameHeight * 0.5f;
        }
    }
}
