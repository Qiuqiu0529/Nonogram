using System;
using UnityEngine;
using UnityEngine.UI;

namespace Nonogram
    {
    public sealed class NonogramBoardLayout : MonoBehaviour
    {
        int clueFontSize;
        FontStyle clueFontStyle;
        float boardWidthRatio;
        float backgroundTopPadding, backgroundLeftPadding, backgroundRightPadding, backgroundBottomPadding;
        RectTransform boardViewport, levelBackground, boardRoot, rowClues, columnClues, separatorRoot;
        Vector2 lastViewportSize;
        float naturalClueWidth, naturalClueHeight;
        RectTransform[] cellRects;
        RectTransform[] verticalSeparators, horizontalSeparators;
        GameObject cellPrefab;
        Font font;
        bool patternMode;
        bool victoryCellSpacing;
        NonogramLevelData displayedLevel;
        Text[] rowLabels, columnLabels;
        bool[] completedRows, completedColumns;
        bool[] ambiguousRows, ambiguousColumns;
        static readonly Color ActiveClueColor = new Color32(65, 52, 59, 255);
        static readonly Color CompletedClueColor = new Color32(150, 150, 150, 255);
        static readonly Color AmbiguousClueColor = new Color32(177, 62, 33, 255);

        public static NonogramBoardLayout Create(RectTransform viewport, GameObject backgroundPrefab,
            GameObject grid, Font font, NonogramLevelData level, bool patterns, Action<GameObject, int, int> setupCell,
            Vector4 padding, int fontSize = 56, FontStyle fontStyle = FontStyle.Normal, float widthRatio = .9f)
        {
            var container = new GameObject("Board Layout", typeof(RectTransform), typeof(NonogramBoardLayout));
            var rect = (RectTransform)container.transform;
            rect.SetParent(viewport, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var layout = container.GetComponent<NonogramBoardLayout>();
            layout.boardViewport = rect;
            layout.cellPrefab = grid;
            layout.font = font;
            layout.patternMode = patterns;
            layout.clueFontSize = fontSize;
            layout.clueFontStyle = fontStyle;
            layout.boardWidthRatio = widthRatio;
            layout.backgroundTopPadding = padding.x;
            layout.backgroundLeftPadding = padding.y;
            layout.backgroundRightPadding = padding.z;
            layout.backgroundBottomPadding = padding.w;
            layout.CreateBackground(backgroundPrefab);
            layout.Draw(level, setupCell);
            return layout;
        }

        void CreateBackground(GameObject prefab)
        {
            levelBackground = (RectTransform)Instantiate(prefab, boardViewport, false).transform;
            Place(levelBackground, Vector2.zero, levelBackground.sizeDelta);
            foreach (var graphic in levelBackground.GetComponentsInChildren<Graphic>(true))
                graphic.raycastTarget = false;
            boardRoot = MakeRect("Board");
            rowClues = MakeRect("Row Clues");
            columnClues = MakeRect("Column Clues");
        }

        RectTransform MakeRect(string name)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(levelBackground, false);
            return rect;
        }

        static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        Text Label(Transform parent, string value, int size, Vector2 position, Vector2 dimensions, TextAnchor alignment)
        {
            var text = new GameObject("Clue", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            text.transform.SetParent(parent, false);
            text.font = font;
            text.fontSize = size;
            text.color = ActiveClueColor;
            text.text = value;
            text.alignment = alignment;
            text.supportRichText = true;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            Place(text.rectTransform, position, dimensions);
            return text;
        }

        void Draw(NonogramLevelData level, Action<GameObject, int, int> setupCell)
        {
            level.RebuildClues();
            displayedLevel = level;
            rowLabels = new Text[level.height];
            columnLabels = new Text[level.width];
            completedRows = new bool[level.height];
            completedColumns = new bool[level.width];
            ambiguousRows = new bool[level.height];
            ambiguousColumns = new bool[level.width];
            for (int y = 0; y < rowLabels.Length; y++)
            {
                rowLabels[y] = Label(rowClues, "", clueFontSize, Vector2.zero, Vector2.zero, TextAnchor.MiddleRight);
                rowLabels[y].fontStyle = clueFontStyle;
            }
            for (int x = 0; x < columnLabels.Length; x++)
            {
                columnLabels[x] = Label(columnClues, "", clueFontSize, Vector2.zero, Vector2.zero, TextAnchor.LowerCenter);
                columnLabels[x].fontStyle = clueFontStyle;
            }
            cellRects = new RectTransform[level.cells.Length];
            for (int y = 0; y < level.height; y++)
            for (int x = 0; x < level.width; x++)
            {
                var cell = Instantiate(cellPrefab, boardRoot, false);
                cell.name = $"Cell_{x}_{y}";
                var rect = cell.GetComponent<RectTransform>();
                // Preserve prefab child proportions while resizing the cell itself.
                Vector2 originalSize = rect.rect.size;
                foreach (RectTransform child in rect)
                {
                    Vector2 center = Vector2.one * .5f + child.anchoredPosition / originalSize;
                    Vector2 half = child.rect.size / originalSize * .5f;
                    child.anchorMin = center - half;
                    child.anchorMax = center + half;
                    child.offsetMin = child.offsetMax = Vector2.zero;
                }
                rect.localScale = Vector3.one;
                cellRects[y * level.width + x] = rect;
                setupCell(cell, x, y);
            }
            CreateSeparators(level);
            ArrangeClues(level);
        }

        void CreateSeparators(NonogramLevelData level)
        {
            separatorRoot = new GameObject("Five-Cell Separators", typeof(RectTransform),
                typeof(CanvasGroup)).GetComponent<RectTransform>();
            separatorRoot.SetParent(boardRoot, false);
            separatorRoot.anchorMin = Vector2.zero;
            separatorRoot.anchorMax = Vector2.one;
            separatorRoot.offsetMin = separatorRoot.offsetMax = Vector2.zero;
            separatorRoot.GetComponent<CanvasGroup>().blocksRaycasts = false;
            verticalSeparators = new RectTransform[(level.width - 1) / 5];
            horizontalSeparators = new RectTransform[(level.height - 1) / 5];
            for (int i = 0; i < verticalSeparators.Length; i++)
                verticalSeparators[i] = CreateSeparator("Vertical separator after " + (i + 1) * 5);
            for (int i = 0; i < horizontalSeparators.Length; i++)
                horizontalSeparators[i] = CreateSeparator("Horizontal separator after " + (i + 1) * 5);
        }

        RectTransform CreateSeparator(string name)
        {
            var line = new GameObject(name, typeof(RectTransform), typeof(Image))
                .GetComponent<RectTransform>();
            line.SetParent(separatorRoot, false);
            var image = line.GetComponent<Image>();
            image.color = new Color32(84, 58, 52, 210);
            image.raycastTarget = false;
            return line;
        }

        public void SetSeparatorOpacity(float opacity)
        {
            if (separatorRoot) separatorRoot.GetComponent<CanvasGroup>().alpha = Mathf.Clamp01(opacity);
        }

        public void SetVictoryCellSpacing()
        {
            victoryCellSpacing = true;
            FitBoard();
        }

        public void SetPatternMode(bool enabled)
        {
            if (displayedLevel == null || patternMode == enabled) return;
            patternMode = enabled;
            ArrangeClues(displayedLevel);
        }

        public void SetLineCompleted(bool row, int index)
        {
            if (displayedLevel == null) return;
            var completed = row ? completedRows : completedColumns;
            if (index < 0 || index >= completed.Length || completed[index]) return;
            completed[index] = true;
            var label = row ? rowLabels[index] : columnLabels[index];
            var clue = row ? displayedLevel.clues.rows[index] : displayedLevel.clues.columns[index];
            label.text = Format(clue, displayedLevel, row ? "   " : "\n", true);
            label.color = CompletedClueColor;
        }

        public void SetAmbiguousLines(bool[] rows, bool[] columns)
        {
            if (displayedLevel == null) return;
            Array.Clear(ambiguousRows, 0, ambiguousRows.Length);
            Array.Clear(ambiguousColumns, 0, ambiguousColumns.Length);
            if (rows != null) Array.Copy(rows, ambiguousRows, Mathf.Min(rows.Length, ambiguousRows.Length));
            if (columns != null) Array.Copy(columns, ambiguousColumns,
                Mathf.Min(columns.Length, ambiguousColumns.Length));
            ArrangeClues(displayedLevel);
        }

        static void SetAmbiguityOutline(Text label, bool visible)
        {
            var outline = label.GetComponent<Outline>();
            if (!outline && visible) outline = label.gameObject.AddComponent<Outline>();
            if (!outline) return;
            outline.effectColor = new Color32(255, 201, 92, 255);
            outline.effectDistance = new Vector2(2f, -2f);
            outline.enabled = visible;
        }

        void ArrangeClues(NonogramLevelData level)
        {
            float fontScale = clueFontSize / 14f;
            naturalClueWidth = 38f * fontScale;
            for (int y = 0; y < level.height; y++)
            {
                var label = rowLabels[y];
                label.fontSize = clueFontSize;
                label.text = Format(level.clues.rows[y], level, "   ",
                    completedRows[y] || ambiguousRows[y]);
                label.color = ambiguousRows[y] ? AmbiguousClueColor :
                    completedRows[y] ? CompletedClueColor : ActiveClueColor;
                SetAmbiguityOutline(label, ambiguousRows[y]);
                naturalClueWidth = Mathf.Max(naturalClueWidth, Mathf.Ceil(label.preferredWidth) + 4f * fontScale);
            }
            naturalClueHeight = 0f;
            for (int x = 0; x < level.width; x++)
            {
                var label = columnLabels[x];
                label.fontSize = clueFontSize;
                label.text = Format(level.clues.columns[x], level, "\n",
                    completedColumns[x] || ambiguousColumns[x]);
                label.color = ambiguousColumns[x] ? AmbiguousClueColor :
                    completedColumns[x] ? CompletedClueColor : ActiveClueColor;
                SetAmbiguityOutline(label, ambiguousColumns[x]);
                naturalClueHeight = Mathf.Max(naturalClueHeight,
                    Mathf.Ceil(label.preferredHeight) + 4f);
            }
            levelBackground.gameObject.SetActive(true);
            FitBoard();
        }

        void LateUpdate()
        {
            if (levelBackground && boardViewport.rect.size != lastViewportSize) FitBoard();
        }

        internal RectTransform GetRowClueRect(int row) => rowLabels[row].rectTransform;
        internal RectTransform GetColumnClueRect(int column) => columnLabels[column].rectTransform;

        public void SetClueOpacity(float opacity)
        {
            SetOpacity(rowClues, opacity);
            SetOpacity(columnClues, opacity);
        }

        static void SetOpacity(RectTransform root, float opacity)
        {
            if (!root.TryGetComponent<CanvasGroup>(out var group))
            {
                group = root.gameObject.AddComponent<CanvasGroup>();
                group.blocksRaycasts = false;
            }
            group.alpha = opacity;
        }

        void FitBoard()
        {
            lastViewportSize = boardViewport.rect.size;
            // Sliced panel borders keep their thickness when Width/Height changes.
            // Reserve UI-space padding before fitting the content.
            float left = Mathf.Max(32f, backgroundLeftPadding);
            float top = Mathf.Max(32f, backgroundTopPadding);
            float right = Mathf.Max(48f, backgroundRightPadding);
            float bottom = Mathf.Max(48f, backgroundBottomPadding);
            const float gap = 8f;
            float clueWidth = naturalClueWidth;
            float clueHeight = naturalClueHeight;
            float pitch = Mathf.Max(1f, Mathf.Min(
                (lastViewportSize.x * boardWidthRatio - left - right - clueWidth - gap) / displayedLevel.width,
                (lastViewportSize.y - top - bottom - clueHeight - gap) / displayedLevel.height));
            float width = displayedLevel.width * pitch;
            float height = displayedLevel.height * pitch;
            levelBackground.localScale = Vector3.one;
            Place(levelBackground, Vector2.zero,
                new Vector2(width + clueWidth + gap + left + right, height + clueHeight + gap + top + bottom));

            Vector2 center = new Vector2(
                (clueWidth + gap + left - right) / 2f,
                (bottom - top - clueHeight - gap) / 2f);
            Place(boardRoot, center, new Vector2(width, height));
            Place(rowClues, center - new Vector2((width + clueWidth) / 2f + gap, 0),
                new Vector2(clueWidth, height));
            Place(columnClues, center + new Vector2(0, (height + clueHeight) / 2f + gap),
                new Vector2(width, clueHeight));

            int fontSize = clueFontSize;
            for (int y = 0; y < displayedLevel.height; y++)
            {
                rowLabels[y].fontSize = fontSize;
                Place(rowLabels[y].rectTransform, new Vector2(0, height / 2f - (y + .5f) * pitch),
                    new Vector2(clueWidth, pitch));
            }
            for (int x = 0; x < displayedLevel.width; x++)
            {
                columnLabels[x].fontSize = fontSize;
                Place(columnLabels[x].rectTransform, new Vector2((x + .5f) * pitch - width / 2f, 0),
                    new Vector2(pitch, clueHeight));
            }
            float cellSize = victoryCellSpacing ? pitch : Mathf.Max(.1f, pitch - 2f);
            for (int y = 0; y < displayedLevel.height; y++)
            for (int x = 0; x < displayedLevel.width; x++)
                Place(cellRects[y * displayedLevel.width + x],
                    new Vector2((x + .5f) * pitch - width / 2f, height / 2f - (y + .5f) * pitch),
                    new Vector2(cellSize, cellSize));
            float separatorWidth = Mathf.Clamp(pitch * .09f, 2.5f, 5f);
            for (int i = 0; i < verticalSeparators.Length; i++)
                Place(verticalSeparators[i],
                    new Vector2((i + 1) * 5 * pitch - width / 2f, 0f),
                    new Vector2(separatorWidth, height));
            for (int i = 0; i < horizontalSeparators.Length; i++)
                Place(horizontalSeparators[i],
                    new Vector2(0f, height / 2f - (i + 1) * 5 * pitch),
                    new Vector2(width, separatorWidth));
        }

        string Format(NonogramClueLine line, NonogramLevelData level, string separator, bool completed)
        {
            if(line==null||line.runs==null||line.runs.Length==0)return "·";var parts=new string[line.runs.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                var run = line.runs[i];
                var brush = level.brushes[run.brushId];
                parts[i] = !patternMode
                    ? completed ? run.length.ToString() : $"<color={brush.colorHex}>{run.length}</color>"
                    : run.length + NonogramBoardView.ShapeGlyph(brush.shape);
            }
            return string.Join(separator,parts);
        }

    }
}
