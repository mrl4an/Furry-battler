using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Globalization;
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
            _createButton.clicked += () => StartConnection(true);
            _joinButton.clicked += () => StartConnection(false);
            _copyButton.clicked += CopyInvite;
            _leaveButton.clicked += LeaveRoom;
            _languageButton.clicked += SwitchLanguage;

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
                    var count = 0;
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await _socket.ReceiveAsync(new ArraySegment<byte>(buffer, count, buffer.Length - count), CancellationToken.None);
                        if (result.MessageType == WebSocketMessageType.Close) break;
                        count += result.Count;
                    } while (!result.EndOfMessage && count < buffer.Length);
                    if (result.MessageType == WebSocketMessageType.Close) break;
                    if (!result.EndOfMessage) throw new WebSocketException();
                    var message = Encoding.UTF8.GetString(buffer, 0, count);
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

#if !UNITY_WEBGL || UNITY_EDITOR
        private async Task SendNativeAsync(string message)
        {
            try
            {
                var bytes = Encoding.UTF8.GetBytes(message);
                await _socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
            }
            catch (Exception)
            {
                _mainThreadActions.Enqueue(() => OnSocketError("connectionFailed"));
            }
        }
#endif

        public void OnSocketMessage(string json)
        {
            var message = JsonUtility.FromJson<ServerMessage>(json);
            if (message.type == "error")
            {
                SetStatus(message.code);
                _intentionalClose = true;
                CloseSocket();
                OnSocketClosed();
                return;
            }
            if (message.type != "roomState") return;

            _roomCode = message.roomCode;
            _playerCount = message.playerCount;
            _connecting = false;
            _entry.AddToClassList("hidden");
            _room.RemoveFromClassList("hidden");
            SetStatus(_playerCount == 2 ? "roomFullStatus" : "waitingForPlayer");
            RefreshText();
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
            _playerCount = 0;
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
        [Serializable] private sealed class ServerMessage
        {
            public string type;
            public string code;
            public string roomCode;
            public int playerCount;
        }
    }
}
