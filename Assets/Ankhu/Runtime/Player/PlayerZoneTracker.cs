using System;
using System.Collections.Generic;
using Ankhu.Core;
using Ankhu.Integration;
using Ankhu.Layout;
using UnityEngine;

namespace Ankhu.Player
{
    /// <summary>
    /// Reports which <see cref="ZoneVolume"/> Ankhu stands in. Polls with Physics2D.OverlapBox every
    /// FixedUpdate instead of trigger callbacks, because a kinematic body gets no reliable callbacks
    /// against static or kinematic triggers without full kinematic contacts. The box covers the lower
    /// body, so the zone reflects where Ankhu stands rather than what his head passes.
    /// The highest priority overlap wins. With no overlap the last known zone is kept.
    /// </summary>
    [DefaultExecutionOrder(110)]
    [DisallowMultipleComponent]
    public sealed class PlayerZoneTracker : MonoBehaviour
    {
        private const float ProbeHeight = 1.0f;

        [SerializeField] private string initialZone = ZoneIds.Floor;

        private readonly List<Collider2D> overlaps = new List<Collider2D>(16);
        private ContactFilter2D filter;
        private Rigidbody2D body;
        private string zoneId;
        private ZoneVolume currentVolume;

        /// <summary>Raised with (previous, current) zone ids when the zone changes.</summary>
        public event Action<string, string> ZoneChanged;

        public string ZoneId => string.IsNullOrEmpty(zoneId) ? initialZone : zoneId;

        /// <summary>The volume currently overlapped and chosen, or null when using the last known zone.</summary>
        public ZoneVolume CurrentVolume => currentVolume;

        public PlayerContext Context
        {
            get
            {
                string id = ZoneId;
                return new PlayerContext(id, id == ZoneIds.Lift, id == ZoneIds.Bridge);
            }
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            zoneId = initialZone;
        }

        private void OnEnable()
        {
            filter = new ContactFilter2D();
            filter.useTriggers = true;
            filter.SetLayerMask(Physics2D.AllLayers);
        }

        private void Start()
        {
            Poll();
        }

        private void FixedUpdate()
        {
            Poll();
        }

        /// <summary>Re-reads the overlapping zones now.</summary>
        public void Poll()
        {
            Vector2 feet = body != null ? body.position : (Vector2)transform.position;
            var center = new Vector2(feet.x, feet.y + ProbeHeight * 0.5f);
            var size = new Vector2(BorrowedDawnLayout.AnkhuWidth, ProbeHeight);
            int count = Physics2D.OverlapBox(center, size, 0f, filter, overlaps);

            ZoneVolume best = null;
            for (int i = 0; i < count; i++)
            {
                Collider2D c = overlaps[i];
                if (c == null || !c.isTrigger)
                {
                    continue;
                }
                ZoneVolume volume = c.GetComponent<ZoneVolume>();
                if (volume == null || !volume.isActiveAndEnabled || string.IsNullOrEmpty(volume.ZoneId))
                {
                    continue;
                }
                if (best == null || volume.Priority > best.Priority)
                {
                    best = volume;
                }
            }

            currentVolume = best;
            if (best != null && best.ZoneId != zoneId)
            {
                string previous = zoneId;
                zoneId = best.ZoneId;
                ZoneChanged?.Invoke(previous, zoneId);
            }
        }
    }
}
