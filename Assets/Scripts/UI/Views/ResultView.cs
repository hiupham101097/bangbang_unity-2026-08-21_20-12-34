using System;
using System.Collections.Generic;
using BangBang.Core.Audio;
using BangBang.Core.Data;
using BangBang.Core.Network;
using BangBang.Core.State;
using UnityEngine;
using UnityEngine.UI;

namespace BangBang.UI.Views
{
    public class ResultView : MonoBehaviour
    {
        [Header("Victory Banner")]
        public Image bannerImage;
        public Text winnerTitleText;
        public Text winnerSubtitleText;

        [Header("Revealed Roles Container")]
        public Transform playerResultsContainer;

        [Header("Action Buttons")]
        public Button rematchButton;
        public Button returnToLobbyButton;

        private readonly List<GameObject> _resultCardObjects = new List<GameObject>();

        private void Awake()
        {
        }

        private void Start()
        {
            BindListeners();
            if (GameStateStore.Instance != null)
            {
                GameStateStore.Instance.OnStateSnapshotUpdated += RenderGameResults;
                if (GameStateStore.Instance.CurrentSnapshot != null && GameStateStore.Instance.CurrentSnapshot.state == ServerGameState.FINISHED)
                {
                    RenderGameResults(GameStateStore.Instance.CurrentSnapshot);
                }
            }
        }

        public void BindListeners()
        {
            if (rematchButton != null)
            {
                rematchButton.onClick.RemoveAllListeners();
                rematchButton.onClick.AddListener(HandleRematchClicked);
            }

            if (returnToLobbyButton != null)
            {
                returnToLobbyButton.onClick.RemoveAllListeners();
                returnToLobbyButton.onClick.AddListener(HandleReturnToLobbyClicked);
            }
        }

        private void OnDestroy()
        {
            if (GameStateStore.Instance != null)
            {
                GameStateStore.Instance.OnStateSnapshotUpdated -= RenderGameResults;
            }
        }

        public void RenderGameResults(MatchStateSnapshotDTO snapshot)
        {
            if (snapshot == null || snapshot.state != ServerGameState.FINISHED) return;

            string winTeam = !string.IsNullOrEmpty(snapshot.winnerTeam) ? snapshot.winnerTeam : "Cảnh Sát Trưởng";
            if (winnerTitleText != null)
            {
                winnerTitleText.text = "🏆 PHE " + winTeam.ToUpper() + " CHIẾN THẮNG!";
                winnerTitleText.color = winTeam.ToLower().Contains("cảnh sát") ? new Color(1f, 0.85f, 0.2f) : new Color(1f, 0.3f, 0.3f);
            }

            if (winnerSubtitleText != null)
            {
                winnerSubtitleText.text = "Trận đấu đã khép lại sau " + snapshot.turnNumber + " lượt.";
            }

            // Play Victory audio
            var local = snapshot.players.Find(p => p.id == GameStateStore.Instance.LocalPlayerId);
            bool isWinner = local != null && local.isAlive;
            AudioManager.Instance?.PlaySFX(isWinner ? "win" : "lose");

            // Reveal all players and roles
            if (playerResultsContainer != null)
            {
                foreach (var go in _resultCardObjects) Destroy(go);
                _resultCardObjects.Clear();

                foreach (var p in snapshot.players)
                {
                    var card = CreatePlayerResultCard(p);
                    card.transform.SetParent(playerResultsContainer, false);
                    _resultCardObjects.Add(card);
                }
            }
        }

        private GameObject CreatePlayerResultCard(PlayerSnapshotDTO player)
        {
            var cardObj = new GameObject("Result_" + player.id, typeof(RectTransform), typeof(Image));
            var rt = cardObj.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(180, 260);

            var bgImg = cardObj.GetComponent<Image>();
            bgImg.color = player.isAlive ? new Color(0.2f, 0.35f, 0.2f, 0.95f) : new Color(0.25f, 0.15f, 0.15f, 0.8f);

            // Avatar
            var avatarObj = new GameObject("Avatar", typeof(RectTransform), typeof(Image));
            avatarObj.transform.SetParent(cardObj.transform, false);
            var avatarRt = avatarObj.GetComponent<RectTransform>();
            avatarRt.anchoredPosition = new Vector2(0, 45f);
            avatarRt.sizeDelta = new Vector2(80, 80);
            var avatarImg = avatarObj.GetComponent<Image>();
            if (!string.IsNullOrEmpty(player.characterId))
            {
                var charInfo = CardCatalogDatabase.GetCharacterInfo(player.characterId);
                avatarImg.sprite = CardCatalogDatabase.LoadSprite(charInfo.resourcePath);
            }

            // Name
            var nameObj = new GameObject("Name", typeof(RectTransform), typeof(Text));
            nameObj.transform.SetParent(cardObj.transform, false);
            var nameRt = nameObj.GetComponent<RectTransform>();
            nameRt.anchoredPosition = new Vector2(0, -15f);
            nameRt.sizeDelta = new Vector2(170, 24);
            var nameTxt = nameObj.GetComponent<Text>();
            nameTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            nameTxt.fontSize = 13;
            nameTxt.fontStyle = FontStyle.Bold;
            nameTxt.alignment = TextAnchor.MiddleCenter;
            nameTxt.color = Color.white;
            nameTxt.text = player.name;

            // Revealed Role
            var roleObj = new GameObject("Role", typeof(RectTransform), typeof(Text));
            roleObj.transform.SetParent(cardObj.transform, false);
            var roleRt = roleObj.GetComponent<RectTransform>();
            roleRt.anchoredPosition = new Vector2(0, -45f);
            roleRt.sizeDelta = new Vector2(170, 24);
            var roleTxt = roleObj.GetComponent<Text>();
            roleTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            roleTxt.fontSize = 13;
            roleTxt.fontStyle = FontStyle.Bold;
            roleTxt.alignment = TextAnchor.MiddleCenter;
            roleTxt.color = new Color(1f, 0.85f, 0.3f);
            roleTxt.text = "Vai trò: " + (!string.IsNullOrEmpty(player.role) ? player.role.ToUpper() : "OUTLAW");

            // Status (Alive / Dead)
            var statusObj = new GameObject("Status", typeof(RectTransform), typeof(Text));
            statusObj.transform.SetParent(cardObj.transform, false);
            var statusRt = statusObj.GetComponent<RectTransform>();
            statusRt.anchoredPosition = new Vector2(0, -75f);
            statusRt.sizeDelta = new Vector2(170, 24);
            var statusTxt = statusObj.GetComponent<Text>();
            statusTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            statusTxt.fontSize = 12;
            statusTxt.alignment = TextAnchor.MiddleCenter;
            statusTxt.color = player.isAlive ? new Color(0.3f, 1f, 0.4f) : Color.red;
            statusTxt.text = player.isAlive ? "🟢 SỐNG SÓT" : "💀 ĐÃ BỊ LOẠI";

            return cardObj;
        }

        private async void HandleRematchClicked()
        {
            AudioManager.Instance?.PlaySFX("button_tap");
            GameStateStore.Instance?.SetRequestPending(true);
            if (GameStateStore.Instance?.Gateway != null)
            {
                await GameStateStore.Instance.Gateway.RequestRematchAsync();
            }
        }

        private async void HandleReturnToLobbyClicked()
        {
            AudioManager.Instance?.PlaySFX("button_tap");
            GameStateStore.Instance?.SetRequestPending(true);
            if (GameStateStore.Instance?.Gateway != null)
            {
                await GameStateStore.Instance.Gateway.LeaveRoomAsync();
            }
            GameFlowController.Instance?.TransitionToState(ServerGameState.LOBBY);
        }
    }
}
