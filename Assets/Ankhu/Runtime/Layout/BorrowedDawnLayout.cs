using UnityEngine;

namespace Ankhu.Layout
{
    /// <summary>
    /// Single source of truth for Borrowed Dawn greybox geometry, in metres.
    /// x runs west (0) to east (34.5). y = 0 is the floor tier walking surface.
    /// The editor builder, the runtime presentation and the design doc all read these values.
    /// Ankhu is 1.8 m tall. There is no jump. Vertical travel is only by elevator or ladder.
    /// </summary>
    public static class BorrowedDawnLayout
    {
        // Character
        public const float AnkhuHeight = 1.8f;
        public const float AnkhuWidth = 0.6f;
        public const float WalkSpeed = 3.2f;
        public const float ClimbSpeed = 2.2f;
        public const float MaxStepHeight = 0.3f;

        // Room envelope
        public const float RoomMinX = 0f;
        public const float RoomMaxX = 34.5f;
        public const float WallThickness = 0.5f;
        public const float BasinBottomY = -4f;
        public const float FloorY = 0f;
        public const float UpperY = 5f;
        public const float UpperSlabThickness = 0.5f;
        public const float CeilingY = 10f;

        // Tiers (walkable spans)
        public const float FloorMinX = 7f;          // east edge of the elevator shaft
        public const float FloorMaxX = 34f;
        public const float GalleryMinX = 7f;
        public const float GalleryMaxX = 22f;
        public const float LandingMinX = 22f;
        public const float LandingMaxX = 26f;
        public const float GapMinX = 26f;            // pendulum bridge gap
        public const float GapMaxX = 29f;
        public const float GateLedgeMinX = 29f;
        public const float GateLedgeMaxX = 34f;

        // West cistern with the mirror raft (filled by "flow west")
        public const float RaftTankMinX = 0.5f;
        public const float RaftTankMaxX = 3.5f;
        public const float RaftTankWallMaxX = 4f;      // east wall of the cistern, x 3.5 to 4.0
        public const float RaftParapetTopY = 5.6f;     // stops Ankhu stepping onto the raft
        public const float RaftDeckLowY = -3f;
        public const float RaftDeckHighY = 5f;
        public const float RaftMirrorX = 2.6f;
        public const float RaftMirrorHeight = 1.0f;    // mirror centre above the raft deck

        // East shaft with the buoyant elevator (filled by "flow east")
        public const float ShaftMinX = 4f;
        public const float ShaftMaxX = 7f;
        public const float PlatformThickness = 0.4f;
        public const float PlatformLowY = 0f;          // platform top surface when docked at the floor
        public const float PlatformHighY = 5f;         // platform top surface when docked at the gallery
        public const float ShaftWaterLowY = -0.5f;
        public const float ShaftWaterHighY = 4.6f;
        public const float DockTolerance = 0.02f;      // platform counts as docked within this distance

        // Lift gates: bronze grilles that are open only while the platform is docked at that level
        public const float LiftGateX = 7f;
        public const float LiftGateThickness = 0.15f;
        public const float LiftGateHeight = 2.4f;

        // Mechanical moves (seconds). Logical state changes instantly, presentation eases.
        public const float MoverTransitionSeconds = 1.2f;

        // Sun and beam
        public static readonly Vector2 ClerestoryPoint = new Vector2(8.6f, 10f);
        public const float LightWellMinX = 9.9f;      // grille in the gallery slab the sun passes through
        public const float LightWellMaxX = 11f;
        public const float SocketX = 11.5f;
        public const float BeamY = 1.4f;              // horizontal beam height on the floor tier
        public const float BearerAlcoveX = 9.6f;      // west of the socket, outside the beam
        public const float BearerPedestalX = 14f;     // inside the beam
        public const float ShadowSealX = 15.6f;
        public const float ShadowSealHeight = 2.6f;
        public const float LightScreenX = 21f;        // rises through a floor slot when the raft is high
        public const float LightScreenHeight = 2.6f;
        public const float DeflectorX = 32.2f;        // turns the beam upward through the gate ledge
        public static readonly Vector2 SunDisk = new Vector2(32.2f, 9f);

        // Water selector (inspect only, spells are spoken)
        public const float WaterSelectorX = 8f;

        // Gallery
        public const float InscriptionMinX = 13f;
        public const float InscriptionMaxX = 17f;
        public const float LadderX = 19.4f;
        public const float LadderHatchMinX = 19f;
        public const float LadderHatchMaxX = 19.8f;

        // Landing, bridge, gate
        public const float BellX = 24f;
        public const float BellY = 7.6f;
        public static readonly Vector2 BridgePivot = new Vector2(26f, 5f);
        public const float BridgeLength = 3f;
        public const float BridgeSwingMinDeg = -65f;  // relative to hanging straight down
        public const float BridgeSwingMaxDeg = 90f;
        public const float BridgeSwingPeriod = 4f;
        public const float GateX = 32.2f;
        public const float GateWidth = 2.4f;
        public const float GateHeight = 3.4f;

        // Spawn and camera
        public static readonly Vector2 Spawn = new Vector2(8.8f, 0f);
        public const float CameraOrthoSize = 6.4f;    // Ankhu is about 14 percent of frame height
        public const float CameraFixedY = 4.8f;
        public const float KillPlaneY = -1.5f;
    }
}
