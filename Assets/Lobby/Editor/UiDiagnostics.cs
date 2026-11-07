using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace AnimalBattler.Client.Editor
{
    public static class UiDiagnostics
    {
        [MenuItem("Animal Battler/Log UI Diagnostics")]
        public static void LogDiagnostics()
        {
            Debug.Log($"UI viewport: {Screen.width}x{Screen.height}; quality={QualitySettings.names[QualitySettings.GetQualityLevel()]}; mipLimit={QualitySettings.globalTextureMipmapLimit}");
            foreach (var document in Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
            {
                var root = document.rootVisualElement.Q<VisualElement>("game-screen");
                if (root == null) continue;
                Debug.Log($"UI panel: {document.name}; bounds={root.worldBound}; reference={document.panelSettings.referenceResolution}; match={document.panelSettings.match}");
            }
            foreach (var path in new[]
            {
                "Assets/Lobby/Art/Icons/coin-common-v3.png",
                "Assets/Lobby/Art/Icons/attack-stat-v5.png",
                "Assets/Lobby/Resources/Characters/armadillo.png",
                "Assets/Lobby/Resources/Backgrounds/courtyard-arch.png"
            })
            {
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (importer == null || texture == null) continue;
                var web = importer.GetPlatformTextureSettings("WebGL");
                Debug.Log($"UI texture: {path}; imported={texture.width}x{texture.height}; format={texture.format}; max={importer.maxTextureSize}; compression={importer.textureCompression}; mipmaps={importer.mipmapEnabled}; webOverride={web.overridden}; webMax={web.maxTextureSize}; webCompression={web.textureCompression}");
            }
        }
    }
}
