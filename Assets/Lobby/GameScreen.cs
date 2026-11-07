using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.UIElements;

namespace AnimalBattler.Client.Lobby
{
    internal sealed partial class GameScreen
    {
        private VisualElement _root;
        private readonly Action<GameRequest> _send;
        private readonly Func<string, string> _localize;
        private readonly Dictionary<string, CardDefinitionMessage> _catalog = new Dictionary<string, CardDefinitionMessage>();
        private readonly Dictionary<string, VisualElement> _battleCards = new Dictionary<string, VisualElement>();
        private readonly Dictionary<string, Label> _battleHealth = new Dictionary<string, Label>();
        private readonly Dictionary<Button, Action> _buttonActions = new Dictionary<Button, Action>();
        private readonly VisualElement[] _boardTargets = new VisualElement[5];
        private VisualElement _line;
        private readonly Action _switchLanguage;
        private readonly VisualTreeAsset _cardTemplate;
        private string _sceneName;
        private ServerMessage _state;
        private RoomPlayer[] _players = Array.Empty<RoomPlayer>();
        private DragSource _drag;
        private VisualElement _ghost;
        private Label _timer;
        private int _visibleBattleEvent = -1;
        private double _battleStartedAt;
        private string _lastBattleKey;
        private string _errorKey;

        private struct DragSource
        {
            public string CardId;
            public int BoardSlot;
            public bool FromBoard;
        }

        public GameScreen(Action<GameRequest> send, Func<string, string> localize,
            Action switchLanguage, VisualTreeAsset cardTemplate)
        {
            _send = send;
            _localize = localize;
            _switchLanguage = switchLanguage;
            _cardTemplate = cardTemplate;
        }

        public void Bind(UIDocument document, string sceneName)
        {
            CloseCardDetails();
            _buttonActions.Clear();
            _sceneName = sceneName;
            _root = document.rootVisualElement.Q<VisualElement>("game-screen");
            _line = _root.Q<VisualElement>("attack-line");
            _root.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                _root.EnableInClassList("compact", evt.newRect.height < 680);
            });
            foreach (var scroll in _root.Query<ScrollView>(className: "capsule-scroll").ToList())
                BindScrollAppearance(scroll);
            _root.Q<Button>("language-button").clicked += _switchLanguage;
            BindCardDetails();
            if (_line != null)
            {
                _line.pickingMode = PickingMode.Ignore;
                _line.generateVisualContent += DrawAttackLine;
            }
        }

        public void SetPlayers(RoomPlayer[] players)
        {
            _players = players ?? Array.Empty<RoomPlayer>();
            if (_state?.phase == "finished" && _state.self != null)
            {
                _state.rematchReady = _players.Any(player => player.playerId == _state.self.playerId && player.ready);
                Refresh();
            }
        }

        public void SetCatalog(CardDefinitionMessage[] cards)
        {
            _catalog.Clear();
            foreach (var card in cards ?? Array.Empty<CardDefinitionMessage>())
                if (!string.IsNullOrEmpty(card.id)) _catalog[card.id] = card;
            Refresh();
        }

        public void SetMatch(ServerMessage state)
        {
            var battleKey = state.phase == "battle" ? state.round + ":" + state.endsAtUtc : null;
            if (battleKey != _lastBattleKey)
            {
                _lastBattleKey = battleKey;
                _visibleBattleEvent = -1;
                _battleStartedAt = Time.realtimeSinceStartupAsDouble;
            }
            _state = state;
            _errorKey = null;
            Refresh();
        }

        public void Hide()
        {
            CloseCardDetails();
            _state = null;
            _lastBattleKey = null;
            _errorKey = null;
            _line?.MarkDirtyRepaint();
        }

        public void ShowError(string key)
        {
            _errorKey = key;
            Refresh();
        }

        public void Refresh()
        {
            if (_state == null || _root == null || _sceneName != SceneFor(_state.phase)) return;
            CloseCardDetails();
            _battleCards.Clear();
            _battleHealth.Clear();
            Array.Clear(_boardTargets, 0, _boardTargets.Length);
            SetText("game-title", "gameTitle");
            SetText("round", "round", _state.round);
            _timer = _root.Q<Label>("timer");
            _root.Q<Button>("language-button").text =
                LocalizationSettings.SelectedLocale?.Identifier.Code == "ru" ? "EN" : "RU";
            var selfName = NameFor(_state.self?.playerId);
            var otherName = NameFor(_state.opponent?.playerId);
            SetText("self-summary", "playerSummary", selfName, _state.self?.health ?? 0,
                _state.self?.coins ?? 0, _state.self?.availableCoins ?? 0);
            SetText("opponent-summary", "opponentSummary", otherName, _state.opponent?.health ?? 0);
            var error = _root.Q<Label>("game-error");
            error.text = string.IsNullOrEmpty(_errorKey) ? string.Empty : Format(_errorKey);
            error.EnableInClassList("hidden", string.IsNullOrEmpty(_errorKey));

            switch (_state.phase)
            {
                case "auction": RenderAuction(); break;
                case "preparation": RenderPreparation(); break;
                case "battle": RenderBattle(); break;
                case "finished": RenderFinished(); break;
            }
            if (_state.phase == "battle" && _state.battle?.events != null)
                ShowBattleProgress(_visibleBattleEvent);
            Tick();
        }

        public void Tick()
        {
            if (_state == null || _sceneName != SceneFor(_state.phase)) return;
            if (_timer != null && DateTimeOffset.TryParse(_state.endsAtUtc, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal, out var deadline))
            {
                var seconds = Math.Max(0, (int)Math.Ceiling((deadline - DateTimeOffset.UtcNow).TotalSeconds));
                _timer.text = Format("timer", seconds);
            }
            else if (_timer != null) _timer.text = string.Empty;

            if (_state.phase != "battle" || _state.battle?.events == null) return;
            var elapsed = Time.realtimeSinceStartupAsDouble - _battleStartedAt;
            var eventIndex = _state.battle.events.Length;
            var passed = 0.0;
            for (var i = 0; i < _state.battle.events.Length; i++)
            {
                var length = passed < 30 ? 3.0 : passed < 60 ? 1.5 : 0.75;
                if (elapsed < passed + length) { eventIndex = i; break; }
                passed += length;
            }
            if (eventIndex != _visibleBattleEvent)
            {
                _visibleBattleEvent = eventIndex;
                ShowBattleProgress(eventIndex);
            }
            _line?.MarkDirtyRepaint();
        }

        private static string SceneFor(string phase) => phase == "auction" ? "Auction"
            : phase == "preparation" ? "Preparation" : phase == "battle" ? "Battle" : "Result";

        private static void BindScrollAppearance(ScrollView scroll)
        {
            var fade = scroll.schedule.Execute(() => scroll.RemoveFromClassList("scroll-active"));
            fade.Pause();
            void ShowTrack()
            {
                scroll.AddToClassList("scroll-active");
                fade.ExecuteLater(700);
            }
            scroll.horizontalScroller.valueChanged += value => ShowTrack();
            scroll.verticalScroller.valueChanged += value => ShowTrack();
            scroll.RegisterCallback<WheelEvent>(evt => ShowTrack(), TrickleDown.TrickleDown);
            foreach (var scroller in new[] { scroll.horizontalScroller, scroll.verticalScroller })
            {
                scroller.RegisterCallback<PointerDownEvent>(evt =>
                {
                    scroll.AddToClassList("scroll-dragging");
                    ShowTrack();
                }, TrickleDown.TrickleDown);
                scroller.RegisterCallback<PointerUpEvent>(evt =>
                {
                    scroll.RemoveFromClassList("scroll-dragging");
                    ShowTrack();
                }, TrickleDown.TrickleDown);
                scroller.RegisterCallback<PointerCaptureOutEvent>(evt =>
                {
                    scroll.RemoveFromClassList("scroll-dragging");
                    ShowTrack();
                });
            }
        }

        private void SetText(string name, string key, params object[] args)
        {
            var label = _root.Q<Label>(name);
            if (label != null) label.text = Format(key, args);
        }

        private void SetButton(string name, string key, Action action, bool enabled)
        {
            var button = _root.Q<Button>(name);
            button.text = Format(key);
            button.SetEnabled(enabled);
            if (_buttonActions.TryGetValue(button, out var previous)) button.clicked -= previous;
            button.clicked += action;
            _buttonActions[button] = action;
        }

        private void RenderAuction()
        {
            RenderAuctionLayout();
        }

        private void RenderPreparation()
        {
            SetText("stage-heading", "preparationHeading");
            var results = _root.Q<ScrollView>("auction-results");
            results.contentContainer.Clear();
            results.parent.EnableInClassList("hidden", _state.auctionResults == null || _state.auctionResults.Length == 0);
            _root.Q<Label>("results-heading").text = _state.auctionResults != null && _state.auctionResults.Length > 0
                ? Format("auctionResultsHeading") : string.Empty;
            if (_state.auctionResults != null && _state.auctionResults.Length > 0)
            {
                foreach (var result in _state.auctionResults)
                {
                    var winner = string.IsNullOrEmpty(result.winnerId) ? Format("noWinner") : NameFor(result.winnerId);
                    var selfFirst = _players.Length > 0 && _players[0].playerId == _state.self.playerId;
                    results.Add(Text("hint", "auctionResult", CardName(result.cardId), winner,
                        selfFirst ? result.firstBid : result.secondBid,
                        selfFirst ? result.secondBid : result.firstBid));
                }
            }
            SetText("shop-heading", "shopHeading");
            var shop = _root.Q<VisualElement>("shop");
            shop.Clear();
            for (var slot = 0; slot < 5; slot++)
            {
                var index = slot;
                var offer = _state.self.shop != null && slot < _state.self.shop.Length ? _state.self.shop[slot] : null;
                var item = Column("offer");
                item.Add(Card(offer?.cardId, 1, false));
                if (offer != null)
                {
                    var actions = Row("offer-actions");
                    actions.Add(Button("buy", () => _send(new GameRequest { type = "buy", slot = index }),
                        !_state.self.ready && _state.self.coins >= 3));
                    actions.Add(Button(offer.frozen ? "unfreeze" : "freeze", () => _send(new GameRequest
                    {
                        type = "setFrozen", slot = index, frozen = !offer.frozen
                    }), !_state.self.ready));
                    item.Add(actions);
                }
                shop.Add(item);
            }
            SetButton("refresh-button", "refreshShop", () => _send(new GameRequest { type = "refreshShop" }),
                !_state.self.ready && _state.self.coins >= 1);
            SetText("board-heading", "boardHeading");
            var board = _root.Q<VisualElement>("board");
            board.Clear();
            for (var slot = 0; slot < 5; slot++)
            {
                var index = slot;
                var card = _state.self.board != null && slot < _state.self.board.Length ? _state.self.board[slot] : null;
                var target = Column("board-slot");
                var heading = Row("slot-heading");
                heading.Add(Text("slot-title", slot == 0 ? "attackSlot" : slot == 4 ? "defenseSlot" : "supportSlot", slot));
                target.Add(heading);
                var cardVisual = Card(card?.cardId, card?.copies ?? 1, true);
                target.Add(cardVisual);
                if (card != null)
                {
                    BindDrag(cardVisual, new DragSource { FromBoard = true, BoardSlot = index, CardId = card.id });
                    heading.Add(Button("sell", () => _send(new GameRequest { type = "sellCard", cardId = card.id }),
                        !_state.self.ready));
                }
                _boardTargets[slot] = target;
                board.Add(target);
            }
            SetText("hand-heading", "handHeading");
            var hand = _root.Q<ScrollView>("hand");
            hand.contentContainer.Clear();
            SetButton("hand-previous", "handPrevious", () => hand.horizontalScroller.value -= 120, true);
            foreach (var card in _state.self.hand ?? Array.Empty<CardInstanceMessage>())
            {
                var item = Column("hand-item");
                var cardVisual = Card(card.cardId, card.copies, true);
                item.Add(cardVisual);
                BindDrag(cardVisual, new DragSource { CardId = card.id });
                item.Add(Button("sell", () => _send(new GameRequest { type = "sellCard", cardId = card.id }),
                    !_state.self.ready));
                hand.Add(item);
            }
            SetButton("hand-next", "handNext", () => hand.horizontalScroller.value += 120, true);
            SetText("drag-hint", "dragHint");
            ReadyButton();
        }

        private void RenderBattle()
        {
            SetText("stage-heading", "battleHeading");
            var opening = _state.battle?.openingCards ?? Array.Empty<BattleCardMessage>();
            var selfIndex = _players.Length > 0 && _players[0].playerId == _state.self.playerId ? 0 : 1;
            for (var position = 0; position < 2; position++)
            {
                var side = position == 0 ? 1 - selfIndex : selfIndex;
                SetText(position == 0 ? "top-side-name" : "bottom-side-name",
                    side == 0 ? "firstSide" : "secondSide",
                    NameFor(side < _players.Length ? _players[side].playerId : null));
                var row = _root.Q<VisualElement>(position == 0 ? "top-board" : "bottom-board");
                row.Clear();
                for (var slot = 0; slot < 5; slot++)
                {
                    var card = opening.FirstOrDefault(item => item.playerIndex == side && item.slot == slot);
                    var holder = Column("board-slot");
                    if (card != null)
                    {
                        holder.Add(Card(card.cardId, card.copies, true));
                        var health = Text("battle-health", "battleStats", card.attack, card.health);
                        holder.Add(health);
                        _battleCards[card.id] = holder;
                        _battleHealth[card.id] = health;
                    }
                    else holder.Add(Card(null, 1, false));
                    row.Add(holder);
                }
            }
            SetText("battle-hint", "battleHint");
        }

        private void RenderFinished()
        {
            var result = _state.isDraw ? "draw" : _state.winnerId == _state.self.playerId ? "victory" : "defeat";
            SetText("result", result);
            SetButton("rematch-button", _state.rematchReady ? "rematchWaiting" : "rematch", () =>
                _send(new GameRequest { type = "acceptRematch" }), !_state.rematchReady);
            SetButton("leave-button", "leaveAfterMatch", () =>
                _send(new GameRequest { type = "declineRematch" }), true);
        }

        private void ReadyButton()
        {
            var ready = _state.self.ready;
            SetButton("ready-button", ready ? "cancelReady" : "ready", () => _send(new GameRequest
            {
                type = "setReady", ready = !ready
            }), true);
            SetText("ready-status", _state.opponent.ready ? "opponentReady" : "opponentNotReady");
        }

        private VisualElement Card(string id, int copies, bool showCopies)
        {
            var visual = _cardTemplate.CloneTree().Q<VisualElement>("game-card");
            if (string.IsNullOrEmpty(id))
            {
                visual.AddToClassList("empty-card");
                visual.Q<Label>("card-name").text = Format("emptySlot");
                visual.Q<Label>("card-tier").text = string.Empty;
                visual.Q<Label>("card-art").text = string.Empty;
                visual.Q<Label>("card-group").text = string.Empty;
                visual.Q<Label>("card-ability").text = string.Empty;
                visual.Q<Label>("card-stats").text = string.Empty;
                visual.Q<Label>("card-copies").text = string.Empty;
                return visual;
            }
            _catalog.TryGetValue(id, out var definition);
            visual.AddToClassList("rarity-" + (definition?.rarity ?? "common"));
            visual.Q<Label>("card-tier").text = Format("tier", copies == 6 ? 3 : copies >= 3 ? 2 : 1);
            visual.Q<Label>("card-art").text = Format("artPlaceholder");
            visual.Q<Label>("card-name").text = CardName(id);
            visual.Q<Label>("card-group").text = Format("group." + (definition?.group ?? "unknown"));
            visual.Q<Label>("card-ability").text = string.Empty;
            if (definition?.ability != null)
            {
                visual.Q<Label>("card-ability").text = Format("ability." + definition.ability.kind,
                    definition.ability.amount * (copies == 6 ? 3 : copies >= 3 ? 2 : 1));
            }
            visual.Q<Label>("card-stats").text = Format("cardStats", (definition?.attack ?? 0) + copies - 1,
                (definition?.health ?? 0) + copies - 1);
            visual.Q<Label>("card-copies").text = showCopies ? Format("copies", copies) : string.Empty;
            return visual;
        }

        private void BindDrag(VisualElement source, DragSource drag)
        {
            source.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (_state?.phase != "preparation" || _state.self.ready || evt.button != 0) return;
                _drag = drag;
                source.CapturePointer(evt.pointerId);
                _ghost = new Label(CardName(_state.self.hand?.FirstOrDefault(card => card.id == drag.CardId)?.cardId
                    ?? _state.self.board?.FirstOrDefault(card => card?.id == drag.CardId)?.cardId));
                _ghost.AddToClassList("drag-ghost");
                _ghost.pickingMode = PickingMode.Ignore;
                _root.Add(_ghost);
                MoveGhost(evt.position);
                evt.StopPropagation();
            });
            source.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (!source.HasPointerCapture(evt.pointerId)) return;
                MoveGhost(evt.position);
                evt.StopPropagation();
            });
            source.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (!source.HasPointerCapture(evt.pointerId)) return;
                source.ReleasePointer(evt.pointerId);
                _ghost?.RemoveFromHierarchy();
                _ghost = null;
                for (var slot = 0; slot < _boardTargets.Length; slot++)
                {
                    if (_boardTargets[slot] == null || !_boardTargets[slot].worldBound.Contains(evt.position)) continue;
                    if (_drag.FromBoard)
                    {
                        if (slot != _drag.BoardSlot) _send(new GameRequest
                        {
                            type = "moveCard", sourceSlot = _drag.BoardSlot, targetSlot = slot
                        });
                    }
                    else _send(new GameRequest { type = "placeCard", cardId = _drag.CardId, slot = slot });
                    break;
                }
                evt.StopPropagation();
            });
        }

        private void MoveGhost(Vector2 position)
        {
            if (_ghost == null) return;
            var point = position - _root.worldBound.position;
            _ghost.style.left = point.x + 10;
            _ghost.style.top = point.y + 10;
        }

        private void ShowBattleProgress(int eventIndex)
        {
            var events = _state.battle.events;
            var health = new Dictionary<string, int>();
            var dead = new HashSet<string>();
            foreach (var card in _state.battle.openingCards ?? Array.Empty<BattleCardMessage>())
                health[card.id] = card.health;
            for (var i = 0; i < events.Length && i < eventIndex; i++)
            {
                health[events[i].attackerId] = events[i].attackerHealthAfter;
                health[events[i].targetId] = events[i].targetHealthAfter;
                foreach (var id in events[i].deadCardIds ?? Array.Empty<string>()) dead.Add(id);
            }
            foreach (var pair in _battleCards)
            {
                pair.Value.style.opacity = dead.Contains(pair.Key) ? 0.25f : 1f;
                if (_battleHealth.TryGetValue(pair.Key, out var label) && health.TryGetValue(pair.Key, out var hp))
                {
                    var opening = _state.battle.openingCards.First(card => card.id == pair.Key);
                    label.text = Format("battleStats", opening.attack, hp);
                }
            }
        }

        private void DrawAttackLine(MeshGenerationContext context)
        {
            if (_state?.phase != "battle" || _state.battle?.events == null
                || _visibleBattleEvent < 0 || _visibleBattleEvent >= _state.battle.events.Length) return;
            var exchange = _state.battle.events[_visibleBattleEvent];
            if (!_battleCards.TryGetValue(exchange.attackerId, out var attacker)
                || !_battleCards.TryGetValue(exchange.targetId, out var target)) return;
            var origin = _line.worldBound.position;
            var a = attacker.worldBound.center - origin;
            var b = target.worldBound.center - origin;
            var pulse = 0.55f + 0.45f * Mathf.Sin(Time.realtimeSinceStartup * 12f);
            var painter = context.painter2D;
            painter.strokeColor = new Color(1f, 0.3f + 0.5f * pulse, 0.15f, 1f);
            painter.lineWidth = 3f + 4f * pulse;
            painter.BeginPath();
            painter.MoveTo(a);
            painter.LineTo(b);
            painter.Stroke();
        }

        private string CardName(string id)
        {
            var key = "card." + id;
            var value = _localize(key);
            return value == key ? id ?? string.Empty : value;
        }

        private string NameFor(string id) => _players.FirstOrDefault(player => player.playerId == id)?.nickname
            ?? Format("playerFallback");

        private string Format(string key, params object[] args)
        {
            var format = _localize(key);
            if (args.Length == 0) return format;
            var culture = LocalizationSettings.SelectedLocale?.Identifier.CultureInfo ?? CultureInfo.InvariantCulture;
            try { return string.Format(culture, format, args); }
            catch (FormatException) { return format; }
        }

        private Label Text(string style, string key, params object[] args)
        {
            var label = new Label(Format(key, args));
            label.AddToClassList(style);
            return label;
        }

        private Button Button(string key, Action action, bool enabled)
        {
            var button = new Button(action) { text = Format(key) };
            button.AddToClassList("game-button");
            button.SetEnabled(enabled);
            return button;
        }

        private static VisualElement Row(string style)
        {
            var element = new VisualElement();
            element.AddToClassList(style);
            return element;
        }

        private static VisualElement Column(string style) => Row(style);

    }
}
