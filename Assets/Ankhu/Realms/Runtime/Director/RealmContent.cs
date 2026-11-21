using Ankhu.Realms.Core;
using Ankhu.Realms.Core.Content;

namespace Ankhu.Realms
{
    /// <summary>Finds the scene script for a realm scene by its scene name.</summary>
    public static class RealmContent
    {
        public static SceneScript ByScene(string sceneName) => WernesScripts.ByScene(sceneName);
    }
}
