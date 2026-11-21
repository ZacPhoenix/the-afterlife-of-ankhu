using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ankhu.EditorTools
{
    /// <summary>
    /// Makes sure the project renders with URP and the Universal (3D) renderer, so a 3D Ankhu and
    /// 2D or 3D scenery share lighting. Runs before every greybox build. Uses reflection so the
    /// editor assembly does not need a compile-time reference to the URP package.
    /// If a render pipeline is already assigned it is left alone.
    /// </summary>
    public static class UniversalPipelineSetup
    {
        public const string SettingsFolder = "Assets/Ankhu/Settings";
        public const string PipelineAssetPath = SettingsFolder + "/Ankhu_URP.asset";
        public const string RendererAssetPath = SettingsFolder + "/Ankhu_URP_Renderer.asset";

        private const string PipelineTypeName =
            "UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset, Unity.RenderPipelines.Universal.Runtime";
        private const string RendererDataTypeName =
            "UnityEngine.Rendering.Universal.UniversalRendererData, Unity.RenderPipelines.Universal.Runtime";

        [MenuItem("Ankhu/Setup/Ensure URP Pipeline")]
        public static void EnsureFromMenu()
        {
            Debug.Log(EnsureUniversalPipeline());
        }

        /// <summary>Returns a one-line description of what it did.</summary>
        public static string EnsureUniversalPipeline()
        {
            if (GraphicsSettings.defaultRenderPipeline != null)
            {
                return "Render pipeline already assigned: " + GraphicsSettings.defaultRenderPipeline.name;
            }

            Type pipelineType = Type.GetType(PipelineTypeName);
            Type rendererDataType = Type.GetType(RendererDataTypeName);
            if (pipelineType == null || rendererDataType == null)
            {
                throw new InvalidOperationException(
                    "URP is not installed. Add com.unity.render-pipelines.universal in Package Manager, then rebuild.");
            }

            RenderPipelineAsset pipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(PipelineAssetPath);
            if (pipeline == null)
            {
                if (!AssetDatabase.IsValidFolder(SettingsFolder))
                {
                    AssetDatabase.CreateFolder("Assets/Ankhu", "Settings");
                }

                ScriptableObject rendererData = ScriptableObject.CreateInstance(rendererDataType);
                AssetDatabase.CreateAsset(rendererData, RendererAssetPath);

                MethodInfo create = pipelineType.GetMethod(
                    "Create",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { rendererDataType.BaseType },
                    null);
                if (create == null)
                {
                    throw new InvalidOperationException("UniversalRenderPipelineAsset.Create(ScriptableRendererData) was not found in this URP version.");
                }
                pipeline = (RenderPipelineAsset)create.Invoke(null, new object[] { rendererData });
                AssetDatabase.CreateAsset(pipeline, PipelineAssetPath);
                AssetDatabase.SaveAssets();
            }

            GraphicsSettings.defaultRenderPipeline = pipeline;
            int originalLevel = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipeline;
            }
            QualitySettings.SetQualityLevel(originalLevel, false);
            AssetDatabase.SaveAssets();
            return "Assigned URP pipeline asset " + PipelineAssetPath;
        }
    }
}
