#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BangBang.Editor
{
    public static class GameSetupMenu
    {
        [MenuItem("Bang Bang/🚀 Chạy Game Ngay (Setup & Play)", false, 1)]
        public static void SetupAndPlayGame()
        {
            var bootstrap = Object.FindFirstObjectByType<GameBootstrap>();
            if (bootstrap == null)
            {
                var go = new GameObject("GameBootstrap", typeof(GameBootstrap));
                Undo.RegisterCreatedObjectUndo(go, "Create GameBootstrap");
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                Debug.Log("[BangBang] Đã tự động tạo GameBootstrap vào Scene!");
            }

            // Enter Play Mode automatically!
            EditorApplication.isPlaying = true;
        }

        [MenuItem("Bang Bang/🛠️ Tạo GameBootstrap vào Scene", false, 2)]
        public static void CreateBootstrapObject()
        {
            var bootstrap = Object.FindFirstObjectByType<GameBootstrap>();
            if (bootstrap == null)
            {
                var go = new GameObject("GameBootstrap", typeof(GameBootstrap));
                Undo.RegisterCreatedObjectUndo(go, "Create GameBootstrap");
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                Debug.Log("[BangBang] Đã gắn GameBootstrap vào Scene thành công!");
            }
            else
            {
                Debug.Log("[BangBang] Scene đã có sẵn GameBootstrap!");
            }
        }
    }
}
#endif
