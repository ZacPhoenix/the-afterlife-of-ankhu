using UnityEngine;

namespace Ankhu.Realms
{
    /// <summary>
    /// Optional scene holder an editor builder can populate so the magic shaders are referenced by the scene
    /// (and therefore included in player builds) and resolved without Shader.Find. Registers on Awake.
    /// The two Ankhu shaders already ship via Resources, so this is a belt and braces measure.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public sealed class MagicShaderRefs : MonoBehaviour
    {
        [Tooltip("For example Ankhu/Realms/MagicAdditive, Ankhu/Realms/GoldRim, Universal Render Pipeline/Particles/Unlit.")]
        public Shader[] shaders = new Shader[0];

        [Tooltip("Optional additive template material (made with MagicMaterials.ConfigureAdditive and saved as an asset).")]
        public Material additiveTemplate;

        [Tooltip("Optional alpha blended template material (MagicMaterials.ConfigureAlphaBlend).")]
        public Material alphaTemplate;

        private void Awake()
        {
            MagicMaterials.RegisterShaders(shaders);
            MagicMaterials.RegisterTemplates(additiveTemplate, alphaTemplate);
        }
    }
}
