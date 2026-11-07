using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace AnimalBattler.Client.Lobby
{
    internal sealed partial class GameScreen
    {
        private string _appliedBackgroundId;
        private VisualElement _synergyHelpAnchor;

        private void ApplyMatchBackground()
        {
            var backdrop = _root.Q<VisualElement>("match-backdrop");
            if (backdrop == null) return;
            var id = _state.backgroundId ?? string.Empty;
            if (_appliedBackgroundId == id) return;
            _appliedBackgroundId = id;
            var known = id == "courtyard-arch" || id == "courtyard-garages" || id == "courtyard-overgrown";
            var image = known ? Resources.Load<Texture2D>("Backgrounds/" + id) : null;
            if (image == null) image = Resources.Load<Texture2D>("Backgrounds/fallback");
            backdrop.style.backgroundImage = image == null ? new StyleBackground(StyleKeyword.None) : new StyleBackground(image);
        }

        private void BindSynergyHelp()
        {
            foreach (var group in ClassGroups)
            {
                var entry = _root.Q<VisualElement>("synergy-" + group);
                if (entry == null) continue;
                var help = entry.Q<Button>("synergy-help");
                help.text = Format("ui.help");
                help.RegisterCallback<PointerEnterEvent>(evt => ShowSynergyHelp(help, group));
                help.RegisterCallback<PointerLeaveEvent>(evt => HideSynergyHelp());
                help.RegisterCallback<FocusInEvent>(evt => ShowSynergyHelp(help, group));
                help.RegisterCallback<FocusOutEvent>(evt => HideSynergyHelp());
                help.clicked += () => ShowSynergyHelp(help, group);
            }
            _root.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Escape) HideSynergyHelp();
            });
            _root.RegisterCallback<WheelEvent>(evt => HideSynergyHelp(), TrickleDown.TrickleDown);
            _root.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (_synergyHelpAnchor != null && !_synergyHelpAnchor.Contains(evt.target as VisualElement)) HideSynergyHelp();
            }, TrickleDown.TrickleDown);
            _root.RegisterCallback<GeometryChangedEvent>(evt => HideSynergyHelp());
        }

        private void ShowSynergyHelp(VisualElement anchor, string group)
        {
            if (_ghost != null) return;
            var panel = _root.Q<VisualElement>("synergy-help-panel");
            if (panel == null) return;
            CloseCardDetails();
            _synergyHelpAnchor = anchor;
            SetText("synergy-help-title", "group." + group);
            SetText("synergy-help-body", "synergy." + group);
            SetText("synergy-help-rules", "synergy.helpRules");
            var point = _root.WorldToLocal(anchor.worldBound.max);
            panel.style.left = Mathf.Clamp(point.x + 16, 16, Mathf.Max(16, _root.contentRect.width - 376));
            panel.style.top = Mathf.Clamp(point.y - 32, 16, Mathf.Max(16, _root.contentRect.height - 300));
            panel.RemoveFromClassList("hidden");
        }

        private void HideSynergyHelp()
        {
            _root?.Q<VisualElement>("synergy-help-panel")?.AddToClassList("hidden");
            _synergyHelpAnchor = null;
        }
    }
}
