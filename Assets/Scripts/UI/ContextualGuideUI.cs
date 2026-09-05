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
        private CanvasGroup visibilityGroup;

        public void Initialize(GameObject guideRoot, Text eyebrow, Text instruction)
        {
            root = guideRoot;
            eyebrowText = eyebrow;
            instructionText = instruction;
            visibilityGroup = guideRoot.GetComponent<CanvasGroup>();
            if (visibilityGroup == null) visibilityGroup = guideRoot.AddComponent<CanvasGroup>();
            visibilityGroup.blocksRaycasts = false;
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

        private void LateUpdate()
        {
            var bootstrap = GameBootstrap.Instance;
            if (visibilityGroup == null || bootstrap == null) return;
            bool visible = (bootstrap.lobbyView != null && bootstrap.lobbyView.gameObject.activeInHierarchy)
                || (bootstrap.waitingRoomView != null && bootstrap.waitingRoomView.gameObject.activeInHierarchy)
                || (bootstrap.roleRevealView != null && bootstrap.roleRevealView.gameObject.activeInHierarchy)
                || (bootstrap.characterSelectionView != null && bootstrap.characterSelectionView.gameObject.activeInHierarchy)
                || (bootstrap.gameTableView != null && bootstrap.gameTableView.gameObject.activeInHierarchy);
            visibilityGroup.alpha = visible ? 1f : 0f;
        }

        private void Render(MatchStateSnapshotDTO snapshot)
        {
            if (root == null) return;
            if (snapshot != null && snapshot.state == ServerGameState.GAME_OVER)
            {
                root.SetActive(false);
                return;
            }

            root.SetActive(true);
            string eyebrow = "BƯỚC 1/4  •  CHỌN PHÒNG";
            string instruction;
            if (snapshot == null || snapshot.state == ServerGameState.LOBBY)
            {
                instruction = "Chọn một phòng đang mở, nhập mã mời, hoặc tạo bàn mới.";
                if (eyebrowText != null) eyebrowText.text = eyebrow;
                if (instructionText != null) instructionText.text = instruction;
                return;
            }

            switch (snapshot.state)
            {
                case ServerGameState.WAITING:
                    eyebrow = "BƯỚC 2/4  •  SẴN SÀNG";
                    instruction = "Kiểm tra người chơi, bật SẴN SÀNG; chủ phòng bắt đầu khi mọi người đã sẵn sàng.";
                    break;
                case ServerGameState.ROLE_DRAFT:
                    eyebrow = "BƯỚC 3/4  •  VAI TRÒ";
                    instruction = "Vai trò đang được chia. Đọc mục tiêu phe của bạn trước khi tiếp tục.";
                    break;
                case ServerGameState.ROLE_LOCK_WAIT:
                    eyebrow = "BƯỚC 3/4  •  VAI TRÒ";
                    instruction = "Đã khóa vai trò. Cảnh sát trưởng sẽ được công khai.";
                    break;
                case ServerGameState.CHARACTER_DRAFT:
                    eyebrow = "BƯỚC 3/4  •  CHỌN NHÂN VẬT";
                    instruction = "So sánh 2 nhân vật, chọn 1 lá rồi nhấn XÁC NHẬN CHỌN TƯỚNG.";
                    break;
                case ServerGameState.CHARACTER_REVEAL:
                case ServerGameState.INITIAL_DEAL:
                    eyebrow = "BƯỚC 4/4  •  VÀO TRẬN";
                    instruction = "Đang công khai nhân vật và phát bài. Cảnh sát trưởng đi trước.";
                    break;
                case ServerGameState.JUDGEMENT:
                    eyebrow = "LƯỢT " + Mathf.Max(1, snapshot.turnNumber) + "  •  PHÁN XÉT";
                    instruction = "Đang xử lý Dynamite rồi Jail. Chờ kết quả phán xét.";
                    break;
                case ServerGameState.DRAW:
                    eyebrow = "LƯỢT " + Mathf.Max(1, snapshot.turnNumber) + "  •  1/3 RÚT BÀI";
                    instruction = snapshot.currentTurnPlayerId == GameStateStore.Instance.LocalPlayerId
                        ? "Đến lượt bạn: rút bài theo chỉ dẫn." : "Đối thủ đang rút bài.";
                    break;
                case ServerGameState.PLAY:
                    bool drawPhase = string.Equals(snapshot.currentPhase, "DRAW", System.StringComparison.OrdinalIgnoreCase);
                    bool discardPhase = string.Equals(snapshot.currentPhase, "DISCARD", System.StringComparison.OrdinalIgnoreCase);
                    eyebrow = "LƯỢT " + Mathf.Max(1, snapshot.turnNumber) + (drawPhase ? "  •  1/3 RÚT BÀI" : discardPhase ? "  •  3/3 BỎ BÀI" : "  •  2/3 ĐÁNH BÀI");
                    if (snapshot.currentTurnPlayerId != GameStateStore.Instance.LocalPlayerId)
                        instruction = "Đang chờ đối thủ. Bạn vẫn có thể được yêu cầu phản ứng.";
                    else if (drawPhase)
                        instruction = "Nhấn RÚT 2 LÁ để mở bước đánh bài.";
                    else if (discardPhase)
                        instruction = "Chọn số lá dư cần bỏ để hoàn tất lượt.";
                    else
                        instruction = "Chọn một lá sáng, chọn mục tiêu hợp lệ nếu cần, rồi xác nhận; hoặc kết thúc lượt.";
                    break;
                case ServerGameState.RESPONSE:
                    eyebrow = "PHẢN ỨNG  •  CẦN TRẢ LỜI";
                    instruction = "Chọn phản ứng hợp lệ trước khi hết giờ; nếu bỏ qua, server sẽ PASS.";
                    break;
                case ServerGameState.DISCARD:
                    eyebrow = "LƯỢT " + Mathf.Max(1, snapshot.turnNumber) + "  •  3/3 BỎ BÀI";
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
