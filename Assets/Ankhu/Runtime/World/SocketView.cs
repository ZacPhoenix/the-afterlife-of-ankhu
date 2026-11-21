using Ankhu.Integration;
using UnityEngine;

namespace Ankhu.World
{
    /// <summary>Shows the mirror in the sun socket while <see cref="RoomSnapshot.Mirror"/> is Socket.</summary>
    public sealed class SocketView : SnapshotListener
    {
        [SerializeField] private GameObject installedMirror;

        public void Configure(GameObject mirror)
        {
            installedMirror = mirror;
        }

        protected override void Apply(RoomSnapshot previous, RoomSnapshot next, bool instant)
        {
            GreyboxRender.SetActive(installedMirror, next.Mirror == MirrorLocation.Socket);
        }
    }
}
