using System;
using UnityEditor;
using UnityEngine;

namespace Ankhu.Realms.EditorTools
{
    public static partial class RealmSceneBuilder
    {
        /// <summary>
        /// Rebuilds Realm_01 to 04 from code without touching Build Settings (BuildAll reorders them).
        ///   Unity -batchmode -projectPath . -executeMethod Ankhu.Realms.EditorTools.RealmSceneBuilder.BuildRealmScenesOnlyCli
        /// </summary>
        public static void BuildRealmScenesOnlyCli()
        {
            try
            {
                RealmKit.EnsureDirs();
                BuildArit1();
                BuildField();
                BuildHouse();
                BuildRow();
                AssetDatabase.SaveAssets();
                Debug.Log("[Realms] Realm_01 to 04 rebuilt, Build Settings untouched.");
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }
    }
}
