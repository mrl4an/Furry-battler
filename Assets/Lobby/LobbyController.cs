using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Net.WebSockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.UIElements;

namespace AnimalBattler.Client.Lobby
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class LobbyController : MonoBehaviour
    {
        private const string TableName = "Lobby";
        [SerializeField] private string configuredServerUrl;
        private readonly ConcurrentQueue<Action> _mainThreadActions = new ConcurrentQueue<Action>();
        private readonly SemaphoreSlim _sendGate = new SemaphoreSlim(1, 1);
        private UIDocument _document;
        private TextField _serverUrl;
        private TextField _roomCodeInput;
        private Label _status;
        private Label _roomCodeDisplay;
        private Label _players;
        private VisualElement _entry;
        private VisualElement _room;
        private Button _createButton;
        private Button _joinButton;
        private Button _copyButton;
        private Button _leaveButton;
        private Button _languageButton;
        private Button _roomReadyButton;
        private Button _nicknameButton;
        private TextField _nicknameInput;
        private Label _roomPlayers;
        private GameScreen _gameScreen;
        private string _playerId;
        private long _latestRevision;
        private bool _roomReady;
        private RoomPlayer[] _roomPlayersSnapshot;
        private string _matchOpponentId;
        private string _roomCode;
        private string _statusKey = "statusDisconnected";
        private int _playerCount;
        private bool _connecting;
        private bool _intentionalClose;
        private bool _createRequested;
        private string _requestedCode;
        private StringTable _strings;
#if !UNITY_WEBGL || UNITY_EDITOR
        private ClientWebSocket _socket;
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void AnimalBattlerSocketConnect(string url, string objectName);
        [DllImport("__Internal")] private static extern void AnimalBattlerSocketSend(string message);
        [DllImport("__Internal")] private static extern void AnimalBattlerSocketClose();
        [DllImport("__Internal")] private static extern string AnimalBattlerGetPageUrl();
        [DllImport("__Internal")] private static extern void AnimalBattlerCopyText(string text);
#endif

        private void Awake()
        {
            _document = GetComponent<UIDocument>();
            var root = _document.rootVisualElement;
            _serverUrl = root.Q<TextField>("server-url");
            _roomCodeInput = root.Q<TextField>("room-code");
            _status = root.Q<Label>("status");
            _roomCodeDisplay = root.Q<Label>("room-code-display");
            _players = root.Q<Label>("players");
            _entry = root.Q<VisualElement>("entry");
            _room = root.Q<VisualElement>("room");
            _createButton = root.Q<Button>("create-button");
            _joinButton = root.Q<Button>("join-button");
            _copyButton = root.Q<Button>("copy-button");
            _leaveButton = root.Q<Button>("leave-button");
            _languageButton = root.Q<Button>("language-button");
            _roomReadyButton = root.Q<Button>("room-ready-button");
            _nicknameButton = root.Q<Button>("nickname-button");
            _nicknameInput = root.Q<TextField>("nickname-input");
            _roomPlayers = root.Q<Label>("room-players");
            _gameScreen = new GameScreen(root, SendGameRequest, Localized);
            _createButton.clicked += () => StartConnection(true);
            _joinButton.clicked += () => StartConnection(false);
            _copyButton.clicked += CopyInvite;
            _leaveButton.clicked += LeaveRoom;
            _languageButton.clicked += SwitchLanguage;
            _roomReadyButton.clicked += () => SendGameRequest(new GameRequest { type = "roomReady", ready = !_roomReady });
            _nicknameButton.clicked += () => SendGameRequest(new GameRequest
            {
                type = "setNickname", nickname = _nicknameInput.value
            });

#if UNITY_WEBGL && !UNITY_EDITOR
            var pageUrl = AnimalBattlerGetPageUrl();
            if (Uri.TryCreate(pageUrl, UriKind.Absolute, out var page))
            {
                _serverUrl.value = string.IsNullOrWhiteSpace(configuredServerUrl)
                    ? new UriBuilder(page) { Scheme = page.Scheme == "https" ? "wss" : "ws", Path = "/ws", Query = "", Fragment = "" }.Uri.ToString()
                    : configuredServerUrl;
                var code = GetRoomCodeFromUrl(page);
                if (!string.IsNullOrEmpty(code)) _roomCodeInput.value = code;
            }
#else
            _serverUrl.value = configuredServerUrl;
#endif
        }

        private IEnumerator Start()
        {
            yield return LocalizationSettings.InitializationOperation;
            LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
            yield return LoadStrings();
            if (!string.IsNullOrWhiteSpace(_roomCodeInput.value)) StartConnection(false);
        }

        private void Update()
        {
            while (_mainThreadActions.TryDequeue(out var action)) action();
            _gameScreen?.Tick();
        }

        private void OnDestroy()
        {
            LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
            CloseSocket();
        }

        private void OnLocaleChanged(Locale _) => StartCoroutine(LoadStrings());

        private IEnumerator LoadStrings()
        {
            var handle = LocalizationSettings.StringDatabase.GetTableAsync(TableName);
            yield return handle;
            _strings = handle.Result;
            RefreshText();
        }

        private string Localized(string key) => _strings?.GetEntry(key)?.LocalizedValue ?? key;

        private void RefreshText()
        {
            var root = _document.rootVisualElement;
            root.Q<Label>("title").text = Localized("title");
            root.Q<Label>("subtitle").text = Localized("subtitle");
            root.Q<Label>("server-label").text = Localized("serverLabel");
            root.Q<Label>("code-label").text = Localized("codeLabel");
            root.Q<Label>("or-label").text = Localized("orLabel");
            root.Q<Label>("room-heading").text = Localized("roomHeading");
            _createButton.text = Localized("createButton");
            _joinButton.text = Localized("joinButton");
            _copyButton.text = Localized("copyButton");
            _leaveButton.text = Localized("leaveButton");
            _languageButton.text = LocalizationSettings.SelectedLocale?.Identifier.Code == "ru" ? "EN" : "RU";
            _roomCodeDisplay.text = _roomCode;
            var culture = LocalizationSettings.SelectedLocale?.Identifier.CultureInfo ?? CultureInfo.InvariantCulture;
            _players.text = string.Format(culture, Localized("playerCount"), _playerCount);
            _status.text = Localized(_statusKey);
            _roomReadyButton.text = Localized(_roomReady ? "cancelReady" : "ready");
            _nicknameButton.text = Localized("setNickname");
            UpdateRoomPlayers();
            _gameScreen.Refresh();
        }

        private void SwitchLanguage()
        {
            var code = LocalizationSettings.SelectedLocale?.Identifier.Code == "ru" ? "en" : "ru";
            LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.GetLocale(code);
        }

        private void SetStatus(string key)
        {
            _statusKey = key;
            _status.text = Localized(key);
        }

        private void StartConnection(bool create)
        {
            if (_connecting || !string.IsNullOrEmpty(_roomCode)) return;
            if (!Uri.TryCreate(_serverUrl.value.Trim(), UriKind.Absolute, out var endpoint)
                || (endpoint.Scheme != "ws" && endpoint.Scheme != "wss"))
            {
                SetStatus("invalidServerUrl");
                return;
            }

            var code = _roomCodeInput.value.Trim().ToUpperInvariant();
            if (!create && code.Length != 8)
            {
                SetStatus("invalidRoomCode");
                return;
            }

            _createRequested = create;
            _requestedCode = code;
            _connecting = true;
            _intentionalClose = false;
            _createButton.SetEnabled(false);
            _joinButton.SetEnabled(false);
            SetStatus("connecting");
#if UNITY_WEBGL && !UNITY_EDITOR
            AnimalBattlerSocketConnect(endpoint.ToString(), gameObject.name);
#else
            _ = ConnectNativeAsync(endpoint);
#endif
        }

#if !UNITY_WEBGL || UNITY_EDITOR
        private async Task ConnectNativeAsync(Uri endpoint)
        {
            try
            {
                _socket = new ClientWebSocket();
                await _socket.ConnectAsync(endpoint, CancellationToken.None);
                _mainThreadActions.Enqueue(OnSocketOpened);
                var buffer = new byte[4096];
                while (_socket.State == WebSocketState.Open)
                {
                    using var messageBuffer = new MemoryStream();
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await _socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                        if (result.MessageType == WebSocketMessageType.Close) break;
                        messageBuffer.Write(buffer, 0, result.Count);
                        if (messageBuffer.Length > 1024 * 1024) throw new WebSocketException();
                    } while (!result.EndOfMessage);
                    if (result.MessageType == WebSocketMessageType.Close) break;
                    var message = Encoding.UTF8.GetString(messageBuffer.ToArray());
                    _mainThreadActions.Enqueue(() => OnSocketMessage(message));
                }
            }
            catch (Exception)
            {
                _mainThreadActions.Enqueue(() => OnSocketError("connectionFailed"));
            }
            finally
            {
                _mainThreadActions.Enqueue(OnSocketClosed);
            }
        }
#endif

        public void OnSocketOpened()
        {
            var request = _createRequested ? "{\"type\":\"createRoom\"}"
                : JsonUtility.ToJson(new JoinRequest { type = "joinRoom", roomCode = _requestedCode });
#if UNITY_WEBGL && !UNITY_EDITOR
            AnimalBattlerSocketSend(request);
#else
            _ = SendNativeAsync(request);
#endif
        }

        private void SendGameRequest(GameRequest request)
        {
            if (_connecting || string.IsNullOrEmpty(_roomCode)) return;
            SendSocketMessage(JsonUtility.ToJson(request));
        }

        private void SendSocketMessage(string request)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            AnimalBattlerSocketSend(request);
#else
            _ = SendNativeAsync(request);
#endif
        }

#if !UNITY_WEBGL || UNITY_EDITOR
        private async Task SendNativeAsync(string message)
        {
            await _sendGate.WaitAsync();
            try
            {
                var bytes = Encoding.UTF8.GetBytes(message);
                await _socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
            }
            catch (Exception)
            {
                _mainThreadActions.Enqueue(() => OnSocketError("connectionFailed"));
            }
            finally { _sendGate.Release(); }
        }
#endif

        public void OnSocketMessage(string json)
        {
            var message = JsonUtility.FromJson<ServerMessage>(json);
            if (message == null) return;
            if (message.type == "error")
            {
                SetStatus(message.code);
                _gameScreen.ShowError(message.code);
                if (string.IsNullOrEmpty(_roomCode))
                {
                    _intentionalClose = true;
                    CloseSocket();
                    OnSocketClosed();
                }
                return;
            }
            if (message.type == "roomClosed")
            {
                _intentionalClose = true;
                CloseSocket();
                OnSocketClosed();
                SetStatus("roomClosed");
                return;
            }
            if (message.type == "catalog")
            {
                _gameScreen.SetCatalog(message.cards);
                return;
            }
            if (message.type != "roomState" && message.type != "matchState") return;
            if (message.revision < _latestRevision) return;
            _latestRevision = message.revision;

            if (message.type == "matchState")
            {
                _matchOpponentId = message.opponent?.playerId;
                _entry.AddToClassList("hidden");
                _room.AddToClassList("hidden");
                _gameScreen.SetMatch(message);
                return;
            }

            _roomCode = message.roomCode;
            _playerId = message.playerId;
            _playerCount = message.playerCount;
            _connecting = false;
            _roomPlayersSnapshot = message.players;
            if (!string.IsNullOrEmpty(_matchOpponentId) && message.players != null
                && message.players.Length == 2
                && !Array.Exists(message.players, player => player.playerId == _matchOpponentId))
            {
                _matchOpponentId = null;
                _gameScreen.Hide();
            }
            _gameScreen.SetPlayers(message.players);
            UpdateRoomPlayers();
            _roomReadyButton.SetEnabled(_playerCount == 2);
            _roomReadyButton.text = Localized(_roomReady ? "cancelReady" : "ready");
            _entry.AddToClassList("hidden");
            _room.RemoveFromClassList("hidden");
            SetStatus(_playerCount == 2 ? "roomFullStatus" : "waitingForPlayer");
            RefreshText();
            SendSocketMessage("{\"type\":\"getCatalog\"}");
        }

        public void OnSocketError(string _)
        {
            _intentionalClose = true;
            SetStatus("connectionFailed");
        }

        public void OnSocketClosed()
        {
            _connecting = false;
            _roomCode = null;
            _playerId = null;
            _matchOpponentId = null;
            _roomPlayersSnapshot = null;
            _latestRevision = 0;
            _roomReady = false;
            _playerCount = 0;
            _gameScreen.Hide();
            _entry.RemoveFromClassList("hidden");
            _room.AddToClassList("hidden");
            _createButton.SetEnabled(true);
            _joinButton.SetEnabled(true);
            if (!_intentionalClose && _statusKey != "connectionFailed") SetStatus("statusDisconnected");
            RefreshText();
        }

        private void LeaveRoom()
        {
            _intentionalClose = true;
            CloseSocket();
            OnSocketClosed();
            SetStatus("statusDisconnected");
        }

        private void UpdateRoomPlayers()
        {
            _roomReady = false;
            var names = new StringBuilder();
            foreach (var player in _roomPlayersSnapshot ?? Array.Empty<RoomPlayer>())
            {
                if (names.Length > 0) names.Append("\n");
                names.Append(player.nickname);
                names.Append(" — ");
                names.Append(Localized(player.ready ? "readyStatus" : "notReadyStatus"));
                if (player.playerId != _playerId) continue;
                _roomReady = player.ready;
                if (_nicknameInput.focusController?.focusedElement != _nicknameInput)
                    _nicknameInput.SetValueWithoutNotify(player.nickname);
            }
            _roomPlayers.text = names.ToString();
            _roomReadyButton.text = Localized(_roomReady ? "cancelReady" : "ready");
        }

        private void CloseSocket()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            AnimalBattlerSocketClose();
#else
            _socket?.Dispose();
            _socket = null;
#endif
        }

        private void CopyInvite()
        {
            if (string.IsNullOrEmpty(_roomCode)) return;
#if UNITY_WEBGL && !UNITY_EDITOR
            var page = new Uri(AnimalBattlerGetPageUrl());
            var builder = new UriBuilder(page) { Query = "room=" + _roomCode, Fragment = "" };
            AnimalBattlerCopyText(builder.Uri.ToString());
#else
            GUIUtility.systemCopyBuffer = _roomCode;
#endif
            SetStatus("inviteCopied");
        }

        private static string GetRoomCodeFromUrl(Uri page)
        {
            var query = page.Query.TrimStart('?').Split('&');
            foreach (var item in query)
            {
                var pair = item.Split('=');
                if (pair.Length == 2 && pair[0] == "room") return Uri.UnescapeDataString(pair[1]).ToUpperInvariant();
            }
            return null;
        }

        [Serializable] private sealed class JoinRequest { public string type; public string roomCode; }
    }
}
