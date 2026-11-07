using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace AnimalBattler.Client.Lobby
{
    internal sealed partial class GameScreen
    {
        private readonly Dictionary<string, double> _synergyPulses = new Dictionary<string, double>();
        private readonly Dictionary<int, double> _bidPulses = new Dictionary<int, double>();
        private readonly List<VisualElement> _ambientLeaves = new List<VisualElement>();
        private VisualElement _atmosphere;
        private VisualElement _celebration;
        private string _sessionStageKey;
        private double _sessionStageStarted;

        private int SynergyTier(MatchSelfMessage self, string group)
        {
            var count = (self.board ?? Array.Empty<CardInstanceMessage>())
                .Where(card => card != null && !string.IsNullOrEmpty(card.cardId)
                    && _catalog.TryGetValue(card.cardId, out var definition) && definition.group == group)
                .Sum(card => card.copies);
            return count >= 10 ? 3 : count >= 6 ? 2 : count >= 3 ? 1 : 0;
        }

        private void CaptureSessionEffects(ServerMessage previous, ServerMessage current)
        {
            if (previous?.self == null || previous.self.playerId != current.self.playerId) return;
            var now = Time.realtimeSinceStartupAsDouble;
            foreach (var group in ClassGroups)
                if (SynergyTier(previous.self, group) < SynergyTier(current.self, group)) _synergyPulses[group] = now;
            if (previous.phase != "auction" || current.phase != "auction" || previous.round != current.round) return;
            for (var slot = 0; slot < 5; slot++)
            {
                var before = previous.self.bids != null && slot < previous.self.bids.Length ? previous.self.bids[slot] : 0;
                var after = current.self.bids != null && slot < current.self.bids.Length ? current.self.bids[slot] : 0;
                if (before == after) continue;
                _bidPulses[slot] = now;
                _playUiSound?.Invoke(after > before ? "Preparation/buy" : "Preparation/sell");
            }
        }

        private void BindSessionEffects()
        {
            _atmosphere?.RemoveFromHierarchy();
            _ambientLeaves.Clear();
            _atmosphere = new VisualElement { pickingMode = PickingMode.Ignore };
            _atmosphere.AddToClassList("session-atmosphere");
            _root.Insert(Math.Min(2, _root.childCount), _atmosphere);
            for (var i = 0; i < 6; i++)
            {
                var leaf = new VisualElement { pickingMode = PickingMode.Ignore };
                leaf.AddToClassList("session-mote");
                _atmosphere.Add(leaf);
                _ambientLeaves.Add(leaf);
            }
            _celebration?.RemoveFromHierarchy();
            _celebration = null;
        }

        private void ClearSessionEffects()
        {
            _synergyPulses.Clear();
            _bidPulses.Clear();
            _atmosphere?.RemoveFromHierarchy();
            _atmosphere = null;
            _ambientLeaves.Clear();
            _celebration?.RemoveFromHierarchy();
            _celebration = null;
            _sessionStageKey = null;
        }

        private void TickSessionEffects()
        {
            var now = Time.realtimeSinceStartupAsDouble;
            var key = _sceneName + ":" + _state.round + ":" + (_auctionRevealActive ? "reveal" : _state.phase);
            if (_sessionStageKey != key)
            {
                _sessionStageKey = key;
                _sessionStageStarted = now;
            }
            var intro = Mathf.Clamp01((float)(now - _sessionStageStarted) / 0.45f);
            var heading = _root.Q<Label>("stage-heading");
            heading.style.scale = new Scale(Vector3.one * (0.94f + PreparationBackOut(intro) * 0.06f));
            heading.style.translate = new Translate(-12 * (1 - Mathf.SmoothStep(0, 1, intro)), 0, 0);
            foreach (var pair in _synergyPulses.ToArray())
            {
                var t = Mathf.Clamp01((float)(now - pair.Value) / 0.6f);
                var entry = _root.Q<VisualElement>("synergy-" + pair.Key);
                if (entry != null)
                {
                    var pulse = Mathf.Sin(t * Mathf.PI);
                    entry.style.scale = new Scale(Vector3.one * (1 + pulse * 0.025f));
                    entry.style.borderTopColor = entry.style.borderBottomColor = entry.style.borderRightColor
                        = Color.Lerp(new Color(0.47f, 0.4f, 0.45f), new Color(0.39f, 0.86f, 0.77f), pulse);
                    if (t >= 1)
                    {
                        entry.style.borderTopColor = entry.style.borderBottomColor = entry.style.borderRightColor = StyleKeyword.Null;
                        entry.style.scale = new Scale(Vector3.one);
                    }
                }
                if (t >= 1) _synergyPulses.Remove(pair.Key);
            }
            foreach (var pair in _bidPulses.ToArray())
            {
                var t = Mathf.Clamp01((float)(now - pair.Value) / 0.25f);
                var coins = _root.Q<VisualElement>("offer-" + pair.Key)?.Q<VisualElement>("bid-coins");
                if (coins != null) coins.style.scale = new Scale(Vector3.one * (1 + Mathf.Sin(t * Mathf.PI) * 0.12f));
                if (t >= 1) _bidPulses.Remove(pair.Key);
            }
            var seconds = _timer != null && DateTimeOffset.TryParse(_state.endsAtUtc, out var deadline)
                ? (deadline - DateTimeOffset.UtcNow).TotalSeconds : double.MaxValue;
            var urgent = seconds > 0 && seconds <= 10 && !_auctionRevealActive && (_state.phase == "auction" || _state.phase == "preparation");
            if (_timer != null)
            {
                _timer.EnableInClassList("timer-urgent", urgent);
                var pulse = urgent ? Mathf.Pow(Mathf.Max(0, Mathf.Sin((float)seconds * Mathf.PI * 2)), 4) * 0.035f : 0;
                _timer.style.scale = new Scale(Vector3.one * (1 + pulse));
            }
            for (var i = 0; i < _ambientLeaves.Count; i++)
            {
                var t = (float)((now + i * 4.3) % 24 / 24);
                var right = i % 2 == 1;
                var x = (right ? _root.contentRect.width - 20 : 4) + Mathf.Sin(t * Mathf.PI * 2 + i) * 4;
                _ambientLeaves[i].style.left = x;
                _ambientLeaves[i].style.top = (0.12f + t * 0.78f) * _root.contentRect.height;
                _ambientLeaves[i].style.rotate = new Rotate(new Angle(t * 120 + i * 40, AngleUnit.Degree));
                _ambientLeaves[i].style.opacity = Mathf.Sin(t * Mathf.PI) * 0.14f;
            }
            if (_sceneName != "Result") return;
            var result = _root.Q<VisualElement>(className: "result-panel");
            if (result == null) return;
            var reveal = Mathf.Clamp01((float)(now - _sessionStageStarted) / 0.55f);
            result.style.scale = new Scale(Vector3.one * (0.96f + PreparationBackOut(reveal) * 0.04f));
            result.style.translate = new Translate(0, 14 * (1 - reveal), 0);
            result.style.opacity = 0.65f + reveal * 0.35f;
            if (!_root.ClassListContains("result-victory")) return;
            if (_celebration == null)
            {
                _celebration = new VisualElement { pickingMode = PickingMode.Ignore };
                _celebration.AddToClassList("session-celebration");
                _root.Add(_celebration);
            }
            var emblem = _root.Q<VisualElement>(className: "result-emblem");
            if (emblem.layout.width <= 0) return;
            var center = _root.WorldToLocal(emblem.worldBound.center);
            PositionRevealFx(_celebration, center, 380, 380);
            _celebration.style.opacity = Mathf.Sin(reveal * Mathf.PI) * 0.75f;
            _celebration.style.scale = new Scale(Vector3.one * (0.8f + reveal * 0.4f));
        }
    }
}
