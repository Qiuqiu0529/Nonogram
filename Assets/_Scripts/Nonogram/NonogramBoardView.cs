using System;
using UnityEngine;
using UnityEngine.UI;

namespace Nonogram
{
    /// <summary>Renders a level and its row/column clues using the Grid prefab.</summary>
    public class NonogramBoardView : MonoBehaviour
    {
        [SerializeField] GameObject gridCellPrefab;
        [SerializeField] RectTransform contentRoot;
        [SerializeField] float cellSize = 48f;
        [SerializeField] float clueGap = 5f;
        [SerializeField] int clueFontSize = 18;
    [SerializeField] Font clueFont;

        public void SetPrefab(GameObject prefab) { gridCellPrefab = prefab; }
        public void SetRoot(RectTransform root) { contentRoot = root; }
        public void SetCellSize(float value) { cellSize = Mathf.Max(8f, value); }

        NonogramLevelData renderedLevel;
        bool renderedSolution;

        void OnEnable()
        {
            SettingManager.PatternModeChanged += RefreshDisplay;
            if (renderedLevel != null) Render(renderedLevel, renderedSolution);
        }

        void OnDisable() => SettingManager.PatternModeChanged -= RefreshDisplay;

        void RefreshDisplay(bool enabled)
        {
            if (renderedLevel != null) Render(renderedLevel, renderedSolution);
        }

        public void Render(NonogramLevelData level, bool showSolution = false)
        {
            if (level == null) throw new ArgumentNullException("level");
            level.RebuildClues();
            if (gridCellPrefab == null) throw new InvalidOperationException("Assign the Grid prefab before rendering.");
            if (contentRoot == null) contentRoot = transform as RectTransform;
            if (contentRoot == null) throw new InvalidOperationException("Board view needs a RectTransform root.");
            renderedLevel = level;
            renderedSolution = showSolution;
            ClearChildren(contentRoot);

            float clueWidth = Mathf.Max(44f, Mathf.Ceil(cellSize * 0.95f));
            float clueHeight = Mathf.Max(44f, Mathf.Ceil(cellSize * 0.95f));
            float boardWidth = level.width * cellSize;
            float boardHeight = level.height * cellSize;
            float left = clueWidth + clueGap;
            float top = clueHeight + clueGap;

            var gridRoot = CreateRect("Cells", contentRoot, left + boardWidth * .5f, -top - boardHeight * .5f, boardWidth, boardHeight);
            var grid = gridRoot.gameObject.AddComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = level.width;
            grid.cellSize = new Vector2(cellSize, cellSize);
            grid.spacing = Vector2.zero;
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperLeft;

            for (int y = 0; y < level.height; y++)
            for (int x = 0; x < level.width; x++)
            {
                var cell = level.GetCell(x, y);
                var instance = Instantiate(gridCellPrefab, gridRoot, false);
                instance.name = "Cell_" + x + "_" + y;
                instance.GetComponent<RectTransform>().sizeDelta = new Vector2(cellSize, cellSize);
                ApplyCell(instance.transform, cell, level, showSolution);
            }

            for (int y = 0; y < level.height; y++)
                AddClueLine(contentRoot, "RowClue_" + y, Format(level.clues.rows[y], level), clueWidth * .5f, -top - y * cellSize - cellSize * .5f, clueWidth, cellSize, TextAnchor.MiddleRight);
            for (int x = 0; x < level.width; x++)
                AddClueLine(contentRoot, "ColumnClue_" + x, Format(level.clues.columns[x], level), left + x * cellSize + cellSize * .5f, -clueHeight * .5f, cellSize, clueHeight, TextAnchor.LowerCenter);
        }

        void ApplyCell(Transform cellRoot, NonogramCell cell, NonogramLevelData level, bool showSolution)
        {
            Transform inside = cellRoot.Find("ImageInside");
            Transform tag = cellRoot.Find("ImageTag");
            var fill = inside ? inside.GetComponent<Image>() : null;
            if (fill)
            {
                bool filled = cell.state == CellState.Filled || (showSolution && cell.state == CellState.Filled);
                fill.enabled = filled;
                if (filled && cell.brushId >= 0 && cell.brushId < level.brushes.Length)
                {
                    var brush = level.brushes[cell.brushId];
                    if (SettingManager.PatternModeEnabled)
                    {
                        fill.color = Color.white;
                        var bounds = ((RectTransform)cellRoot).rect.size;
                        AddClueLine(cellRoot, "Brush Shape", ShapeGlyph(brush.shape),
                            bounds.x / 2, -bounds.y / 2, bounds.x, bounds.y, TextAnchor.MiddleCenter);
                    }
                    else if (ColorUtility.TryParseHtmlString(brush.colorHex, out var color))
                        fill.color = color;
                }
            }
            if (tag) tag.gameObject.SetActive(cell.state == CellState.Marked);
        }

        void AddClueLine(Transform parent, string name, string value, float x, float y, float w, float h, TextAnchor alignment)
        {
            var rect = CreateRect(name, parent, x, y, w, h);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = clueFont ? clueFont : Resources.Load<Font>("SourceHanSansCN-Medium");
            text.fontSize = clueFontSize;
            text.color = new Color32(65, 52, 59, 255);
            text.alignment = alignment;
            text.text = value;
            text.supportRichText = true;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
        }

        string Format(NonogramClueLine line, NonogramLevelData level)
        {
            if (line == null || line.runs == null || line.runs.Length == 0) return "·";
            string[] parts = new string[line.runs.Length];
            for (int i = 0; i < line.runs.Length; i++)
            {
                var run = line.runs[i];
                var brush = level.brushes[run.brushId];
                parts[i] = SettingManager.PatternModeEnabled
                    ? run.length + ShapeGlyph(brush.shape)
                    : $"<color={brush.colorHex}>{run.length}</color>";
            }
            return string.Join("  ", parts);
        }

        internal static string ShapeGlyph(string shape)
        {
            switch (shape)
            {
                case "circle": return "●";
                case "triangle": return "▲";
                case "diamond": return "◆";
                case "square": return "■";
                case "star": return "★";
                case "heart": return "♥";
                default: return shape;
            }
        }

        static RectTransform CreateRect(string name, Transform parent, float x, float y, float width, float height)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i);
                if (Application.isPlaying)
                {
                    child.gameObject.SetActive(false);
                    Destroy(child.gameObject);
                }
                else DestroyImmediate(child.gameObject);
            }
        }
    }
}
