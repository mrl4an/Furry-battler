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
            { "packCub", "support" }, { "dog", "attack" }, { "jackal", "attack" }, { "coyote", "attack" }, { "wolf", "support" },
            { "turtle", "defense" }, { "porcupine", "defense" }, { "armadillo", "defense" }, { "pangolin", "support" },
            { "hare", "attack" }, { "squirrel", "support" }, { "deer", "support" }, { "moose", "defense" },
            { "raven", "attack" }, { "owl", "support" }, { "hawk", "attack" }, { "eagle", "attack" },
            { "spider", "defense" }, { "poisonFrog", "defense" }, { "scorpion", "defense" }, { "viper", "defense" },
            { "pigeon", "defense" }, { "strayCat", "support" }, { "rat", "attack" }, { "raccoon", "support" }
        };

        private static readonly Dictionary<string, string> SpellPortraits = new Dictionary<string, string>
        {
            { "hazelnut", "hazelnut-v1" }, { "nutCache", "nut-cache-v1" }
        };

        private VisualTreeAsset _characterTemplate;
        private VisualElement _detailsLayer;
        private VisualElement _detailsHost;
        private VisualElement _detailsAnchor;
        private bool _touchDetails;
        private bool _touchInput;

        private void RenderAuctionLayout()
        {
            SetText("stage-heading", "auctionHeading");
            SetText("auction-hint", "auction.stepHint");
            _root.Q<Label>("auction-hint").RemoveFromClassList("hidden");
            SetText("hand-heading", "ui.number", _state.self.hand?.Length ?? 0);
            SetText("opponent-hand-count", "ui.number", _state.opponent?.handCount ?? 0);
            RenderMatchSidebar();

            var bids = _state.self.bids ?? Array.Empty<int>();
            var offers = _state.auction ?? Array.Empty<string>();
            for (var slot = 0; slot < 5; slot++)
            {
                var index = slot;
                var holder = _root.Q<VisualElement>("offer-" + slot);
                holder.Q<VisualElement>("bid-controls").RemoveFromClassList("hidden");
                holder.Q<VisualElement>("reveal-bids").AddToClassList("hidden");
                var host = holder.Q<VisualElement>("card-host");
                host.Clear();
                var id = slot < offers.Length ? offers[slot] : null;
                host.Add(CharacterCard(id, 1, false));
                var bid = slot < bids.Length ? bids[slot] : 0;
                holder.Q<Label>("bid-value").text = Format("ui.number", bid);
                holder.Q<VisualElement>("opponent-bet").AddToClassList("hidden");
                RenderBetCoins(holder.Q<VisualElement>("bid-coins"), bid);
                var refund = holder.Q<Button>("bid-refund");
                var place = holder.Q<Button>("bid-place");
                var enabled = !string.IsNullOrEmpty(id) && !_state.self.ready;
                void SendBid(int amount)
                {
                    refund.SetEnabled(false);
                    place.SetEnabled(false);
                    _send(new GameRequest { type = "setBid", slot = index, amount = amount });
                }
                BindAuctionButton(refund, "auction.refundBid", () => SendBid(0), enabled && bid > 0);
                refund.text = string.Empty;
                refund.tooltip = Format("auction.refundBid");
                refund.EnableInClassList("bid-refund-empty", bid == 0);
                BindAuctionButton(place, "auction.placeBid", () => SendBid(bid == 0 ? 2 : bid + 1),
                    enabled && _state.self.availableCoins >= (bid == 0 ? 2 : 1));
                place.text = string.Empty;
                place.tooltip = Format("auction.placeBid");

            }

            var hand = _root.Q<ScrollView>("auction-hand");
            var handOffset = hand.scrollOffset;
            hand.Clear();
            foreach (var card in _state.self.hand ?? Array.Empty<CardInstanceMessage>())
                hand.Add(InstanceCard(card, false));
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
            ReadyButton();
        }

        private void RenderMatchSidebar()
        {
            SetText("self-name", "firstSide", NameFor(_state.self.playerId));
            SetText("opponent-name", "firstSide", NameFor(_state.opponent?.playerId));
            SetText("self-health", "ui.number", _state.self.health);
            SetText("opponent-health", "ui.number", _state.opponent?.health ?? 0);
            SetText("self-coins", "ui.number", _state.self.coins);
            SetText("available-coins", "ui.number", _state.self.availableCoins);
            SetText("available-caption", "match.available");

            SetText("synergy-heading", "auction.synergies");
            RenderAuctionSynergies();
        }

        private static void RenderBetCoins(VisualElement host, int amount, bool revealed = false)
        {
            host.Clear();
            host.parent.EnableInClassList("bet-empty", amount == 0);
            host.parent.EnableInClassList("bet-revealed", revealed);
            var count = amount == 0 ? 1 : Math.Min(amount, 3);
            for (var i = 0; i < count; i++)
            {
                var coin = new VisualElement { pickingMode = PickingMode.Ignore };
                coin.AddToClassList("coin-icon");
                coin.AddToClassList("bet-coin-face");
                if (amount == 0)
                {
                    coin.AddToClassList("bet-hint-coin");
                    coin.style.left = revealed ? 26 : 6;
                    coin.style.top = 2;
                }
                else
                {
                    coin.style.left = (104 - (48 + (count - 1) * 10)) / 2f + i * 10;
                    coin.style.top = 8 - i * 3;
                }
                host.Add(coin);
            }
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
                entry.tooltip = Format("synergy." + group);
                entry.EnableInClassList("active", count >= 3);
                entry.EnableInClassList("inactive", count < 3);
            }
        }

        private VisualElement CharacterCard(string id, int copies, bool showMerge, bool detailed = false,
            int? attack = null, int? health = null, CardInstanceMessage instance = null)
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
            card.userData = instance;
            var tier = copies >= 6 ? 3 : copies >= 3 ? 2 : 1;
            var level = card.Q<Label>("character-level");
            level.style.display = tier >= 2 && definition?.type != "spell" ? DisplayStyle.Flex : DisplayStyle.None;
            level.text = tier >= 2 ? Format("card.mergeLevel", tier) : string.Empty;
            card.RemoveFromClassList("rarity-common");
            card.AddToClassList("rarity-" + (definition?.rarity ?? "common"));
            card.Q<Label>("character-name").text = CardName(id);
            card.Q<Label>("character-group").text = definition == null ? Format("ui.unknown")
                : definition.type == "spell" ? string.Empty : Format("group." + definition.group);
            var ability = card.Q<Label>("character-ability");
            var hasAbility = !string.IsNullOrEmpty(definition?.ability?.kind);
            card.EnableInClassList("no-ability", !hasAbility);
            ability.text = AbilityText(definition, tier, instance);
            if (!hasAbility) ability.style.display = DisplayStyle.None;
            if (definition != null)
            {
                SetCharacterStat(card.Q<Label>("character-attack"), definition.attack + copies - 1);
                SetCharacterStat(card.Q<Label>("character-health"), definition.health + copies - 1);
            }
            else
            {
                card.Q<Label>("character-attack").text = Format("ui.unknown");
                card.Q<Label>("character-health").text = Format("ui.unknown");
            }
            if (attack.HasValue) SetCharacterStat(card.Q<Label>("character-attack"), attack.Value);
            if (health.HasValue) SetCharacterStat(card.Q<Label>("character-health"), health.Value);
            foreach (var number in card.Query<Label>(className: "stat-number").ToList())
                number.EnableInClassList("wide-number", number.text.Length >= 3);
            var drops = tier == 3 ? 0 : tier == 2 ? copies - 3 : copies;
            for (var i = 0; i < 3; i++)
                card.Q<VisualElement>("merge-" + i).EnableInClassList("filled", i < drops);
            foreach (var name in new[] { "character-class", "detail-class-icon", "character-art" })
            {
                var icon = card.Q<VisualElement>(name);
                icon.RemoveFromClassList("group-pack");
                if (definition != null && definition.type != "spell") icon.AddToClassList("group-" + definition.group);
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
            var portrait = Resources.Load<Texture2D>("Characters/" + (id == "packCub" ? "pack-cub-v1" : id));
            if (portrait != null)
            {
                art.style.backgroundImage = new StyleBackground(portrait);
                art.AddToClassList("has-portrait");
            }
            if (definition?.type == "spell")
            {
                card.AddToClassList("spell-card");
                var spellArt = SpellPortraits.TryGetValue(id, out var resource) ? resource : id;
                art.style.backgroundImage = new StyleBackground(Resources.Load<Texture2D>("Spells/" + spellArt));
            }
            if (!detailed) BindCharacterDetails(card, id, copies);
            return card;
        }

        private bool IsSpell(string id) => !string.IsNullOrEmpty(id)
            && _catalog.TryGetValue(id, out var definition) && definition.type == "spell";

        private VisualElement InstanceCard(CardInstanceMessage card, bool showMerge)
        {
            return CharacterCard(card?.cardId, card?.copies ?? 1, showMerge, false,
                card?.attack, card?.health, card);
        }

        private string AbilityText(CardDefinitionMessage definition, int tier, CardInstanceMessage instance = null)
        {
            var ability = definition?.ability;
            if (string.IsNullOrEmpty(ability?.kind)) return string.Empty;
            var amount = ability.kind.StartsWith("play", StringComparison.Ordinal) ? ability.amount : ability.amount * tier;
            if (ability.kind == "applyNut") amount = ability.amount + (_state?.self?.nutAttackBonus ?? 0);
            if (ability.kind == "growingAttackPoison") amount = ability.amount;
            var charges = ability.kind == "reflectDamage" ? ability.amount * tier : ability.charges;
            var growth = ability.kind == "growingAttackPoison" ? ability.growth * tier : ability.growth;
            var healthAmount = ability.kind == "applyNut"
                ? ability.amount + (_state?.self?.nutHealthBonus ?? 0)
                : ability.healthAmount * tier;
            return Format("ability." + ability.kind, amount, healthAmount,
                ability.bothAmount * tier, charges, ability.attackAmount,
                instance?.cacheProgress ?? 0, growth, instance?.playedCreaturesThisRound ?? 0);
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
            if (_detailsLayer == null || _detailsHost == null || _ghost != null) return;
            HideSynergyHelp();
            _touchDetails = touch;
            _detailsAnchor = anchor;
            ForgetStatLabels(_detailsHost);
            _detailsHost.Clear();
            var detail = CharacterCard(id, copies, false, true, instance: anchor.userData as CardInstanceMessage);
            detail.focusable = false;
            _detailsHost.Add(detail);
            SyncDetailStats();
            _detailsLayer.RemoveFromClassList("hidden");
            _detailsLayer.EnableInClassList("touch-details", touch);
            foreach (var element in _detailsLayer.Query<VisualElement>().ToList())
                element.pickingMode = touch ? PickingMode.Position : PickingMode.Ignore;
            _detailsLayer.pickingMode = touch ? PickingMode.Position : PickingMode.Ignore;
            var rootBounds = _root.contentRect;
            var source = _root.WorldToLocal(anchor.worldBound.max);
            var x = touch ? (rootBounds.width - 280) / 2 : source.x + 24;
            if (x + 304 > rootBounds.width)
                x = _root.WorldToLocal(anchor.worldBound.min).x - 304;
            var y = touch ? (rootBounds.height - 468) / 2 : _root.WorldToLocal(anchor.worldBound.min).y;
            x = Mathf.Clamp(x, 24, Mathf.Max(24, rootBounds.width - 304));
            y = Mathf.Clamp(y, 24, Mathf.Max(24, rootBounds.height - 468));
            _detailsHost.style.left = x;
            _detailsHost.style.top = y;
            var close = _root.Q<Button>("close-card-details");
            close.text = Format("card.close");
            close.EnableInClassList("hidden", !touch);
            close.style.left = x + 80;
            close.style.top = y + 420;
        }

        private void SetCharacterStat(Label label, int value)
        {
            _statValues[label] = value;
            label.text = Format("ui.number", value);
            label.EnableInClassList("wide-number", label.text.Length >= 3);
        }

        private void SyncDetailStats(bool animate = false, float pace = 1f)
        {
            if (_detailsAnchor == null || _detailsHost == null) return;
            var sourceAbility = _detailsAnchor.Q<Label>("character-ability");
            var targetAbility = _detailsHost.Q<Label>("character-ability");
            if (sourceAbility != null && targetAbility != null) targetAbility.text = sourceAbility.text;
            foreach (var name in new[] { "character-attack", "character-health" })
            {
                var source = _detailsAnchor.Q<Label>(name);
                var target = _detailsHost.Q<Label>(name);
                if (source == null || target == null) continue;
                if (_statValues.TryGetValue(source, out var value)) ApplyStatValue(target, value, animate, source.ClassListContains("stat-poison"), pace);
                else target.text = source.text;
                target.EnableInClassList("wide-number", source.ClassListContains("wide-number"));
            }
        }

        private void CloseCardDetails()
        {
            _detailsLayer?.AddToClassList("hidden");
            ForgetStatLabels(_detailsHost);
            _detailsHost?.Clear();
            _detailsAnchor = null;
            _touchDetails = false;
        }
    }
}
