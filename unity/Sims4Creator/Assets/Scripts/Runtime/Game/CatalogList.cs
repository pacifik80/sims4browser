using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Sims4Creator.Game
{
    /// <summary>One entry in a catalog list: an icon (or a colour chip if none), a label, and payload.</summary>
    public sealed class CatalogEntry
    {
        public string label;
        public Texture2D icon;
        public Color swatch = Color.clear; // shown when there's no icon
        public object data;                // arbitrary payload (a catalog index, an id, …)
    }

    /// <summary>
    /// Builds and drives a **virtualized** UI Toolkit <see cref="ListView"/> of icon+label rows — the
    /// reusable workhorse for every long catalog (furniture, and later CAS hair/clothing with hundreds of
    /// swatches). This is the feature that made UI Toolkit the right call (D-105): only the visible rows
    /// are instantiated, so thousands of entries stay cheap.
    ///
    /// Uses **FixedHeight** virtualization deliberately — the DynamicHeight path calls <c>bindItem</c> for
    /// *every* item (4000 items → 4000 calls vs ~35), which would defeat the purpose.
    /// </summary>
    public sealed class CatalogList
    {
        public readonly ListView View;
        public event Action<CatalogEntry> Selected;

        private readonly List<CatalogEntry> _items = new List<CatalogEntry>();

        public CatalogList(float rowHeight = 30f)
        {
            View = new ListView
            {
                fixedItemHeight = rowHeight,
                virtualizationMethod = CollectionVirtualizationMethod.FixedHeight,
                selectionType = SelectionType.Single,
                showBorder = false,
                itemsSource = _items,
                makeItem = MakeRow,
                bindItem = BindRow,
            };
            View.AddToClassList("catalog");
            View.selectionChanged += OnSelectionChanged;
        }

        public void SetItems(IEnumerable<CatalogEntry> items)
        {
            _items.Clear();
            _items.AddRange(items);
            View.Rebuild();
        }

        public CatalogEntry SelectedEntry =>
            (View.selectedIndex >= 0 && View.selectedIndex < _items.Count) ? _items[View.selectedIndex] : null;

        public void ClearSelection() => View.ClearSelection();

        private static VisualElement MakeRow()
        {
            var row = new VisualElement();
            row.AddToClassList("catalog-row");

            var icon = new VisualElement { name = "icon" };
            icon.AddToClassList("catalog-icon");

            var label = new Label { name = "label" };
            label.AddToClassList("catalog-label");

            row.Add(icon);
            row.Add(label);
            return row;
        }

        private void BindRow(VisualElement row, int i)
        {
            if (i < 0 || i >= _items.Count) return;
            var e = _items[i];
            var icon = row.Q<VisualElement>("icon");
            var label = row.Q<Label>("label");

            if (label != null) label.text = e.label;
            if (icon != null)
            {
                if (e.icon != null)
                {
                    icon.style.backgroundImage = new StyleBackground(e.icon);
                    icon.style.backgroundColor = Color.clear;
                }
                else
                {
                    icon.style.backgroundImage = new StyleBackground((Texture2D)null);
                    icon.style.backgroundColor = e.swatch;
                }
            }
        }

        private void OnSelectionChanged(IEnumerable<object> _)
        {
            var e = SelectedEntry;
            if (e != null) Selected?.Invoke(e);
        }
    }
}
