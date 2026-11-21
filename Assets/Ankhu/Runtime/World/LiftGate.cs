using Ankhu.Layout;
using UnityEngine;

namespace Ankhu.World
{
    /// <summary>
    /// Bronze grille at the shaft mouth on one tier. It is solid unless the elevator platform is
    /// physically docked at this level (within DockTolerance). This reads the platform's real
    /// position, not puzzle state, so the shaft is always safe to step into.
    /// </summary>
    public sealed class LiftGate : MonoBehaviour
    {
        [SerializeField] private ElevatorPlatform platform;
        [SerializeField] private float dockY;
        [SerializeField] private Collider2D blocker;
        [Tooltip("Visual bars that slide up into the lintel when open.")]
        [SerializeField] private Transform bars;
        [SerializeField] private float openLift = BorrowedDawnLayout.LiftGateHeight * 0.92f;
        [SerializeField] private Renderer lamp;

        private Vector3 barsClosedLocal;
        private float openAmount;
        private bool isOpen;
        private bool lampInitialised;

        public bool IsOpen => isOpen;
        public float DockY => dockY;

        public void Configure(ElevatorPlatform targetPlatform, float levelY)
        {
            platform = targetPlatform;
            dockY = levelY;
        }

        public void ConfigureParts(Collider2D solidBlocker, Transform barsVisual, Renderer lampRenderer)
        {
            blocker = solidBlocker;
            bars = barsVisual;
            lamp = lampRenderer;
        }

        private void Awake()
        {
            if (bars != null)
            {
                barsClosedLocal = bars.localPosition;
            }
        }

        private void Start()
        {
            if (platform == null)
            {
                platform = Object.FindFirstObjectByType<ElevatorPlatform>();
                if (platform == null)
                {
                    Debug.LogError("LiftGate has no ElevatorPlatform to watch. It stays closed.", this);
                }
            }
            Evaluate(true);
        }

        private void FixedUpdate()
        {
            Evaluate(false);
        }

        private void Evaluate(bool force)
        {
            bool open = platform != null && platform.IsDockedAt(dockY);
            if (open == isOpen && !force)
            {
                return;
            }
            isOpen = open;
            if (blocker != null)
            {
                blocker.enabled = !open;
            }
            if (lamp != null)
            {
                GreyboxRender.Tint(lamp, open ? GreyboxPalette.Gold : GreyboxPalette.Red);
                lampInitialised = true;
            }
            if (force)
            {
                openAmount = open ? 1f : 0f;
                PlaceBars();
            }
        }

        private void Update()
        {
            if (lamp != null && !lampInitialised)
            {
                GreyboxRender.Tint(lamp, isOpen ? GreyboxPalette.Gold : GreyboxPalette.Red);
                lampInitialised = true;
            }
            float target = isOpen ? 1f : 0f;
            if (Mathf.Approximately(openAmount, target))
            {
                return;
            }
            // Bars always settle within a quarter of the mover time so they never lag the dock.
            float speed = 4f / Mathf.Max(0.01f, BorrowedDawnLayout.MoverTransitionSeconds);
            openAmount = Mathf.MoveTowards(openAmount, target, speed * Time.deltaTime);
            PlaceBars();
        }

        private void PlaceBars()
        {
            if (bars == null)
            {
                return;
            }
            bars.localPosition = barsClosedLocal + Vector3.up * (openLift * GreyboxRender.Ease(openAmount));
        }
    }
}
