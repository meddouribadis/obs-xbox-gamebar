using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using ObsGameBarWidget.Models;

namespace ObsGameBarWidget.Services
{
    public class ObsWebSocketService : IDisposable
    {
        private ClientWebSocket _webSocket;
        private CancellationTokenSource _cts;
        private readonly ConcurrentDictionary<string, TaskCompletionSource<JObject>> _pendingRequests =
            new ConcurrentDictionary<string, TaskCompletionSource<JObject>>();

        public ObsConnectionState State { get; private set; } = ObsConnectionState.Disconnected;
        public ObsServerConfig Config { get; set; } = new ObsServerConfig();

        // Events
        public event EventHandler<ObsConnectionState> ConnectionStateChanged;
        public event EventHandler<(bool isActive, bool isPaused, string state)> RecordStateChanged;
        public event EventHandler<(bool isActive, string state)> StreamStateChanged;
        public event EventHandler<bool> VirtualCamStateChanged;
        public event EventHandler<bool> ReplayBufferStateChanged;
        public event EventHandler<string> ReplayBufferSaved;
        public event EventHandler<string> CurrentSceneChanged;
        public event EventHandler<List<string>> SceneListChanged;
        public event EventHandler<(string inputName, bool isMuted)> InputMuteChanged;
        public event EventHandler<string> StatusMessage;

        public async Task ConnectAsync()
        {
            if (State == ObsConnectionState.Connecting || State == ObsConnectionState.Connected)
                return;

            SetState(ObsConnectionState.Connecting);
            _cts?.Cancel();
            _cts = new CancellationTokenSource();

            try
            {
                _webSocket?.Dispose();
                _webSocket = new ClientWebSocket();

                var uri = new Uri($"ws://{Config.Host}:{Config.Port}");
                await _webSocket.ConnectAsync(uri, _cts.Token);

                // Start message receive loop
                _ = Task.Run(() => ReceiveLoopAsync(_cts.Token));
            }
            catch (Exception ex)
            {
                SetState(ObsConnectionState.Error);
                StatusMessage?.Invoke(this, $"Erreur de connexion : {ex.Message}");
                Disconnect();
            }
        }

        public void Disconnect()
        {
            _cts?.Cancel();
            try
            {
                if (_webSocket != null && _webSocket.State == WebSocketState.Open)
                {
                    _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None).Wait(1000);
                }
            }
            catch { }
            finally
            {
                _webSocket?.Dispose();
                _webSocket = null;
                SetState(ObsConnectionState.Disconnected);
            }
        }

        private async Task ReceiveLoopAsync(CancellationToken ct)
        {
            var buffer = new byte[8192];
            var ms = new MemoryStream();

            try
            {
                while (!ct.IsCancellationRequested && _webSocket != null && _webSocket.State == WebSocketState.Open)
                {
                    ms.SetLength(0);
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            Disconnect();
                            return;
                        }
                        ms.Write(buffer, 0, result.Count);
                    }
                    while (!result.EndOfMessage);

                    ms.Seek(0, SeekOrigin.Begin);
                    using (var reader = new StreamReader(ms, Encoding.UTF8, false, 8192, true))
                    {
                        string message = await reader.ReadToEndAsync();
                        ProcessMessage(message);
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                StatusMessage?.Invoke(this, $"Déconnexion : {ex.Message}");
            }
            finally
            {
                Disconnect();
            }
        }

        private void ProcessMessage(string message)
        {
            try
            {
                var json = JObject.Parse(message);
                int op = json["op"]?.Value<int>() ?? -1;
                var d = json["d"] as JObject;

                switch (op)
                {
                    case 0: // Hello
                        HandleHello(d);
                        break;

                    case 2: // Identified
                        SetState(ObsConnectionState.Connected);
                        _ = InitializeObsStateAsync();
                        break;

                    case 5: // Event
                        HandleEvent(d);
                        break;

                    case 7: // RequestResponse
                        HandleRequestResponse(d);
                        break;
                }
            }
            catch (Exception ex)
            {
                StatusMessage?.Invoke(this, $"Format de message invalide : {ex.Message}");
            }
        }

        private void HandleHello(JObject d)
        {
            string authResponse = null;
            var auth = d?["authentication"] as JObject;
            if (auth != null)
            {
                string challenge = auth["challenge"]?.Value<string>();
                string salt = auth["salt"]?.Value<string>();

                if (!string.IsNullOrEmpty(challenge) && !string.IsNullOrEmpty(salt))
                {
                    authResponse = ComputeAuthHash(Config.Password ?? "", salt, challenge);
                }
            }

            // Send Op 1 (Identify)
            var identifyPayload = new JObject
            {
                ["op"] = 1,
                ["d"] = new JObject
                {
                    ["rpcVersion"] = 1,
                    // Subscriptions: General(1) + Config(2) + Scenes(4) + Inputs(8) + Outputs(64)
                    ["eventSubscriptions"] = 1 | 2 | 4 | 8 | 64
                }
            };

            if (!string.IsNullOrEmpty(authResponse))
            {
                ((JObject)identifyPayload["d"])["authentication"] = authResponse;
            }

            _ = SendRawAsync(identifyPayload.ToString());
        }

        private string ComputeAuthHash(string password, string salt, string challenge)
        {
            using (var sha256 = SHA256.Create())
            {
                // Secret = base64(sha256(password + salt))
                byte[] secretBytes = Encoding.UTF8.GetBytes(password + salt);
                byte[] secretHash = sha256.ComputeHash(secretBytes);
                string secretBase64 = Convert.ToBase64String(secretHash);

                // Auth = base64(sha256(secretBase64 + challenge))
                byte[] authBytes = Encoding.UTF8.GetBytes(secretBase64 + challenge);
                byte[] authHash = sha256.ComputeHash(authBytes);
                return Convert.ToBase64String(authHash);
            }
        }

        private void HandleEvent(JObject d)
        {
            if (d == null) return;
            string eventType = d["eventType"]?.Value<string>();
            var eventData = d["eventData"] as JObject;

            switch (eventType)
            {
                case "RecordStateChanged":
                    {
                        bool active = eventData?["outputActive"]?.Value<bool>() ?? false;
                        string state = eventData?["outputState"]?.Value<string>() ?? "";
                        bool paused = state.Equals("OBS_WEBSOCKET_OUTPUT_PAUSED", StringComparison.OrdinalIgnoreCase);
                        RecordStateChanged?.Invoke(this, (active, paused, state));
                    }
                    break;

                case "StreamStateChanged":
                    {
                        bool active = eventData?["outputActive"]?.Value<bool>() ?? false;
                        string state = eventData?["outputState"]?.Value<string>() ?? "";
                        StreamStateChanged?.Invoke(this, (active, state));
                    }
                    break;

                case "VirtualcamStateChanged":
                    {
                        bool active = eventData?["outputActive"]?.Value<bool>() ?? false;
                        VirtualCamStateChanged?.Invoke(this, active);
                    }
                    break;

                case "ReplayBufferStateChanged":
                    {
                        bool active = eventData?["outputActive"]?.Value<bool>() ?? false;
                        ReplayBufferStateChanged?.Invoke(this, active);
                    }
                    break;

                case "ReplayBufferSaved":
                    {
                        string path = eventData?["savedReplayPath"]?.Value<string>() ?? "Clip sauvegardé";
                        ReplayBufferSaved?.Invoke(this, path);
                    }
                    break;

                case "CurrentProgramSceneChanged":
                    {
                        string scene = eventData?["sceneName"]?.Value<string>();
                        if (!string.IsNullOrEmpty(scene))
                        {
                            CurrentSceneChanged?.Invoke(this, scene);
                        }
                    }
                    break;

                case "SceneListChanged":
                    _ = RefreshSceneListAsync();
                    break;

                case "InputMuteStateChanged":
                    {
                        string inputName = eventData?["inputName"]?.Value<string>();
                        bool muted = eventData?["inputMuted"]?.Value<bool>() ?? false;
                        if (!string.IsNullOrEmpty(inputName))
                        {
                            InputMuteChanged?.Invoke(this, (inputName, muted));
                        }
                    }
                    break;
            }
        }

        private void HandleRequestResponse(JObject d)
        {
            if (d == null) return;
            string requestId = d["requestId"]?.Value<string>();
            if (!string.IsNullOrEmpty(requestId) && _pendingRequests.TryRemove(requestId, out var tcs))
            {
                tcs.TrySetResult(d);
            }
        }

        public async Task<JObject> SendRequestAsync(string requestType, JObject requestData = null)
        {
            if (_webSocket == null || _webSocket.State != WebSocketState.Open)
                throw new InvalidOperationException("Non connecté à OBS");

            string requestId = Guid.NewGuid().ToString("N");
            var tcs = new TaskCompletionSource<JObject>();
            _pendingRequests[requestId] = tcs;

            var payload = new JObject
            {
                ["op"] = 6,
                ["d"] = new JObject
                {
                    ["requestType"] = requestType,
                    ["requestId"] = requestId,
                    ["requestData"] = requestData ?? new JObject()
                }
            };

            await SendRawAsync(payload.ToString());

            // Timeout after 5 seconds
            var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(5000));
            if (completedTask == tcs.Task)
            {
                return await tcs.Task;
            }

            _pendingRequests.TryRemove(requestId, out _);
            throw new TimeoutException($"Délai d'attente dépassé pour la requête OBS : {requestType}");
        }

        private async Task SendRawAsync(string jsonString)
        {
            if (_webSocket == null || _webSocket.State != WebSocketState.Open)
                return;

            byte[] bytes = Encoding.UTF8.GetBytes(jsonString);
            await _webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        public async Task InitializeObsStateAsync()
        {
            try
            {
                // Record status
                var rec = await SendRequestAsync("GetRecordStatus");
                var recData = rec?["responseData"] as JObject;
                if (recData != null)
                {
                    bool active = recData["outputActive"]?.Value<bool>() ?? false;
                    bool paused = recData["outputPaused"]?.Value<bool>() ?? false;
                    RecordStateChanged?.Invoke(this, (active, paused, active ? "RECORDING" : "STOPPED"));
                }

                // Stream status
                var stream = await SendRequestAsync("GetStreamStatus");
                var streamData = stream?["responseData"] as JObject;
                if (streamData != null)
                {
                    bool active = streamData["outputActive"]?.Value<bool>() ?? false;
                    StreamStateChanged?.Invoke(this, (active, active ? "STREAMING" : "STOPPED"));
                }

                // Virtual cam status
                var vcam = await SendRequestAsync("GetVirtualCamStatus");
                var vcamData = vcam?["responseData"] as JObject;
                if (vcamData != null)
                {
                    bool active = vcamData["outputActive"]?.Value<bool>() ?? false;
                    VirtualCamStateChanged?.Invoke(this, active);
                }

                // Scenes
                await RefreshSceneListAsync();

                // Special audio inputs (Desktop Audio, Mic/Aux)
                await RefreshAudioInputsAsync();
            }
            catch (Exception ex)
            {
                StatusMessage?.Invoke(this, $"Initialisation OBS : {ex.Message}");
            }
        }

        public async Task RefreshSceneListAsync()
        {
            try
            {
                var res = await SendRequestAsync("GetSceneList");
                var resData = res?["responseData"] as JObject;
                if (resData != null)
                {
                    string currentProgram = resData["currentProgramSceneName"]?.Value<string>();
                    var scenes = resData["scenes"] as JArray;
                    var sceneNames = new List<string>();

                    if (scenes != null)
                    {
                        foreach (var sc in scenes)
                        {
                            string name = sc["sceneName"]?.Value<string>();
                            if (!string.IsNullOrEmpty(name))
                            {
                                sceneNames.Add(name);
                            }
                        }
                    }

                    SceneListChanged?.Invoke(this, sceneNames);
                    if (!string.IsNullOrEmpty(currentProgram))
                    {
                        CurrentSceneChanged?.Invoke(this, currentProgram);
                    }
                }
            }
            catch (Exception ex)
            {
                StatusMessage?.Invoke(this, $"Erreur liste des scènes : {ex.Message}");
            }
        }

        public async Task RefreshAudioInputsAsync()
        {
            try
            {
                var res = await SendRequestAsync("GetSpecialInputs");
                var resData = res?["responseData"] as JObject;
                if (resData != null)
                {
                    string[] keys = { "desktop1", "desktop2", "mic1", "mic2" };
                    foreach (var key in keys)
                    {
                        string inputName = resData[key]?.Value<string>();
                        if (!string.IsNullOrEmpty(inputName))
                        {
                            var muteRes = await SendRequestAsync("GetInputMute", new JObject { ["inputName"] = inputName });
                            var muteData = muteRes?["responseData"] as JObject;
                            bool isMuted = muteData?["inputMuted"]?.Value<bool>() ?? false;
                            InputMuteChanged?.Invoke(this, (inputName, isMuted));
                        }
                    }
                }
            }
            catch { }
        }

        // Quick Controls
        public Task ToggleRecordAsync() => SendRequestAsync("ToggleRecord");
        public Task ToggleRecordPauseAsync() => SendRequestAsync("ToggleRecordPause");
        public Task ToggleStreamAsync() => SendRequestAsync("ToggleStream");
        public Task ToggleVirtualCamAsync() => SendRequestAsync("ToggleVirtualCam");
        public Task SaveReplayBufferAsync() => SendRequestAsync("SaveReplayBuffer");
        public Task SetCurrentProgramSceneAsync(string sceneName) =>
            SendRequestAsync("SetCurrentProgramScene", new JObject { ["sceneName"] = sceneName });
        public Task ToggleInputMuteAsync(string inputName) =>
            SendRequestAsync("ToggleInputMute", new JObject { ["inputName"] = inputName });

        private void SetState(ObsConnectionState newState)
        {
            if (State != newState)
            {
                State = newState;
                ConnectionStateChanged?.Invoke(this, newState);
            }
        }

        public void Dispose()
        {
            Disconnect();
        }
    }
}
