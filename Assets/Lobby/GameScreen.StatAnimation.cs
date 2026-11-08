using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace AnimalBattler.Client.Lobby
{
    internal sealed partial class GameScreen
    {
        private readonly Dictionary<Label, int> _statValues = new Dictionary<Label, int>();
        private readonly List<StatTween> _statTweens = new List<StatTween>();
        private readonly List<CardPulse> _cardPulses = new List<CardPulse>();
        private readonly Dictionary<string, CardInstanceMessage> _pendingPreparationCards = new Dictionary<string, CardInstanceMessage>();

        private sealed class StatTween
        {
            public Label Number;
            public Label Delta;
            public int OldValue;
            public int NewValue;
            public double Started;
            public float Duration;
        }

        private sealed class CardPulse
        {
            public VisualElement Card;
            public double Started;
            public float Strength;
            public float Duration;
        }

        private void CapturePreparationChanges(ServerMessage previous, ServerMessage current)
        {
            _pendingPreparationCards.Clear();
            if (previous?.phase != "preparation" || current.phase != "preparation" || previous.round != current.round) return;
            foreach (var card in (previous.self.hand ?? Array.Empty<CardInstanceMessage>())
                .Concat(previous.self.board ?? Array.Empty<CardInstanceMessage>()))
                if (!string.IsNullOrEmpty(card?.id)) _pendingPreparationCards[card.id] = card;
        }

        private void AnimatePreparationCard(VisualElement visual, CardInstanceMessage card)
        {
            if (card == null || IsSpell(card.cardId) || !_pendingPreparationCards.TryGetValue(card.id, out var old)) return;
            _pendingPreparationCards.Remove(card.id);
            if (old.attack == card.attack && old.health == card.health) return;
            var attack = visual.Q<Label>("character-attack");
            var health = visual.Q<Label>("character-health");
            SetCharacterStat(attack, old.attack);
            SetCharacterStat(health, old.health);
            AnimateStat(attack, card.attack);
            AnimateStat(health, card.health);
            var tierUp = old.tier < card.tier;
            _cardPulses.Add(new CardPulse { Card = visual, Started = Time.realtimeSinceStartupAsDouble,
                Strength = tierUp ? 0.14f : 0.07f, Duration = tierUp ? 0.6f : 0.38f });
        }

        private void AnimateStat(Label number, int value, bool poison = false, float pace = 1f)
        {
            if (!_statValues.TryGetValue(number, out var oldValue)) { SetCharacterStat(number, value); return; }
            if (oldValue == value) return;
            for (var i = _statTweens.Count - 1; i >= 0; i--)
                if (_statTweens[i].Number == number) { FinishStatTween(_statTweens[i]); _statTweens.RemoveAt(i); }
            SetCharacterStat(number, value);
            number.text = Format("ui.number", oldValue);
            var delta = new Label(Format("ui.statDelta", value - oldValue)) { pickingMode = PickingMode.Ignore };
            delta.AddToClassList("stat-delta");
            var tone = poison && value < oldValue ? "stat-poison" : value > oldValue ? "stat-gain" : number.name == "character-health" ? "stat-loss" : "stat-expired";
            delta.AddToClassList(tone);
            number.AddToClassList(tone);
            delta.style.position = Position.Absolute;
            number.parent.Add(delta);
            _statTweens.Add(new StatTween
            {
                Number = number, Delta = delta, OldValue = oldValue, NewValue = value,
                Started = Time.realtimeSinceStartupAsDouble, Duration = 0.55f * Mathf.Clamp(pace, 0.25f, 1f)
            });
        }

        private void TickStatAnimations()
        {
            var now = Time.realtimeSinceStartupAsDouble;
            for (var i = _statTweens.Count - 1; i >= 0; i--)
            {
                var tween = _statTweens[i];
                var progress = Mathf.Clamp01((float)(now - tween.Started) / tween.Duration);
                var numberProgress = Mathf.Clamp01(progress / 0.55f);
                tween.Number.text = Format("ui.number", numberProgress < 0.35f ? tween.OldValue : tween.NewValue);
                var pulse = Mathf.Sin(numberProgress * Mathf.PI);
                tween.Number.style.scale = new Scale(new Vector3(1 + pulse * 0.24f, 1 + pulse * 0.24f, 1));
                tween.Delta.style.translate = new Translate(0, -28 * progress, 0);
                tween.Delta.style.opacity = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.55f, 1, progress));
                if (progress >= 1) { FinishStatTween(tween); _statTweens.RemoveAt(i); }
            }
            for (var i = _cardPulses.Count - 1; i >= 0; i--)
            {
                var pulse = _cardPulses[i];
                var progress = Mathf.Clamp01((float)(now - pulse.Started) / pulse.Duration);
                var scale = 1 + Mathf.Sin(progress * Mathf.PI) * pulse.Strength;
                pulse.Card.style.scale = new Scale(new Vector3(scale, scale, 1));
                if (progress >= 1) _cardPulses.RemoveAt(i);
            }
        }

        private void FinishStatTween(StatTween tween)
        {
            tween.Number.text = Format("ui.number", tween.NewValue);
            tween.Number.style.scale = new Scale(Vector3.one);
            foreach (var tone in new[] { "stat-poison", "stat-gain", "stat-loss", "stat-expired" }) tween.Number.RemoveFromClassList(tone);
            tween.Delta.RemoveFromHierarchy();
        }

        private void ClearStatAnimations()
        {
            foreach (var tween in _statTweens) FinishStatTween(tween);
            foreach (var pulse in _cardPulses) pulse.Card.style.scale = new Scale(Vector3.one);
            _statTweens.Clear();
            _cardPulses.Clear();
            _statValues.Clear();
        }

        private void ForgetStatLabels(VisualElement subtree)
        {
            if (subtree == null) return;
            foreach (var label in subtree.Query<Label>(className: "stat-number").ToList())
            {
                for (var i = _statTweens.Count - 1; i >= 0; i--)
                    if (_statTweens[i].Number == label) { FinishStatTween(_statTweens[i]); _statTweens.RemoveAt(i); }
                _statValues.Remove(label);
            }
        }
    }
}
