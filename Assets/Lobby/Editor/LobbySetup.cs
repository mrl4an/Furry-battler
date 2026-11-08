using UnityEditor;
using UnityEditor.Localization;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

namespace AnimalBattler.Client.Editor
{
    public static class LobbySetup
    {
        private const string LocalizationSettingsPath = "Assets/Lobby/Localization/LocalizationSettings.asset";

        [InitializeOnLoadMethod]
        private static void InitializeLocalization()
        {
            EditorApplication.delayCall += EnsureSettingsAfterReload;
        }

        private static void EnsureSettingsAfterReload()
        {
            if (LocalizationEditorSettings.ActiveLocalizationSettings != null) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
                return;
            }
            EnsureLocalizationSettings();
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode) return;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EnsureLocalizationSettings();
        }

        private static void EnsureLocalizationSettings()
        {
            if (LocalizationEditorSettings.ActiveLocalizationSettings != null) return;
            var settings = AssetDatabase.LoadAssetAtPath<LocalizationSettings>(LocalizationSettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<LocalizationSettings>();
                settings.name = "Animal Battler Localization";
                AssetDatabase.CreateAsset(settings, LocalizationSettingsPath);
            }
            LocalizationEditorSettings.ActiveLocalizationSettings = settings;
            AssetDatabase.SaveAssets();
        }

        public static void BuildWebForVerification()
        {
            var output = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Builds", "WebGL");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled)
                    .Select(scene => scene.path).ToArray(),
                locationPathName = output,
                target = BuildTarget.WebGL,
                options = BuildOptions.Development
            });
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new System.Exception("Web build failed.");
            Debug.Log("Web build succeeded.");
        }

        [MenuItem("Animal Battler/Update Game Localization")]
        public static void UpdateGameLocalization()
        {
            EnsureLocalizationSettings();
            EnsureLocalization();
            AssetDatabase.SaveAssets();
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
            Add(english, russian, "connectionHeading", "Before the match", "Перед матчем");
            Add(english, russian, "playerPassHeading", "Player pass", "Пропуск игрока");
            Add(english, russian, "serverShortLabel", "Server", "Сервер");
            Add(english, russian, "nicknameLabel", "Nickname", "Никнейм");
            Add(english, russian, "nicknameHint", "A random name is ready. You can replace it with your own.",
                "Случайное имя уже выбрано. Можно заменить его своим.");
            Add(english, russian, "codeLabel", "Room code", "Код комнаты");
            Add(english, russian, "orLabel", "or", "или");
            Add(english, russian, "roomHeading", "Your room", "Ваша комната");
            Add(english, russian, "createButton", "Create room", "Создать комнату");
            Add(english, russian, "botButton", "Play with bot", "Играть с ботом");
            Add(english, russian, "botName", "Bot", "Бот");
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
            Add(english, russian, "ready", "Ready", "Готов");
            Add(english, russian, "cancelReady", "Cancel readiness", "Снять готовность");
            Add(english, russian, "readyStatus", "Ready", "Готов");
            Add(english, russian, "notReadyStatus", "Not ready", "Не готов");
            Add(english, russian, "setNickname", "Change nickname", "Изменить ник");
            Add(english, russian, "roomClosed", "Room closed", "Комната закрыта");
            Add(english, russian, "gameTitle", "Animal Battler", "Битва животных");
            Add(english, russian, "round", "Round {0}", "Раунд {0}");
            Add(english, russian, "timer", "Time: {0}s", "Время: {0} с");
            Add(english, russian, "playerSummary", "{0}: {1} HP · {2} coins ({3} available)",
                "{0}: {1} здоровья · {2} монет ({3} доступно)");
            Add(english, russian, "opponentSummary", "{0}: {1} HP", "{0}: {1} здоровья");
            Add(english, russian, "playerFallback", "Player", "Игрок");
            Add(english, russian, "auctionHeading", "Auction", "Аукцион");
            Add(english, russian, "auctionHint", "Enter 0 to remove a bid. Minimum bid: 2.",
                "Введите 0, чтобы снять ставку. Минимальная ставка: 2.");
            Add(english, russian, "opponentHandCount", "Opponent's hand: {0}", "Карт в руке соперника: {0}");
            Add(english, russian, "bidLabel", "Bid", "Ставка");
            Add(english, russian, "setBid", "Place bid", "Сделать ставку");
            Add(english, russian, "preparationHeading", "Preparation", "Подготовка");
            Add(english, russian, "auctionResultsHeading", "Auction results", "Итоги аукциона");
            Add(english, russian, "auctionResult", "{0}: {1}, bids {2}/{3}", "{0}: {1}, ставки {2}/{3}");
            Add(english, russian, "noWinner", "No winner", "Без победителя");
            Add(english, russian, "shopHeading", "Shop", "Магазин");
            Add(english, russian, "buy", "Buy · 3", "Купить · 3");
            Add(english, russian, "freeze", "Freeze", "Заморозить");
            Add(english, russian, "unfreeze", "Unfreeze", "Разморозить");
            Add(english, russian, "refreshShop", "Refresh shop · 1", "Обновить магазин · 1");
            Add(english, russian, "boardHeading", "Board", "Поле");
            Add(english, russian, "handHeading", "Hand", "Рука");
            Add(english, russian, "handPrevious", "<", "<");
            Add(english, russian, "handNext", ">", ">");
            Add(english, russian, "attackSlot", "Attack", "Атака");
            Add(english, russian, "supportSlot", "Support {0}", "Поддержка {0}");
            Add(english, russian, "defenseSlot", "Defense", "Защита");
            Add(english, russian, "sell", "Sell", "Продать");
            Add(english, russian, "dragHint", "Drag animals to board slots; drag a nut onto an ally to use it.", "Перетаскивайте животных в слоты поля, орех — на союзника для применения.");
            Add(english, russian, "opponentReady", "Opponent is ready", "Соперник готов");
            Add(english, russian, "opponentNotReady", "Opponent is not ready", "Соперник не готов");
            Add(english, russian, "battleHeading", "Battle", "Бой");
            Add(english, russian, "battleHint", "Battle plays automatically", "Бой проходит автоматически");
            Add(english, russian, "firstSide", "{0}", "{0}");
            Add(english, russian, "secondSide", "{0}", "{0}");
            Add(english, russian, "battleStats", "Attack {0} · HP {1}", "Атака {0} · здоровье {1}");
            Add(english, russian, "victory", "Victory", "Победа");
            Add(english, russian, "defeat", "Defeat", "Поражение");
            Add(english, russian, "draw", "Draw", "Ничья");
            Add(english, russian, "rematch", "Rematch", "Реванш");
            Add(english, russian, "rematchWaiting", "Waiting for opponent", "Ожидание соперника");
            Add(english, russian, "leaveAfterMatch", "Leave room", "Покинуть комнату");
            Add(english, russian, "emptySlot", "Empty", "Пусто");
            Add(english, russian, "artPlaceholder", "Artwork", "Иллюстрация");
            Add(english, russian, "tier", "Tier {0}", "Тир {0}");
            Add(english, russian, "cardStats", "{0} ATK · {1} HP", "{0} АТК · {1} ЗДР");
            Add(english, russian, "copies", "Copies: {0}", "Копий: {0}");
            Add(english, russian, "group.pack", "Pack", "Стая");
            Add(english, russian, "group.armored", "Armored", "Бронированные");
            Add(english, russian, "group.forest", "Forest", "Лесные");
            Add(english, russian, "group.feathered", "Feathered", "Пернатые");
            Add(english, russian, "group.venomous", "Venomous", "Ядовитые");
            Add(english, russian, "group.urban", "Urban", "Городские");
            Add(english, russian, "ability.playAttackBoth", "Play: gives the ally in attack +{0}/+{0}.", "Розыгрыш: даёт союзнику в атаке +{0}/+{0}.");
            Add(english, russian, "ability.playDefenseHealth", "Play: gives the ally in defense +{0} health.", "Розыгрыш: даёт союзнику в защите +{0} здоровья.");
            Add(english, russian, "ability.endSelfAttack", "End of preparation: gains +{0} attack.", "Конец подготовки: получает +{0} атаки.");
            Add(english, russian, "ability.endAttackHealth", "End of preparation: gives the ally in attack +{0} health.", "Конец подготовки: даёт союзнику в атаке +{0} здоровья.");
            Add(english, russian, "ability.endOtherGroupHealth", "End of preparation: gives other Armored allies +{0} health.", "Конец подготовки: даёт остальным Бронированным +{0} здоровья.");
            Add(english, russian, "ability.endNutGrowth", "End of preparation: permanently improves your nuts by +{0}/+{0}.", "Конец подготовки: навсегда усиливает ваши орехи на +{0}/+{0}.");
            Add(english, russian, "ability.startGiveNut", "Start of preparation: gives {0} nuts.", "Начало подготовки: даёт {0} орехов.");
            Add(english, russian, "ability.endGiveCache", "End of preparation: gives {0} nut caches.", "Конец подготовки: даёт {0} тайников с орехами.");
            Add(english, russian, "ability.startOpenCaches", "Every third preparation: opens all caches. Applies {0} nuts per cache to random allies. Progress: {5}/3.", "Каждая 3-я подготовка: вскрывает все тайники. За каждый применяет {0} орехов к случайным союзникам. Прогресс: {5}/3.");
            Add(english, russian, "ability.endRandomBuff", "End of preparation: gives a random other ally +{0} attack, +{1} health or +{2}/+{2} at random.", "Конец подготовки: даёт случайному другому союзнику +{0} атаки, +{1} здоровья или +{2}/+{2} случайным образом.");
            Add(english, russian, "ability.featheredModifier", "End of preparation: permanently improves your Feathered abilities by +{0} attack or health at random.", "Конец подготовки: навсегда усиливает способности ваших Пернатых на +{0} атаки или здоровья случайным образом.");
            Add(english, russian, "ability.repeatRavens", "End of preparation: each raven gives its bonus {0} extra times.", "Конец подготовки: каждый ворон выдаёт бонус ещё {0} раз.");
            Add(english, russian, "ability.directShield", "Reduces direct damage by {0}. Uses left: {3}.", "Снижает прямой урон на {0}. Осталось {3} раза.");
            Add(english, russian, "ability.poisonedAttack", "Reduces the opponent's attack by {0} for 1 turn. Attack: gains +{4} attack against a poisoned target.", "Снижает атаку противника на {0} на 1 ход. Атака: получает +{4} атаки против отравленной цели.");
            Add(english, russian, "ability.openingPoison", "Battle start: reduces all enemies' attack by {0} for 1 turn.", "Начало боя: снижает атаку всех противников на {0} на 1 ход.");
            Add(english, russian, "ability.playCoins", "Play: gives {0} coins.", "Розыгрыш: даёт {0} монет.");
            Add(english, russian, "ability.playPurchaseBuff", "Play: gives the ally in attack +{0}/+{0} per purchase this round, up to 5 purchases.", "Розыгрыш: даёт союзнику в атаке +{0}/+{0} за покупку в этом раунде. До 5 покупок.");
            Add(english, russian, "ability.endPurchaseBuff", "End of preparation: with 3+ purchases, gives other allies +{0}/+{0}.", "Конец подготовки: при 3+ покупках даёт остальным союзникам +{0}/+{0}.");
            Add(english, russian, "ability.endRefreshCoins", "End of preparation: with 3+ paid refreshes, gives {0} coins.", "Конец подготовки: при 3+ платных обновлениях даёт {0} монет.");
            Add(english, russian, "ability.applyNut", "Gives a chosen ally +{0}/+{0} permanently.", "Навсегда даёт выбранному союзнику +{0}/+{0}.");
            Add(english, russian, "ability.lockedCache", "Cannot be used manually or sold. A moose opens it and applies nuts.", "Нельзя применить вручную или продать. Лось вскрывает тайник и применяет орехи.");
            Add(english, russian, "card.hazelnut", "Hazelnut", "Лесной орех");
            Add(english, russian, "card.nutCache", "Nut cache", "Тайник с орехами");
            Add(english, russian, "spellCannotBePlaced", "Spells cannot be placed on the board.", "Заклинание нельзя выставить на поле.");
            Add(english, russian, "spellCannotBeSold", "Spells cannot be sold.", "Заклинание нельзя продать.");
            Add(english, russian, "spellCannotBeCast", "This card cannot be cast manually.", "Эту карту нельзя применить вручную.");
            Add(english, russian, "invalidSpellTarget", "Choose an ally on your board.", "Выберите союзника на своём поле.");
            Add(english, russian, "ability.attackOnOwnTurn", "Attack: gains +{0} attack for this hit.", "Атака: получает +{0} атаки на этот удар.");
            Add(english, russian, "ability.attackBeforeFirstTurn", "+{0} attack before first turn", "+{0} к атаке перед первым ходом");
            Add(english, russian, "ability.buffOtherGroupAttack", "+{0} attack to other allies", "+{0} к атаке другим союзникам");
            Add(english, russian, "ability.exchangeDamage", "After attack: deals {0} extra damage.", "После атаки: наносит противнику {0} дополнительного урона.");
            Add(english, russian, "ability.firstDirectShield", "Blocks {0} first direct damage", "Блокирует {0} первого прямого урона");
            Add(english, russian, "ability.buffOtherGroupHealth", "+{0} health to other allies", "+{0} здоровья другим союзникам");
            Add(english, russian, "ability.buffPreviousBoth", "+{0} attack and health to previous slot", "+{0} атаки и здоровья предыдущему слоту");
            Add(english, russian, "ability.buffDefenseHealth", "+{0} health to defense slot", "+{0} здоровья защитному слоту");
            Add(english, russian, "ability.buffAttackSlotAttack", "+{0} attack to attack slot", "+{0} атаки атакующему слоту");
            Add(english, russian, "ability.poisonOnExchange", "Reduces the opponent's attack by {0} for 1 turn.", "Снижает атаку противника на {0} на 1 ход.");
            Add(english, russian, "card.dog", "Dog", "Собака");
            Add(english, russian, "card.jackal", "Jackal", "Шакал");
            Add(english, russian, "card.coyote", "Coyote", "Койот");
            Add(english, russian, "card.wolf", "Wolf", "Волк");
            Add(english, russian, "card.turtle", "Turtle", "Черепаха");
            Add(english, russian, "card.porcupine", "Porcupine", "Дикобраз");
            Add(english, russian, "card.armadillo", "Armadillo", "Броненосец");
            Add(english, russian, "card.pangolin", "Pangolin", "Панголин");
            Add(english, russian, "card.hare", "Hare", "Заяц");
            Add(english, russian, "card.squirrel", "Squirrel", "Белка");
            Add(english, russian, "card.deer", "Deer", "Олень");
            Add(english, russian, "card.moose", "Moose", "Лось");
            Add(english, russian, "card.raven", "Raven", "Ворон");
            Add(english, russian, "card.owl", "Owl", "Сова");
            Add(english, russian, "card.hawk", "Hawk", "Ястреб");
            Add(english, russian, "card.eagle", "Eagle", "Орёл");
            Add(english, russian, "card.spider", "Spider", "Паук");
            Add(english, russian, "card.poisonFrog", "Poison frog", "Ядовитая лягушка");
            Add(english, russian, "card.scorpion", "Scorpion", "Скорпион");
            Add(english, russian, "card.viper", "Viper", "Гадюка");
            Add(english, russian, "card.pigeon", "Pigeon", "Голубь");
            Add(english, russian, "card.strayCat", "Stray cat", "Бездомный кот");
            Add(english, russian, "card.rat", "Rat", "Крыса");
            Add(english, russian, "card.raccoon", "Raccoon", "Енот");
            Add(english, russian, "wrongPhase", "Action unavailable now", "Действие сейчас недоступно");
            Add(english, russian, "invalidNickname", "Invalid nickname", "Некорректный ник");
            Add(english, russian, "notEnoughCoins", "Not enough coins", "Недостаточно монет");
            Add(english, russian, "invalidBid", "Invalid bid", "Некорректная ставка");
            Add(english, russian, "invalidShopSlot", "Invalid shop slot", "Некорректный слот магазина");
            Add(english, russian, "invalidBoardSlot", "Invalid board slot", "Некорректный слот поля");
            Add(english, russian, "cardNotFound", "Card not found", "Карта не найдена");
            Add(english, russian, "invalidMerge", "Cards cannot be merged", "Карты нельзя объединить");
            Add(english, russian, "notInMatch", "Player is not in match", "Игрок не участвует в матче");
            Add(english, russian, "playerReady", "Cancel readiness before acting", "Снимите готовность перед действием");
            Add(english, russian, "synergy.scope", "Combat bonuses affect this class. Thresholds: 3 / 6 / 10 field copies.", "Боевые бонусы — своему классу. Пороги: 3 / 6 / 10 копий на поле.");
            Add(english, russian, "synergy.short.pack", "Attack +1 / +2 / +4", "Атака +1 / +2 / +4");
            Add(english, russian, "synergy.short.armored", "Health +2 / +4 / +7", "Здоровье +2 / +4 / +7");
            Add(english, russian, "synergy.short.forest", "Attack & health +1 / +2 / +3", "Атака и здоровье +1 / +2 / +3");
            Add(english, russian, "synergy.short.feathered", "Own-turn attack +2 / +4 / +7", "Атака в свой ход +2 / +4 / +7");
            Add(english, russian, "synergy.short.venomous", "Extra poison: −1 / −2 / −3 attack", "Яд: ещё −1 / −2 / −3 к атаке");
            Add(english, russian, "synergy.short.urban", "Next-round income +1 / +2 / +3", "Доход со следующего раунда +1 / +2 / +3");
            Add(english, russian, "match.refresh", "Refresh", "Обновить");
            Add(english, russian, "match.available", "Available", "Доступно");
            Add(english, russian, "ui.help", "?", "?");
            Add(english, russian, "synergy.helpRules", "Only field copies count. Only the highest reached threshold applies; bonuses do not stack.", "Считаются только копии на поле. Действует наибольший достигнутый порог; бонусы не складываются.");
            Add(english, russian, "auction.stepHint", "Click the tray: 2 coins to start, then +1. The arrow returns your bid.", "Нажмите на площадку: сначала 2 монеты, затем +1. Стрелка возвращает ставку.");
            Add(english, russian, "sale.dropHint", "Drop a card here", "Перетащите карту сюда");
            Add(english, russian, "result.heading", "Match complete", "Матч завершён");
            Add(english, russian, "result.you", "You", "Вы");
            Add(english, russian, "auction.placeBid", "Place 2 coins, then add 1 per click", "Поставить 2 монеты, затем добавлять по 1");
            Add(english, russian, "auction.refundBid", "Return the entire bid", "Вернуть всю ставку");
            Add(english, russian, "auction.revealTitle", "Auction results", "Итоги торгов");
            Add(english, russian, "auction.revealHint", "Bids revealed", "Ставки открыты");
            Add(english, russian, "auction.revealTimerHint", "Preparation timer is running", "Время подготовки уже идёт");
            Add(english, russian, "auction.revealBusy", "Awarding", "Выдача карт");
            Add(english, russian, "auction.noBid", "—", "—");
            Add(english, russian, "auction.unclaimed", "No bids", "Без ставок");
            Add(english, russian, "auction.tied", "Tie", "Жребий");
            Add(english, russian, "auction.youWon", "Your card", "Ваша карта");
            Add(english, russian, "auction.opponentWon", "Opponent's card", "Карта соперника");
            Add(english, russian, "result.opponent", "Opponent", "Соперник");
            Add(english, russian, "ui.statDelta", "{0:+0;−0;0}", "{0:+0;−0;0}");
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
