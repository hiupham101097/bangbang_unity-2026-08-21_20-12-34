using UnityEditor;
using UnityEngine;

public class FixUIBlur : EditorWindow
{
    [MenuItem("BangBang/Fix Blurry Images")]
    public static void FixBlurryImages()
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/GameAssets" });
        int count = 0;

        try
        {
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;

                if (importer != null)
                {
                    bool changed = false;

                    // Set compression to High Quality or None to fix blurriness
                    if (importer.textureCompression != TextureImporterCompression.Uncompressed)
                    {
                        importer.textureCompression = TextureImporterCompression.Uncompressed;
                        changed = true;
                    }

                    // Increase Max Size if it's too small for large background images
                    if (importer.maxTextureSize < 4096)
                    {
                        importer.maxTextureSize = 4096;
                        changed = true;
                    }
                    
                    // Filter Mode
                    if (importer.filterMode != FilterMode.Bilinear && importer.filterMode != FilterMode.Trilinear)
                    {
                        importer.filterMode = FilterMode.Bilinear;
                        changed = true;
                    }

                    if (changed)
                    {
                        EditorUtility.DisplayProgressBar("Fixing Blur", $"Processing {path}", (float)count / guids.Length);
                        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                        count++;
                    }
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        Debug.Log($"[FixUIBlur] Fixed {count} blurry images in Assets/GameAssets.");
    }
}
