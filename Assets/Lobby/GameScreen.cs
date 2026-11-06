using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.UIElements;

namespace AnimalBattler.Client.Lobby
{
    internal sealed class GameScreen
    {
        private readonly VisualElement _root;
        private readonly VisualElement _content;
        private readonly Action<GameRequest> _send;
        private readonly Func<string, string> _localize;
        private readonly Dictionary<string, CardDefinitionMessage> _catalog = new Dictionary<string, CardDefinitionMessage>();
        private readonly Dictionary<string, VisualElement> _battleCards = new Dictionary<string, VisualElement>();
        private readonly Dictionary<string, Label> _battleHealth = new Dictionary<string, Label>();
        private readonly VisualElement[] _boardTargets = new VisualElement[5];
        private readonly VisualElement _line;
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

        public GameScreen(VisualElement parent, Action<GameRequest> send, Func<string, string> localize)
        {
            _send = send;
            _localize = localize;
            _root = new VisualElement { name = "game-screen" };
            _root.AddToClassList("game-screen");
            _root.AddToClassList("hidden");
            _root.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                _root.EnableInClassList("compact", evt.newRect.width < 650);
            });
            parent.Add(_root);
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            _root.Add(scroll);
            _content = scroll.contentContainer;
            _line = new VisualElement { pickingMode = PickingMode.Ignore };
            _line.style.position = Position.Absolute;
            _line.style.left = 0;
            _line.style.top = 0;
            _line.style.right = 0;
            _line.style.bottom = 0;
            _line.generateVisualContent += DrawAttackLine;
            _root.Add(_line);
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
            _root.RemoveFromClassList("hidden");
            Refresh();
        }

        public void Hide()
        {
            _state = null;
            _lastBattleKey = null;
            _errorKey = null;
            _root.AddToClassList("hidden");
            _line.MarkDirtyRepaint();
        }

        public void ShowError(string key)
        {
            _errorKey = key;
            Refresh();
        }

        public void Refresh()
        {
            if (_state == null) return;
            _content.Clear();
            _battleCards.Clear();
            _battleHealth.Clear();
            Array.Clear(_boardTargets, 0, _boardTargets.Length);

            var header = Row("game-header");
            header.Add(Text("game-title", "gameTitle"));
            header.Add(Text("round", "round", _state.round));
            _timer = Text("timer", "timer", 0);
            header.Add(_timer);
            _content.Add(header);
            var selfName = NameFor(_state.self?.playerId);
            var otherName = NameFor(_state.opponent?.playerId);
            _content.Add(Text("game-summary", "playerSummary", selfName, _state.self?.health ?? 0,
                _state.self?.coins ?? 0, _state.self?.availableCoins ?? 0));
            _content.Add(Text("game-summary", "opponentSummary", otherName, _state.opponent?.health ?? 0));
            if (!string.IsNullOrEmpty(_errorKey)) _content.Add(Text("game-error", _errorKey));

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
            if (_state == null) return;
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
            _line.MarkDirtyRepaint();
        }

        private void RenderAuction()
        {
            _content.Add(Text("section-title", "auctionHeading"));
            _content.Add(Text("hint", "auctionHint"));
            _content.Add(Text("hint", "opponentHandCount", _state.opponent?.handCount ?? 0));
            var list = Scroll("offer-scroll");
            var bids = _state.self?.bids ?? Array.Empty<int>();
            var offers = _state.auction ?? Array.Empty<string>();
            for (var slot = 0; slot < 5; slot++)
            {
                var index = slot;
                var container = Column("offer");
                var cardId = slot < offers.Length ? offers[slot] : null;
                container.Add(Card(cardId, 1, false));
                if (!string.IsNullOrEmpty(cardId))
                {
                    var field = new IntegerField(Format("bidLabel")) { value = slot < bids.Length ? bids[slot] : 0 };
                    field.SetEnabled(!_state.self.ready);
                    container.Add(field);
                    container.Add(Button("setBid", () => _send(new GameRequest
                    {
                        type = "setBid", slot = index, amount = field.value
                    }), !_state.self.ready));
                }
                list.Add(container);
            }
            _content.Add(list);
            ReadyButton();
        }

        private void RenderPreparation()
        {
            _content.Add(Text("section-title", "preparationHeading"));
            if (_state.auctionResults != null && _state.auctionResults.Length > 0)
            {
                var results = Scroll("results-scroll");
                results.Add(Text("section-title", "auctionResultsHeading"));
                foreach (var result in _state.auctionResults)
                {
                    var winner = string.IsNullOrEmpty(result.winnerId) ? Format("noWinner") : NameFor(result.winnerId);
                    var selfFirst = _players.Length > 0 && _players[0].playerId == _state.self.playerId;
                    results.Add(Text("hint", "auctionResult", CardName(result.cardId), winner,
                        selfFirst ? result.firstBid : result.secondBid,
                        selfFirst ? result.secondBid : result.firstBid));
                }
                _content.Add(results);
            }
            _content.Add(Text("section-title", "shopHeading"));
            var shop = Scroll("offer-scroll");
            for (var slot = 0; slot < 5; slot++)
            {
                var index = slot;
                var offer = _state.self.shop != null && slot < _state.self.shop.Length ? _state.self.shop[slot] : null;
                var item = Column("offer");
                item.Add(Card(offer?.cardId, 1, false));
                if (offer != null)
                {
                    item.Add(Button("buy", () => _send(new GameRequest { type = "buy", slot = index }),
                        !_state.self.ready && _state.self.coins >= 3));
                    item.Add(Button(offer.frozen ? "unfreeze" : "freeze", () => _send(new GameRequest
                    {
                        type = "setFrozen", slot = index, frozen = !offer.frozen
                    }), !_state.self.ready));
                }
                shop.Add(item);
            }
            _content.Add(shop);
            _content.Add(Button("refreshShop", () => _send(new GameRequest { type = "refreshShop" }),
                !_state.self.ready && _state.self.coins >= 1));
            _content.Add(Text("section-title", "boardHeading"));
            var board = Scroll("board-row");
            for (var slot = 0; slot < 5; slot++)
            {
                var index = slot;
                var card = _state.self.board != null && slot < _state.self.board.Length ? _state.self.board[slot] : null;
                var target = Column("board-slot");
                target.Add(Text("slot-title", slot == 0 ? "attackSlot" : slot == 4 ? "defenseSlot" : "supportSlot", slot));
                var cardVisual = Card(card?.cardId, card?.copies ?? 1, true);
                target.Add(cardVisual);
                if (card != null)
                {
                    BindDrag(cardVisual, new DragSource { FromBoard = true, BoardSlot = index, CardId = card.id });
                    target.Add(Button("sell", () => _send(new GameRequest { type = "sellCard", cardId = card.id }),
                        !_state.self.ready));
                }
                _boardTargets[slot] = target;
                board.Add(target);
            }
            _content.Add(board);
            _content.Add(Text("section-title", "handHeading"));
            var hand = Scroll("hand-scroll");
            var handControls = Row("hand-controls");
            handControls.Add(Button("handPrevious", () => hand.horizontalScroller.value -= 120, true));
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
            handControls.Add(hand);
            handControls.Add(Button("handNext", () => hand.horizontalScroller.value += 120, true));
            _content.Add(handControls);
            _content.Add(Text("hint", "dragHint"));
            ReadyButton();
        }

        private void RenderBattle()
        {
            _content.Add(Text("section-title", "battleHeading"));
            var opening = _state.battle?.openingCards ?? Array.Empty<BattleCardMessage>();
            var selfIndex = _players.Length > 0 && _players[0].playerId == _state.self.playerId ? 0 : 1;
            for (var position = 0; position < 2; position++)
            {
                var side = position == 0 ? 1 - selfIndex : selfIndex;
                _content.Add(Text("slot-title", side == 0 ? "firstSide" : "secondSide",
                    NameFor(side < _players.Length ? _players[side].playerId : null)));
                var row = Scroll("board-row");
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
                    row.Add(holder);
                }
                _content.Add(row);
            }
            _content.Add(Text("hint", "battleHint"));
        }

        private void RenderFinished()
        {
            var result = _state.isDraw ? "draw" : _state.winnerId == _state.self.playerId ? "victory" : "defeat";
            _content.Add(Text("result", result));
            _content.Add(Button(_state.rematchReady ? "rematchWaiting" : "rematch", () =>
                _send(new GameRequest { type = "acceptRematch" }), !_state.rematchReady));
            _content.Add(Button("leaveAfterMatch", () => _send(new GameRequest { type = "declineRematch" }), true));
        }

        private void ReadyButton()
        {
            var ready = _state.self.ready;
            _content.Add(Button(ready ? "cancelReady" : "ready", () => _send(new GameRequest
            {
                type = "setReady", ready = !ready
            }), true));
            _content.Add(Text("hint", _state.opponent.ready ? "opponentReady" : "opponentNotReady"));
        }

        private VisualElement Card(string id, int copies, bool showCopies)
        {
            var visual = Column("game-card");
            if (string.IsNullOrEmpty(id))
            {
                visual.Add(Text("card-name", "emptySlot"));
                return visual;
            }
            _catalog.TryGetValue(id, out var definition);
            visual.AddToClassList("rarity-" + (definition?.rarity ?? "common"));
            visual.Add(Text("card-tier", "tier", copies == 6 ? 3 : copies >= 3 ? 2 : 1));
            visual.Add(Text("card-art", "artPlaceholder"));
            var name = new Label(CardName(id));
            name.AddToClassList("card-name");
            visual.Add(name);
            var group = new Label(Format("group." + (definition?.group ?? "unknown")));
            group.AddToClassList("card-group");
            visual.Add(group);
            if (definition?.ability != null)
            {
                var ability = new Label(Format("ability." + definition.ability.kind,
                    definition.ability.amount * (copies == 6 ? 3 : copies >= 3 ? 2 : 1)));
                ability.AddToClassList("card-ability");
                visual.Add(ability);
            }
            visual.Add(Text("card-stats", "cardStats", (definition?.attack ?? 0) + copies - 1,
                (definition?.health ?? 0) + copies - 1));
            if (showCopies) visual.Add(Text("card-copies", "copies", copies));
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

        private static ScrollView Scroll(string style)
        {
            var scroll = new ScrollView(ScrollViewMode.Horizontal);
            scroll.AddToClassList(style);
            return scroll;
        }
    }
}
