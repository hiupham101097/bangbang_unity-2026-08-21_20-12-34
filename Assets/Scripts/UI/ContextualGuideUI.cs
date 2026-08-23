using BangBang.Core.Network;
using BangBang.Core.State;
using UnityEngine;
using UnityEngine.UI;

namespace BangBang.UI
{
    /// <summary>One concise, state-driven instruction line. It never predicts legal actions.</summary>
    public sealed class ContextualGuideUI : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private Text eyebrowText;
        [SerializeField] private Text instructionText;

        public void Initialize(GameObject guideRoot, Text eyebrow, Text instruction)
        {
            root = guideRoot;
            eyebrowText = eyebrow;
            instructionText = instruction;
        }

        private void Start()
        {
            if (GameStateStore.Instance != null)
            {
                GameStateStore.Instance.OnStateSnapshotUpdated += Render;
                Render(GameStateStore.Instance.CurrentSnapshot);
            }
        }

        private void OnDestroy()
        {
            if (GameStateStore.Instance != null) GameStateStore.Instance.OnStateSnapshotUpdated -= Render;
        }

        private void Render(MatchStateSnapshotDTO snapshot)
        {
            if (root == null) return;
            if (snapshot == null || snapshot.state == ServerGameState.LOBBY || snapshot.state == ServerGameState.GAME_OVER)
            {
                root.SetActive(false);
                return;
            }

            root.SetActive(true);
            string eyebrow = "BƯỚC HIỆN TẠI";
            string instruction;
            switch (snapshot.state)
            {
                case ServerGameState.WAITING:
                    instruction = "Sẵn sàng khi bạn đã ổn. Chủ phòng bắt đầu khi đủ người.";
                    break;
                case ServerGameState.ROLE_DRAFT:
                    instruction = "Chọn một lá úp. Vai trò chỉ được tiết lộ riêng cho bạn.";
                    break;
                case ServerGameState.ROLE_LOCK_WAIT:
                    instruction = "Đã khóa vai trò. Cảnh sát trưởng sẽ được công khai.";
                    break;
                case ServerGameState.CHARACTER_DRAFT:
                    instruction = "Chọn hai lá nhân vật, đọc kỹ HP và kỹ năng rồi xác nhận một lá.";
                    break;
                case ServerGameState.CHARACTER_REVEAL:
                case ServerGameState.INITIAL_DEAL:
                    instruction = "Đang công khai nhân vật và phát bài. Cảnh sát trưởng đi trước.";
                    break;
                case ServerGameState.JUDGEMENT:
                    instruction = "Đang xử lý Dynamite rồi Jail. Chờ kết quả phán xét.";
                    break;
                case ServerGameState.DRAW:
                    instruction = snapshot.currentTurnPlayerId == GameStateStore.Instance.LocalPlayerId
                        ? "Đến lượt bạn: rút bài theo chỉ dẫn." : "Đối thủ đang rút bài.";
                    break;
                case ServerGameState.PLAY:
                    instruction = snapshot.currentTurnPlayerId == GameStateStore.Instance.LocalPlayerId
                        ? "Chọn một lá sáng, chọn mục tiêu hợp lệ, rồi xác nhận. Hoặc kết thúc lượt."
                        : "Đang chờ đối thủ. Bạn vẫn có thể được yêu cầu phản ứng.";
                    break;
                case ServerGameState.RESPONSE:
                    eyebrow = "PHẢN ỨNG";
                    instruction = "Chọn phản ứng hợp lệ trước khi hết giờ; nếu bỏ qua, server sẽ PASS.";
                    break;
                case ServerGameState.DISCARD:
                    instruction = "Bỏ số lá dư được yêu cầu để kết thúc lượt.";
                    break;
                default:
                    instruction = "Đang đồng bộ trạng thái trận đấu…";
                    break;
            }

            if (eyebrowText != null) eyebrowText.text = eyebrow;
            if (instructionText != null) instructionText.text = instruction;
        }
    }
}
