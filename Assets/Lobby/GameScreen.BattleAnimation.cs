using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace AnimalBattler.Client.Lobby
{
    internal sealed partial class GameScreen
    {
        private readonly Dictionary<string, BattleCardMessage> _battleDefinitions = new Dictionary<string, BattleCardMessage>();
        private readonly HashSet<string> _battleDead = new HashSet<string>();
        private int _battleStep = -1;
        private int _battlePhase = -1;
        private bool _battleSeek;
        private VisualElement _battleFx;
        private VisualElement _routeReveal;
        private VisualElement _routeArt;
        private VisualElement _battleImpact;
        private string _lastRoute;
        private bool _routeAvailable;

        private void ResetBattlePlayback()
        {
            HideBattleRoute();
            _battleStep = -1;
            _battlePhase = -1;
            _battleDefinitions.Clear();
            _battleDead.Clear();
            _lastRoute = null;
            _battleFx = null;
            _routeReveal = null;
            _routeArt = null;
            _battleImpact = null;
        }

        private void InitializeBattleCards()
        {
            ResetBattlePlayback();
            _battleSeek = Time.realtimeSinceStartupAsDouble - _battleStartedAt > 0.15;
            _battleFx = _root.Q<VisualElement>("battle-fx-layer");
            _routeReveal = _root.Q<VisualElement>("attack-line");
            _routeArt = _root.Q<VisualElement>("attack-line-art");
            _battleImpact = _root.Q<VisualElement>("battle-impact");
            if (_battleImpact != null)
                _battleImpact.style.backgroundImage = new StyleBackground(Resources.Load<VectorImage>("Battle/impact"));
            foreach (var card in _state.battle?.openingCards ?? Array.Empty<BattleCardMessage>()) _battleDefinitions[card.id] = card;
            var initial = _state.battle?.unbuffedCards;
            if (initial == null || initial.Length == 0) initial = _state.battle?.openingCards;
            ApplyOpeningStats(initial, false, 1);
        }

        private void TickBattlePlayback()
        {
            if (_state?.phase != "battle" || _state.battle?.events == null) return;
            var events = _state.battle.events;
            var elapsed = Math.Max(0, Time.realtimeSinceStartupAsDouble - _battleStartedAt);
            var passed = 0.0;
            var length = 3.0;
            var index = events.Length;
            for (var i = 0; i < events.Length; i++)
            {
                length = passed < 30 ? 3.0 : passed < 60 ? 1.5 : 0.75;
                if (elapsed < passed + length) { index = i; break; }
                passed += length;
            }
            if (index == events.Length)
            {
                if (_battleStep != index)
                {
                    if (events.Length > 0) ApplyEventAfter(events[events.Length - 1], false, 1, true);
                    else ApplyOpeningStats(_state.battle.openingCards, !_battleSeek, 1);
                    foreach (var exchange in events)
                        foreach (var id in exchange.deadCardIds ?? Array.Empty<string>()) _battleDead.Add(id);
                    _battleStep = index;
                    UpdateBattleHighlights(null, 1);
                    HideBattleRoute();
                }
                _battleSeek = false;
                SyncDetailStats();
                return;
            }

            var exchangeEvent = events[index];
            var progress = Mathf.Clamp01((float)((elapsed - passed) / length));
            var pace = (float)length / 3f;
            var skipped = _battleSeek || (_battleStep >= 0 && index > _battleStep + 1);
            var phase = progress < 0.12f ? 0 : progress < 0.24f ? 1 : progress < 0.54f ? 2 : progress < 0.74f ? 3 : 4;
            if (index != _battleStep)
            {
                _battleDead.Clear();
                for (var i = 0; i < index; i++)
                    foreach (var id in events[i].deadCardIds ?? Array.Empty<string>()) _battleDead.Add(id);
                if (index > 0) ApplyEventAfter(events[index - 1], false, pace, true);
                _battleStep = index;
                _battlePhase = -1;
            }
            if (phase != _battlePhase)
            {
                var animate = !skipped;
                if (phase <= 1)
                {
                    if (index > 0 || phase == 1)
                    {
                        if (index == 0) ApplyOpeningStats(_state.battle.openingCards, animate, pace);
                        ApplyBattleStats(exchangeEvent.statsBefore, animate, pace, true, true, exchangeEvent);
                    }
                }
                else if (phase <= 3)
                {
                    ApplyBattleStats(exchangeEvent.statsOnHit, animate, pace, true, true, exchangeEvent, true);
                    if (phase == 3) ApplyEventAfter(exchangeEvent, animate, pace, false);
                }
                else ApplyEventAfter(exchangeEvent, animate, pace, true);
                _battlePhase = phase;
            }
            if (progress >= 0.54f)
                foreach (var id in exchangeEvent.deadCardIds ?? Array.Empty<string>()) _battleDead.Add(id);
            UpdateBattleHighlights(exchangeEvent, progress);
            DrawBattleRoute(exchangeEvent, progress);
            _battleSeek = false;
            SyncDetailStats(true, pace);
        }

        private void ApplyOpeningStats(BattleCardMessage[] cards, bool animate, float pace)
        {
            foreach (var card in cards ?? Array.Empty<BattleCardMessage>())
                if (_battleCards.TryGetValue(card.id, out var visual))
                {
                    ApplyStatValue(visual.Q<Label>("character-attack"), card.attack, animate, false, pace);
                    ApplyStatValue(visual.Q<Label>("character-health"), card.health, animate, false, pace);
                }
        }

        private void ApplyBattleStats(BattleStatMessage[] stats, bool animate, float pace, bool attack, bool health, BattleEventMessage exchange, bool poisonEffects = false)
        {
            foreach (var stat in stats ?? Array.Empty<BattleStatMessage>())
            {
                if (_battleCards.TryGetValue(stat.id, out var visual))
                {
                    var showAnimation = animate && !_battleDead.Contains(stat.id);
                    var poison = poisonEffects && (stat.id == exchange.attackerId ? exchange.poisonToAttacker > 0
                        : stat.id == exchange.targetId && exchange.poisonToTarget > 0);
                    if (attack) ApplyStatValue(visual.Q<Label>("character-attack"), stat.attack, showAnimation, poison, pace);
                    if (health) ApplyStatValue(visual.Q<Label>("character-health"), stat.health, showAnimation, false, pace);
                }
            }
        }

        private void ApplyEventAfter(BattleEventMessage exchange, bool animate, float pace, bool includeAttack)
        {
            if (exchange.statsAfter != null && exchange.statsAfter.Length > 0)
                ApplyBattleStats(exchange.statsAfter, animate, pace, includeAttack, true, exchange);
            else
            {
                if (_battleCards.TryGetValue(exchange.attackerId, out var attacker))
                    ApplyStatValue(attacker.Q<Label>("character-health"), exchange.attackerHealthAfter, animate, false, pace);
                if (_battleCards.TryGetValue(exchange.targetId, out var target))
                    ApplyStatValue(target.Q<Label>("character-health"), exchange.targetHealthAfter, animate, false, pace);
            }
        }

        private void ApplyStatValue(Label label, int value, bool animate, bool poison, float pace)
        {
            if (animate) AnimateStat(label, value, poison, pace);
            else
            {
                for (var i = _statTweens.Count - 1; i >= 0; i--)
                    if (_statTweens[i].Number == label) { FinishStatTween(_statTweens[i]); _statTweens.RemoveAt(i); }
                SetCharacterStat(label, value);
            }
        }

        private void UpdateBattleHighlights(BattleEventMessage exchange, float progress)
        {
            foreach (var pair in _battleCards)
            {
                var attacker = exchange != null && pair.Key == exchange.attackerId;
                var target = exchange != null && pair.Key == exchange.targetId;
                var active = progress < 0.88f;
                pair.Value.EnableInClassList("battle-attacker", attacker && active);
                pair.Value.EnableInClassList("battle-target", target && active);
                var emphasis = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0, 0.20f, progress))
                    * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.72f, 0.92f, progress)));
                var scale = attacker ? 1 + emphasis * 0.08f : 1f;
                pair.Value.style.scale = new Scale(new Vector3(scale, scale, 1));
                var impact = target && progress >= 0.54f && progress < 0.68f
                    ? Mathf.Sin((progress - 0.54f) / 0.14f * Mathf.PI * 4) * 3 : 0;
                pair.Value.style.translate = new Translate(impact, 0, 0);
                var diedHere = exchange != null && (attacker || target)
                    && Array.IndexOf(exchange.deadCardIds ?? Array.Empty<string>(), pair.Key) >= 0;
                var deathFade = diedHere ? Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.60f, 0.82f, progress)) : 1f;
                pair.Value.style.opacity = _battleDead.Contains(pair.Key) ? Mathf.Lerp(1, 0.25f, deathFade) : 1f;
            }
        }

        private void DrawBattleRoute(BattleEventMessage exchange, float progress)
        {
            if (_battleFx == null || _routeReveal == null || _routeArt == null || _battleImpact == null
                || !_battleCards.TryGetValue(exchange.attackerId, out var attacker)
                || !_battleCards.TryGetValue(exchange.targetId, out var target)
                || !_battleDefinitions.TryGetValue(exchange.attackerId, out var sourceDefinition)
                || !_battleDefinitions.TryGetValue(exchange.targetId, out var targetDefinition)) return;
            if (attacker.layout.width <= 0 || target.layout.width <= 0
                || float.IsNaN(attacker.layout.width) || float.IsNaN(target.layout.width)) return;
            var a = _battleFx.WorldToLocal(attacker.parent.LocalToWorld(attacker.layout.center));
            var b = _battleFx.WorldToLocal(target.parent.LocalToWorld(target.layout.center));
            var up = a.y > b.y;
            a.y += (up ? -1 : 1) * (attacker.layout.height / 2 + 8);
            b.y += (up ? 1 : -1) * (target.layout.height / 2 + 8);
            var route = (up ? "up-" : "down-") + sourceDefinition.slot + "-" + targetDefinition.slot;
            if (_lastRoute != route)
            {
                _lastRoute = route;
                var asset = Resources.Load<VectorImage>("Battle/Routes/" + route);
                _routeAvailable = asset != null;
                _routeArt.style.backgroundImage = asset == null ? new StyleBackground(StyleKeyword.None) : new StyleBackground(asset);
                if (asset == null) Debug.LogWarning("Battle route could not be loaded as VectorImage. Check the asset and SVG import type: " + route);
            }
            var width = Math.Abs(a.x - b.x) < 1 ? 140 : Math.Abs(a.x - b.x) / 0.8f;
            var height = Math.Max(1, Math.Abs(a.y - b.y) / 0.8f);
            var left = Math.Min(a.x, b.x) - (Math.Abs(a.x - b.x) < 1 ? width / 2 : width * 0.1f);
            var top = Math.Min(a.y, b.y) - height * 0.1f;
            var draw = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.28f, 0.54f, progress));
            _routeReveal.style.left = left;
            _routeReveal.style.width = width;
            _routeReveal.style.height = height * draw;
            _routeReveal.style.top = up ? new StyleLength(StyleKeyword.Auto) : new StyleLength(top);
            _routeReveal.style.bottom = up ? new StyleLength(_battleFx.contentRect.height - top - height) : new StyleLength(StyleKeyword.Auto);
            _routeReveal.style.opacity = _routeAvailable ? 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.62f, 0.84f, progress)) : 0;
            _routeArt.style.width = width;
            _routeArt.style.height = height;
            _routeArt.style.top = up ? new StyleLength(StyleKeyword.Auto) : new StyleLength(0f);
            _routeArt.style.bottom = up ? new StyleLength(0f) : new StyleLength(StyleKeyword.Auto);
            var hit = Mathf.InverseLerp(0.54f, 0.74f, progress);
            _battleImpact.style.left = b.x - 48;
            _battleImpact.style.top = b.y - 48;
            _battleImpact.style.opacity = progress >= 0.54f && progress < 0.74f ? Mathf.Sin(hit * Mathf.PI) : 0;
            var scale = 0.65f + hit * 0.5f;
            _battleImpact.style.scale = new Scale(new Vector3(scale, scale, 1));
        }

        private void HideBattleRoute()
        {
            if (_routeReveal != null) _routeReveal.style.opacity = 0;
            if (_battleImpact != null) _battleImpact.style.opacity = 0;
        }
    }
}
