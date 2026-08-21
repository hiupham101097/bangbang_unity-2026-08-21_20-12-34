using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BangBang.Core.Data;
using UnityEngine;
using UnityEngine.Networking;

namespace BangBang.Core.Network
{
    [Serializable]
    public class SessionResponse
    {
        public string token;
    }

    [Serializable]
    public class RoomResponse
    {
        public string status;
        public string id;
        public string code;
    }

    public class BangNetworkClient : MonoBehaviour
    {
        public static BangNetworkClient Instance { get; private set; }

        [Header("Configuration")]
        public string serverBaseUrl = "https://blue-frog-fec8.hieupham101097.workers.dev";
        public string deviceId;
        public string displayName = "Cao bồi viễn tây";

        public string AuthToken { get; private set; }
        public string CurrentRoomId { get; private set; }
        public MatchStateModel CurrentState { get; private set; }

        public event Action<MatchStateModel> OnRoomStateUpdated;
        public event Action<string> OnError;

        private ClientWebSocket _webSocket;
        private CancellationTokenSource _cts;
        private bool _isConnectingWs;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                if (string.IsNullOrEmpty(deviceId))
                {
                    deviceId = PlayerPrefs.GetString("bang_device_id", "");
                    if (string.IsNullOrEmpty(deviceId))
                    {
                        deviceId = Guid.NewGuid().ToString("N").Substring(0, 16);
                        PlayerPrefs.SetString("bang_device_id", deviceId);
                    }
                }
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public void EnsureSignedIn(Action<bool> onComplete)
        {
            if (!string.IsNullOrEmpty(AuthToken))
            {
                onComplete?.Invoke(true);
                return;
            }

            StartCoroutine(PostSessionCoroutine(onComplete));
        }

        private IEnumerator PostSessionCoroutine(Action<bool> onComplete)
        {
            string url = serverBaseUrl.TrimEnd('/') + "/v1/session";
            string jsonBody = "{\"deviceId\":\"" + deviceId + "\",\"displayName\":\"" + displayName + "\"}";

            using (var req = new UnityWebRequest(url, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
                req.uploadHandler = new UploadHandlerRaw(bodyRaw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");

                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    var res = JsonUtility.FromJson<SessionResponse>(req.downloadHandler.text);
                    AuthToken = res.token;
                    onComplete?.Invoke(true);
                }
                else
                {
                    Debug.LogWarning("[BangNet] Session failed: " + req.error);
                    OnError?.Invoke("Không thể đăng nhập máy chủ.");
                    onComplete?.Invoke(false);
                }
            }
        }

        public void SendAction(string action, string payloadJson = "{}")
        {
            if (string.IsNullOrEmpty(CurrentRoomId)) return;
            StartCoroutine(SendCommandCoroutine(CurrentRoomId, action, payloadJson));
        }

        private IEnumerator SendCommandCoroutine(string roomId, string action, string payloadJson)
        {
            string url = serverBaseUrl.TrimEnd('/') + "/v1/rooms/" + roomId;
            string requestId = DateTime.UtcNow.Ticks.ToString();
            string body = "{\"action\":\"" + action + "\",\"payload\":" + payloadJson + "}";

            using (var req = new UnityWebRequest(url, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(body);
                req.uploadHandler = new UploadHandlerRaw(bodyRaw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("Authorization", "Bearer " + AuthToken);

                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning("[BangNet] Command error: " + req.error + " => " + req.downloadHandler.text);
                }
            }
        }

        public void ConnectToRoom(string roomId)
        {
            CurrentRoomId = roomId;
            EnsureSignedIn((success) =>
            {
                if (success)
                {
                    StartWebSocket();
                    StartCoroutine(PollingFallbackCoroutine());
                }
            });
        }

        private async void StartWebSocket()
        {
            if (_isConnectingWs) return;
            _isConnectingWs = true;

            try
            {
                _cts = new CancellationTokenSource();
                _webSocket = new ClientWebSocket();
                string wsUrl = serverBaseUrl.Replace("https://", "wss://").Replace("http://", "ws://") + "/v1/rooms/" + CurrentRoomId + "/ws?token=" + AuthToken;

                await _webSocket.ConnectAsync(new Uri(wsUrl), _cts.Token);
                Debug.Log("[BangNet] WebSocket connected to " + CurrentRoomId);

                byte[] buffer = new byte[16384];
                while (_webSocket.State == WebSocketState.Open)
                {
                    var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed", CancellationToken.None);
                    }
                    else
                    {
                        string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        ParseRoomJson(message);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[BangNet] WebSocket exception (will rely on polling): " + ex.Message);
            }
            finally
            {
                _isConnectingWs = false;
            }
        }

        private IEnumerator PollingFallbackCoroutine()
        {
            while (!string.IsNullOrEmpty(CurrentRoomId))
            {
                if (_webSocket == null || _webSocket.State != WebSocketState.Open)
                {
                    string url = serverBaseUrl.TrimEnd('/') + "/v1/rooms/" + CurrentRoomId;
                    using (var req = UnityWebRequest.Get(url))
                    {
                        req.SetRequestHeader("Authorization", "Bearer " + AuthToken);
                        yield return req.SendWebRequest();
                        if (req.result == UnityWebRequest.Result.Success)
                        {
                            ParseRoomJson(req.downloadHandler.text);
                        }
                    }
                }
                yield return new WaitForSeconds(2.0f);
            }
        }

        private void ParseRoomJson(string jsonText)
        {
            try
            {
                // In Unity, parse state and update match
                if (jsonText.Contains("\"room\"") || jsonText.Contains("\"players\""))
                {
                    // Dispatch state
                    // OnRoomStateUpdated?.Invoke(parsed);
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[BangNet] JSON Parse error: " + e.Message);
            }
        }

        private void OnDestroy()
        {
            _cts?.Cancel();
            _webSocket?.Dispose();
        }
    }
}
