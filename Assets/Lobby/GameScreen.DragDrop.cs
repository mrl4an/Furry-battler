using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace AnimalBattler.Client.Lobby
{
    internal sealed partial class GameScreen
    {
        private VisualElement _dragSource;
        private int _dragPointerId = -1;
        private Vector2 _dragStart;
        private Vector2 _dragOffset;
        private bool _dragging;
        private bool _dropPending;

        private void BindDragCancellation()
        {
            _root.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Escape) CancelDrag();
            }, TrickleDown.TrickleDown);
            _root.RegisterCallback<DetachFromPanelEvent>(evt =>
            {
                if (ReferenceEquals(evt.target, _root)) CancelDrag();
            });
        }

        private void BindDrag(VisualElement source, DragSource drag)
        {
            source.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (_state?.phase != "preparation" || _state.self.ready || _dropPending || evt.button != 0) return;
                if (drag.FromShop && _state.self.coins < 3) return;
                CancelDrag();
                CloseCardDetails();
                HideSynergyHelp();
                _drag = drag;
                _dragSource = source;
                _dragPointerId = evt.pointerId;
                _dragStart = evt.position;
                _dragOffset = source.WorldToLocal(evt.position);
                source.CapturePointer(evt.pointerId);
                evt.StopPropagation();
            });
            source.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (_dragSource != source || _dragPointerId != evt.pointerId) return;
                if (!_dragging && Vector2.Distance(_dragStart, evt.position) < 8) return;
                if (!_dragging)
                {
                    _dragging = true;
                    CloseCardDetails();
                    _ghost = CharacterCard(drag.DefinitionId, drag.Copies, false);
                    _ghost.focusable = false;
                    _ghost.AddToClassList("drag-card-ghost");
                    _ghost.style.position = Position.Absolute;
                    _ghost.style.width = source.resolvedStyle.width;
                    _ghost.style.height = source.resolvedStyle.height;
                    _ghost.style.marginLeft = 0;
                    _ghost.style.marginRight = 0;
                    _ghost.style.marginTop = 0;
                    _ghost.style.marginBottom = 0;
                    _ghost.pickingMode = PickingMode.Ignore;
                    _ghost.Query<VisualElement>().ForEach(element => element.pickingMode = PickingMode.Ignore);
                    (_root.Q<VisualElement>("drag-layer") ?? _root).Add(_ghost);
                    source.AddToClassList("drag-source");
                    var salePrice = _root.Q<VisualElement>("sale-price-row");
                    salePrice?.EnableInClassList("sale-price-hidden", drag.FromShop);
                    SetText("sale-price", "ui.number", drag.Copies >= 6 ? 3 : drag.Copies >= 3 ? 2 : 1);
                }
                MoveGhost(evt.position);
                UpdateDropTargets(evt.position);
                evt.StopPropagation();
            });
            source.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (_dragSource != source || _dragPointerId != evt.pointerId) return;
                var request = _dragging ? DropRequest(evt.position) : null;
                var wasDragging = _dragging;
                CancelDrag();
                if (request != null && !_dropPending)
                {
                    _dropPending = true;
                    _send(request);
                }
                if (wasDragging) evt.StopPropagation();
            });
            source.RegisterCallback<PointerCancelEvent>(evt =>
            {
                if (_dragSource == source && _dragPointerId == evt.pointerId) CancelDrag();
            });
            source.RegisterCallback<PointerCaptureOutEvent>(evt =>
            {
                if (_dragSource == source && _dragPointerId == evt.pointerId) CancelDrag();
            });
        }

        private bool CanDropOnBoard(int slot)
        {
            if (_drag.FromBoard && _drag.BoardSlot == slot) return false;
            var board = _state.self.board;
            var target = board != null && slot < board.Length ? board[slot] : null;
            if (string.IsNullOrEmpty(target?.cardId)) return true;
            if (_drag.FromBoard && target.cardId != _drag.DefinitionId) return true;
            return target.cardId == _drag.DefinitionId && target.copies + _drag.Copies <= 6;
        }

        private GameRequest DropRequest(Vector2 position)
        {
            if (_state?.phase != "preparation" || _state.self.ready) return null;
            var sale = _root.Q<VisualElement>("sale-zone");
            if (!_drag.FromShop && sale != null && sale.worldBound.Contains(position))
                return new GameRequest { type = "sellCard", cardId = _drag.CardId };
            for (var slot = 0; slot < _boardTargets.Length; slot++)
            {
                if (_boardTargets[slot] == null || !_boardTargets[slot].worldBound.Contains(position)
                    || !CanDropOnBoard(slot)) continue;
                if (_drag.FromShop)
                    return new GameRequest { type = "buyToBoard", shopSlot = _drag.ShopSlot, targetSlot = slot };
                if (_drag.FromBoard)
                    return new GameRequest { type = "moveCard", sourceSlot = _drag.BoardSlot, targetSlot = slot };
                return new GameRequest { type = "placeCard", cardId = _drag.CardId, slot = slot };
            }
            var hand = _root.Q<VisualElement>("hand-drop-zone");
            if (_drag.FromShop && hand != null && hand.worldBound.Contains(position))
                return new GameRequest { type = "buy", slot = _drag.ShopSlot };
            return null;
        }

        private void UpdateDropTargets(Vector2 position)
        {
            for (var slot = 0; slot < _boardTargets.Length; slot++)
            {
                var target = _boardTargets[slot];
                if (target == null) continue;
                var allowed = CanDropOnBoard(slot);
                target.EnableInClassList("slot-drop-hover", allowed && target.worldBound.Contains(position));
            }
            var sale = _root.Q<VisualElement>("sale-zone");
            sale?.EnableInClassList("drop-allowed", !_drag.FromShop);
            sale?.EnableInClassList("drop-hover", !_drag.FromShop && sale.worldBound.Contains(position));
            var hand = _root.Q<VisualElement>("hand-drop-zone");
            hand?.EnableInClassList("drop-allowed", _drag.FromShop);
            hand?.EnableInClassList("drop-hover", _drag.FromShop && hand.worldBound.Contains(position));
        }

        private void MoveGhost(Vector2 position)
        {
            if (_ghost == null) return;
            var layer = _ghost.parent;
            var point = layer.WorldToLocal(position) - _dragOffset;
            var width = _ghost.style.width.value.value;
            var height = _ghost.style.height.value.value;
            _ghost.style.left = Mathf.Clamp(point.x, 28, Mathf.Max(28, layer.contentRect.width - width - 24));
            _ghost.style.top = Mathf.Clamp(point.y, 20, Mathf.Max(20, layer.contentRect.height - height - 24));
        }

        private void CancelDrag()
        {
            var source = _dragSource;
            var pointerId = _dragPointerId;
            _dragSource = null;
            _dragPointerId = -1;
            _dragging = false;
            source?.RemoveFromClassList("drag-source");
            if (source != null && pointerId >= 0 && source.HasPointerCapture(pointerId)) source.ReleasePointer(pointerId);
            ForgetStatLabels(_ghost);
            _ghost?.RemoveFromHierarchy();
            _ghost = null;
            if (_root == null) return;
            _root.Query<VisualElement>(className: "drop-allowed").ForEach(element => element.RemoveFromClassList("drop-allowed"));
            _root.Query<VisualElement>(className: "drop-hover").ForEach(element => element.RemoveFromClassList("drop-hover"));
            _root.Query<VisualElement>(className: "slot-drop-hover").ForEach(element => element.RemoveFromClassList("slot-drop-hover"));
            _root.Q<VisualElement>("sale-price-row")?.AddToClassList("sale-price-hidden");
        }
    }
}
