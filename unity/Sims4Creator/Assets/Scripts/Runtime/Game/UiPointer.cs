using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Sims4Creator.Game
{
    /// <summary>
    /// The ONE authoritative "is the pointer over UI?" service for the game (verified against Unity 6
    /// docs — see docs/game/systems/ui-framework.md). Every UIDocument registers itself; world-click,
    /// camera-drag and scroll-zoom code asks this instead of hand-rolling coordinate math.
    ///
    /// Correct hit-test recipe (the source of the "clicking the dock placed furniture" bug — both steps
    /// are mandatory):
    ///   1. FLIP Y first: Input.mousePosition is bottom-left origin, panel space is top-left. ScreenToPanel
    ///      does NOT flip for you (official doc example flips before calling).
    ///   2. RuntimePanelUtils.ScreenToPanel de-scales for the "Scale With Screen Size" PanelSettings.
    ///   3. panel.Pick — pixel-accurate against real pickable elements (roots are picking-mode Ignore, so
    ///      empty screen space correctly reads as NOT over UI). Never compare raw pixels to worldBound.
    /// </summary>
    public static class UiPointer
    {
        private static readonly List<UIDocument> Docs = new List<UIDocument>();

        private static int _cachedFrame = -1;
        private static bool _cachedOver;

        public static void Register(UIDocument doc)
        {
            if (doc != null && !Docs.Contains(doc)) Docs.Add(doc);
        }

        public static void Unregister(UIDocument doc) => Docs.Remove(doc);

        /// <summary>True when the cursor is over any pickable element of any registered document. Cached
        /// per frame — cheap to call from every guard.</summary>
        public static bool OverAnyUi()
        {
            if (Time.frameCount == _cachedFrame) return _cachedOver;
            _cachedFrame = Time.frameCount;
            _cachedOver = Compute();
            return _cachedOver;
        }

        /// <summary>True while a text field (e.g. the build-dock search box) has keyboard focus — used to
        /// keep WASD/Q/E/F/R/Del camera & tool keys from firing while the user is typing.</summary>
        public static bool TextInputFocused()
        {
            for (int i = Docs.Count - 1; i >= 0; i--)
            {
                var doc = Docs[i];
                if (doc == null) { Docs.RemoveAt(i); continue; }
                var root = doc.rootVisualElement;
                var focused = root?.panel?.focusController?.focusedElement as VisualElement;
                if (focused == null) continue;
                if (focused is TextField || focused.GetFirstAncestorOfType<TextField>() != null) return true;
            }
            return false;
        }

        private static bool Compute()
        {
            // Flip to top-left origin BEFORE ScreenToPanel (it de-scales but does NOT flip).
            var screen = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
            for (int i = Docs.Count - 1; i >= 0; i--)
            {
                var doc = Docs[i];
                if (doc == null) { Docs.RemoveAt(i); continue; }
                var root = doc.rootVisualElement;
                var panel = root?.panel;
                if (panel == null) continue;
                if (root.resolvedStyle.display == DisplayStyle.None) continue; // hidden HUD/dock
                if (panel.Pick(RuntimePanelUtils.ScreenToPanel(panel, screen)) != null) return true;
            }
            return false;
        }
    }
}
