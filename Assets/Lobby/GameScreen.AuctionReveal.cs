using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace AnimalBattler.Client.Lobby
{
    internal sealed partial class GameScreen
    {
        private const float RevealLeadIn = 0.3f;
        private const float EmptyLotDuration = 0.6f;
        private const float NormalLotDuration = 1.2f;
        private const float TiedLotDuration = 2f;
        private bool _auctionRevealActive;
        private ServerMessage _auctionBeforeReveal;
        private double _auctionRevealStarted;
        private readonly List<RevealLot> _revealLots = new List<RevealLot>();
        private VisualElement _auctionFxLayer;
        private VisualElement _auctionFlyingCard;
        private VisualElement _auctionCoin;
        private VisualElement _auctionSparks;
        private VisualElement _clashSelf;
        private VisualElement _clashOpponent;
        private VisualElement _auctionRim;
        private VisualElement _auctionSweep;
        private Label _auctionCoinDelta;
        private int _revealDelivered = -1;
        private readonly List<VisualElement> _auctionTrails = new List<VisualElement>();
        private int _revealVisualLot = -1;
        private int _revealSoundLot = -1;
        private bool _revealMainSound;
        private bool _revealCoinSound;
        private bool _revealClashSound;
        private bool _revealLandSound;

        private sealed class RevealLot
        {
            public AuctionResultMessage Result;
            public int SelfBid;
            public int OpponentBid;
            public bool SelfWon;
            public bool Tied;
            public float Duration;
            public VisualElement Card;
            public Label SelfNumber;
            public Label OpponentNumber;
            public Label Status;
        }

        private void CaptureAuctionReveal(ServerMessage previous, ServerMessage current)
        {
            if (_auctionRevealActive)
            {
                if (current.phase != "preparation" || current.round != _auctionBeforeReveal.round) CancelAuctionReveal();
                return;
            }
            if (previous?.phase != "auction" || current.phase != "preparation" || previous.round != current.round
                || _sceneName != "Auction" || current.auctionResults == null || current.auctionResults.Length == 0) return;
            _auctionBeforeReveal = previous;
            _auctionRevealStarted = Time.realtimeSinceStartupAsDouble;
            _revealLots.Clear();
            var selfFirst = _players.Length > 0 && _players[0].playerId == current.self.playerId;
            foreach (var result in current.auctionResults.OrderBy(item => item.slot))
            {
                var selfBid = selfFirst ? result.firstBid : result.secondBid;
                var opponentBid = selfFirst ? result.secondBid : result.firstBid;
                var tied = selfBid > 0 && selfBid == opponentBid;
                _revealLots.Add(new RevealLot
                {
                    Result = result, SelfBid = selfBid, OpponentBid = opponentBid,
                    SelfWon = result.winnerId == current.self.playerId, Tied = tied,
                    Duration = string.IsNullOrEmpty(result.winnerId) ? EmptyLotDuration : tied ? TiedLotDuration : NormalLotDuration
                });
            }
            _revealSoundLot = -1;
            _auctionRevealActive = true;
        }

        private void RenderAuctionReveal()
        {
            _root.AddToClassList("auction-revealing");
            CloseCardDetails();
            RenderMatchSidebar();
            SetText("stage-heading", "auction.revealTitle");
            _root.Q<Label>("auction-hint").AddToClassList("hidden");
            SetText("ready-status", "auction.revealTimerHint");
            var ready = _root.Q<Button>("ready-button");
            ready.text = Format("auction.revealBusy");
            ready.SetEnabled(false);
            _auctionFxLayer = _root.Q<VisualElement>("auction-reveal-fx");
            ClearAuctionFx();
            _revealVisualLot = -1;
            _revealDelivered = -1;
            for (var slot = 0; slot < 5; slot++)
            {
                var offer = _root.Q<VisualElement>("offer-" + slot);
                offer.Q<VisualElement>("bid-controls")?.AddToClassList("hidden");
                var host = offer.Q<VisualElement>("card-host");
                host.Clear();
                var lot = _revealLots.FirstOrDefault(item => item.Result.slot == slot);
                var card = CharacterCard(lot?.Result.cardId, 1, false);
                DisableRevealPicking(card);
                host.Add(card);
                var panel = offer.Q<VisualElement>("reveal-bids");
                panel.EnableInClassList("hidden", lot == null);
                offer.Q<VisualElement>("opponent-bet").EnableInClassList("hidden", lot == null);
                if (lot == null) continue;
                RenderBetCoins(offer.Q<VisualElement>("reveal-self-coins"), lot.SelfBid, true);
                RenderBetCoins(offer.Q<VisualElement>("reveal-opponent-coins"), lot.OpponentBid, true);
                lot.Card = card;
                lot.SelfNumber = offer.Q<Label>("reveal-self-bid");
                lot.OpponentNumber = offer.Q<Label>("reveal-opponent-bid");
                lot.Status = offer.Q<Label>("reveal-lot-status");
                lot.SelfNumber.text = Format("ui.number", lot.SelfBid);
                lot.OpponentNumber.text = Format("ui.number", lot.OpponentBid);
                lot.Status.text = string.Empty;
            }
            RebuildRevealHands(0);
            SetText("self-coins", "ui.number", RevealCoins(0));
        }

        private static void DisableRevealPicking(VisualElement element)
        {
            element.focusable = false;
            element.pickingMode = PickingMode.Ignore;
            element.Query<VisualElement>().ForEach(child => { child.pickingMode = PickingMode.Ignore; child.focusable = false; });
        }

        private int RevealCoins(int completed)
        {
            var coins = _auctionBeforeReveal.self.coins;
            foreach (var lot in _revealLots.Take(completed))
            {
                if (lot.SelfWon) coins -= lot.SelfBid;
                else if (lot.Tied && !string.IsNullOrEmpty(lot.Result.winnerId)) coins++;
            }
            return completed >= _revealLots.Count ? _state.self.coins : coins;
        }

        private void RebuildRevealHands(int completed)
        {
            var own = _root.Q<ScrollView>("auction-hand");
            ForgetStatLabels(own);
            own.Clear();
            foreach (var card in _auctionBeforeReveal.self.hand ?? Array.Empty<CardInstanceMessage>())
            {
                var visual = CharacterCard(card.cardId, card.copies, false);
                DisableRevealPicking(visual);
                own.Add(visual);
            }
            foreach (var lot in _revealLots.Take(completed).Where(item => item.SelfWon))
            {
                var visual = CharacterCard(lot.Result.cardId, 1, false);
                DisableRevealPicking(visual);
                own.Add(visual);
            }
            var opponent = _root.Q<ScrollView>("opponent-hand");
            opponent.Clear();
            var enemyWins = _revealLots.Take(completed).Count(item => !item.SelfWon && !string.IsNullOrEmpty(item.Result.winnerId));
            var enemyCount = (_auctionBeforeReveal.opponent?.handCount ?? 0) + enemyWins;
            for (var i = 0; i < enemyCount; i++)
            {
                var back = new VisualElement { pickingMode = PickingMode.Ignore };
                back.AddToClassList("card-back");
                opponent.Add(back);
            }
            SetText("hand-heading", "ui.number", (_auctionBeforeReveal.self.hand?.Length ?? 0) + _revealLots.Take(completed).Count(item => item.SelfWon));
            SetText("opponent-hand-count", "ui.number", enemyCount);
        }

        private void TickAuctionReveal()
        {
            if (!_auctionRevealActive || _sceneName != "Auction" || _state?.phase != "preparation") return;
            var elapsed = (float)(Time.realtimeSinceStartupAsDouble - _auctionRevealStarted) - RevealLeadIn;
            var index = 0;
            while (index < _revealLots.Count && elapsed >= _revealLots[index].Duration) elapsed -= _revealLots[index++].Duration;
            if (index == _revealLots.Count)
            {
                CancelAuctionReveal();
                _requestScene?.Invoke("Preparation");
                return;
            }
            var lot = _revealLots[index];
            if (_auctionFxLayer == null || _auctionFxLayer.layout.width <= 0 || lot.Card == null || lot.Card.layout.width <= 0) return;
            var progress = Mathf.Clamp01(elapsed / lot.Duration);
            for (var i = 0; i < _revealLots.Count; i++)
            {
                var item = _revealLots[i];
                if (item.Card != null) item.Card.style.opacity = i < index ? 0 : 1;
                if (i >= index) continue;
                var empty = string.IsNullOrEmpty(item.Result.winnerId);
                item.Status.text = Format(empty ? "auction.unclaimed" : item.SelfWon ? "auction.youWon" : "auction.opponentWon");
                item.SelfNumber.EnableInClassList("reveal-tied", false);
                item.OpponentNumber.EnableInClassList("reveal-tied", false);
                item.SelfNumber.EnableInClassList("reveal-winning", !empty && item.SelfWon);
                item.OpponentNumber.EnableInClassList("reveal-winning", !empty && !item.SelfWon);
                item.SelfNumber.EnableInClassList("reveal-losing", !empty && !item.SelfWon);
                item.OpponentNumber.EnableInClassList("reveal-losing", !empty && item.SelfWon);
            }
            if (_revealVisualLot != index)
            {
                ClearAuctionFx();
                _revealVisualLot = index;
                RebuildRevealHands(index);
                _revealDelivered = index;
            }
            if (_revealSoundLot != index)
            {
                _revealSoundLot = index;
                _revealMainSound = _revealCoinSound = _revealClashSound = _revealLandSound = false;
            }
            if (elapsed < 0) return;
            AnimateRevealLot(lot, progress);
            var credited = progress >= 0.94f ? index + 1 : index;
            var delivered = progress >= 0.90f ? index + 1 : index;
            if (_revealDelivered != delivered)
            {
                RebuildRevealHands(delivered);
                _revealDelivered = delivered;
            }
            SetText("self-coins", "ui.number", RevealCoins(credited));
            SetText("available-coins", "ui.number", RevealCoins(credited));
            var wallet = _root.Q<Label>("self-coins");
            var pulse = lot.Tied && !lot.SelfWon ? Mathf.Sin(Mathf.InverseLerp(0.92f, 1, progress) * Mathf.PI) : 0;
            wallet.style.scale = new Scale(new Vector3(1 + pulse * 0.2f, 1 + pulse * 0.2f, 1));
            if (delivered > index && lot.SelfWon)
            {
                var hand = _root.Q<ScrollView>("auction-hand");
                var landed = hand.contentContainer.Children().LastOrDefault();
                if (landed != null) landed.style.scale = new Scale(Vector3.one * (1 + Mathf.Sin(Mathf.InverseLerp(0.90f, 1, progress) * Mathf.PI) * 0.08f));
            }
        }

        private void AnimateRevealLot(RevealLot lot, float progress)
        {
            var empty = string.IsNullOrEmpty(lot.Result.winnerId);
            var payoff = lot.Tied ? 0.52f : 0.25f;
            var winnerShown = !lot.Tied || progress >= payoff;
            var selfWinner = lot.SelfWon && winnerShown;
            var opponentWinner = !lot.SelfWon && winnerShown && !empty;
            foreach (var number in new[] { lot.SelfNumber, lot.OpponentNumber })
            {
                number.EnableInClassList("reveal-tied", lot.Tied && !winnerShown);
                number.EnableInClassList("reveal-winning", number == lot.SelfNumber ? selfWinner : opponentWinner);
                number.EnableInClassList("reveal-losing", winnerShown && !empty && (number == lot.SelfNumber ? !lot.SelfWon : lot.SelfWon));
                var bounceTime = winnerShown ? Mathf.InverseLerp(payoff, payoff + 0.2f, progress) : Mathf.Clamp01(progress / 0.3f);
                var bounce = Mathf.Sin(bounceTime * Mathf.PI) * 0.2f;
                var winning = number == lot.SelfNumber ? selfWinner : opponentWinner;
                number.style.scale = new Scale(Vector3.one * (winning || !winnerShown ? 1 + bounce : 1));
                var shake = !winnerShown || (!lot.SelfWon && number == lot.SelfNumber && progress < 0.3f)
                    ? Mathf.Sin(progress * 65) * 3 : 0;
                number.style.translate = new Translate(shake, winning ? 0 : winnerShown ? 4 : 0, 0);
            }
            lot.Status.text = empty ? Format("auction.unclaimed") : !winnerShown ? Format("auction.tied")
                : Format(lot.SelfWon ? "auction.youWon" : "auction.opponentWon");
            if (empty)
            {
                var fade = Mathf.Pow(Mathf.InverseLerp(0.2f, 1, progress), 3);
                lot.Card.style.opacity = 1 - fade;
                lot.Card.style.scale = new Scale(Vector3.one * (1 - fade * 0.65f));
                lot.Card.style.translate = new Translate(0, fade * 48, 0);
                lot.Card.Q<VisualElement>("character-art").style.unityBackgroundImageTintColor = Color.Lerp(Color.white, new Color(0.55f, 0.52f, 0.54f), progress);
                if (!_revealMainSound && progress >= 0.2f) { _playUiSound?.Invoke("Auction/empty-lot"); _revealMainSound = true; }
                return;
            }
            if (lot.Tied && !winnerShown)
            {
                lot.Card.style.rotate = new Rotate(new Angle(Mathf.Sin(progress * 60) * 4, AngleUnit.Degree));
                AnimateClash(lot, progress);
                if (!_revealClashSound) { _playUiSound?.Invoke("Auction/tie-clash"); _revealClashSound = true; }
            }
            if (lot.SelfWon && winnerShown && progress < payoff) AnimateWinnerRim(lot, progress / payoff);
            if (!_revealMainSound && progress >= payoff)
            {
                _playUiSound?.Invoke(lot.SelfWon ? "Auction/self-win" : "Auction/opponent-win");
                _revealMainSound = true;
            }
            if (progress < payoff) return;
            lot.Card.style.rotate = new Rotate(new Angle(0f));
            AnimateAwardFlight(lot, Mathf.InverseLerp(payoff, 0.90f, progress));
            if (!_revealLandSound && progress >= 0.90f)
            {
                _playUiSound?.Invoke("Auction/card-land");
                _revealLandSound = true;
            }
            if (_auctionRim != null) _auctionRim.style.opacity = 0;
            if (_auctionSparks != null)
            {
                var landing = Mathf.InverseLerp(0.90f, 1, progress);
                _auctionSparks.style.opacity = lot.SelfWon ? 1 - landing : 0;
                _auctionSparks.style.scale = new Scale(Vector3.one * (0.65f + landing * 0.7f));
            }
            if (lot.Tied)
            {
                AnimateCompensation(lot, Mathf.InverseLerp(0.64f, 0.94f, progress));
                if (!_revealCoinSound && progress >= 0.94f) { _playUiSound?.Invoke("Auction/compensation"); _revealCoinSound = true; }
            }
        }

        private Vector2 FxCenter(VisualElement element) => _auctionFxLayer.WorldToLocal(element.worldBound.center);

        private static float CubicOut(float value) => 1 - Mathf.Pow(1 - Mathf.Clamp01(value), 3);

        private static Vector2 RevealArc(Vector2 start, Vector2 end, float progress, float lift)
        {
            var t = CubicOut(progress);
            return Vector2.Lerp(start, end, t) + Vector2.up * (-Mathf.Sin(t * Mathf.PI) * lift);
        }

        private VisualElement AddRevealFx(string className)
        {
            var element = new VisualElement { pickingMode = PickingMode.Ignore };
            element.AddToClassList(className);
            element.style.position = Position.Absolute;
            _auctionFxLayer.Add(element);
            return element;
        }

        private void AnimateWinnerRim(RevealLot lot, float progress)
        {
            if (_auctionRim == null) _auctionRim = AddRevealFx("auction-rim");
            PositionRevealFx(_auctionRim, FxCenter(lot.Card), 198, 230);
            _auctionRim.style.opacity = Mathf.Sin(progress * Mathf.PI);
            lot.Card.style.scale = new Scale(Vector3.one * (1 + Mathf.Sin(progress * Mathf.PI) * 0.05f));
        }

        private void AnimateClash(RevealLot lot, float progress)
        {
            if (_clashSelf == null) _clashSelf = AddRevealFx("auction-clash-self");
            if (_clashOpponent == null) _clashOpponent = AddRevealFx("auction-clash-opponent");
            var end = FxCenter(lot.Card);
            var t = CubicOut(Mathf.InverseLerp(0.04f, 0.28f, progress));
            var own = Vector2.Lerp(FxCenter(lot.SelfNumber), end, t);
            var enemy = Vector2.Lerp(FxCenter(lot.OpponentNumber), end, t);
            PositionRevealFx(_clashSelf, own, 12, 12);
            PositionRevealFx(_clashOpponent, enemy, 12, 12);
            _clashSelf.style.opacity = _clashOpponent.style.opacity = progress < 0.36f ? 1 : 0;
        }

        private void AnimateAwardFlight(RevealLot lot, float progress)
        {
            if (_auctionFlyingCard == null)
            {
                _auctionFlyingCard = CharacterCard(lot.Result.cardId, 1, false);
                DisableRevealPicking(_auctionFlyingCard);
                _auctionFlyingCard.style.position = Position.Absolute;
                _auctionFlyingCard.style.width = 160;
                _auctionFlyingCard.style.height = 192;
                _auctionFxLayer.Add(_auctionFlyingCard);
                _auctionSweep = new VisualElement { pickingMode = PickingMode.Ignore };
                _auctionSweep.AddToClassList("auction-sweep");
                _auctionFlyingCard.Q<VisualElement>(className: "character-frame").Add(_auctionSweep);
                for (var i = 0; i < 2; i++)
                {
                    var trail = AddRevealFx("auction-card-trail");
                    trail.style.backgroundImage = new StyleBackground(Resources.Load<Texture2D>("Characters/" + lot.Result.cardId));
                    _auctionTrails.Add(trail);
                }
                _auctionFlyingCard.BringToFront();
            }
            lot.Card.style.opacity = 0;
            if (_clashSelf != null) _clashSelf.style.opacity = _clashOpponent.style.opacity = 0;
            var start = FxCenter(lot.Card);
            var hand = _root.Q<ScrollView>(lot.SelfWon ? "auction-hand" : "opponent-hand");
            var end = FxCenter(hand);
            var point = RevealArc(start, end, progress, lot.SelfWon ? 85 : 45);
            PositionRevealFx(_auctionFlyingCard, point, 160, 192);
            _auctionFlyingCard.style.scale = new Scale(Vector3.one * Mathf.Lerp(1.08f, 0.65f, CubicOut(progress)));
            _auctionSweep.style.left = Mathf.Lerp(-60, 220, Mathf.Clamp01(progress / 0.45f));
            _auctionSweep.style.opacity = lot.SelfWon ? 1 : 0;
            _auctionFlyingCard.style.rotate = new Rotate(new Angle(Mathf.Sin(progress * Mathf.PI) * (lot.SelfWon ? -7 : 7), AngleUnit.Degree));
            _auctionFlyingCard.EnableInClassList("auction-award-self", lot.SelfWon);
            _auctionFlyingCard.EnableInClassList("auction-award-opponent", !lot.SelfWon && progress > 0.48f);
            _auctionFlyingCard.style.opacity = progress < 1 ? 1 : 0;
            for (var i = 0; i < _auctionTrails.Count; i++)
            {
                var trail = _auctionTrails[i];
                PositionRevealFx(trail, RevealArc(start, end, Mathf.Max(0, progress - (i + 1) * 0.04f), 45), 130, 156);
                trail.style.opacity = !lot.SelfWon && progress > 0.05f && progress < 0.98f ? 0.13f - i * 0.04f : 0;
            }
            if (progress >= 1)
            {
                if (_auctionSparks == null) _auctionSparks = AddRevealFx("auction-sparks");
                PositionRevealFx(_auctionSparks, end, 100, 100);
                _auctionSparks.style.opacity = lot.SelfWon ? 1 : 0;
                var lastCard = hand.contentContainer.Children().LastOrDefault();
                if (lastCard != null) hand.ScrollTo(lastCard);
            }
        }

        private void AnimateCompensation(RevealLot lot, float progress)
        {
            if (_auctionCoin == null)
            {
                _auctionCoin = AddRevealFx("auction-compensation");
                _auctionCoin.AddToClassList("coin-icon");
            }
            var destination = _root.Q<VisualElement>(lot.SelfWon ? "opponent-health" : "self-coins");
            var point = RevealArc(FxCenter(lot.Card), FxCenter(destination), progress, 130);
            point.y -= Mathf.Abs(Mathf.Sin(Mathf.InverseLerp(0.82f, 1, progress) * Mathf.PI * 2)) * 8 * (1 - progress);
            PositionRevealFx(_auctionCoin, point, 42, 42);
            _auctionCoin.style.opacity = progress > 0 && progress < 1 ? 1 : 0;
            _auctionCoin.style.rotate = new Rotate(new Angle(progress * 300, AngleUnit.Degree));
            if (_auctionCoinDelta == null && !lot.SelfWon)
            {
                _auctionCoinDelta = new Label(Format("ui.statDelta", 1)) { pickingMode = PickingMode.Ignore };
                _auctionCoinDelta.AddToClassList("auction-coin-delta");
                _auctionFxLayer.Add(_auctionCoinDelta);
            }
            if (_auctionCoinDelta != null)
            {
                PositionRevealFx(_auctionCoinDelta, FxCenter(destination) + new Vector2(-32, -32), 64, 42);
                _auctionCoinDelta.style.opacity = progress >= 1 ? 1 : 0;
            }
        }

        private static void PositionRevealFx(VisualElement element, Vector2 center, float width, float height)
        {
            element.style.width = width;
            element.style.height = height;
            element.style.left = center.x - width / 2;
            element.style.top = center.y - height / 2;
        }

        private void ClearAuctionFx()
        {
            ForgetStatLabels(_auctionFxLayer);
            _auctionFxLayer?.Clear();
            _auctionFlyingCard = _auctionCoin = _auctionSparks = _clashSelf = _clashOpponent = null;
            _auctionRim = _auctionSweep = null;
            _auctionCoinDelta = null;
            _auctionTrails.Clear();
        }

        private void CancelAuctionReveal()
        {
            ClearAuctionFx();
            var wallet = _root?.Q<Label>("self-coins");
            if (wallet != null) wallet.style.scale = new Scale(Vector3.one);
            _root?.RemoveFromClassList("auction-revealing");
            _auctionRevealActive = false;
            _auctionBeforeReveal = null;
            _revealLots.Clear();
            _revealVisualLot = -1;
        }
    }
}
