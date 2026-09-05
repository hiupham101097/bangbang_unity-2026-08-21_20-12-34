#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using BangBang.UI;

namespace BangBang.Editor
{
    public static class GameSetupMenu
    {
        [MenuItem("Bang Bang/▶ Chạy Game", false, 1)]
        public static void SetupAndPlayGame()
        {
            var bootstrap = EnsureBootstrap();
            if (bootstrap != null && !EditorApplication.isPlaying)
            {
                bootstrap.useLiveCloudflareServer = true;
                EditorUtility.SetDirty(bootstrap);
            }
            if (!EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = true;
            }
        }

        private static GameBootstrap EnsureBootstrap()
        {
            var bootstrap = Object.FindAnyObjectByType<GameBootstrap>();
            if (bootstrap != null) return bootstrap;

            var go = new GameObject("GameBootstrap", typeof(GameBootstrap));
            if (!EditorApplication.isPlaying)
            {
                Undo.RegisterCreatedObjectUndo(go, "Create GameBootstrap");
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            }
            return go.GetComponent<GameBootstrap>();
        }
    }
}
#endif
