﻿using System.IO;
using UnityEditor;
using UnityEngine;
using UniGLTF;


namespace VRM
{
    public static class VRMImporterMenu
    {
        [MenuItem(VRMVersion.MENU + "/Import", priority = 1)]
        static void ImportMenu()
        {
            var path = EditorUtility.OpenFilePanel("open vrm", "", "vrm");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            if (Application.isPlaying)
            {
                // load into scene
                var parser = new GltfParser();
                parser.ParsePath(path);
                var context = new VRMImporterContext(parser);
                context.Load();
                context.ShowMeshes();
                context.EnableUpdateWhenOffscreen();
                Selection.activeGameObject = context.Root;
            }
            else
            {
                if (path.StartsWithUnityAssetPath())
                {
                    Debug.LogWarningFormat("disallow import from folder under the Assets");
                    return;
                }

                var assetPath = EditorUtility.SaveFilePanel("save prefab", "Assets", Path.GetFileNameWithoutExtension(path), "prefab");
                if (string.IsNullOrEmpty(path))
                {
                    return;
                }

                if (!assetPath.StartsWithUnityAssetPath())
                {
                    Debug.LogWarningFormat("out of asset path: {0}", assetPath);
                    return;
                }

                // import as asset
                var prefabPath = UnityPath.FromUnityPath(assetPath);
                var parser = new GltfParser();
                parser.ParseGlb(File.ReadAllBytes(path));
                var context = new VRMImporterContext(parser);
                context.ExtractImages(prefabPath);

                EditorApplication.delayCall += () =>
                {
                    //
                    // after textures imported
                    //
                    context.Load();
                    context.SaveAsAsset(prefabPath);
                    context.EditorDestroyRoot();
                };
            }
        }
    }
}
