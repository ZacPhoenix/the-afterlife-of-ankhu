using System.IO;
using UnityEditor;
using UnityEngine;

namespace Ankhu.EditorTools.Character
{
    /// <summary>
    /// Import settings for the rigged Ankhu model and his Mixamo clips, applied on every import so a
    /// fresh clone or a reimport reproduces them. Paths are fixed: the model lives in
    /// Assets/Characters/Ankhu/Model and the clips in Assets/Characters/Ankhu/Animations.
    ///
    /// Model: Humanoid, avatar created from this model, materials remapped to the URP materials that
    /// <see cref="AnkhuCharacterSetup"/> writes. Clips: Humanoid with their own avatar (Mixamo Y Bot),
    /// per-clip settings (loop, root baking) are written by AnkhuCharacterSetup.ConfigureClips because
    /// they need the take list that only exists after a first import.
    /// </summary>
    public sealed class AnkhuCharacterImport : AssetPostprocessor
    {
        public const string Root = "Assets/Characters/Ankhu";
        public const string ModelFolder = Root + "/Model";
        public const string AnimationFolder = Root + "/Animations";
        public const string TextureFolder = Root + "/Textures";
        public const string MaterialFolder = Root + "/Materials";
        public const string ModelPath = ModelFolder + "/Ankhu_mixamorig_v3.fbx";

        /// <summary>
        /// Uniform import scale. The Blender export is in metres, 1.8 m tall, so 1 keeps it at 1.8 m.
        /// AnkhuVisual also normalises the instance to BorrowedDawnLayout.AnkhuHeight at runtime.
        /// </summary>
        public const float ModelGlobalScale = 1f;

        public static bool InFolder(string path, string folder)
        {
            return path.Replace('\\', '/').StartsWith(folder + "/", System.StringComparison.OrdinalIgnoreCase);
        }

        private void OnPreprocessModel()
        {
            var importer = (ModelImporter)assetImporter;
            if (InFolder(assetPath, ModelFolder))
            {
                importer.globalScale = ModelGlobalScale;
                importer.useFileScale = true;
                importer.bakeAxisConversion = true;
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.importAnimation = false;
                importer.importBlendShapes = true;
                importer.importCameras = false;
                importer.importLights = false;
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
                importer.isReadable = false;
                importer.meshCompression = ModelImporterMeshCompression.Off;
            }
            else if (InFolder(assetPath, AnimationFolder))
            {
                importer.useFileScale = true;
                importer.globalScale = 1f;
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.importAnimation = true;
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.importCameras = false;
                importer.importLights = false;
                importer.resampleCurves = true;
                importer.animationCompression = ModelImporterAnimationCompression.KeyframeReduction;
            }
        }

        private void OnPreprocessTexture()
        {
            if (!InFolder(assetPath, TextureFolder))
            {
                return;
            }
            var importer = (TextureImporter)assetImporter;
            TextureRole role = AnkhuCharacterSetup.RoleOf(Path.GetFileNameWithoutExtension(assetPath));
            importer.mipmapEnabled = true;
            importer.maxTextureSize = 4096;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            switch (role)
            {
                case TextureRole.Normal:
                    importer.textureType = TextureImporterType.NormalMap;
                    importer.sRGBTexture = false;
                    break;
                case TextureRole.Packed:
                    importer.textureType = TextureImporterType.Default;
                    importer.sRGBTexture = false;
                    break;
                default:
                    importer.textureType = TextureImporterType.Default;
                    importer.sRGBTexture = true;
                    break;
            }
        }
    }

    public enum TextureRole
    {
        BaseColor,
        Normal,
        Packed,
    }
}
