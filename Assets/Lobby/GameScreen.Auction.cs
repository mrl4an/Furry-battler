using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using PointerType = UnityEngine.UIElements.PointerType;

namespace AnimalBattler.Client.Lobby
{
    internal sealed partial class GameScreen
    {
        private static readonly string[] ClassGroups = { "pack", "armored", "forest", "feathered", "venomous", "urban" };
        private static readonly Dictionary<string, string> RecommendedRoles = new Dictionary<string, string>
        {
            { "dog", "attack" }, { "jackal", "attack" }, { "coyote", "attack" }, { "wolf", "support" },
            { "turtle", "defense" }, { "porcupine", "defense" }, { "armadillo", "defense" }, { "pangolin", "support" },
            { "hare", "attack" }, { "squirrel", "support" }, { "deer", "support" }, { "moose", "defense" },
            { "raven", "attack" }, { "owl", "support" }, { "hawk", "attack" }, { "eagle", "attack" },
            { "spider", "defense" }, { "poisonFrog", "defense" }, { "scorpion", "defense" }, { "viper", "defense" },
            { "pigeon", "defense" }, { "strayCat", "support" }, { "rat", "attack" }, { "raccoon", "support" }
        };

        private VisualTreeAsset _characterTemplate;
        private VisualElement _detailsLayer;
        private VisualElement _detailsHost;
        private bool _touchDetails;
        private bool _touchInput;

        private void RenderAuctionLayout()
        {
            SetText("stage-heading", "auctionHeading");
            SetText("auction-hint", "auction.stepHint");
            SetText("hand-heading", "handHeading");
            SetText("opponent-hand-count", "opponentHandCount", _state.opponent?.handCount ?? 0);
            SetText("self-name", "firstSide", NameFor(_state.self.playerId));
            SetText("opponent-name", "firstSide", NameFor(_state.opponent?.playerId));
            SetText("self-health", "auction.health", _state.self.health);
            SetText("opponent-health", "auction.health", _state.opponent?.health ?? 0);
            SetText("self-coins", "auction.coins", _state.self.coins, _state.self.availableCoins);
            SetText("synergy-heading", "auction.synergies");

            var bids = _state.self.bids ?? Array.Empty<int>();
            var offers = _state.auction ?? Array.Empty<string>();
            for (var slot = 0; slot < 5; slot++)
            {
                var index = slot;
                var holder = _root.Q<VisualElement>("offer-" + slot);
                var host = holder.Q<VisualElement>("card-host");
                host.Clear();
                var id = slot < offers.Length ? offers[slot] : null;
                host.Add(CharacterCard(id, 1, false));
                var bid = slot < bids.Length ? bids[slot] : 0;
                holder.Q<Label>("bid-value").text = Format("ui.number", bid);
                var minus = holder.Q<Button>("bid-minus");
                var plus = holder.Q<Button>("bid-plus");
                var enabled = !string.IsNullOrEmpty(id) && !_state.self.ready;
                void SendBid(int amount)
                {
                    minus.SetEnabled(false);
                    plus.SetEnabled(false);
                    _send(new GameRequest { type = "setBid", slot = index, amount = amount });
                }
                BindAuctionButton(minus, "auction.minus", () => SendBid(bid <= 2 ? 0 : bid - 1), enabled && bid > 0);
                BindAuctionButton(plus, "auction.plus", () => SendBid(bid == 0 ? 2 : bid + 1),
                    enabled && _state.self.availableCoins >= (bid == 0 ? 2 : 1));
                minus.tooltip = Format("auction.decreaseBid");
                plus.tooltip = Format("auction.increaseBid");
            }

            var hand = _root.Q<ScrollView>("auction-hand");
            var handOffset = hand.scrollOffset;
            hand.Clear();
            foreach (var card in _state.self.hand ?? Array.Empty<CardInstanceMessage>())
                hand.Add(CharacterCard(card.cardId, card.copies, false));
            hand.schedule.Execute(() => hand.scrollOffset = handOffset);

            var opponentHand = _root.Q<ScrollView>("opponent-hand");
            var opponentOffset = opponentHand.scrollOffset;
            opponentHand.Clear();
            for (var i = 0; i < (_state.opponent?.handCount ?? 0); i++)
            {
                var back = new VisualElement();
                back.AddToClassList("card-back");
                back.tooltip = Format("auction.hiddenCard");
                opponentHand.Add(back);
            }
            opponentHand.schedule.Execute(() => opponentHand.scrollOffset = opponentOffset);
            RenderAuctionSynergies();
            ReadyButton();
        }

        private void BindAuctionButton(Button button, string key, Action action, bool enabled)
        {
            button.text = Format(key);
            button.SetEnabled(enabled);
            if (_buttonActions.TryGetValue(button, out var previous)) button.clicked -= previous;
            button.clicked += action;
            _buttonActions[button] = action;
        }

        private void RenderAuctionSynergies()
        {
            foreach (var group in ClassGroups)
            {
                var entry = _root.Q<VisualElement>("synergy-" + group);
                var count = (_state.self.board ?? Array.Empty<CardInstanceMessage>())
                    .Where(card => card != null && !string.IsNullOrEmpty(card.cardId)
                        && _catalog.TryGetValue(card.cardId, out var definition)
                        && definition.group == group)
                    .Sum(card => card.copies);
                entry.Q<Label>("synergy-name").text = Format("group." + group);
                entry.Q<Label>("synergy-count").text = Format("ui.number", count);
                entry.Q<Label>("synergy-thresholds").text = Format("auction.thresholds");
                entry.tooltip = Format("synergy." + group);
                entry.EnableInClassList("active", count >= 3);
                entry.EnableInClassList("inactive", count < 3);
                for (var i = 0; i < 3; i++)
                    entry.Q<VisualElement>("threshold-" + i).EnableInClassList("filled",
                        count >= (i == 0 ? 3 : i == 1 ? 6 : 10));
            }
        }

        private VisualElement CharacterCard(string id, int copies, bool showMerge, bool detailed = false)
        {
            if (_characterTemplate == null) _characterTemplate = Resources.Load<VisualTreeAsset>("Cards/CharacterCard");
            var tree = _characterTemplate.CloneTree();
            var card = tree.Q<VisualElement>("character-card");
            for (var i = 0; i < tree.styleSheets.count; i++)
                card.styleSheets.Add(tree.styleSheets[i]);
            card.EnableInClassList("detailed-card", detailed);
            card.Q<VisualElement>("character-merge").style.display = showMerge ? DisplayStyle.Flex : DisplayStyle.None;
            if (string.IsNullOrEmpty(id))
            {
                card.AddToClassList("empty-character");
                card.focusable = false;
                return card;
            }
            _catalog.TryGetValue(id, out var definition);
            var tier = copies >= 6 ? 3 : copies >= 3 ? 2 : 1;
            var drops = tier == 3 ? 0 : tier == 2 ? copies - 3 : copies;
            card.RemoveFromClassList("rarity-common");
            card.AddToClassList("rarity-" + (definition?.rarity ?? "common"));
            card.Q<Label>("character-name").text = CardName(id);
            card.Q<Label>("character-group").text = definition == null ? Format("ui.unknown")
                : Format("group." + definition.group);
            card.Q<Label>("character-ability").text = definition?.ability == null ? Format("card.noAbility")
                : Format("ability." + definition.ability.kind, definition.ability.amount * tier);
            card.Q<Label>("character-attack").text = definition == null ? Format("ui.unknown")
                : Format("ui.number", definition.attack + copies - 1);
            card.Q<Label>("character-health").text = definition == null ? Format("ui.unknown")
                : Format("ui.number", definition.health + copies - 1);
            card.Q<Label>("character-tier").text = Format("card.level", tier);
            for (var i = 0; i < 3; i++)
                card.Q<VisualElement>("merge-" + i).EnableInClassList("filled", i < drops);
            foreach (var name in new[] { "character-class", "detail-class-icon", "character-art" })
            {
                var icon = card.Q<VisualElement>(name);
                icon.RemoveFromClassList("group-pack");
                if (definition != null) icon.AddToClassList("group-" + definition.group);
            }
            var role = card.Q<VisualElement>("character-role");
            role.RemoveFromClassList("role-support");
            if (RecommendedRoles.TryGetValue(id, out var recommended))
            {
                role.AddToClassList("role-" + recommended);
                role.tooltip = Format("card.recommendation", Format("role." + recommended));
            }
            else role.style.display = DisplayStyle.None;
            var art = card.Q<VisualElement>("character-art");
            var portrait = Resources.Load<Texture2D>("Characters/" + id);
            if (portrait != null)
            {
                art.style.backgroundImage = new StyleBackground(portrait);
                art.AddToClassList("has-portrait");
            }
            if (!detailed) BindCharacterDetails(card, id, copies);
            return card;
        }

        private void BindCardDetails()
        {
            _detailsLayer = _root.Q<VisualElement>("card-details-layer");
            _detailsHost = _root.Q<VisualElement>("card-details-host");
            if (_detailsLayer == null) return;
            var close = _root.Q<Button>("close-card-details");
            close.clicked += CloseCardDetails;
            _root.Q<VisualElement>("card-details-scrim").RegisterCallback<PointerDownEvent>(evt =>
            {
                if (_touchDetails) { CloseCardDetails(); evt.StopPropagation(); }
            });
            _root.RegisterCallback<KeyDownEvent>(evt =>
            {
                _touchInput = false;
                if (evt.keyCode == KeyCode.Escape) CloseCardDetails();
            });
            _root.RegisterCallback<PointerDownEvent>(evt => _touchInput = evt.pointerType == PointerType.touch,
                TrickleDown.TrickleDown);
            _root.RegisterCallback<WheelEvent>(evt => { if (!_touchDetails) CloseCardDetails(); },
                TrickleDown.TrickleDown);
            _root.RegisterCallback<GeometryChangedEvent>(evt => CloseCardDetails());
        }

        private void BindCharacterDetails(VisualElement card, string id, int copies)
        {
            card.RegisterCallback<PointerEnterEvent>(evt =>
            {
                if (evt.pointerType == PointerType.mouse && !_touchDetails) OpenCardDetails(card, id, copies, false);
            });
            card.RegisterCallback<PointerLeaveEvent>(evt => { if (!_touchDetails) CloseCardDetails(); });
            card.RegisterCallback<FocusInEvent>(evt =>
            {
                if (!_touchInput) OpenCardDetails(card, id, copies, false);
            });
            card.RegisterCallback<FocusOutEvent>(evt => { if (!_touchDetails) CloseCardDetails(); });
            Vector2 touchStart = default;
            card.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.pointerType == PointerType.touch) touchStart = evt.position;
            });
            card.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (evt.pointerType == PointerType.touch && Vector2.Distance(touchStart, evt.position) < 12)
                {
                    OpenCardDetails(card, id, copies, true);
                    evt.StopPropagation();
                }
            });
        }

        private void OpenCardDetails(VisualElement anchor, string id, int copies, bool touch)
        {
            if (_detailsLayer == null || _detailsHost == null) return;
            _touchDetails = touch;
            _detailsHost.Clear();
            var detail = CharacterCard(id, copies, false, true);
            detail.focusable = false;
            _detailsHost.Add(detail);
            _detailsLayer.RemoveFromClassList("hidden");
            _detailsLayer.EnableInClassList("touch-details", touch);
            foreach (var element in _detailsLayer.Query<VisualElement>().ToList())
                element.pickingMode = touch ? PickingMode.Position : PickingMode.Ignore;
            _detailsLayer.pickingMode = touch ? PickingMode.Position : PickingMode.Ignore;
            var rootBounds = _root.contentRect;
            var source = _root.WorldToLocal(anchor.worldBound.max);
            var x = touch ? (rootBounds.width - 224) / 2 : source.x + 24;
            if (x + 240 > rootBounds.width)
                x = _root.WorldToLocal(anchor.worldBound.min).x - 248;
            var y = touch ? (rootBounds.height - 380) / 2 : _root.WorldToLocal(anchor.worldBound.min).y;
            x = Mathf.Clamp(x, 20, Mathf.Max(20, rootBounds.width - 244));
            y = Mathf.Clamp(y, 20, Mathf.Max(20, rootBounds.height - 380));
            _detailsHost.style.left = x;
            _detailsHost.style.top = y;
            var close = _root.Q<Button>("close-card-details");
            close.text = Format("card.close");
            close.EnableInClassList("hidden", !touch);
            close.style.left = x + 62;
            close.style.top = y + 340;
        }

        private void CloseCardDetails()
        {
            _detailsLayer?.AddToClassList("hidden");
            _detailsHost?.Clear();
            _touchDetails = false;
        }
    }
}
