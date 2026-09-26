using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Sims4Creator.Game
{
    /// <summary>
    /// Virtualized thumbnail grid for the Buy catalogue. UI Toolkit's ListView is single-column, so items
    /// are packed N per row and the ListView virtualizes the ROWS (FixedHeight — only visible rows exist).
    ///
    /// Selection is a DATA-MODEL field applied in bindItem — never a mid-dispatch RefreshItems/Rebuild
    /// (ListView recycles rows aggressively; rebinding from inside a row's own click handler is a known
    /// glitch source). Clicking updates the model, then re-applies the highlight class to the currently
    /// visible cells directly; recycled rows pick it up in bindItem.
    /// </summary>
    public sealed class CatalogGrid
    {
        public readonly ListView View;
        public event Action<BuildableDef> Selected;

        private readonly int _columns;
        private readonly List<BuildableDef> _items = new List<BuildableDef>();
        private readonly List<List<BuildableDef>> _rows = new List<List<BuildableDef>>();
        private BuildableDef _selected;

        public CatalogGrid(int columns = 5, float cellHeight = 96f)
        {
            _columns = Mathf.Max(1, columns);
            View = new ListView
            {
                fixedItemHeight = cellHeight,
                virtualizationMethod = CollectionVirtualizationMethod.FixedHeight,
                selectionType = SelectionType.None, // selection is ours, not the ListView's
                showBorder = false,
                itemsSource = _rows,
                makeItem = MakeRow,
                bindItem = BindRow,
            };
            View.AddToClassList("catalog-grid");
        }

        public BuildableDef SelectedItem => _selected;

        public void SetItems(IEnumerable<BuildableDef> items)
        {
            _items.Clear();
            _items.AddRange(items);
            _rows.Clear();
            for (int i = 0; i < _items.Count; i += _columns)
                _rows.Add(_items.GetRange(i, Math.Min(_columns, _items.Count - i)));
            if (_selected != null && !_items.Contains(_selected)) _selected = null;
            View.Rebuild(); // called from filter changes, never from inside a cell click
        }

        public void ClearSelection()
        {
            _selected = null;
            ApplySelectionToVisible();
        }

        private void Select(BuildableDef d)
        {
            _selected = d;
            ApplySelectionToVisible();
            Selected?.Invoke(d);
        }

        /// <summary>Toggle the highlight class on the currently instantiated cells only — recycled rows
        /// re-apply it in bindItem. No rebind, no dispatch re-entry.</summary>
        private void ApplySelectionToVisible()
        {
            View.Query<Button>(className: "grid-cell").ForEach(cell =>
                cell.EnableInClassList("grid-cell--sel",
                    cell.userData is BuildableDef d && ReferenceEquals(d, _selected)));
        }

        private VisualElement MakeRow()
        {
            var row = new VisualElement();
            row.AddToClassList("grid-row");
            for (int c = 0; c < _columns; c++)
            {
                var cell = new Button();
                cell.AddToClassList("grid-cell");

                var thumb = new VisualElement { name = "thumb" };
                thumb.AddToClassList("grid-thumb");

                var label = new Label { name = "label" };
                label.AddToClassList("grid-label");

                var price = new Label { name = "price" };
                price.AddToClassList("grid-price");

                cell.Add(thumb);
                cell.Add(label);
                cell.Add(price);

                // One handler for the cell's lifetime; reads whichever item is currently bound.
                cell.clicked += () => { if (cell.userData is BuildableDef d) Select(d); };
                row.Add(cell);
            }
            return row;
        }

        private void BindRow(VisualElement row, int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= _rows.Count) return;
            var chunk = _rows[rowIndex];
            for (int c = 0; c < _columns; c++)
            {
                var cell = (Button)row[c];
                if (c < chunk.Count)
                {
                    var d = chunk[c];
                    cell.userData = d;
                    cell.style.visibility = Visibility.Visible;

                    var thumb = cell.Q<VisualElement>("thumb");
                    var label = cell.Q<Label>("label");
                    var price = cell.Q<Label>("price");

                    if (label != null) label.text = d.name;
                    if (price != null) price.text = d.price > 0 ? "§" + d.price : "";
                    if (thumb != null)
                    {
                        var img = d.ThumbOrFallback; // real BuyBuildThumbnail, else primary diffuse
                        if (img != null)
                        {
                            thumb.style.backgroundImage = new StyleBackground(img);
                            thumb.style.backgroundColor = Color.clear;
                        }
                        else
                        {
                            thumb.style.backgroundImage = new StyleBackground((Texture2D)null);
                            thumb.style.backgroundColor = new Color(0.28f, 0.31f, 0.4f);
                        }
                    }
                    cell.EnableInClassList("grid-cell--sel", ReferenceEquals(d, _selected));
                }
                else
                {
                    cell.userData = null;
                    cell.style.visibility = Visibility.Hidden;
                    cell.EnableInClassList("grid-cell--sel", false);
                }
            }
        }
    }
}
