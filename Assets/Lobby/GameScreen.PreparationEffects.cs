using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace AnimalBattler.Client.Lobby
{
    internal sealed partial class GameScreen
    {
        private readonly Dictionary<string, VisualElement> _preparationCards = new Dictionary<string, VisualElement>();
        private readonly VisualElement[] _preparationOffers = new VisualElement[5];
        private readonly List<PreparationChange> _preparationChanges = new List<PreparationChange>();
        private readonly List<PreparationEffect> _preparationEffects = new List<PreparationEffect>();
        private VisualElement _preparationFx;
        private double _dragLiftStarted;

        private enum PreparationChangeKind { Arrival, Merge, Sale, Refresh, Freeze, Thaw, Wallet }
        private enum PreparationEffectKind { Pop, Burst, Sale, Return, Frost, Coin, Wallet }

        private sealed class PreparationChange
        {
            public PreparationChangeKind Kind;
            public CardInstanceMessage Card;
            public int Slot;
            public int Delta;
            public bool TierUp;
            public Vector2 Origin;
            public Vector2 Size;
        }

        private sealed class PreparationEffect
        {
            public PreparationEffectKind Kind;
            public VisualElement Art;
            public VisualElement Target;
            public VisualElement OriginTarget;
            public Vector2 Origin;
            public Vector2 Size;
            public double Started = -1;
            public float Duration;
            public float Delay;
            public bool Frozen;
        }

        private static IEnumerable<CardInstanceMessage> PreparationInventory(MatchSelfMessage self)
        {
            return (self.hand ?? Array.Empty<CardInstanceMessage>()).Concat(self.board ?? Array.Empty<CardInstanceMessage>())
                .Where(card => !string.IsNullOrEmpty(card?.id));
        }

        private void CapturePreparationEffects(ServerMessage previous, ServerMessage current)
        {
            _preparationChanges.Clear();
            if (previous?.phase != "preparation" || current.phase != "preparation" || previous.round != current.round
                || _sceneName != "Preparation" || _root == null || previous.self.playerId != current.self.playerId) return;
            var before = PreparationInventory(previous.self).ToDictionary(card => card.id);
            var after = PreparationInventory(current.self).ToDictionary(card => card.id);
            var delta = current.self.coins - previous.self.coins;
            foreach (var card in after.Values)
            {
                if (!before.TryGetValue(card.id, out var old))
                    _preparationChanges.Add(new PreparationChange { Kind = PreparationChangeKind.Arrival, Card = card });
                else if (old.copies < card.copies)
                    _preparationChanges.Add(new PreparationChange { Kind = PreparationChangeKind.Merge, Card = card, TierUp = old.tier < card.tier });
                else
                {
                    var oldSlot = Array.FindIndex(previous.self.board ?? Array.Empty<CardInstanceMessage>(), item => item?.id == card.id);
                    var newSlot = Array.FindIndex(current.self.board ?? Array.Empty<CardInstanceMessage>(), item => item?.id == card.id);
                    if (oldSlot != newSlot) _preparationChanges.Add(new PreparationChange { Kind = PreparationChangeKind.Arrival, Card = card });
                }
            }
            if (delta > 0)
                foreach (var card in before.Values.Where(card => !after.ContainsKey(card.id)))
                    if (_preparationCards.TryGetValue(card.id, out var visual) && visual.layout.width > 0)
                        _preparationChanges.Add(new PreparationChange { Kind = PreparationChangeKind.Sale, Card = card,
                            Origin = _root.WorldToLocal(visual.worldBound.center), Size = visual.layout.size });
            for (var slot = 0; slot < 5; slot++)
            {
                var old = previous.self.shop != null && slot < previous.self.shop.Length ? previous.self.shop[slot] : null;
                var offer = current.self.shop != null && slot < current.self.shop.Length ? current.self.shop[slot] : null;
                if (string.IsNullOrEmpty(offer?.cardId)) continue;
                if (old?.cardId == offer.cardId && old.frozen != offer.frozen)
                    _preparationChanges.Add(new PreparationChange { Kind = offer.frozen ? PreparationChangeKind.Freeze : PreparationChangeKind.Thaw, Slot = slot });
                else if (delta == -1 && old?.frozen != true)
                    _preparationChanges.Add(new PreparationChange { Kind = PreparationChangeKind.Refresh, Slot = slot });
            }
            if (delta != 0) _preparationChanges.Add(new PreparationChange { Kind = PreparationChangeKind.Wallet, Delta = delta });
        }

        private VisualElement PreparationSprite(string resource, bool texture = false)
        {
            var art = new VisualElement { pickingMode = PickingMode.Ignore };
            art.AddToClassList("preparation-effect-art");
            if (texture) art.AddToClassList("coin-icon");
            else art.style.backgroundImage = new StyleBackground(Resources.Load<VectorImage>(resource));
            _preparationFx.Add(art);
            return art;
        }

        private void StartPreparationEffects()
        {
            _preparationFx = _root.Q<VisualElement>("preparation-fx");
            if (_preparationFx == null) { _preparationChanges.Clear(); return; }
            var sounds = new HashSet<string>();
            foreach (var change in _preparationChanges)
            {
                var target = change.Card != null && _preparationCards.TryGetValue(change.Card.id, out var card) ? card : null;
                switch (change.Kind)
                {
                    case PreparationChangeKind.Arrival:
                        if (target != null)
                        {
                            AddPreparationEffect(PreparationEffectKind.Pop, target, target, 0.26f);
                            var hand = _root.Q<ScrollView>("hand");
                            if (hand.contentContainer.Contains(target))
                            {
                                var arrived = target;
                                hand.schedule.Execute(() => { if (arrived.panel != null) hand.ScrollTo(arrived); });
                            }
                        }
                        break;
                    case PreparationChangeKind.Merge:
                        if (target == null) break;
                        var burst = PreparationSprite(change.TierUp ? "Auction/sparks" : "Preparation/merge-burst");
                        AddPreparationEffect(PreparationEffectKind.Burst, burst, target, change.TierUp ? 0.6f : 0.38f);
                        target.EnableInClassList("preparation-tier-up", change.TierUp);
                        sounds.Add(change.TierUp ? "tier-up" : "merge");
                        break;
                    case PreparationChangeKind.Sale:
                        var sold = CharacterCard(change.Card.cardId, change.Card.copies, false);
                        DisableRevealPicking(sold);
                        sold.style.position = Position.Absolute;
                        sold.style.opacity = 0;
                        _preparationFx.Add(sold);
                        var sale = _root.Q<VisualElement>("sale-zone");
                        var flight = AddPreparationEffect(PreparationEffectKind.Sale, sold, sale, 0.24f);
                        flight.Origin = change.Origin;
                        flight.Size = change.Size;
                        var coin = AddPreparationEffect(PreparationEffectKind.Coin, PreparationSprite(null, true), _root.Q<Label>("self-coins"), 0.28f);
                        coin.Origin = _root.WorldToLocal(sale.worldBound.center);
                        coin.OriginTarget = sale;
                        coin.Delay = 0.16f;
                        sounds.Add("sell");
                        break;
                    case PreparationChangeKind.Refresh:
                        target = _preparationOffers[change.Slot];
                        var refresh = AddPreparationEffect(PreparationEffectKind.Pop, target, target, 0.25f);
                        refresh.Delay = change.Slot * 0.035f;
                        sounds.Add("refresh");
                        break;
                    case PreparationChangeKind.Freeze:
                    case PreparationChangeKind.Thaw:
                        target = _preparationOffers[change.Slot];
                        var frost = target.Q<VisualElement>(className: "frost-overlay");
                        frost.style.display = DisplayStyle.Flex;
                        frost.style.opacity = change.Kind == PreparationChangeKind.Freeze ? 0 : 1;
                        var icing = AddPreparationEffect(PreparationEffectKind.Frost, frost, target, 0.28f);
                        icing.Frozen = change.Kind == PreparationChangeKind.Freeze;
                        if (!icing.Frozen) AddPreparationEffect(PreparationEffectKind.Burst, PreparationSprite("Preparation/ice-shards"), target, 0.34f);
                        sounds.Add(icing.Frozen ? "freeze" : "thaw");
                        break;
                    case PreparationChangeKind.Wallet:
                        var delta = new Label(Format("ui.statDelta", change.Delta)) { pickingMode = PickingMode.Ignore };
                        delta.AddToClassList("preparation-wallet-delta");
                        delta.EnableInClassList("preparation-spend", change.Delta < 0);
                        _preparationFx.Add(delta);
                        AddPreparationEffect(PreparationEffectKind.Wallet, delta, _root.Q<Label>("self-coins"), 0.5f);
                        if (change.Delta == -3) sounds.Add("buy");
                        break;
                }
            }
            _preparationChanges.Clear();
            foreach (var sound in sounds) _playUiSound?.Invoke("Preparation/" + sound);
        }

        private PreparationEffect AddPreparationEffect(PreparationEffectKind kind, VisualElement art, VisualElement target, float duration)
        {
            var effect = new PreparationEffect { Kind = kind, Art = art, Target = target, Duration = duration };
            if (kind == PreparationEffectKind.Pop)
            {
                art.style.scale = new Scale(Vector3.one * 0.91f);
                art.style.translate = new Translate(0, 9, 0);
                art.style.opacity = 0.3f;
            }
            else if (kind == PreparationEffectKind.Burst || kind == PreparationEffectKind.Wallet) art.style.opacity = 0;
            _preparationEffects.Add(effect);
            return effect;
        }

        private void ReturnDragGhost(VisualElement ghost, VisualElement source)
        {
            _preparationFx = _root.Q<VisualElement>("preparation-fx");
            if (_preparationFx == null) { ForgetStatLabels(ghost); ghost.RemoveFromHierarchy(); return; }
            var origin = _root.WorldToLocal(ghost.worldBound.center);
            var size = new Vector2(ghost.style.width.value.value, ghost.style.height.value.value);
            _preparationFx.Add(ghost);
            source.style.opacity = 0;
            var effect = AddPreparationEffect(PreparationEffectKind.Return, ghost, source, 0.17f);
            effect.Origin = origin;
            effect.Size = size;
        }

        private static float PreparationBackOut(float t)
        {
            var value = Mathf.Clamp01(t) - 1;
            return 1 + 2.70158f * value * value * value + 1.70158f * value * value;
        }

        private void TickPreparationEffects()
        {
            if (_state?.phase != "preparation" || _sceneName != "Preparation") return;
            var now = Time.realtimeSinceStartupAsDouble;
            if (_ghost != null)
            {
                var lift = Mathf.SmoothStep(0, 1, (float)(now - _dragLiftStarted) / 0.12f);
                _ghost.style.scale = new Scale(Vector3.one * (1 + lift * 0.06f));
                _ghost.style.rotate = new Rotate(new Angle(-2 * lift, AngleUnit.Degree));
            }
            for (var i = _preparationEffects.Count - 1; i >= 0; i--)
            {
                var effect = _preparationEffects[i];
                if (effect.Target.panel == null) { FinishPreparationEffect(effect); _preparationEffects.RemoveAt(i); continue; }
                if (_preparationFx == null || _preparationFx.layout.width <= 0 || effect.Target.layout.width <= 0
                    || float.IsNaN(_preparationFx.layout.width) || float.IsNaN(effect.Target.layout.width)) continue;
                if (effect.Started < 0) effect.Started = now;
                var t = Mathf.Clamp01(((float)(now - effect.Started) - effect.Delay) / effect.Duration);
                var eased = Mathf.SmoothStep(0, 1, t);
                var end = _preparationFx.WorldToLocal(effect.Target.worldBound.center);
                switch (effect.Kind)
                {
                    case PreparationEffectKind.Pop:
                        effect.Art.style.scale = new Scale(Vector3.one * (0.91f + PreparationBackOut(t) * 0.09f));
                        effect.Art.style.translate = new Translate(0, 9 * (1 - eased), 0);
                        effect.Art.style.opacity = 0.3f + eased * 0.7f;
                        break;
                    case PreparationEffectKind.Burst:
                        PositionRevealFx(effect.Art, end, 220, 220);
                        effect.Art.style.scale = new Scale(Vector3.one * (0.65f + eased * 0.5f));
                        effect.Art.style.opacity = Mathf.Sin(t * Mathf.PI) * 0.85f;
                        break;
                    case PreparationEffectKind.Frost:
                        effect.Art.style.opacity = effect.Frozen ? eased : 1 - eased;
                        effect.Art.style.scale = new Scale(Vector3.one * (effect.Frozen ? 1.08f - eased * 0.08f : 1 + eased * 0.08f));
                        break;
                    case PreparationEffectKind.Return:
                    case PreparationEffectKind.Sale:
                    case PreparationEffectKind.Coin:
                        var start = _preparationFx.WorldToLocal(_root.LocalToWorld(effect.Origin));
                        if (effect.OriginTarget != null) start = _preparationFx.WorldToLocal(effect.OriginTarget.worldBound.center);
                        var point = Vector2.Lerp(start, end, eased);
                        if (effect.Kind == PreparationEffectKind.Coin) point.y -= Mathf.Sin(eased * Mathf.PI) * 65;
                        var size = effect.Kind == PreparationEffectKind.Coin ? Vector2.one * 36 : effect.Size;
                        PositionRevealFx(effect.Art, point, size.x, size.y);
                        var scale = effect.Kind == PreparationEffectKind.Sale ? 1 - eased * 0.72f : effect.Kind == PreparationEffectKind.Return ? 1.06f - eased * 0.06f : 1;
                        effect.Art.style.scale = new Scale(Vector3.one * scale);
                        effect.Art.style.opacity = now - effect.Started < effect.Delay ? 0 : effect.Kind == PreparationEffectKind.Sale ? 1 - eased : 1;
                        effect.Art.style.rotate = new Rotate(new Angle(effect.Kind == PreparationEffectKind.Coin ? eased * 240 : effect.Kind == PreparationEffectKind.Return ? -2 * (1 - eased) : -7 * eased, AngleUnit.Degree));
                        break;
                    case PreparationEffectKind.Wallet:
                        PositionRevealFx(effect.Art, end + new Vector2(-20, -32 - eased * 24), 100, 42);
                        effect.Art.style.opacity = Mathf.Sin(t * Mathf.PI);
                        effect.Target.style.scale = new Scale(Vector3.one * (1 + Mathf.Sin(t * Mathf.PI) * 0.14f));
                        break;
                }
                if (t < 1) continue;
                FinishPreparationEffect(effect);
                _preparationEffects.RemoveAt(i);
            }
        }

        private void FinishPreparationEffect(PreparationEffect effect)
        {
            effect.Target.style.scale = new Scale(Vector3.one);
            effect.Target.RemoveFromClassList("preparation-tier-up");
            if (effect.Kind == PreparationEffectKind.Pop)
            {
                effect.Art.style.translate = new Translate(0, 0, 0);
                effect.Art.style.opacity = 1;
            }
            else if (effect.Kind == PreparationEffectKind.Frost)
            {
                effect.Art.style.display = effect.Frozen ? DisplayStyle.Flex : DisplayStyle.None;
                effect.Art.style.opacity = 1;
                effect.Art.style.scale = new Scale(Vector3.one);
            }
            else
            {
                if (effect.Kind == PreparationEffectKind.Return) effect.Target.style.opacity = 1;
                ForgetStatLabels(effect.Art);
                effect.Art.RemoveFromHierarchy();
            }
        }

        private void ClearPreparationEffects()
        {
            foreach (var effect in _preparationEffects) FinishPreparationEffect(effect);
            _preparationEffects.Clear();
            _preparationFx?.Clear();
        }
    }
}
