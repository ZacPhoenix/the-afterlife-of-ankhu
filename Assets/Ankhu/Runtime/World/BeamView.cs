using Ankhu.Integration;
using Ankhu.Layout;
using UnityEngine;

namespace Ankhu.World
{
    /// <summary>
    /// Draws the sunlight from snapshot fields only.
    /// Sun shaft: clerestory to socket. Faint always (sunlight is constant), full when the mirror
    /// is in the socket.
    /// Near beam: socket eastward, shown when <see cref="RoomSnapshot.Mirror"/> is Socket. It ends
    /// at the deflector when <see cref="RoomSnapshot.SunReachesGate"/>, otherwise at the first
    /// blocker the snapshot reports (Bearer asleep on his pedestal, then a raised light screen).
    /// Far beam: deflector up to the sun disk, shown only when SunReachesGate. The sun disk lights
    /// up with it.
    /// </summary>
    public sealed class BeamView : SnapshotListener
    {
        [Header("Lines")]
        [Tooltip("Zac 2026-11-26: the teaching rooms have no mirror socket, so the faint shaft read as a stray white bar across the painted wall. Off there, the shaft only shows once the mirror is installed.")]
        [SerializeField] private bool faintShaft = true;
        [SerializeField] private LineRenderer sunShaft;
        [SerializeField] private LineRenderer nearCore;
        [SerializeField] private LineRenderer nearGlow;
        [SerializeField] private LineRenderer farCore;
        [SerializeField] private LineRenderer farGlow;

        [Header("Sun disk above the gate")]
        [SerializeField] private Renderer sunDisk;
        [SerializeField] private Renderer sunHalo;

        [Header("Geometry (defaults from BorrowedDawnLayout)")]
        [SerializeField] private float z = GreyboxDepth.BeamZ;
        [SerializeField] private float bearerStopX = BorrowedDawnLayout.BearerPedestalX - 0.35f;
        [SerializeField] private float screenStopX = BorrowedDawnLayout.LightScreenX - 0.12f;

        private bool shaftFull;
        private bool farOn;

        public void Configure(LineRenderer shaft, LineRenderer nearC, LineRenderer nearG, LineRenderer farC, LineRenderer farG, Renderer disk, Renderer halo)
        {
            sunShaft = shaft;
            nearCore = nearC;
            nearGlow = nearG;
            farCore = farC;
            farGlow = farG;
            sunDisk = disk;
            sunHalo = halo;
        }

        private void Awake()
        {
            EnsureMaterial(sunShaft);
            EnsureMaterial(nearCore);
            EnsureMaterial(nearGlow);
            EnsureMaterial(farCore);
            EnsureMaterial(farGlow);
        }

        private static void EnsureMaterial(LineRenderer line)
        {
            if (line == null || line.sharedMaterial != null)
            {
                return;
            }
            Shader shader = GreyboxRender.FindUnlitShader();
            if (shader != null)
            {
                line.sharedMaterial = new Material(shader) { name = "BeamRuntime" };
            }
        }

        protected override void Apply(RoomSnapshot previous, RoomSnapshot next, bool instant)
        {
            Vector3 clerestory = new Vector3(BorrowedDawnLayout.ClerestoryPoint.x, BorrowedDawnLayout.ClerestoryPoint.y, z);
            Vector3 socket = new Vector3(BorrowedDawnLayout.SocketX, BorrowedDawnLayout.BeamY, z);
            Vector3 deflector = new Vector3(BorrowedDawnLayout.DeflectorX, BorrowedDawnLayout.BeamY, z);
            Vector3 disk = new Vector3(BorrowedDawnLayout.SunDisk.x, BorrowedDawnLayout.SunDisk.y, z);

            bool installed = next.Mirror == MirrorLocation.Socket;
            shaftFull = installed;
            SetLine(sunShaft, clerestory, socket, installed || faintShaft, installed ? GreyboxPalette.WithAlpha(GreyboxPalette.SunShaft, 0.55f) : GreyboxPalette.SunShaft);

            float endX;
            if (next.SunReachesGate)
            {
                endX = deflector.x;
            }
            else if (!next.BearerAwake)
            {
                endX = bearerStopX;
            }
            else if (next.LightScreenRaised)
            {
                endX = screenStopX;
            }
            else
            {
                endX = deflector.x;
            }
            Vector3 nearEnd = new Vector3(endX, BorrowedDawnLayout.BeamY, z);
            SetLine(nearCore, socket, nearEnd, installed, GreyboxPalette.BeamCore);
            SetLine(nearGlow, socket, nearEnd, installed, GreyboxPalette.BeamGlow);

            farOn = next.SunReachesGate;
            SetLine(farCore, deflector, disk, farOn, GreyboxPalette.BeamCore);
            SetLine(farGlow, deflector, disk, farOn, GreyboxPalette.BeamGlow);

            GreyboxRender.Tint(sunDisk, farOn ? GreyboxPalette.Gold : GreyboxPalette.GoldDim);
            GreyboxRender.SetVisible(sunHalo, farOn);
        }

        private void Update()
        {
            // Gentle shimmer on the glow lines and halo. Cosmetic only.
            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * 3.1f);
            if (nearGlow != null && nearGlow.enabled)
            {
                GreyboxRender.Tint(nearGlow, GreyboxPalette.WithAlpha(GreyboxPalette.BeamGlow, GreyboxPalette.BeamGlow.a * pulse));
            }
            if (farOn)
            {
                GreyboxRender.Tint(farGlow, GreyboxPalette.WithAlpha(GreyboxPalette.BeamGlow, GreyboxPalette.BeamGlow.a * pulse));
                GreyboxRender.Tint(sunHalo, GreyboxPalette.WithAlpha(GreyboxPalette.Gold, 0.35f * pulse));
            }
            if (shaftFull && sunShaft != null)
            {
                GreyboxRender.Tint(sunShaft, GreyboxPalette.WithAlpha(GreyboxPalette.SunShaft, 0.45f + 0.1f * pulse));
            }
        }

        private static void SetLine(LineRenderer line, Vector3 a, Vector3 b, bool visible, Color color)
        {
            if (line == null)
            {
                return;
            }
            line.enabled = visible;
            if (!visible)
            {
                return;
            }
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.SetPosition(0, a);
            line.SetPosition(1, b);
            // Vertex colour stays white. Colour comes from the property block so that both URP
            // Unlit (which ignores vertex colour) and Sprites/Default render the same tint.
            line.startColor = Color.white;
            line.endColor = Color.white;
            GreyboxRender.Tint(line, color);
        }
    }
}
