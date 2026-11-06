using AnimalBattler.Client.Lobby;
using UnityEditor;
using UnityEditor.Localization;
using UnityEditor.SceneManagement;
using System.IO;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;
using UnityEngine.UIElements;

namespace AnimalBattler.Client.Editor
{
    public static class LobbySetup
    {
        private const string LobbyScenePath = "Assets/Scenes/Lobby.unity";

        [MenuItem("Animal Battler/Setup Lobby")]
        public static void Create()
        {
            EnsureLocalization();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/Lobby/LobbyPanel.asset");
            if (panel == null)
            {
                panel = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(panel, "Assets/Lobby/LobbyPanel.asset");
            }

            var root = new GameObject("LobbyUI");
            var document = root.AddComponent<UIDocument>();
            document.panelSettings = panel;
            document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Lobby/LobbyView.uxml");
            root.AddComponent<LobbyController>();
            EditorSceneManager.SaveScene(scene, LobbyScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(LobbyScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("Lobby scene and localization are ready.");
        }

        public static void BuildWebForVerification()
        {
            var output = Path.Combine(Path.GetTempPath(), "AnimalBattlerWebBuild");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { LobbyScenePath },
                locationPathName = output,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            });
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new System.Exception("Web build failed.");
            Debug.Log("Web build succeeded.");
        }

        private static void EnsureLocalization()
        {
            const string directory = "Assets/Lobby/Localization";
            if (!AssetDatabase.IsValidFolder(directory))
                AssetDatabase.CreateFolder("Assets/Lobby", "Localization");

            EnsureLocale(SystemLanguage.English, "en", directory);
            EnsureLocale(SystemLanguage.Russian, "ru", directory);

            var collection = LocalizationEditorSettings.GetStringTableCollection("Lobby")
                ?? LocalizationEditorSettings.CreateStringTableCollection("Lobby", directory);
            var english = (StringTable)collection.GetTable("en");
            var russian = (StringTable)collection.GetTable("ru");

            Add(english, russian, "title", "Animal Battler", "Битва животных");
            Add(english, russian, "subtitle", "Create a room or join a friend", "Создайте комнату или присоединитесь к другу");
            Add(english, russian, "serverLabel", "Server WebSocket address", "Адрес WebSocket-сервера");
            Add(english, russian, "codeLabel", "Room code", "Код комнаты");
            Add(english, russian, "orLabel", "or", "или");
            Add(english, russian, "roomHeading", "Your room", "Ваша комната");
            Add(english, russian, "createButton", "Create room", "Создать комнату");
            Add(english, russian, "joinButton", "Join room", "Войти в комнату");
            Add(english, russian, "copyButton", "Copy invite", "Копировать приглашение");
            Add(english, russian, "leaveButton", "Leave room", "Покинуть комнату");
            Add(english, russian, "playerCount", "Players: {0}/2", "Игроки: {0}/2");
            Add(english, russian, "statusDisconnected", "Disconnected", "Нет подключения");
            Add(english, russian, "connecting", "Connecting...", "Подключение...");
            Add(english, russian, "waitingForPlayer", "Waiting for another player", "Ожидание второго игрока");
            Add(english, russian, "roomFullStatus", "Both players are in the room", "Оба игрока в комнате");
            Add(english, russian, "invalidServerUrl", "Enter a ws:// or wss:// address", "Введите адрес ws:// или wss://");
            Add(english, russian, "invalidRoomCode", "Enter an 8-character room code", "Введите код комнаты из 8 символов");
            Add(english, russian, "connectionFailed", "Connection failed", "Не удалось подключиться");
            Add(english, russian, "inviteCopied", "Invite copied", "Приглашение скопировано");
            Add(english, russian, "roomNotFound", "Room not found", "Комната не найдена");
            Add(english, russian, "roomFull", "Room is full", "Комната заполнена");
            Add(english, russian, "invalidMessage", "Invalid request", "Некорректный запрос");
            Add(english, russian, "alreadyInRoom", "Already in a room", "Вы уже в комнате");
            EditorUtility.SetDirty(english);
            EditorUtility.SetDirty(russian);
            EditorUtility.SetDirty(english.SharedData);
        }

        private static void EnsureLocale(SystemLanguage language, string code, string directory)
        {
            foreach (var locale in LocalizationEditorSettings.GetLocales())
                if (locale.Identifier.Code == code) return;

            var created = Locale.CreateLocale(language);
            AssetDatabase.CreateAsset(created, $"{directory}/{code}.asset");
            LocalizationEditorSettings.AddLocale(created);
        }

        private static void Add(StringTable english, StringTable russian, string key, string en, string ru)
        {
            if (english.GetEntry(key) == null) english.AddEntry(key, en);
            if (russian.GetEntry(key) == null) russian.AddEntry(key, ru);
        }
    }
}
