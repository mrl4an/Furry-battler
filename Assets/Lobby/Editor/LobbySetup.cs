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
            Add(english, russian, "card.mergeLevel", "LVL{0}", "LVL{0}");
            Add(english, russian, "tier", "Tier {0}", "Тир {0}");
            Add(english, russian, "cardStats", "{0} ATK · {1} HP", "{0} АТК · {1} ЗДР");
            Add(english, russian, "copies", "Copies: {0}", "Копий: {0}");
            Add(english, russian, "group.pack", "Pack", "Стая");
            Add(english, russian, "group.armored", "Armored", "Бронированные");
            Add(english, russian, "group.forest", "Forest", "Лесные");
            Add(english, russian, "group.feathered", "Feathered", "Пернатые");
            Add(english, russian, "group.venomous", "Venomous", "Ядовитые");
            Add(english, russian, "group.urban", "Urban", "Городские");
            Add(english, russian, "card.packCub", "Pack cub", "Детёныш стаи");
            Add(english, russian, "ability.playGiveCub", "Play: adds a pack cub to your hand.", "Розыгрыш: добавляет в руку детёныша стаи.");
            Add(english, russian, "ability.playCubGrowth", "Play: permanently improves your future pack cubs by +{0}/+{0}, including cubs in hand.", "Розыгрыш: навсегда усиливает будущих детёнышей стаи на +{0}/+{0}, включая детёнышей в руке.");
            Add(english, russian, "ability.playGiveNut", "Play: gives {0} nut and permanently improves all nuts by +{4}/+0.", "Розыгрыш: даёт {0} орех и навсегда усиливает все орехи на +{4}/+0.");
            Add(english, russian, "ability.endGroupAttack", "End of preparation: gives all allies of its class +{0} attack.", "Конец подготовки: всем союзникам своего класса +{0} атаки.");
            Add(english, russian, "ability.endAllHealth", "End of preparation: gives all allies +{0} health.", "Конец подготовки: всем союзникам +{0} здоровья.");
            Add(english, russian, "ability.reflectDamage", "When attacked: reflects received direct damage to the attacker, even if killed. Uses left: {3}.", "Когда атакован: отражает полученный прямой урон атакующему, даже при гибели. Осталось: {3}.");
            Add(english, russian, "ability.endOtherGroupStats", "End of preparation: other allies of its class gain +{4} attack and +{0} health.", "Конец подготовки: остальным союзникам своего класса +{4} атаки и +{0} здоровья.");
            Add(english, russian, "ability.repeatEndEffects", "All end-of-preparation effects trigger {0} extra times.", "Все эффекты конца подготовки срабатывают ещё {0} раз.");
            Add(english, russian, "ability.attackPoison", "Attack: applies {0} poison until battle ends. Poison stacks and reduces attack.", "Атака: накладывает {0} яда до конца боя. Яд складывается и снижает атаку.");
            Add(english, russian, "ability.openingPoisonDamage", "Battle start: up to {3} different random enemies receive {0} poison and {4} damage.", "Начало боя: до {3} разных случайных противников получают {0} яда и {4} урона.");
            Add(english, russian, "ability.growingAttackPoison", "Attack: applies {0} poison. After its attack: poison strength grows by {6} for this battle.", "Атака: накладывает {0} яда. После своей атаки: сила яда растёт на {6} до конца боя.");
            Add(english, russian, "ability.otherPlayBuff", "When another ally is played: gives a random ally +{0}/+{0}.", "Розыгрыш другого союзника: случайному союзнику +{0}/+{0}.");
            Add(english, russian, "ability.endPlayedBuff", "End of preparation: a random ally gains +{0}/+{0} per creature played this round after this creature entered the board. Count: {7}.", "Конец подготовки: случайному союзнику +{0}/+{0} за существо, разыгранное в этом раунде после появления кота. Счётчик: {7}.");
            Add(english, russian, "ability.attackNextRoundCoins", "Attack: gives {0} coins at the start of the next round.", "Атака: даёт {0} монет в начале следующего раунда.");
            Add(english, russian, "synergy.pack", "3/7/12 Pack copies: at preparation start, receive 1/2/3 pack cubs. Cub plays improve future cubs.", "3/7/12 копий Стаи: в начале подготовки получаете 1/2/3 детёныша стаи. Розыгрыши детёнышей усиливают будущих.");
            Add(english, russian, "synergy.armored", "3/7/12 Armored copies: at preparation end, all Armored allies permanently gain +2/+4/+9 health.", "3/7/12 копий Бронированных: в конце подготовки все Бронированные навсегда получают +2/+4/+9 здоровья.");
            Add(english, russian, "synergy.forest", "3/7/12 Forest copies: at preparation end, each cache gains 1/2/3 opening progress. At 3, it is consumed and applies one nut to every ally.", "3/7/12 копий Лесных: в конце подготовки каждый тайник получает 1/2/3 прогресса открытия. При 3 расходуется и применяет по ореху ко всем союзникам.");
            Add(english, russian, "synergy.feathered", "3/7/12 Feathered copies: every own attack adds +2/+4/+7 attack until battle ends. Counters do not trigger the increase.", "3/7/12 копий Пернатых: каждая собственная атака добавляет +2/+4/+7 атаки до конца боя. Ответные удары не запускают прибавку.");
            Add(english, russian, "synergy.venomous", "3/7/12 Venomous copies: each poison application gains +1/+3/+8 strength. Poison stacks until battle ends.", "3/7/12 копий Ядовитых: каждое наложение яда усиливается на +1/+3/+8. Яд складывается до конца боя.");
            Add(english, russian, "synergy.urban", "3/7/12 Urban copies: receive +2/+5/+10 coins before the next round's auction.", "3/7/12 копий Городских: получаете +2/+5/+10 монет перед аукционом следующего раунда.");
            Add(english, russian, "ability.playAttackBoth", "Play: gives the ally in attack +{0}/+{0}.", "Розыгрыш: даёт союзнику в атаке +{0}/+{0}.");
            Add(english, russian, "ability.playDefenseHealth", "Play: gives the ally in defense +{0} health.", "Розыгрыш: даёт союзнику в защите +{0} здоровья.");
            Add(english, russian, "ability.endNutGrowth", "End of preparation: permanently improves future nuts by +0/+{0}.", "Конец подготовки: навсегда усиливает будущие орехи на +0/+{0}.");
            Add(english, russian, "ability.startGiveNut", "Start of preparation: gives {0} nuts.", "Начало подготовки: даёт {0} орехов.");
            Add(english, russian, "ability.endGiveCache", "End of preparation: gives {0} nut caches.", "Конец подготовки: даёт {0} тайников с орехами.");
            Add(english, russian, "ability.endRandomBuff", "End of preparation: a random other ally gains +{0} attack, +{1} health or +{2}/+{2} at random.", "Конец подготовки: случайному другому союзнику +{0} атаки, +{1} здоровья или +{2}/+{2} случайным образом.");
            Add(english, russian, "ability.featheredModifier", "End of preparation: permanently improves future Feathered abilities by +{0}/+{0}.", "Конец подготовки: навсегда усиливает будущие способности Пернатых на +{0}/+{0}.");
            Add(english, russian, "ability.directShield", "Reduces direct damage by {0}. Uses left: {3}.", "Снижает прямой урон на {0}. Осталось: {3}.");
            Add(english, russian, "ability.openingPoison", "Battle start: applies {0} poison to every enemy until battle ends.", "Начало боя: накладывает {0} яда на всех противников до конца боя.");
            Add(english, russian, "ability.playCoins", "Play: gives {0} coins.", "Розыгрыш: даёт {0} монет.");
            Add(english, russian, "ability.applyNut", "Permanently gives a chosen ally +{0}/+{1}.", "Навсегда даёт выбранному союзнику +{0}/+{1}.");
            Add(english, russian, "ability.lockedCache", "Opening progress: {5}/{0}. Forest synergy advances it. When opened, applies one nut to every ally. Cannot be cast or sold.", "Прогресс открытия: {5}/{0}. Лесная синергия продвигает открытие. При вскрытии применяет по ореху ко всем союзникам. Нельзя применить вручную или продать.");
            Add(english, russian, "card.hazelnut", "Hazelnut", "Лесной орех");
            Add(english, russian, "card.nutCache", "Nut cache", "Тайник с орехами");
            Add(english, russian, "spellCannotBePlaced", "Spells cannot be placed on the board.", "Заклинание нельзя выставить на поле.");
            Add(english, russian, "spellCannotBeSold", "Spells cannot be sold.", "Заклинание нельзя продать.");
            Add(english, russian, "spellCannotBeCast", "This card cannot be cast manually.", "Эту карту нельзя применить вручную.");
            Add(english, russian, "invalidSpellTarget", "Choose an ally on your board.", "Выберите союзника на своём поле.");
            Add(english, russian, "ability.attackOnOwnTurn", "Attack: gains +{0} attack for this hit.", "Атака: получает +{0} атаки на этот удар.");
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
            Add(english, russian, "synergy.scope", "Thresholds: 3 / 7 / 12 field copies. Effects depend on the class.", "Пороги: 3 / 7 / 12 копий на поле. Эффект зависит от класса.");
            Add(english, russian, "synergy.short.pack", "Preparation start: 1 / 2 / 3 pack cubs", "Начало подготовки: 1 / 2 / 3 детёныша");
            Add(english, russian, "synergy.short.armored", "Preparation end: permanent +2 / +4 / +9 health", "Конец подготовки: навсегда +2 / +4 / +9 здоровья");
            Add(english, russian, "synergy.short.forest", "Preparation end: +1 / +2 / +3 cache progress", "Конец подготовки: +1 / +2 / +3 прогресса тайников");
            Add(english, russian, "synergy.short.feathered", "Each own attack: +2 / +4 / +7 attack for this battle", "Каждая своя атака: +2 / +4 / +7 атаки до конца боя");
            Add(english, russian, "synergy.short.venomous", "Poison strength +1 / +3 / +8", "Сила яда +1 / +3 / +8");
            Add(english, russian, "synergy.short.urban", "Next-round income +2 / +5 / +10", "Доход следующего раунда +2 / +5 / +10");
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
