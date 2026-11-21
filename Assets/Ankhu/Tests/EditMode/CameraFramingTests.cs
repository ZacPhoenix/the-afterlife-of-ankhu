using Ankhu.CameraRig;
using NUnit.Framework;
using UnityEngine;

namespace Ankhu.Tests
{
    public sealed class CameraFramingTests
    {
        [Test]
        public void ClampKeepsViewInsideRoom()
        {
            Assert.AreEqual(10f, CameraFollow2D.ClampCameraX(2f, 10f, 0f, 34.5f), 1e-4f);
            Assert.AreEqual(24.5f, CameraFollow2D.ClampCameraX(40f, 10f, 0f, 34.5f), 1e-4f);
            Assert.AreEqual(17f, CameraFollow2D.ClampCameraX(17f, 10f, 0f, 34.5f), 1e-4f);
        }

        [Test]
        public void ClampCentresWhenRoomIsNarrowerThanView()
        {
            Assert.AreEqual(17.25f, CameraFollow2D.ClampCameraX(3f, 20f, 0f, 34.5f), 1e-4f);
        }

        [Test]
        public void PerspectiveDistanceMatchesOrthographicSpan()
        {
            float distance = CameraFollow2D.DistanceForFieldOfView(6.4f, 20f);
            float halfHeight = distance * Mathf.Tan(10f * Mathf.Deg2Rad);
            Assert.AreEqual(6.4f, halfHeight, 1e-3f);
        }
    }
}
