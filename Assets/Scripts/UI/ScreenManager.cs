using System;
using System.Collections;
using System.Collections.Generic;
using BangBang.Core.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace BangBang.UI
{
    public enum AppScreenState
    {
        Splash,
        Home,
        RoomList,
        RoomLobby,
        Battle
    }

    public class ScreenManager : MonoBehaviour
    {
        public static ScreenManager Instance { get; private set; }

        [Header("Screen Panels")]
        public GameObject splashPanel;
        public GameObject homePanel;
        public GameObject roomListPanel;
        public GameObject roomLobbyPanel;
        public GameObject battlePanel;

        [Header("Global Fade Overlay")]
        public Image fadeOverlayImage;

        public AppScreenState CurrentScreen { get; private set; }

        public event Action<AppScreenState> OnScreenChanged;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        public void SwitchToScreen(AppScreenState screen, bool useFade = true)
        {
            if (useFade && fadeOverlayImage != null)
            {
                StartCoroutine(FadeTransitionCoroutine(screen));
            }
            else
            {
                ApplyScreenVisibility(screen);
            }
        }

        private IEnumerator FadeTransitionCoroutine(AppScreenState screen)
        {
            if (fadeOverlayImage != null)
            {
                fadeOverlayImage.gameObject.SetActive(true);
                // Fade in
                for (float t = 0; t <= 1f; t += Time.deltaTime * 3f)
                {
                    fadeOverlayImage.color = new Color(0, 0, 0, t);
                    yield return null;
                }
                fadeOverlayImage.color = Color.black;

                ApplyScreenVisibility(screen);

                // Fade out
                for (float t = 1f; t >= 0f; t -= Time.deltaTime * 3f)
                {
                    fadeOverlayImage.color = new Color(0, 0, 0, t);
                    yield return null;
                }
                fadeOverlayImage.gameObject.SetActive(false);
            }
            else
            {
                ApplyScreenVisibility(screen);
            }
        }

        private void ApplyScreenVisibility(AppScreenState screen)
        {
            CurrentScreen = screen;

            if (splashPanel != null) splashPanel.SetActive(screen == AppScreenState.Splash);
            if (homePanel != null) homePanel.SetActive(screen == AppScreenState.Home);
            if (roomListPanel != null) roomListPanel.SetActive(screen == AppScreenState.RoomList);
            if (roomLobbyPanel != null) roomLobbyPanel.SetActive(screen == AppScreenState.RoomLobby);
            if (battlePanel != null) battlePanel.SetActive(screen == AppScreenState.Battle);

            OnScreenChanged?.Invoke(screen);
        }
    }
}
