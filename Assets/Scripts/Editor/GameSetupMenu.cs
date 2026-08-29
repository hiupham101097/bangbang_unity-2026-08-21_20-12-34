#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using BangBang.UI;

namespace BangBang.Editor
{
    public static class GameSetupMenu
    {
        [MenuItem("Bang Bang/▶ Chạy Game Theo Cấu Hình", false, 1)]
        public static void SetupAndPlayGame()
        {
            EnsureBootstrap();
            EditorApplication.isPlaying = true;
        }

        [MenuItem("Bang Bang/▶ Chạy Toàn Bộ Flow Offline", false, 2)]
        public static void SetupAndPlayOffline()
        {
            var bootstrap = EnsureBootstrap();
            Undo.RecordObject(bootstrap, "Use Bang Bang Offline Gateway");
            bootstrap.useLiveCloudflareServer = false;
            EditorUtility.SetDirty(bootstrap);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[BangBang] Chế độ Offline đã bật: có thể test đầy đủ tạo phòng, chọn vai trò, chọn nhân vật và bàn đấu.");
            EditorApplication.isPlaying = true;
        }

        [MenuItem("Bang Bang/🛠 Tạo GameBootstrap Trong Scene", false, 20)]
        public static void CreateBootstrapObject()
        {
            EnsureBootstrap();
        }

        private static GameBootstrap EnsureBootstrap()
        {
            var bootstrap = Object.FindAnyObjectByType<GameBootstrap>();
            if (bootstrap != null)
            {
                Debug.Log("[BangBang] Scene đã có GameBootstrap và sẵn sàng chạy.");
                return bootstrap;
            }

            var go = new GameObject("GameBootstrap", typeof(GameBootstrap));
            Undo.RegisterCreatedObjectUndo(go, "Create GameBootstrap");
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[BangBang] Đã tạo GameBootstrap trong scene.");
            return go.GetComponent<GameBootstrap>();
        }
    }
}
#endif
