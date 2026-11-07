using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace AnimalBattler.Client.Lobby
{
    internal sealed partial class GameScreen
    {
        private readonly List<BattleAccent> _battleAccents = new List<BattleAccent>();
        private readonly HashSet<string> _playedBattleCues = new HashSet<string>();
        private VisualElement _battleAccentLayer;
        private VisualElement _battleVerdictMote;
        private Label _battlePlayerDelta;
        private int _accentStep = -1;
        private int _battleSelfHealthBefore;
        private int _battleOpponentHealthBefore;

        private sealed class BattleAccent
        {
            public VisualElement Art;
            public string CardId;
            public float Start;
            public float End;
            public float Size;
            public Vector2 Offset;
        }

        private void CaptureBattlePresentation(ServerMessage previous, ServerMessage current)
        {
            _playedBattleCues.Clear();
            if (current.phase != "battle") return;
            var hasPrevious = previous?.phase == "preparation" && previous.round == current.round
                && previous.self.playerId == current.self.playerId;
            _battleSelfHealthBefore = hasPrevious ? previous.self.health : current.self.health;
            _battleOpponentHealthBefore = hasPrevious ? previous.opponent.health : current.opponent.health;
        }

        private void ResetBattleEffects()
        {
            foreach (var name in new[] { "self-health", "opponent-health" })
            {
                var label = _root?.Q<Label>(name);
                if (label == null) continue;
                label.style.scale = new Scale(Vector3.one);
                label.style.translate = new Translate(0, 0, 0);
                label.style.color = StyleKeyword.Null;
            }
            _battleAccentLayer?.RemoveFromHierarchy();
            _battleAccentLayer = _battleVerdictMote = null;
            _battlePlayerDelta = null;
            _battleAccents.Clear();
            _accentStep = -1;
        }

        private void InitializeBattleEffects()
        {
            if (_battleFx == null) return;
            _battleAccentLayer = new VisualElement { pickingMode = PickingMode.Ignore };
            _battleAccentLayer.AddToClassList("battle-accent-layer");
            _battleFx.Add(_battleAccentLayer);
        }

        private VisualElement BattleEffectArt(string resource)
        {
            var art = new VisualElement { pickingMode = PickingMode.Ignore };
            art.AddToClassList("battle-accent");
            art.style.backgroundImage = new StyleBackground(Resources.Load<VectorImage>("Battle/" + resource));
            _battleAccentLayer.Add(art);
            return art;
        }

        private void AddBattleAccent(string id, string resource, float start, float end, float size, Vector2 offset)
        {
            if (!_battleCards.ContainsKey(id)) return;
            _battleAccents.Add(new BattleAccent
            {
                Art = BattleEffectArt(resource), CardId = id, Start = start, End = end, Size = size, Offset = offset
            });
        }

        private void BuildExchangeAccents(BattleEventMessage exchange, int index)
        {
            foreach (var accent in _battleAccents) accent.Art.RemoveFromHierarchy();
            _battleAccents.Clear();
            _accentStep = index;
            var portrait = new Vector2(0, -24);
            if (exchange.attackDamage > 0) AddBattleAccent(exchange.targetId, "contact-ink", 0.54f, 0.70f, 108, portrait);
            if (exchange.counterDamage > 0) AddBattleAccent(exchange.attackerId, "contact-ink", 0.54f, 0.70f, 94, portrait);
            if (exchange.poisonToAttacker > 0) AddBattleAccent(exchange.attackerId, "poison-strokes", 0.24f, 0.48f, 86, new Vector2(-26, 0));
            if (exchange.poisonToTarget > 0) AddBattleAccent(exchange.targetId, "poison-strokes", 0.24f, 0.48f, 86, new Vector2(-26, 0));
            if (exchange.attackerShieldUsed) AddBattleAccent(exchange.attackerId, "shield-break", 0.51f, 0.76f, 100, portrait);
            if (exchange.targetShieldUsed) AddBattleAccent(exchange.targetId, "shield-break", 0.51f, 0.76f, 100, portrait);
            if (exchange.extraDamageToAttacker > 0) AddBattleAccent(exchange.attackerId, "quills", 0.58f, 0.80f, 106, portrait);
            if (exchange.extraDamageToTarget > 0) AddBattleAccent(exchange.targetId, "quills", 0.58f, 0.80f, 106, portrait);
            foreach (var id in exchange.deadCardIds ?? Array.Empty<string>())
                AddBattleAccent(id, "death-dust", 0.60f, 0.88f, 144, new Vector2(0, 34));
        }

        private Vector2 BattleCardCenter(VisualElement card)
        {
            return _battleAccentLayer.WorldToLocal(card.parent.LocalToWorld(card.layout.center));
        }

        private void DrawExchangeAccents(BattleEventMessage exchange, float progress, int index, bool skipped)
        {
            if (_battleAccentLayer == null || _battleAccentLayer.layout.width <= 0 || float.IsNaN(_battleAccentLayer.layout.width)) return;
            if (_accentStep != index) BuildExchangeAccents(exchange, index);
            foreach (var accent in _battleAccents)
            {
                if (!_battleCards.TryGetValue(accent.CardId, out var card) || card.layout.width <= 0 || float.IsNaN(card.layout.width)) continue;
                var t = Mathf.InverseLerp(accent.Start, accent.End, progress);
                var center = BattleCardCenter(card) + accent.Offset + new Vector2(0, -10 * t);
                PositionRevealFx(accent.Art, center, accent.Size, accent.Size);
                accent.Art.style.opacity = progress >= accent.Start && progress < accent.End
                    ? Mathf.Sin(t * Mathf.PI) * 0.9f : 0;
                accent.Art.style.scale = new Scale(Vector3.one * (0.65f + 0.55f * Mathf.SmoothStep(0, 1, t)));
            }
            if (progress >= 0.24f && (exchange.poisonToAttacker > 0 || exchange.poisonToTarget > 0)) BattleCue(index, "poison", skipped);
            if (progress < 0.54f) return;
            if (exchange.attackDamage > 0 || exchange.counterDamage > 0) BattleCue(index, "hit", skipped);
            if (exchange.attackerShieldUsed || exchange.targetShieldUsed) BattleCue(index, "shield", skipped);
            if (exchange.extraDamageToAttacker > 0 || exchange.extraDamageToTarget > 0) BattleCue(index, "quills", skipped);
            if (exchange.deadCardIds?.Length > 0) BattleCue(index, "fall", skipped);
        }

        private void BattleCue(int index, string name, bool skipped)
        {
            if (_playedBattleCues.Add(index + ":" + name) && !skipped)
                _playUiSound?.Invoke("Battle/" + name);
        }

        private void DrawBattleFinale(float progress, int step, bool skipped)
        {
            var selfIndex = _players.Length > 0 && _players[0].playerId == _state.self.playerId ? 0 : 1;
            var winner = _state.battle.winnerIndex;
            var hasWinner = winner == 0 || winner == 1;
            foreach (var pair in _battleCards)
                pair.Value.EnableInClassList("battle-victor", progress > 0 && hasWinner
                    && !_battleDead.Contains(pair.Key) && _battleDefinitions[pair.Key].playerIndex == winner);
            var healthProgress = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.35f, 0.85f, progress));
            SetText("self-health", "ui.number", Mathf.RoundToInt(Mathf.Lerp(_battleSelfHealthBefore, _state.self.health, healthProgress)));
            SetText("opponent-health", "ui.number", Mathf.RoundToInt(Mathf.Lerp(_battleOpponentHealthBefore, _state.opponent.health, healthProgress)));
            if (progress <= 0 || _battleAccentLayer == null) return;
            SetText("battle-hint", !hasWinner ? "draw" : winner == selfIndex ? "victory" : "defeat");
            if (!hasWinner) return;
            var selfLost = winner != selfIndex;
            var oldHealth = selfLost ? _battleSelfHealthBefore : _battleOpponentHealthBefore;
            var health = selfLost ? _state.self.health : _state.opponent.health;
            if (oldHealth <= health) return;
            var label = _root.Q<Label>(selfLost ? "self-health" : "opponent-health");
            var row = _root.Q<VisualElement>(winner == selfIndex ? "bottom-board" : "top-board");
            if (label.layout.width <= 0 || row.layout.width <= 0 || float.IsNaN(label.layout.width) || float.IsNaN(row.layout.width)) return;
            var end = _battleAccentLayer.WorldToLocal(label.worldBound.center);
            var start = _battleAccentLayer.WorldToLocal(row.worldBound.center);
            var travel = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0, 0.42f, progress));
            if (_battleVerdictMote == null) _battleVerdictMote = BattleEffectArt("verdict-spark");
            var point = Vector2.Lerp(start, end, travel) + new Vector2(0, -Mathf.Sin(travel * Mathf.PI) * 40);
            PositionRevealFx(_battleVerdictMote, point, 52, 52);
            _battleVerdictMote.style.opacity = progress < 0.42f ? Mathf.Sin(travel * Mathf.PI) : 0;
            var pulse = Mathf.Sin(Mathf.InverseLerp(0.35f, 0.9f, progress) * Mathf.PI);
            label.style.scale = new Scale(Vector3.one * (1 + pulse * 0.18f));
            label.style.translate = new Translate(Mathf.Sin(progress * 60) * pulse * 3, 0, 0);
            label.style.color = Color.Lerp(new Color(0.95f, 0.89f, 0.82f), new Color(0.94f, 0.63f, 0.56f), pulse);
            if (_battlePlayerDelta == null)
            {
                _battlePlayerDelta = new Label(Format("ui.statDelta", health - oldHealth)) { pickingMode = PickingMode.Ignore };
                _battlePlayerDelta.AddToClassList("battle-player-delta");
                _battleAccentLayer.Add(_battlePlayerDelta);
            }
            PositionRevealFx(_battlePlayerDelta, end + new Vector2(-16, -36 - healthProgress * 24), 100, 46);
            _battlePlayerDelta.style.opacity = progress >= 0.35f ? Mathf.Sin(healthProgress * Mathf.PI) : 0;
            if (progress >= 0.35f) BattleCue(step, "player-hit", skipped);
        }
    }
}
