using System;
using System.Collections.Generic;
using Ankhu.Core;
using Ankhu.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ankhu.Tests
{
    /// <summary>
    /// Guards against a greybox that builds but answers no spells: every generated scene of the
    /// sequence must carry the rules binding for its own room. Runs the real builder, which rewrites
    /// Assets/Ankhu/Generated.
    /// </summary>
    public sealed class GreyboxSceneWiringTests
    {
        [Test]
        public void RulesInstallerTypeResolves()
        {
            Assert.IsNotNull(Type.GetType(BorrowedDawnGreyboxBuilder.RulesInstallerTypeName),
                "Ankhu.Bindings did not compile or the installer was renamed. The builder looks it up by this name.");
            Assert.IsNotNull(Type.GetType(BorrowedDawnGreyboxBuilder.TeachingDirectorTypeName),
                "TeachingRoomDirector did not compile or was renamed. The builder looks it up by this name.");
        }

        [Test]
        public void EverySceneHasOneHostWiredToItsRoom()
        {
            BorrowedDawnGreyboxBuilder.Build();
            Type installerType = Type.GetType(BorrowedDawnGreyboxBuilder.RulesInstallerTypeName);
            Type directorType = Type.GetType(BorrowedDawnGreyboxBuilder.TeachingDirectorTypeName);
            Assert.IsNotNull(installerType);
            Assert.IsNotNull(directorType);

            EditorBuildSettingsScene[] buildScenes = EditorBuildSettings.scenes;
            for (int i = 0; i < BorrowedDawnGreyboxBuilder.Sequence.Length; i++)
            {
                BorrowedDawnGreyboxBuilder.RoomSceneSpec spec = BorrowedDawnGreyboxBuilder.Sequence[i];
                Assert.AreEqual(spec.ScenePath, buildScenes[i].path, "Build Settings must list the sequence in play order");

                Scene scene = EditorSceneManager.OpenScene(spec.ScenePath, OpenSceneMode.Single);
                var hosts = new List<RoomRulesHost>();
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    hosts.AddRange(root.GetComponentsInChildren<RoomRulesHost>(true));
                }
                Assert.AreEqual(1, hosts.Count, spec.SceneName + " must contain exactly one RoomRulesHost");

                Component installer = hosts[0].GetComponent(installerType);
                Assert.IsNotNull(installer, spec.SceneName + " has no BorrowedDawnRulesInstaller, so it would run without rules");
                Assert.AreEqual(spec.RoomId, new SerializedObject(installer).FindProperty("roomId").stringValue, spec.SceneName + " plays the wrong room");

                bool teaching = spec.RoomId != "borrowed-dawn";
                Assert.AreEqual(teaching, hosts[0].GetComponent(directorType) != null,
                    spec.SceneName + (teaching ? " needs Anpu's TeachingRoomDirector" : " must not run a teaching director"));
            }

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
    }
}
