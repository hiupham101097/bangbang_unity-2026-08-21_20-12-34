#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using BangBang.UI;

namespace BangBang.Editor
{
    public static class GameSetupMenu
    {
        [MenuItem("Bang Bang/🤖 ▶ BẬT VÀ CHẠY AUTO-PLAY (BOT TỰ ĐÁNH TEST LOGIC)", false, 0)]
        public static void SetupAndRunAutoPlay()
        {
            var bootstrap = EnsureBootstrap();
            if (bootstrap == null) return;

            if (!EditorApplication.isPlaying)
            {
                Undo.RecordObject(bootstrap, "Use Bang Bang Offline Gateway & AutoPlay");
                bootstrap.useLiveCloudflareServer = false;
                bootstrap.autoPlayTestBot = true;
                EditorUtility.SetDirty(bootstrap);

                var runner = Object.FindAnyObjectByType<BangBang.Core.Logic.AutoPlayBotRunner>();
                if (runner == null)
                {
                    var runnerGo = new GameObject("AutoPlayBotRunner", typeof(BangBang.Core.Logic.AutoPlayBotRunner));
                    Undo.RegisterCreatedObjectUndo(runnerGo, "Create AutoPlayBotRunner");
                }
                else
                {
                    runner.isAutoPlayActive = true;
                }

                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                Debug.Log("<color=cyan><b>[BangBang]</b> Chế độ Auto-Play đã bật! Bắt đầu PlayMode để Bot tự đánh và kiểm tra toàn bộ logic.</color>");
                EditorApplication.isPlaying = true;
            }
            else
            {
                bootstrap.useLiveCloudflareServer = false;
                bootstrap.autoPlayTestBot = true;
                bootstrap.SwitchToOfflineMode();
                var runner = Object.FindAnyObjectByType<BangBang.Core.Logic.AutoPlayBotRunner>();
                if (runner == null)
                {
                    bootstrap.gameObject.AddComponent<BangBang.Core.Logic.AutoPlayBotRunner>();
                }
                else
                {
                    runner.isAutoPlayActive = true;
                    runner.StartRunner();
                }
                Debug.Log("<color=cyan><b>[BangBang]</b> Đã kích hoạt Auto-Play Bot ngay trong lượt chơi hiện tại!</color>");
            }
        }

        [MenuItem("Bang Bang/▶ Chạy Game Theo Cấu Hình", false, 1)]
        public static void SetupAndPlayGame()
        {
            EnsureBootstrap();
            if (!EditorApplication.isPlaying) EditorApplication.isPlaying = true;
        }

        [MenuItem("Bang Bang/▶ Chạy Toàn Bộ Flow Offline", false, 2)]
        public static void SetupAndPlayOffline()
        {
            var bootstrap = EnsureBootstrap();
            if (bootstrap == null) return;

            if (!EditorApplication.isPlaying)
            {
                Undo.RecordObject(bootstrap, "Use Bang Bang Offline Gateway");
                bootstrap.useLiveCloudflareServer = false;
                EditorUtility.SetDirty(bootstrap);
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                Debug.Log("[BangBang] Chế độ Offline đã bật: có thể test đầy đủ tạo phòng, chọn vai trò, chọn nhân vật và bàn đấu.");
                EditorApplication.isPlaying = true;
            }
            else
            {
                bootstrap.SwitchToOfflineMode();
            }
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
                return bootstrap;
            }

            var go = new GameObject("GameBootstrap", typeof(GameBootstrap));
            if (!EditorApplication.isPlaying)
            {
                Undo.RegisterCreatedObjectUndo(go, "Create GameBootstrap");
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            }
            Debug.Log("[BangBang] Đã tạo GameBootstrap trong scene.");
            return go.GetComponent<GameBootstrap>();
        }
    }
}
#endif
