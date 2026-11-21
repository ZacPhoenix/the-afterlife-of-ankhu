using Ankhu.CameraRig;
using Ankhu.Layout;
using Ankhu.Speech;
using UnityEngine;
using UnityEngine.UI;

namespace Ankhu.Player
{
    /// <summary>
    /// Builds the playable Ankhu rig: the player body with physics and gameplay components, its
    /// "Visual" child, the overlay canvas with the speech bubble and HUD, and the camera rig.
    /// Works in edit mode (called from the scene builder) and in play mode. Uses no Awake side
    /// effects, so everything it wires is stored in serialized fields and survives a scene save.
    /// </summary>
    public static class PlayerRigFactory
    {
        public const string PlayerName = "Ankhu";
        public const string VisualName = "Visual";
        public const string CanvasName = "Ankhu UI";

        private static readonly Color BubbleColor = new Color(0.96f, 0.92f, 0.82f, 0.96f);
        private static readonly Color LogPanelColor = new Color(0.04f, 0.035f, 0.03f, 0.78f);
        private const float LogPanelWidth = 900f;
        private static readonly Color RimLightColor = new Color(0.72f, 0.84f, 1f);
        private const float RimLightIntensity = 2.2f;
        private const float RimLightRange = 2.6f;
        private static readonly Color StripColor = new Color(0.05f, 0.04f, 0.03f, 0.55f);
        private static readonly Color HudTextColor = new Color(0.95f, 0.90f, 0.78f);
        private static readonly Color LegendColor = new Color(0.80f, 0.76f, 0.66f);
        private static readonly Color PromptColor = new Color(1f, 0.93f, 0.74f);

        public static GameObject Create(Vector2 spawn, Camera camera)
        {
            if (camera == null)
            {
                camera = Camera.main;
            }
            if (camera == null)
            {
                camera = Object.FindAnyObjectByType<Camera>();
            }

            // Player root: feet at the transform origin, on the z = 0 play plane.
            var player = new GameObject(PlayerName);
            player.transform.SetPositionAndRotation(new Vector3(spawn.x, spawn.y, 0f), Quaternion.identity);

            var body = player.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.sleepMode = RigidbodySleepMode2D.NeverSleep;

            var capsule = player.AddComponent<CapsuleCollider2D>();
            capsule.direction = CapsuleDirection2D.Vertical;
            capsule.size = new Vector2(BorrowedDawnLayout.AnkhuWidth, BorrowedDawnLayout.AnkhuHeight);
            capsule.offset = new Vector2(0f, BorrowedDawnLayout.AnkhuHeight * 0.5f);

            var visualGo = new GameObject(VisualName);
            visualGo.transform.SetParent(player.transform, false);
            visualGo.transform.localPosition = Vector3.zero;
            visualGo.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            var visual = visualGo.AddComponent<AnkhuVisual>();

            AddRimLights(player.transform);

            var controller = player.AddComponent<PlayerController2D>();
            controller.Configure(spawn, visual);
            var tracker = player.AddComponent<PlayerZoneTracker>();
            var focus = player.AddComponent<InteractionFocus>();

            // Overlay canvas with HUD and speech bubble.
            var canvasGo = new GameObject(CanvasName, typeof(RectTransform));
            canvasGo.layer = 5;
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            canvas.pixelPerfect = false;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            var canvasRect = (RectTransform)canvasGo.transform;

            // Status strip and legend, top-left.
            Image strip = AnkhuUi.CreateImage("Status Strip", canvasRect, StripColor);
            AnkhuUi.Place(strip.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -16f), new Vector2(1240f, 76f));
            Text status = AnkhuUi.CreateText("Status", strip.rectTransform, 24, TextAnchor.UpperLeft, HudTextColor, true);
            AnkhuUi.Stretch(status.rectTransform, new Vector2(14f, 0f), new Vector2(-14f, -8f));
            Text legend = AnkhuUi.CreateText("Legend", strip.rectTransform, 19, TextAnchor.LowerLeft, LegendColor, true);
            AnkhuUi.Stretch(legend.rectTransform, new Vector2(14f, 8f), new Vector2(-14f, 0f));

            // Feedback log, bottom-left on a backing panel, oldest line on top. Lines wrap at the panel
            // width and the panel grows upward to fit them.
            Image logPanel = AnkhuUi.CreateImage("Feedback Log", canvasRect, LogPanelColor);
            RectTransform logRoot = logPanel.rectTransform;
            AnkhuUi.Place(logRoot, Vector2.zero, Vector2.zero, new Vector2(16f, 16f), new Vector2(LogPanelWidth, 0f));
            var layout = logRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 10, 10);
            layout.spacing = 6f;
            layout.childAlignment = TextAnchor.LowerLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = logRoot.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            var lines = new Text[FeedbackLog.LineCount];
            for (int i = 0; i < lines.Length; i++)
            {
                Text line = AnkhuUi.CreateText("Line " + (i + 1), logRoot, 22, TextAnchor.UpperLeft, HudTextColor, true);
                line.horizontalOverflow = HorizontalWrapMode.Wrap;
                line.verticalOverflow = VerticalWrapMode.Overflow;
                line.lineSpacing = 1.05f;
                line.gameObject.SetActive(false);
                lines[i] = line;
            }
            logPanel.gameObject.SetActive(false);

            // Interaction prompt, positioned above the head every frame.
            Text prompt = AnkhuUi.CreateText("Interaction Prompt", canvasRect, 26, TextAnchor.LowerCenter, PromptColor, true);
            AnkhuUi.Place(prompt.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(520f, 72f));
            prompt.gameObject.SetActive(false);

            // Speech bubble. The root origin is the panel's bottom-left corner.
            RectTransform bubbleRoot = AnkhuUi.CreateRect("Speech Bubble", canvasRect);
            AnkhuUi.Place(bubbleRoot, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, Vector2.zero);
            Image panel = AnkhuUi.CreateImage("Panel", bubbleRoot, BubbleColor);
            AnkhuUi.Place(panel.rectTransform, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(200f, 52f));
            Image tail = AnkhuUi.CreateImage("Tail", panel.rectTransform, BubbleColor);
            AnkhuUi.Place(tail.rectTransform, Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(14f, 0f), new Vector2(16f, 16f));
            tail.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            Text input = AnkhuUi.CreateText("Text", panel.rectTransform, 30, TextAnchor.MiddleLeft, new Color(0.17f, 0.11f, 0.05f), false);
            AnkhuUi.Stretch(input.rectTransform, new Vector2(18f, 0f), new Vector2(-18f, 0f));
            Text reply = AnkhuUi.CreateText("Reply", bubbleRoot, 24, TextAnchor.UpperLeft, Color.white, true);
            reply.horizontalOverflow = HorizontalWrapMode.Wrap;
            AnkhuUi.Place(reply.rectTransform, Vector2.zero, new Vector2(0f, 1f), new Vector2(6f, -10f), new Vector2(640f, 32f));
            reply.gameObject.SetActive(false);
            bubbleRoot.gameObject.SetActive(false);

            var bubble = canvasGo.AddComponent<SpeechBubbleController>();
            bubble.Configure(camera, player.transform, tracker, canvasRect, bubbleRoot, panel.rectTransform, input, reply);
            var log = canvasGo.AddComponent<FeedbackLog>();
            log.Configure(lines, logPanel.gameObject);
            var hud = canvasGo.AddComponent<RoomHud>();
            hud.Configure(status, legend, controller, tracker);
            status.text = RoomHud.BuildStatus(null, Ankhu.Integration.ZoneIds.Floor);

            focus.Configure(tracker, camera, canvasRect, prompt);

            if (camera != null)
            {
                CameraFollow2D follow = camera.GetComponent<CameraFollow2D>();
                if (follow == null)
                {
                    follow = camera.gameObject.AddComponent<CameraFollow2D>();
                }
                follow.Configure(player.transform, CameraFollow2D.Projection.Perspective);
                follow.SnapToTarget();
            }
            else
            {
                Debug.LogWarning("PlayerRigFactory: no camera found, so the camera rig was not set up.");
            }

            return player;
        }

        /// <summary>
        /// Two cool point lights behind Ankhu, one on each side, so his silhouette separates from dark
        /// stone and from bright painted backgrounds. They follow the root, not the visual, so they do
        /// not flip when he turns. No shadows, to keep the cost to two additional lights.
        /// </summary>
        private static void AddRimLights(Transform playerRoot)
        {
            var rig = new GameObject("RimLights");
            rig.transform.SetParent(playerRoot, false);
            for (int side = -1; side <= 1; side += 2)
            {
                var go = new GameObject(side < 0 ? "Rim_West" : "Rim_East");
                go.transform.SetParent(rig.transform, false);
                go.transform.localPosition = new Vector3(0.75f * side, BorrowedDawnLayout.AnkhuHeight * 0.75f, 0.8f);
                var light = go.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = RimLightColor;
                light.intensity = RimLightIntensity;
                light.range = RimLightRange;
                light.shadows = LightShadows.None;
                light.renderMode = LightRenderMode.ForcePixel;
            }
        }
    }
}
