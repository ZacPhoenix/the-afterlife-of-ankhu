using System;
using Ankhu.Core;
using Ankhu.EditorTools;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ankhu.Tests
{
    /// <summary>
    /// Guards against a greybox that builds but answers no spells: the generated scene must carry the
    /// rules binding on its RoomRulesHost. Runs the real builder, which rewrites Assets/Ankhu/Generated.
    /// </summary>
    public sealed class GreyboxSceneWiringTests
    {
        [Test]
        public void RulesInstallerTypeResolves()
        {
            Assert.IsNotNull(Type.GetType(BorrowedDawnGreyboxBuilder.RulesInstallerTypeName),
                "Ankhu.Bindings did not compile or the installer was renamed. The builder looks it up by this name.");
        }

        [Test]
        public void BuiltSceneHasExactlyOneHostWithTheRulesInstaller()
        {
            BorrowedDawnGreyboxBuilder.Build();

            var hosts = new System.Collections.Generic.List<RoomRulesHost>();
            foreach (GameObject root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                hosts.AddRange(root.GetComponentsInChildren<RoomRulesHost>(true));
            }
            Assert.AreEqual(1, hosts.Count, "The greybox scene must contain exactly one RoomRulesHost");

            Type installer = Type.GetType(BorrowedDawnGreyboxBuilder.RulesInstallerTypeName);
            Assert.IsNotNull(installer);
            Assert.IsNotNull(hosts[0].GetComponent(installer), "RoomRulesHost has no BorrowedDawnRulesInstaller, so the room would run without rules");

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
    }
}
