using UnityEngine;

namespace Ankhu.Core
{
    /// <summary>
    /// Trigger volume that names a place (see Ankhu.Integration.ZoneIds). The player's zone
    /// tracker reports the highest-priority volume it overlaps. The lift zone travels with the
    /// elevator platform and outranks the static zones.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class ZoneVolume : MonoBehaviour
    {
        [SerializeField] private string zoneId = Ankhu.Integration.ZoneIds.Floor;
        [SerializeField] private int priority;

        public string ZoneId => zoneId;
        public int Priority => priority;

        public void Configure(string id, int zonePriority)
        {
            zoneId = id;
            priority = zonePriority;
            GetComponent<BoxCollider2D>().isTrigger = true;
        }

        private void Reset()
        {
            GetComponent<BoxCollider2D>().isTrigger = true;
        }
    }
}
