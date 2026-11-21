using System;
using System.Collections.Generic;
using System.IO;
using Ankhu.Core;
using Ankhu.Integration;
using Ankhu.Testing;
using Ankhu.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using D = Ankhu.World.GreyboxDepth;
using L = Ankhu.Layout.BorrowedDawnLayout;
using P = Ankhu.World.GreyboxPalette;
using Object = UnityEngine.Object;

namespace Ankhu.EditorTools
{
    /// <summary>
    /// Builds the Borrowed Dawn greybox scene from BorrowedDawnLayout values. Idempotent: every run
    /// overwrites the generated materials, prefabs and scene under Assets/Ankhu/Generated.
    /// Visuals are 3D cubes for URP (Universal renderer). Gameplay collision is Physics2D in the XY
    /// plane with the player walking at z = 0.
    /// </summary>
    public static class BorrowedDawnGreyboxBuilder
    {
        public const string GeneratedRoot = "Assets/Ankhu/Generated";
        public const string MaterialsFolder = GeneratedRoot + "/Materials";
        public const string PrefabsFolder = GeneratedRoot + "/Prefabs";
        public const string ScenesFolder = GeneratedRoot + "/Scenes";
        public const string ScenePath = ScenesFolder + "/BorrowedDawn_Greybox.unity";
        public const string TeachingDirectorTypeName = "Ankhu.Bindings.TeachingRoomDirector, Ankhu.Bindings";

        /// <summary>
        /// One playable scene of the sequence. Teaching rooms reuse the full Borrowed Dawn kit, so the
        /// geometry is identical. Only the rules room, Ankhu's spawn and an optional seal differ.
        /// </summary>
        public sealed class RoomSceneSpec
        {
            public string SceneName;
            public string RoomId;
            public Vector2 Spawn;
            /// <summary>Seals the upper walkway at the east end of the gallery so the lesson stays in view.</summary>
            public bool SealGalleryEast;

            public string ScenePath => ScenesFolder + "/" + SceneName + ".unity";
        }

        /// <summary>Play order. Scene names must match Ankhu.Bindings.BorrowedDawnRooms.</summary>
        public static readonly RoomSceneSpec[] Sequence =
        {
            new RoomSceneSpec { SceneName = "Teach1_Cistern", RoomId = "teach-1-cistern", Spawn = L.Spawn, SealGalleryEast = true },
            new RoomSceneSpec
            {
                SceneName = "Teach2_HeldStep", RoomId = "teach-2-held-step",
                Spawn = new Vector2((L.ShaftMinX + L.ShaftMaxX) * 0.5f, L.PlatformHighY), SealGalleryEast = true,
            },
            new RoomSceneSpec { SceneName = "Teach3_LingeringShadow", RoomId = "teach-3-lingering-shadow", Spawn = L.Spawn },
            new RoomSceneSpec { SceneName = "BorrowedDawn_Greybox", RoomId = "borrowed-dawn", Spawn = L.Spawn },
        };

        /// <summary>The first teaching room, opened after a build so pressing Play starts at the beginning.</summary>
        public static string FirstScenePath => Sequence[0].ScenePath;

        // Cosmetic sizes that are not part of the layout contract.
        private const float PedestalHeight = 0.3f;
        private const float RaftDraft = 0.25f;
        private const float ZoneHeight = L.AnkhuHeight + 0.6f;
        private const float BarrierHeight = L.AnkhuHeight + 0.6f;
        private const float BarrierThickness = 0.15f;
        private const float BridgeDeckThickness = 0.3f;
        private const float InteractHeight = L.AnkhuHeight;

        private static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();
        private static readonly HashSet<Material> NoShadowMaterials = new HashSet<Material>();

        [MenuItem("Ankhu/Build Borrowed Dawn Greybox")]
        public static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }
            Build();
            Debug.Log("Borrowed Dawn greybox built at " + ScenePath + ", with Anpu's teaching rooms. Press Play to start at " + Sequence[0].SceneName + ".");
        }

        /// <summary>Entry point for -executeMethod Ankhu.EditorTools.BorrowedDawnGreyboxBuilder.BuildFromCommandLine.</summary>
        public static void BuildFromCommandLine()
        {
            try
            {
                Build();
                Debug.Log("Borrowed Dawn greybox built at " + ScenePath);
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        /// <summary>
        /// Builds every scene of the sequence (three teaching rooms, then Borrowed Dawn), puts them in
        /// Build Settings in play order, and leaves the first teaching room open.
        /// </summary>
        public static void Build()
        {
            Debug.Log(UniversalPipelineSetup.EnsureUniversalPipeline());
            Materials.Clear();
            NoShadowMaterials.Clear();
            EnsureFolder(MaterialsFolder);
            EnsureFolder(PrefabsFolder);
            EnsureFolder(ScenesFolder);
            CreateMaterials();

            foreach (RoomSceneSpec spec in Sequence)
            {
                BuildRoomScene(spec);
            }
            SetBuildSettingsOrder();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.OpenScene(FirstScenePath, OpenSceneMode.Single);
        }

        private static void BuildRoomScene(RoomSceneSpec spec)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Transform room = new GameObject("Room").transform;
            Transform structure = Group("Structure", room);
            Transform props = Group("Props", room);
            Transform movers = Group("Movers", room);
            Transform zones = Group("Zones", room);
            Transform interactables = Group("Interactables", room);

            BuildStructure(structure);
            BuildZones(zones);

            ElevatorPlatform platform = BuildElevator(movers);
            BuildLiftGates(movers, platform);
            GameObject mirrorPickup = BuildMirrorPickup(interactables);
            BuildRaft(movers, mirrorPickup);
            BuildWater(movers);
            BuildFloorProps(props, interactables);
            BuildBearer(movers);
            BuildLightScreen(movers);
            BuildBeam(props);
            BuildLadder(movers);
            BuildUpperProps(props, interactables);
            BuildBell(props, interactables);
            PendulumBridge bridge = BuildBridge(movers);
            BuildChainBarriers(movers, bridge);
            BuildGate(props, interactables);
            if (spec.SealGalleryEast)
            {
                BuildLessonSeal(structure);
            }
            BuildLighting();

            GameObject hostObject = new GameObject("RoomRulesHost");
            hostObject.AddComponent<RoomRulesHost>();
            AddRulesInstaller(hostObject, spec.RoomId);
            if (spec.RoomId != "borrowed-dawn")
            {
                AddByName(hostObject, TeachingDirectorTypeName);
            }
            new GameObject("Debug").AddComponent<TraversalDebugPanel>();

            Camera camera = BuildCamera(spec.Spawn);
            Ankhu.Player.PlayerRigFactory.Create(spec.Spawn, camera);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, spec.ScenePath))
            {
                throw new InvalidOperationException("Could not save scene at " + spec.ScenePath);
            }
        }

        /// <summary>A stone wall across the upper walkway at the gallery's east end.</summary>
        private static void BuildLessonSeal(Transform parent)
        {
            Solid("Lesson Seal", parent, L.GalleryMaxX - 0.4f, L.UpperY, L.GalleryMaxX, L.CeilingY, "StoneDark");
        }

        public const string RulesInstallerTypeName = "Ankhu.Bindings.BorrowedDawnRulesInstaller, Ankhu.Bindings";

        /// <summary>
        /// Adds the rules library binding. The greybox is not useful without it, so a missing binding
        /// fails the build loudly instead of silently producing a scene that answers no spells.
        /// Reflection keeps this editor assembly free of a compile-time reference to the rules package.
        /// </summary>
        private static void AddRulesInstaller(GameObject hostObject, string roomId)
        {
            Component installer = AddByName(hostObject, RulesInstallerTypeName);
            var serialized = new SerializedObject(installer);
            SerializedProperty room = serialized.FindProperty("roomId");
            if (room == null)
            {
                throw new InvalidOperationException("BorrowedDawnRulesInstaller has no serialized roomId field.");
            }
            room.stringValue = roomId;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Component AddByName(GameObject target, string assemblyQualifiedName)
        {
            Type type = Type.GetType(assemblyQualifiedName);
            if (type == null)
            {
                throw new InvalidOperationException(
                    "Ankhu.Bindings is not compiled, so the greybox would have no rules. Check the Console for compile errors " +
                    "in Assets/Ankhu/Bindings or Packages/com.ankhu.borrowed-dawn, fix them, then build again.");
            }
            return target.AddComponent(type);
        }

        // Materials

        private static void CreateMaterials()
        {
            Shader lit = GreyboxRender.FindLitShader();
            Shader unlit = GreyboxRender.FindUnlitShader();
            if (lit == null || unlit == null)
            {
                throw new InvalidOperationException("Greybox shaders missing. See the previous error for details.");
            }
            Lit(lit, "Stone", P.Stone);
            Lit(lit, "StoneDark", P.GreyDark);
            Lit(lit, "StoneLight", P.StoneLight);
            Lit(lit, "UpperSlab", P.UpperSlab);
            Lit(lit, "BackWall", P.BackWall);
            Lit(lit, "Gold", P.Gold);
            Lit(lit, "Bronze", P.Bronze);
            Lit(lit, "BronzeDull", P.BronzeDull);
            Lit(lit, "Black", P.Black);
            Lit(lit, "Red", P.Red);
            Lit(lit, "Water", P.Water, true);
            Unlit(unlit, "GlowGold", P.Gold);
            Unlit(unlit, "GlowViolet", P.Violet);
            Unlit(unlit, "GlowLamp", P.White);
            Unlit(unlit, "Dawn", P.DawnBeyond);
            Unlit(unlit, "ShadowFigure", P.ShadowFigure);
            Unlit(unlit, "Clerestory", P.WithAlpha(P.DawnBeyond, 0.9f));
            Unlit(unlit, "Beam", P.White);
        }

        private static void Lit(Shader shader, string name, Color color, bool transparent = false)
        {
            Material m = LoadOrCreateMaterial(shader, name);
            SetMaterialColor(m, color);
            ConfigureSurface(m, transparent);
            if (transparent)
            {
                NoShadowMaterials.Add(m);
            }
            EditorUtility.SetDirty(m);
            Materials[name] = m;
        }

        private static void Unlit(Shader shader, string name, Color color)
        {
            Material m = LoadOrCreateMaterial(shader, name);
            SetMaterialColor(m, color);
            ConfigureSurface(m, true);
            NoShadowMaterials.Add(m);
            EditorUtility.SetDirty(m);
            Materials[name] = m;
        }

        private static Material LoadOrCreateMaterial(Shader shader, string name)
        {
            string path = MaterialsFolder + "/" + name + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(m, path);
            }
            else if (m.shader != shader)
            {
                m.shader = shader;
            }
            return m;
        }

        private static void SetMaterialColor(Material m, Color color)
        {
            if (m.HasProperty("_BaseColor"))
            {
                m.SetColor("_BaseColor", color);
            }
            if (m.HasProperty("_Color"))
            {
                m.SetColor("_Color", color);
            }
            if (m.HasProperty("_Smoothness"))
            {
                m.SetFloat("_Smoothness", 0.15f);
            }
            if (m.HasProperty("_Glossiness"))
            {
                m.SetFloat("_Glossiness", 0.15f);
            }
        }

        private static void ConfigureSurface(Material m, bool transparent)
        {
            if (m.HasProperty("_Surface"))
            {
                // URP Lit and URP Unlit.
                m.SetFloat("_Surface", transparent ? 1f : 0f);
                if (m.HasProperty("_Blend"))
                {
                    m.SetFloat("_Blend", 0f);
                }
                m.SetFloat("_SrcBlend", transparent ? (float)BlendMode.SrcAlpha : (float)BlendMode.One);
                m.SetFloat("_DstBlend", transparent ? (float)BlendMode.OneMinusSrcAlpha : (float)BlendMode.Zero);
                if (m.HasProperty("_SrcBlendAlpha"))
                {
                    m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                    m.SetFloat("_DstBlendAlpha", transparent ? (float)BlendMode.OneMinusSrcAlpha : (float)BlendMode.Zero);
                }
                m.SetFloat("_ZWrite", transparent ? 0f : 1f);
                m.SetOverrideTag("RenderType", transparent ? "Transparent" : "Opaque");
                m.SetShaderPassEnabled("DepthOnly", !transparent);
                if (transparent)
                {
                    m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    m.renderQueue = (int)RenderQueue.Transparent;
                }
                else
                {
                    m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    m.renderQueue = -1;
                }
                return;
            }
            if (m.HasProperty("_Mode"))
            {
                // Built-in Standard. Fade mode for transparency.
                m.SetFloat("_Mode", transparent ? 2f : 0f);
                m.SetFloat("_SrcBlend", transparent ? (float)BlendMode.SrcAlpha : (float)BlendMode.One);
                m.SetFloat("_DstBlend", transparent ? (float)BlendMode.OneMinusSrcAlpha : (float)BlendMode.Zero);
                m.SetFloat("_ZWrite", transparent ? 0f : 1f);
                m.SetOverrideTag("RenderType", transparent ? "Transparent" : "Opaque");
                if (transparent)
                {
                    m.EnableKeyword("_ALPHABLEND_ON");
                    m.renderQueue = (int)RenderQueue.Transparent;
                }
                else
                {
                    m.DisableKeyword("_ALPHABLEND_ON");
                    m.renderQueue = -1;
                }
            }
        }

        private static Material M(string name)
        {
            if (!Materials.TryGetValue(name, out Material m))
            {
                throw new InvalidOperationException("Greybox material not created: " + name);
            }
            return m;
        }

        // Primitive helpers. Every visual is a leaf. Parents stay unscaled.

        private static Transform Group(string name, Transform parent)
        {
            Transform t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        private static GameObject Empty(string name, Transform parent, Vector3 localPosition)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            return go;
        }

        private static MeshRenderer Cube(string name, Transform parent, Vector3 localCentre, Vector3 size, string material)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<BoxCollider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localCentre;
            go.transform.localScale = size;
            return SetupRenderer(go, material);
        }

        /// <summary>A flat disc facing the camera (cylinder with its axis along z).</summary>
        private static MeshRenderer Disc(string name, Transform parent, Vector3 localCentre, float diameter, float thickness, string material)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localCentre;
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            go.transform.localScale = new Vector3(diameter, thickness * 0.5f, diameter);
            return SetupRenderer(go, material);
        }

        private static MeshRenderer SetupRenderer(GameObject go, string material)
        {
            MeshRenderer r = go.GetComponent<MeshRenderer>();
            Material m = M(material);
            r.sharedMaterial = m;
            if (NoShadowMaterials.Contains(m))
            {
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            return r;
        }

        /// <summary>World-space block on an origin-aligned parent, spanning walkway depth unless given.</summary>
        private static MeshRenderer Block(string name, Transform parent, float x0, float y0, float x1, float y1, string material,
            float zCentre = D.WalkCentreZ, float depth = D.WalkDepth)
        {
            return Cube(name, parent, new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, zCentre), new Vector3(x1 - x0, y1 - y0, depth), material);
        }

        /// <summary>Block with a solid BoxCollider2D. The collider inherits the cube scale, so size stays 1 by 1.</summary>
        private static MeshRenderer Solid(string name, Transform parent, float x0, float y0, float x1, float y1, string material,
            float zCentre = D.WalkCentreZ, float depth = D.WalkDepth)
        {
            MeshRenderer r = Block(name, parent, x0, y0, x1, y1, material, zCentre, depth);
            BoxCollider2D c = r.gameObject.AddComponent<BoxCollider2D>();
            c.size = Vector2.one;
            c.offset = Vector2.zero;
            return r;
        }

        private static BoxCollider2D ColliderBox(string name, Transform parent, Vector2 localCentre, Vector2 size, bool trigger)
        {
            GameObject go = Empty(name, parent, localCentre);
            BoxCollider2D c = go.AddComponent<BoxCollider2D>();
            c.size = size;
            c.isTrigger = trigger;
            return c;
        }

        private static Interactable MakeInteractable(string name, Transform parent, Vector2 localCentre, Vector2 size,
            string displayName, string inspect, string target, string interaction = "", string prompt = "")
        {
            BoxCollider2D c = ColliderBox(name, parent, localCentre, size, true);
            Interactable i = c.gameObject.AddComponent<Interactable>();
            i.Configure(displayName, inspect, target, interaction, prompt);
            return i;
        }

        private static ZoneVolume MakeZone(string name, Transform parent, Vector2 localCentre, Vector2 size, string zoneId, int priority)
        {
            BoxCollider2D c = ColliderBox(name, parent, localCentre, size, true);
            ZoneVolume z = c.gameObject.AddComponent<ZoneVolume>();
            z.Configure(zoneId, priority);
            return z;
        }

        /// <summary>
        /// Kinematic body. Only bodies moved with MovePosition interpolate. Bodies posed through
        /// their transform (Bearer, bridge) must not, or interpolation would overwrite the pose.
        /// </summary>
        private static Rigidbody2D KinematicBody(GameObject go, bool interpolate)
        {
            Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
            rb.interpolation = interpolate ? RigidbodyInterpolation2D.Interpolate : RigidbodyInterpolation2D.None;
            return rb;
        }

        private static GameObject SavePrefab(GameObject built, string prefabName)
        {
            string path = PrefabsFolder + "/" + prefabName + ".prefab";
            GameObject asset = PrefabUtility.SaveAsPrefabAssetAndConnect(built, path, InteractionMode.AutomatedAction, out bool success);
            if (!success || asset == null)
            {
                throw new InvalidOperationException("Could not save prefab " + path);
            }
            return asset;
        }

        private static GameObject PlacePrefab(GameObject asset, Transform parent, Vector3 position)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            instance.transform.position = position;
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
            return instance;
        }

        private static void RecordOverrides(Object component)
        {
            if (PrefabUtility.IsPartOfPrefabInstance(component))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            }
            EditorUtility.SetDirty(component);
        }

        // Static structure

        private static void BuildStructure(Transform parent)
        {
            float w = L.WallThickness;
            float bottom = L.BasinBottomY - w;
            float top = L.CeilingY + w;
            float slabBottom = L.UpperY - L.UpperSlabThickness;
            float fullDepthCentre = (D.WalkCentreZ - D.WalkDepth * 0.5f + D.BackWallZ) * 0.5f;
            float fullDepth = D.BackWallZ - (D.WalkCentreZ - D.WalkDepth * 0.5f);

            Block("BackWall", parent, L.RoomMinX, bottom, L.RoomMaxX, top, "BackWall", D.BackWallZ, D.BackWallDepth);
            Solid("WestWall", parent, L.RoomMinX, bottom, L.RoomMinX + w, top, "Stone", fullDepthCentre, fullDepth);
            Solid("EastWall", parent, L.RoomMaxX - w, bottom, L.RoomMaxX, top, "Stone", fullDepthCentre, fullDepth);

            // Ceiling: one collider, visual split around the clerestory opening.
            float clx = L.ClerestoryPoint.x;
            ColliderBox("CeilingCollider", parent, new Vector2((L.RoomMinX + L.RoomMaxX) * 0.5f, L.CeilingY + w * 0.5f),
                new Vector2(L.RoomMaxX - L.RoomMinX, w), false);
            Block("Ceiling_West", parent, L.RoomMinX + w, L.CeilingY, clx - 0.5f, L.CeilingY + w, "Stone", fullDepthCentre, fullDepth);
            Block("Ceiling_East", parent, clx + 0.5f, L.CeilingY, L.RoomMaxX - w, L.CeilingY + w, "Stone", fullDepthCentre, fullDepth);
            Block("Clerestory", parent, clx - 0.5f, L.CeilingY + w * 0.4f, clx + 0.5f, L.CeilingY + w, "Clerestory", fullDepthCentre, fullDepth);

            // Basin floor under the cistern and the shaft.
            Solid("BasinFloor", parent, L.RoomMinX + w, bottom, L.FloorMinX, L.BasinBottomY, "StoneDark");
            // Cistern east wall that rises to the parapet. Ankhu cannot step over it from the platform.
            Solid("CisternWall_Parapet", parent, L.RaftTankMaxX, L.BasinBottomY, L.RaftTankWallMaxX, L.RaftParapetTopY, "Stone");
            Block("ParapetCap", parent, L.RaftTankMaxX - 0.05f, L.RaftParapetTopY - 0.12f, L.RaftTankWallMaxX + 0.05f, L.RaftParapetTopY, "UpperSlab",
                D.WalkCentreZ, D.WalkDepth + 0.1f);

            // Floor tier foundation (also forms the shaft's east wall below the floor).
            Solid("FloorFoundation", parent, L.FloorMinX, bottom, L.FloorMaxX, L.FloorY, "Stone");
            Block("FloorTread", parent, L.FloorMinX, L.FloorY - 0.06f, L.FloorMaxX, L.FloorY + 0.01f, "StoneLight");
            Block("LightScreenSlot", parent, L.LightScreenX - 0.2f, L.FloorY, L.LightScreenX + 0.2f, L.FloorY + 0.02f, "Black", D.PropZ, 1.4f);

            // Lintels above the lift gates on the floor side of the shaft mouth.
            float gx0 = L.LiftGateX;
            float gx1 = L.LiftGateX + L.LiftGateThickness;
            Solid("ShaftLintel_Floor", parent, gx0, L.FloorY + L.LiftGateHeight, gx1, slabBottom, "Stone");
            Solid("ShaftLintel_Upper", parent, gx0, L.UpperY + L.LiftGateHeight, gx1, L.CeilingY, "Stone");

            // Shaft guide rails (visual only).
            Block("ShaftRail_West", parent, L.ShaftMinX + 0.05f, L.BasinBottomY, L.ShaftMinX + 0.15f, L.CeilingY, "Bronze", D.WalkCentreZ + 1.3f, 0.1f);
            Block("ShaftRail_East", parent, L.ShaftMaxX - 0.15f, L.BasinBottomY, L.ShaftMaxX - 0.05f, L.CeilingY, "Bronze", D.WalkCentreZ + 1.3f, 0.1f);

            // Upper tier walk colliders: one per continuous span so there are no seams to snag on.
            Vector2 Span(float x0, float x1) => new Vector2((x0 + x1) * 0.5f, slabBottom + L.UpperSlabThickness * 0.5f);
            Vector2 Size(float x0, float x1) => new Vector2(x1 - x0, L.UpperSlabThickness);
            ColliderBox("UpperWalk_GalleryWest", parent, Span(L.GalleryMinX, L.LadderHatchMinX), Size(L.GalleryMinX, L.LadderHatchMinX), false);
            ColliderBox("UpperWalk_GalleryEastAndLanding", parent, Span(L.LadderHatchMaxX, L.LandingMaxX), Size(L.LadderHatchMaxX, L.LandingMaxX), false);
            ColliderBox("UpperWalk_GateLedge", parent, Span(L.GateLedgeMinX, L.GateLedgeMaxX), Size(L.GateLedgeMinX, L.GateLedgeMaxX), false);

            // Upper tier visuals, split at the light well, the hatch and the deflector slot.
            Block("GallerySlab_A", parent, L.GalleryMinX, slabBottom, L.LightWellMinX, L.UpperY, "UpperSlab");
            Block("GallerySlab_B", parent, L.LightWellMaxX, slabBottom, L.LadderHatchMinX, L.UpperY, "UpperSlab");
            Block("GallerySlab_C", parent, L.LadderHatchMaxX, slabBottom, L.GalleryMaxX, L.UpperY, "UpperSlab");
            Block("LandingSlab", parent, L.LandingMinX, slabBottom, L.LandingMaxX, L.UpperY, "UpperSlab");
            Block("GateLedgeSlab_West", parent, L.GateLedgeMinX, slabBottom, L.DeflectorX - 0.15f, L.UpperY, "UpperSlab");
            Block("GateLedgeSlab_East", parent, L.DeflectorX + 0.15f, slabBottom, L.GateLedgeMaxX, L.UpperY, "UpperSlab");

            // Light well: a real opening in the slab, framed in gold so it reads as a hole the sun
            // passes through. Three thin bronze bars form the walkable grille (the walk collider is
            // continuous). The opening is wider than the beam's footprint at slab height.
            Block("LightWellLip_West", parent, L.LightWellMinX - 0.08f, slabBottom - 0.05f, L.LightWellMinX, L.UpperY + 0.02f, "Bronze");
            Block("LightWellLip_East", parent, L.LightWellMaxX, slabBottom - 0.05f, L.LightWellMaxX + 0.08f, L.UpperY + 0.02f, "Bronze");
            int bars = 3;
            float span = L.LightWellMaxX - L.LightWellMinX;
            for (int i = 0; i < bars; i++)
            {
                float x = L.LightWellMinX + span * (i + 1f) / (bars + 1f);
                Block("LightWellBar_" + i, parent, x - 0.02f, L.UpperY - 0.06f, x + 0.02f, L.UpperY, "Bronze");
            }

            // Foreground framing pillars, no colliders.
            // Kept inside the wall thickness so they never cover Ankhu.
            Block("ForegroundPillar_West", parent, L.RoomMinX, bottom, L.RoomMinX + 0.3f, top, "StoneDark", D.ForegroundZ, 0.6f);
            Block("ForegroundPillar_East", parent, L.RoomMaxX - 0.3f, bottom, L.RoomMaxX, top, "StoneDark", D.ForegroundZ, 0.6f);
        }

        private static void BuildZones(Transform parent)
        {
            Vector2 C(float x0, float x1, float y) => new Vector2((x0 + x1) * 0.5f, y + ZoneHeight * 0.5f);
            Vector2 S(float x0, float x1) => new Vector2(x1 - x0, ZoneHeight);
            float floorStart = L.FloorMinX + L.LiftGateThickness;
            MakeZone("Zone_Floor", parent, C(floorStart, L.FloorMaxX, L.FloorY), S(floorStart, L.FloorMaxX), ZoneIds.Floor, 0);
            MakeZone("Zone_Gallery", parent, C(floorStart, L.GalleryMaxX, L.UpperY), S(floorStart, L.GalleryMaxX), ZoneIds.Gallery, 0);
            MakeZone("Zone_Landing", parent, C(L.LandingMinX, L.LandingMaxX, L.UpperY), S(L.LandingMinX, L.LandingMaxX), ZoneIds.Landing, 0);
            MakeZone("Zone_Gate", parent, C(L.GateLedgeMinX, L.GateLedgeMaxX, L.UpperY), S(L.GateLedgeMinX, L.GateLedgeMaxX), ZoneIds.Gate, 0);
        }

        // Elevator and lift gates

        private static ElevatorPlatform BuildElevator(Transform parent)
        {
            float width = L.ShaftMaxX - L.ShaftMinX;
            float t = L.PlatformThickness;
            GameObject root = Empty("ElevatorPlatform", parent, new Vector3((L.ShaftMinX + L.ShaftMaxX) * 0.5f, L.PlatformLowY, 0f));
            KinematicBody(root, true);
            BoxCollider2D deckCollider = root.AddComponent<BoxCollider2D>();
            deckCollider.size = new Vector2(width, t);
            deckCollider.offset = new Vector2(0f, -t * 0.5f);
            ElevatorPlatform platform = root.AddComponent<ElevatorPlatform>();
            platform.Configure(L.PlatformLowY, L.PlatformHighY, L.MoverTransitionSeconds);

            Transform tr = root.transform;
            float front = D.WalkCentreZ - D.WalkDepth * 0.5f;
            Cube("Deck", tr, new Vector3(0f, -t * 0.5f, D.WalkCentreZ), new Vector3(width, t, D.WalkDepth), "UpperSlab");
            Cube("DeckTrim", tr, new Vector3(0f, -0.03f, front + 0.04f), new Vector3(width, 0.06f, 0.08f), "Gold");
            float drumHeight = 0.8f;
            Cube("FloatDrum", tr, new Vector3(0f, -t - drumHeight * 0.5f, D.WalkCentreZ), new Vector3(width - 0.4f, drumHeight, D.WalkDepth - 0.6f), "Bronze");

            GameObject binding = Empty("Binding_Elevator", tr, Vector3.zero);
            MeshRenderer thread = Cube("Thread", binding.transform,
                new Vector3(0f, -t - drumHeight * 0.5f, front + 0.25f), new Vector3(width - 0.2f, 0.12f, 0.05f), "GlowViolet");
            binding.AddComponent<BindingIndicator>().Configure(BindingTarget.Elevator, new Renderer[] { thread });

            MakeZone("Zone_Lift", tr, new Vector2(0f, ZoneHeight * 0.5f), new Vector2(width, ZoneHeight), ZoneIds.Lift, 10);
            MakeInteractable("Inspect_Elevator", tr, new Vector2(width * 0.3f, InteractHeight * 0.5f), new Vector2(1f, InteractHeight),
                "Elevator platform",
                "A buoyant platform in the shaft. Water lifts it to the gallery and a binding can hold it up after the water leaves. The bronze grilles open only at the level where it is docked.",
                "elevator");

            GameObject instance = root;
            SavePrefab(root, "ElevatorPlatform");
            return instance.GetComponent<ElevatorPlatform>();
        }

        private static void BuildLiftGates(Transform parent, ElevatorPlatform platform)
        {
            float h = L.LiftGateHeight;
            float th = L.LiftGateThickness;
            float x = L.LiftGateX + th * 0.5f;
            GameObject root = Empty("LiftGate_Floor", parent, new Vector3(x, L.PlatformLowY, 0f));
            BoxCollider2D blocker = root.AddComponent<BoxCollider2D>();
            blocker.size = new Vector2(th, h);
            blocker.offset = new Vector2(0f, h * 0.5f);
            LiftGate gate = root.AddComponent<LiftGate>();

            GameObject bars = Empty("Bars", root.transform, Vector3.zero);
            float zFront = D.WalkCentreZ - D.WalkDepth * 0.5f + 0.15f;
            float zBack = D.WalkCentreZ + D.WalkDepth * 0.5f - 0.15f;
            int count = 6;
            for (int i = 0; i < count; i++)
            {
                float z = Mathf.Lerp(zFront, zBack, i / (float)(count - 1));
                Cube("Bar_" + i, bars.transform, new Vector3(0f, h * 0.5f, z), new Vector3(0.1f, h, 0.08f), "Bronze");
            }
            Cube("Rail_Top", bars.transform, new Vector3(0f, h - 0.06f, D.WalkCentreZ), new Vector3(0.12f, 0.12f, zBack - zFront), "Bronze");
            Cube("Rail_Bottom", bars.transform, new Vector3(0f, 0.3f, D.WalkCentreZ), new Vector3(0.12f, 0.08f, zBack - zFront), "Bronze");
            MeshRenderer lamp = Disc("Lamp", root.transform, new Vector3(0.3f, h + 0.3f, D.WalkCentreZ - D.WalkDepth * 0.5f - 0.03f), 0.2f, 0.04f, "GlowLamp");

            gate.ConfigureParts(blocker, bars.transform, lamp);
            GameObject asset = SavePrefab(root, "LiftGate");
            gate.Configure(platform, L.PlatformLowY);
            RecordOverrides(gate);

            GameObject upper = PlacePrefab(asset, parent, new Vector3(x, L.PlatformHighY, 0f));
            upper.name = "LiftGate_Upper";
            LiftGate upperGate = upper.GetComponent<LiftGate>();
            upperGate.Configure(platform, L.PlatformHighY);
            RecordOverrides(upperGate);
        }

        // Raft, mirror pickup and water

        private static GameObject BuildMirrorPickup(Transform parent)
        {
            // Over the parapet, reachable only while standing at the west end of the docked-high platform.
            float x0 = L.ShaftMinX;
            float x1 = L.ShaftMinX + 0.9f;
            Interactable pickup = MakeInteractable("Use_TakeMirror", parent,
                new Vector2((x0 + x1) * 0.5f, L.PlatformHighY + InteractHeight * 0.5f), new Vector2(x1 - x0, InteractHeight),
                "Sun mirror",
                "The sun mirror rides the raft in the west cistern. From the west end of the platform, docked at the gallery, it is within reach over the parapet when the raft floats high beside it.",
                "mirror", InteractionIds.TakeMirror, "Take mirror");
            return pickup.gameObject;
        }

        private static void BuildRaft(Transform parent, GameObject mirrorPickup)
        {
            float cx = (L.RaftTankMinX + L.RaftTankMaxX) * 0.5f;
            float width = L.RaftTankMaxX - L.RaftTankMinX - 0.3f;
            GameObject root = Empty("Raft", parent, new Vector3(cx, L.RaftDeckLowY, 0f));
            RaftView raft = root.AddComponent<RaftView>();
            Transform tr = root.transform;
            Cube("Deck", tr, new Vector3(0f, -0.2f, D.WalkCentreZ), new Vector3(width, 0.4f, D.WalkDepth - 0.6f), "BronzeDull");
            Cube("Pontoons", tr, new Vector3(0f, -0.55f, D.WalkCentreZ), new Vector3(width - 0.3f, 0.3f, D.WalkDepth - 1f), "StoneDark");
            float mx = L.RaftMirrorX - cx;
            Cube("MirrorStand", tr, new Vector3(mx, L.RaftMirrorHeight * 0.5f, D.PropZ), new Vector3(0.12f, L.RaftMirrorHeight, 0.12f), "Bronze");
            GameObject mirror = Empty("Mirror", tr, Vector3.zero);
            Disc("MirrorDisc", mirror.transform, new Vector3(mx, L.RaftMirrorHeight, D.PropZ - 0.08f), 0.7f, 0.08f, "Gold");
            raft.Configure(mirror, null);
            SavePrefab(root, "Raft");
            raft.SetPickup(mirrorPickup);
            RecordOverrides(raft);
        }

        private static void BuildWater(Transform parent)
        {
            GameObject cistern = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cistern.name = "Water_Cistern";
            Object.DestroyImmediate(cistern.GetComponent<BoxCollider>());
            cistern.transform.SetParent(parent, false);
            SetupRenderer(cistern, "Water");
            cistern.AddComponent<WaterColumnView>().Configure(WaterSide.West, L.RaftTankMinX, L.RaftTankMaxX, L.BasinBottomY,
                L.RaftDeckLowY - RaftDraft, L.RaftDeckHighY - RaftDraft, D.WalkCentreZ, D.WalkDepth);

            GameObject shaft = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shaft.name = "Water_Shaft";
            Object.DestroyImmediate(shaft.GetComponent<BoxCollider>());
            shaft.transform.SetParent(parent, false);
            SetupRenderer(shaft, "Water");
            shaft.AddComponent<WaterColumnView>().Configure(WaterSide.East, L.ShaftMinX, L.ShaftMaxX, L.BasinBottomY,
                L.ShaftWaterLowY, L.ShaftWaterHighY, D.WalkCentreZ, D.WalkDepth);
        }

        // Floor tier props

        private static void BuildFloorProps(Transform props, Transform interactables)
        {
            float z = D.PropZ;

            // Water selector (inspect only)
            GameObject selector = Empty("WaterSelector", props, new Vector3(L.WaterSelectorX, L.FloorY, 0f));
            Transform s = selector.transform;
            Cube("Post", s, new Vector3(0f, 0.55f, z), new Vector3(0.3f, 1.1f, 0.3f), "Stone");
            Disc("Dial", s, new Vector3(0f, 1.35f, z - 0.2f), 0.6f, 0.08f, "Bronze");
            GameObject pointer = Empty("Pointer", s, new Vector3(0f, 1.35f, z - 0.26f));
            Cube("Needle", pointer.transform, new Vector3(0f, 0.17f, 0f), new Vector3(0.06f, 0.34f, 0.03f), "Black");
            MeshRenderer westLamp = Disc("Lamp_West", s, new Vector3(-0.25f, 1.85f, z - 0.2f), 0.16f, 0.04f, "GlowLamp");
            MeshRenderer eastLamp = Disc("Lamp_East", s, new Vector3(0.25f, 1.85f, z - 0.2f), 0.16f, 0.04f, "GlowLamp");
            selector.AddComponent<WaterSelectorView>().Configure(pointer.transform, westLamp, eastLamp);
            MakeInteractable("Inspect_WaterSelector", interactables, new Vector2(L.WaterSelectorX, L.FloorY + InteractHeight * 0.5f), new Vector2(0.9f, InteractHeight),
                "Water selector",
                "A bronze dial with two channels. Only one basin can hold the water. West fills the cistern under the mirror raft. East fills the shaft and floats the elevator. The dial only shows the flow. Speak 'flow west' or 'flow east' to change it.",
                "water_selector");

            // Bearer alcove, a dark recess in the back wall.
            Block("BearerAlcove", props, L.BearerAlcoveX - 0.6f, L.FloorY, L.BearerAlcoveX + 0.6f, L.FloorY + 2.9f, "Black",
                D.BackWallZ - D.BackWallDepth * 0.5f - 0.03f, 0.05f);

            // Sun socket
            GameObject socket = Empty("SunSocket", props, new Vector3(L.SocketX, L.FloorY, 0f));
            Transform so = socket.transform;
            float cupY = L.BeamY - 0.4f;
            Cube("Pedestal", so, new Vector3(0f, cupY * 0.5f, z), new Vector3(0.6f, cupY, 0.6f), "Stone");
            Cube("Cup", so, new Vector3(0f, cupY + 0.06f, z), new Vector3(0.8f, 0.12f, 0.7f), "Gold");
            Disc("EmptySeat", so, new Vector3(0f, L.BeamY, z + 0.12f), 0.55f, 0.04f, "Black");
            GameObject installed = Empty("InstalledMirror", so, Vector3.zero);
            Disc("MirrorDisc", installed.transform, new Vector3(0f, L.BeamY, z - 0.05f), 0.6f, 0.08f, "Gold");
            socket.AddComponent<SocketView>().Configure(installed);
            MakeInteractable("Use_InstallMirror", interactables, new Vector2(L.SocketX, L.FloorY + InteractHeight * 0.5f), new Vector2(1.2f, InteractHeight),
                "Sun socket",
                "An empty sun socket under the light well. Sunlight from the clerestory falls here. A mirror set in it would throw the light east along the hall.",
                "socket", InteractionIds.InstallMirror, "Install mirror");

            // Bearer pedestal (static, the statue itself is a mover).
            Block("BearerPedestal", props, L.BearerPedestalX - 0.55f, L.FloorY, L.BearerPedestalX + 0.55f, L.FloorY + PedestalHeight, "Stone", z, 0.9f);

            // Shadow seal
            GameObject seal = Empty("ShadowSeal", props, new Vector3(L.ShadowSealX, L.FloorY, 0f));
            Transform se = seal.transform;
            float sh = L.ShadowSealHeight;
            Cube("Frame", se, new Vector3(0f, sh * 0.5f, z + 0.3f), new Vector3(1.1f, sh, 0.2f), "Bronze");
            Cube("Panel", se, new Vector3(0f, sh * 0.5f, z + 0.18f), new Vector3(0.9f, sh - 0.2f, 0.05f), "StoneLight");
            GameObject figure = Empty("ShadowFigure", se, Vector3.zero);
            Cube("Body", figure.transform, new Vector3(0f, 0.95f, z + 0.14f), new Vector3(0.45f, 1.5f, 0.02f), "ShadowFigure");
            Cube("Head", figure.transform, new Vector3(0f, 1.95f, z + 0.14f), new Vector3(0.32f, 0.36f, 0.02f), "ShadowFigure");
            seal.AddComponent<ShadowSealView>().Configure(figure);
            GameObject sealBinding = Empty("Binding_Shadow", se, Vector3.zero);
            MeshRenderer band = Cube("Band", sealBinding.transform, new Vector3(0f, 1.3f, z + 0.1f), new Vector3(1.25f, 0.1f, 0.04f), "GlowViolet");
            MeshRenderer knot = Disc("Knot", sealBinding.transform, new Vector3(0f, 1.3f, z + 0.07f), 0.26f, 0.03f, "GlowViolet");
            sealBinding.AddComponent<BindingIndicator>().Configure(BindingTarget.Shadow, new Renderer[] { band, knot });
            MakeInteractable("Inspect_ShadowSeal", interactables, new Vector2(L.ShadowSealX, L.FloorY + InteractHeight * 0.5f), new Vector2(1.1f, InteractHeight),
                "Shadow seal",
                "A seal that wants a shadow. A figure standing in the light would cast one here. A binding could keep the shadow after its owner walks away.",
                "shadow_seal");

            // Deflector at the east end: turns the beam up through the gate ledge.
            GameObject deflector = Empty("Deflector", props, new Vector3(L.DeflectorX, L.FloorY, 0f));
            Cube("Pillar", deflector.transform, new Vector3(0f, (L.BeamY - 0.3f) * 0.5f, z), new Vector3(0.5f, L.BeamY - 0.3f, 0.5f), "Stone");
            MeshRenderer plate = Cube("Plate", deflector.transform, new Vector3(0f, L.BeamY, z), new Vector3(0.6f, 0.08f, 0.5f), "Gold");
            plate.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        }

        private static void BuildBearer(Transform parent)
        {
            GameObject root = Empty("Bearer", parent, new Vector3(L.BearerPedestalX, L.FloorY + PedestalHeight, 0f));
            KinematicBody(root, false);
            BearerView view = root.AddComponent<BearerView>();
            GameObject body = Empty("Body", root.transform, new Vector3(0f, 0f, D.PropZ));
            Transform b = body.transform;
            Cube("Torso", b, new Vector3(0f, 1.15f, 0f), new Vector3(0.7f, 1.3f, 0.6f), "StoneLight");
            Cube("Leg_Back", b, new Vector3(-0.15f, 0.25f, 0.05f), new Vector3(0.25f, 0.5f, 0.45f), "StoneLight");
            Cube("Leg_Front", b, new Vector3(0.22f, 0.25f, -0.05f), new Vector3(0.25f, 0.5f, 0.45f), "StoneLight");
            Cube("Head", b, new Vector3(0.05f, 2.05f, 0f), new Vector3(0.45f, 0.5f, 0.5f), "StoneLight");
            Cube("Headdress", b, new Vector3(-0.05f, 2.2f, 0f), new Vector3(0.6f, 0.3f, 0.55f), "Gold");
            Cube("Face", b, new Vector3(0.3f, 2.02f, 0f), new Vector3(0.1f, 0.14f, 0.2f), "StoneLight");
            view.Configure(L.FloorY + PedestalHeight, b);
            MakeInteractable("Inspect_Bearer", root.transform, new Vector2(0f, InteractHeight * 0.5f), new Vector2(1f, InteractHeight),
                "The Bearer",
                "The Bearer stands on his pedestal in the path of the light. Woken, he walks west into his alcove. Told to sleep, he returns to the pedestal.",
                "bearer");
            SavePrefab(root, "Bearer");
        }

        private static void BuildLightScreen(Transform parent)
        {
            GameObject root = Empty("LightScreen", parent, new Vector3(L.LightScreenX, L.FloorY, 0f));
            float h = L.LightScreenHeight;
            MeshRenderer panel = Cube("Panel", root.transform, new Vector3(0f, -h * 0.5f, D.PropZ), new Vector3(0.25f, h, 1.2f), "StoneLight");
            root.AddComponent<LightScreenView>().Configure(panel.transform, h);
        }

        private static void BuildBeam(Transform parent)
        {
            GameObject root = Empty("Beam", parent, Vector3.zero);
            Transform r = root.transform;
            LineRenderer shaft = Line("SunShaft", r, 0.9f, 0.5f);
            LineRenderer nearGlow = Line("NearGlow", r, 0.28f, 0.28f);
            LineRenderer nearCore = Line("NearCore", r, 0.06f, 0.06f);
            LineRenderer farGlow = Line("FarGlow", r, 0.28f, 0.28f);
            LineRenderer farCore = Line("FarCore", r, 0.06f, 0.06f);
            Vector3 disk = new Vector3(L.SunDisk.x, L.SunDisk.y, D.PropZ + 0.4f);
            MeshRenderer sun = Disc("SunDisk", r, disk, 0.9f, 0.06f, "GlowGold");
            MeshRenderer halo = Disc("SunHalo", r, disk + new Vector3(0f, 0f, 0.06f), 1.8f, 0.02f, "GlowGold");
            root.AddComponent<BeamView>().Configure(shaft, nearCore, nearGlow, farCore, farGlow, sun, halo);
        }

        private static LineRenderer Line(string name, Transform parent, float startWidth, float endWidth)
        {
            GameObject go = Empty(name, parent, Vector3.zero);
            LineRenderer line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = M("Beam");
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.startWidth = startWidth;
            line.endWidth = endWidth;
            line.numCapVertices = 2;
            line.startColor = Color.white;
            line.endColor = Color.white;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.sortingOrder = GreyboxSorting.Beam;
            line.enabled = false;
            return line;
        }

        // Ladder

        private static void BuildLadder(Transform parent)
        {
            float slab = L.UpperSlabThickness;
            float hatchWidth = L.LadderHatchMaxX - L.LadderHatchMinX;
            GameObject root = Empty("Ladder", parent, new Vector3(L.LadderX, L.UpperY, 0f));
            Transform tr = root.transform;
            LadderHatch view = root.AddComponent<LadderHatch>();

            BoxCollider2D hatchCollider = ColliderBox("Hatch", tr, new Vector2((L.LadderHatchMinX + L.LadderHatchMaxX) * 0.5f - L.LadderX, -slab * 0.5f),
                new Vector2(hatchWidth, slab), false);
            PlatformEffector2D effector = hatchCollider.gameObject.AddComponent<PlatformEffector2D>();
            effector.useOneWay = true;
            effector.surfaceArc = 160f;
            effector.enabled = false;
            hatchCollider.usedByEffector = false;
            MeshRenderer lid = Cube("Lid", hatchCollider.transform, new Vector3(0f, 0f, D.WalkCentreZ), new Vector3(hatchWidth, slab, D.WalkDepth), "Bronze");

            float extra = L.AnkhuHeight * 0.5f;
            float climbHeight = (L.UpperY - L.FloorY) + extra;
            BoxCollider2D climb = ColliderBox("LadderVolume", tr, new Vector2(0f, (L.FloorY + L.UpperY + extra) * 0.5f - L.UpperY),
                new Vector2(0.7f, climbHeight), true);
            LadderVolume volume = climb.gameObject.AddComponent<LadderVolume>();
            volume.Configure(L.FloorY, L.UpperY, hatchCollider);
            climb.enabled = false;

            float ladderZ = D.PlayerZ + 0.4f;
            MeshRenderer extended = Cube("ExtendedLadder", tr, new Vector3(0f, -slab - 0.5f, ladderZ), new Vector3(0.6f, 1f, 0.08f), "Gold");
            extended.gameObject.SetActive(false);
            GameObject folded = Empty("FoldedLadder", tr, Vector3.zero);
            Cube("Bundle", folded.transform, new Vector3(0f, -slab - 0.25f, ladderZ), new Vector3(0.7f, 0.5f, 0.3f), "Gold");
            Cube("Strap", folded.transform, new Vector3(0f, -slab - 0.25f, ladderZ - 0.16f), new Vector3(0.75f, 0.08f, 0.02f), "Black");

            view.Configure(hatchCollider, effector, volume, lid, extended.transform, folded);

            MakeInteractable("Use_LowerLadder", tr, new Vector2(0f, InteractHeight * 0.5f), new Vector2(1.2f, InteractHeight),
                "Ladder hatch",
                "A hatch in the gallery floor with a ladder folded beneath it. Lowered, the ladder links the gallery and the hall below.",
                "ladder_hatch", InteractionIds.LowerLadder, "Lower ladder");
            MakeInteractable("Inspect_LadderFoot", tr, new Vector2(0f, L.FloorY - L.UpperY + InteractHeight * 0.5f), new Vector2(1.2f, InteractHeight),
                "Folded ladder",
                "A ladder hangs folded under the gallery floor, out of reach from here. It can only be let down from above.",
                "ladder_hatch");
            SavePrefab(root, "Ladder");
        }

        // Upper tier

        private static void BuildUpperProps(Transform props, Transform interactables)
        {
            float wallFace = D.BackWallZ - D.BackWallDepth * 0.5f;
            GameObject inscription = Empty("Inscription", props, Vector3.zero);
            Transform ins = inscription.transform;
            Block("Panel", ins, L.InscriptionMinX, L.UpperY + 0.4f, L.InscriptionMaxX, L.UpperY + 2.8f, "StoneLight", wallFace - 0.05f, 0.1f);
            List<Renderer> glyphs = new List<Renderer>();
            int columns = 5;
            float span = L.InscriptionMaxX - L.InscriptionMinX;
            for (int c = 0; c < columns; c++)
            {
                float x = L.InscriptionMinX + span * (c + 0.5f) / columns;
                for (int row = 0; row < 4; row++)
                {
                    float y = L.UpperY + 0.8f + row * 0.5f;
                    float w = (c + row) % 2 == 0 ? 0.3f : 0.18f;
                    glyphs.Add(Cube("Glyph_" + c + "_" + row, ins, new Vector3(x, y, wallFace - 0.12f), new Vector3(w, 0.22f, 0.03f), "Black"));
                }
            }
            inscription.AddComponent<InscriptionView>().Configure(glyphs.ToArray());
            float ix = (L.InscriptionMinX + L.InscriptionMaxX) * 0.5f;
            MakeInteractable("Use_ReadInscription", interactables, new Vector2(ix, L.UpperY + InteractHeight * 0.5f), new Vector2(span, InteractHeight),
                "Inscription",
                "Carved columns name the gate and speak of a shadow that outlasts its owner. Reading them closely would teach the name.",
                "inscription", InteractionIds.ReadInscription, "Read");

            MakeInteractable("Inspect_Bridge", interactables, new Vector2(L.LandingMaxX - 0.6f, L.UpperY + InteractHeight * 0.5f), new Vector2(1f, InteractHeight),
                "Pendulum bridge",
                "A pendulum bridge swings across the gap between the bell landing and the gate ledge. Bound, it would come to rest level and the chains would drop. Catching it needs a free binding.",
                "bridge");
        }

        private static void BuildBell(Transform props, Transform interactables)
        {
            float pivotY = L.BellY + 1.4f;
            float z = D.PropZ;
            Block("BellPost", props, L.LandingMinX + 0.3f, L.UpperY, L.LandingMinX + 0.55f, pivotY + 0.25f, "Bronze", z, 0.25f);
            Block("BellBeam", props, L.LandingMinX + 0.3f, pivotY, L.BellX + 0.8f, pivotY + 0.25f, "Bronze", z, 0.25f);

            GameObject root = Empty("Bell", props, new Vector3(L.BellX, pivotY, 0f));
            GameObject swing = Empty("Swing", root.transform, Vector3.zero);
            Transform sw = swing.transform;
            float bodyY = L.BellY - pivotY;
            Cube("Shank", sw, new Vector3(0f, -0.3f, z), new Vector3(0.1f, 0.6f, 0.1f), "Bronze");
            MeshRenderer crown = Cube("Crown", sw, new Vector3(0f, bodyY + 0.65f, z), new Vector3(0.6f, 0.3f, 0.6f), "BronzeDull");
            MeshRenderer body = Cube("Body", sw, new Vector3(0f, bodyY, z), new Vector3(1.0f, 1.1f, 0.9f), "BronzeDull");
            MeshRenderer lip = Cube("Lip", sw, new Vector3(0f, bodyY - 0.6f, z), new Vector3(1.4f, 0.18f, 1.1f), "BronzeDull");
            GameObject crack = Empty("Crack", sw, Vector3.zero);
            MeshRenderer crackMesh = Cube("CrackLine", crack.transform, new Vector3(0.12f, bodyY + 0.05f, z - 0.47f), new Vector3(0.06f, 0.8f, 0.02f), "Red");
            crackMesh.transform.localRotation = Quaternion.Euler(0f, 0f, 20f);
            Cube("Rope", sw, new Vector3(0f, bodyY - 1.2f, z), new Vector3(0.05f, 1.0f, 0.05f), "Bronze");

            List<Transform> rings = new List<Transform>();
            for (int i = 0; i < 3; i++)
            {
                GameObject holder = Empty("SoundRing_" + i, root.transform, new Vector3(0f, bodyY, z + 0.6f + i * 0.02f));
                Disc("Disc", holder.transform, Vector3.zero, 1.4f, 0.02f, "GlowGold").enabled = false;
                rings.Add(holder.transform);
            }
            root.AddComponent<BellView>().Configure(sw, new Renderer[] { crown, body, lip }, crack, rings.ToArray());

            MakeInteractable("Use_StrikeBell", interactables, new Vector2(L.BellX, L.UpperY + InteractHeight * 0.5f), new Vector2(1.6f, InteractHeight),
                "Bronze bell",
                "A cracked bronze bell. 'Recall bell' would make it whole. Strike it, then 'echo bell' to keep the sound alive.",
                "bell", InteractionIds.StrikeBell, "Strike bell");
        }

        private static PendulumBridge BuildBridge(Transform parent)
        {
            float len = L.BridgeLength;
            float t = BridgeDeckThickness;
            GameObject root = Empty("PendulumBridge", parent, new Vector3(L.BridgePivot.x, L.BridgePivot.y, 0f));
            KinematicBody(root, false);
            PendulumBridge bridge = root.AddComponent<PendulumBridge>();
            Transform tr = root.transform;

            // Local -y runs along the deck, local +x is the walking surface normal when horizontal.
            BoxCollider2D deck = ColliderBox("Deck", tr, new Vector2(-t * 0.5f, -len * 0.5f), new Vector2(t, len), false);
            deck.enabled = false;
            Cube("DeckMesh", deck.transform, new Vector3(0f, 0f, D.WalkCentreZ), new Vector3(t, len, D.WalkDepth - 0.6f), "UpperSlab");
            Disc("Hub", tr, new Vector3(0f, 0f, D.WalkCentreZ - D.WalkDepth * 0.5f), 0.4f, 0.1f, "Gold");

            GameObject binding = Empty("Binding_Bridge", tr, Vector3.zero);
            MeshRenderer thread = Cube("Thread", binding.transform, new Vector3(0.02f, -len * 0.5f, D.WalkCentreZ - D.WalkDepth * 0.5f + 0.2f),
                new Vector3(0.06f, len * 0.95f, 0.06f), "GlowViolet");
            binding.AddComponent<BindingIndicator>().Configure(BindingTarget.Bridge, new Renderer[] { thread });

            ZoneVolume zone = MakeZone("Zone_Bridge", tr, new Vector2(L.AnkhuHeight * 0.5f, -len * 0.5f), new Vector2(L.AnkhuHeight, len), ZoneIds.Bridge, 5);
            zone.gameObject.SetActive(false);

            bridge.Configure(deck, zone.gameObject);
            SavePrefab(root, "PendulumBridge");
            return bridge;
        }

        private static void BuildChainBarriers(Transform parent, PendulumBridge bridge)
        {
            float x = L.LandingMaxX - BarrierThickness * 0.5f;
            GameObject root = Empty("ChainBarrier_Landing", parent, new Vector3(x, L.UpperY, 0f));
            BoxCollider2D blocker = root.AddComponent<BoxCollider2D>();
            blocker.size = new Vector2(BarrierThickness, BarrierHeight);
            blocker.offset = new Vector2(0f, BarrierHeight * 0.5f);
            ChainBarrier barrier = root.AddComponent<ChainBarrier>();
            Transform tr = root.transform;
            float zFront = D.WalkCentreZ - D.WalkDepth * 0.5f + 0.2f;
            float zBack = D.WalkCentreZ + D.WalkDepth * 0.5f - 0.2f;
            float chainDepth = zBack - zFront;
            Cube("Post_Front", tr, new Vector3(0f, 0.65f, zFront), new Vector3(0.15f, 1.3f, 0.15f), "Bronze");
            Cube("Post_Back", tr, new Vector3(0f, 0.65f, zBack), new Vector3(0.15f, 1.3f, 0.15f), "Bronze");
            GameObject raised = Empty("RaisedChain", tr, Vector3.zero);
            Cube("ChainHigh", raised.transform, new Vector3(0f, 1.15f, D.WalkCentreZ), new Vector3(0.06f, 0.06f, chainDepth), "Red");
            Cube("ChainLow", raised.transform, new Vector3(0f, 0.75f, D.WalkCentreZ), new Vector3(0.06f, 0.06f, chainDepth), "Red");
            GameObject dropped = Empty("DroppedChain", tr, Vector3.zero);
            Cube("ChainSlack", dropped.transform, new Vector3(0f, 0.03f, D.WalkCentreZ), new Vector3(0.08f, 0.06f, chainDepth), "Red");
            dropped.SetActive(false);
            barrier.ConfigureParts(blocker, raised, dropped);
            GameObject asset = SavePrefab(root, "ChainBarrier");
            barrier.Configure(bridge);
            RecordOverrides(barrier);

            GameObject second = PlacePrefab(asset, parent, new Vector3(L.GateLedgeMinX + BarrierThickness * 0.5f, L.UpperY, 0f));
            second.name = "ChainBarrier_GateLedge";
            ChainBarrier secondBarrier = second.GetComponent<ChainBarrier>();
            secondBarrier.Configure(bridge);
            RecordOverrides(secondBarrier);
        }

        private static void BuildGate(Transform props, Transform interactables)
        {
            float w = L.GateWidth;
            float h = L.GateHeight;
            float z = D.PropZ + 0.4f;
            GameObject root = Empty("SunGate", props, new Vector3(L.GateX, L.UpperY, 0f));
            Transform tr = root.transform;
            Cube("Post_West", tr, new Vector3(-w * 0.5f - 0.1f, (h + 0.25f) * 0.5f, z), new Vector3(0.2f, h + 0.25f, 0.4f), "Gold");
            Cube("Post_East", tr, new Vector3(w * 0.5f + 0.1f, (h + 0.25f) * 0.5f, z), new Vector3(0.2f, h + 0.25f, 0.4f), "Gold");
            Cube("Lintel", tr, new Vector3(0f, h + 0.125f, z), new Vector3(w + 0.4f, 0.25f, 0.4f), "Gold");
            MeshRenderer dawn = Cube("Dawn", tr, new Vector3(0f, h * 0.5f, z + 0.18f), new Vector3(w, h, 0.03f), "Dawn");
            dawn.enabled = false;
            MeshRenderer westLeaf = Cube("Leaf_West", tr, new Vector3(-w * 0.25f, h * 0.5f, z), new Vector3(w * 0.5f, h, 0.15f), "StoneDark");
            MeshRenderer eastLeaf = Cube("Leaf_East", tr, new Vector3(w * 0.25f, h * 0.5f, z), new Vector3(w * 0.5f, h, 0.15f), "StoneDark");
            root.AddComponent<GateView>().Configure(westLeaf.transform, eastLeaf.transform, dawn, w);

            MakeInteractable("Inspect_Gate", interactables, new Vector2(L.GateX, L.UpperY + InteractHeight * 0.5f), new Vector2(w, InteractHeight),
                "Sun gate",
                "The gate wants sunlight, a shadow and a sustained sound at the same time. Learn its name and speak it from the threshold.",
                "gate");
        }

        // Lighting and camera

        private static void BuildLighting()
        {
            GameObject sunGo = new GameObject("Sun_Directional");
            Light sun = sunGo.AddComponent<Light>();
            sun.type = UnityEngine.LightType.Directional;
            sun.color = new Color(1f, 0.92f, 0.78f);
            sun.intensity = 1.1f;
            sun.shadows = LightShadows.Soft;
            sunGo.transform.rotation = Quaternion.LookRotation(new Vector3(0.45f, -0.75f, 0.5f));

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.10f, 0.13f, 0.25f);

            Vector3 from = new Vector3(L.ClerestoryPoint.x, L.ClerestoryPoint.y - 0.1f, D.BeamZ);
            Vector3 to = new Vector3(L.SocketX, L.BeamY, D.BeamZ);
            GameObject spotGo = new GameObject("Clerestory_Spot");
            spotGo.transform.position = from;
            spotGo.transform.rotation = Quaternion.LookRotation(to - from);
            Light spot = spotGo.AddComponent<Light>();
            spot.type = UnityEngine.LightType.Spot;
            spot.color = new Color(1f, 0.85f, 0.55f);
            spot.intensity = 4f;
            spot.range = Vector3.Distance(from, to) + 3f;
            spot.spotAngle = 28f;
            spot.shadows = LightShadows.Soft;
        }

        private static Camera BuildCamera(Vector2 spawn)
        {
            GameObject go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            Camera camera = go.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = P.DeepBlue;
            go.AddComponent<AudioListener>();
            // Projection is configured by the player factory. The start x is clamped as if the view
            // were the reference orthographic frame at 16 by 9.
            float halfWidth = L.CameraOrthoSize * 16f / 9f;
            float minX = L.RoomMinX + halfWidth;
            float maxX = L.RoomMaxX - halfWidth;
            float x = minX <= maxX ? Mathf.Clamp(spawn.x, minX, maxX) : (L.RoomMinX + L.RoomMaxX) * 0.5f;
            go.transform.position = new Vector3(x, L.CameraFixedY, -10f);
            return camera;
        }

        // Assets and settings

        /// <summary>Puts the sequence first in Build Settings, in play order, and keeps any other scenes after it.</summary>
        private static void SetBuildSettingsOrder()
        {
            var list = new List<EditorBuildSettingsScene>();
            var ours = new HashSet<string>();
            foreach (RoomSceneSpec spec in Sequence)
            {
                list.Add(new EditorBuildSettingsScene(spec.ScenePath, true));
                ours.Add(spec.ScenePath);
            }
            foreach (EditorBuildSettingsScene existing in EditorBuildSettings.scenes)
            {
                if (!ours.Contains(existing.path))
                {
                    list.Add(existing);
                }
            }
            EditorBuildSettings.scenes = list.ToArray();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string leaf = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
