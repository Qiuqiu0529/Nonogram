using UnityEngine;
using UnityEngine.UI;

namespace Nonogram
{
    public sealed partial class NonogramLevelList
    {
        enum Lesson { None, FirstFill, SecondFill, LeftColumn, RightColumn, SplitRow, Hint, Fish }
        Lesson lesson;
        int lessonRow, checkRow;
        float fishTipEndsAt;
        RectTransform lessonLayer, lessonPanel, lessonPaw;
        RectTransform[] lessonFrame;
        TMPro.TMP_Text lessonText;
        float measuredScreenWidth;
        Vector2 measuredTextSize;
        readonly Vector3[] lessonCorners = new Vector3[4];

        void BeginTutorial()
        {
            if (currentLevelNumber != 1 || SelectedLevel.brushes.Length != 1 ||
                NonogramProgress.Get(SelectedLevel.id).completions > 0 ||
                !brushModeButton || !markModeButton || !hintButton)
                return;

            lessonRow = checkRow = -1;
            int width = SelectedLevel.width;
            for (int y = 0; y < SelectedLevel.height; y++)
            {
                var runs = SelectedLevel.clues.rows[y].runs;
                if (runs.Length == 1 && runs[0].length == width)
                {
                    if (lessonRow < 0) lessonRow = y;
                    else if (checkRow < 0) checkRow = y;
                }
            }
            var firstRow = SelectedLevel.clues.rows[0].runs;
            if (width != 5 || lessonRow < 0 || checkRow != lessonRow + 1 || lessonRow == 0 ||
                firstRow.Length != 2 || firstRow[0].length != 1 || firstRow[1].length != 1 ||
                !IsColumnTwo(0) || !IsColumnTwo(width - 1)) return;

            if (CreateTutorialUI()) SetLesson(Lesson.FirstFill);
        }

        bool IsColumnTwo(int column)
        {
            var runs = SelectedLevel.clues.columns[column].runs;
            return runs.Length == 1 && runs[0].length == 2;
        }

        bool CreateTutorialUI()
        {
            lessonPanel = gameRoot.Find("ImageTutorial") as RectTransform;
            var text = lessonPanel ? lessonPanel.Find("Text (TMP)") : null;
            lessonText = text ? text.GetComponent<TMPro.TMP_Text>() : null;
            if (!lessonText) return false;
            lessonText.raycastTarget = false;
            if (lessonPanel.TryGetComponent<Image>(out var background)) background.raycastTarget = false;

            lessonLayer = CreateRect("Interactive Tutorial", gameOverlay, Vector2.zero, Vector2.one);
            lessonFrame = new RectTransform[4];
            for (int i = 0; i < lessonFrame.Length; i++)
            {
                var edge = CreateRect("Focus Border", lessonLayer, Vector2.one * .5f, Vector2.one * .5f);
                AddImage(edge, new Color32(212, 105, 45, 255)).raycastTarget = false;
                lessonFrame[i] = edge;
            }
            lessonPanel.SetAsLastSibling();
            lessonPanel.gameObject.SetActive(true);

            var source = gameRoot.Find("Image paw");
            if (source && source.TryGetComponent<Image>(out var image))
            {
                lessonPaw = CreateRect("Paw Pointer", lessonLayer, Vector2.one * .5f, Vector2.one * .5f);
                var paw = AddImage(lessonPaw, image.color);
                paw.sprite = image.sprite;
                paw.preserveAspect = true;
                paw.raycastTarget = false;
            }
            return true;
        }

        void SetLesson(Lesson next)
        {
            lesson = next;
            measuredScreenWidth = -1f;
            switch (next)
            {
                case Lesson.FirstFill: lessonText.text = "跟着数字，把藏起来的图案画出来吧！\n数字 5 表示这一行要连续填满 5 格，试试看！"; break;
                case Lesson.SecondFill: lessonText.text = "这一行也是 5，把它填满吧！"; break;
                case Lesson.LeftColumn: lessonText.text = marking
                    ? "给第一列剩下的空格标上 ×。"
                    : "第一列的 2 代表连续填 2 格。已经填满，点 × 标记其余空格。"; break;
                case Lesson.RightColumn: lessonText.text = "第 5 列也是 2。自己把剩下的空格标上 × 吧！"; break;
                case Lesson.SplitRow: lessonText.text = marking
                    ? "切回画笔，看看第一行的 1 1。"
                    : "1 1 表示两组各 1 格，中间至少空 1 格。试着填完第一行。"; break;
                case Lesson.Hint: lessonText.text = "遇到困难时，可以使用“提示”。"; break;
                case Lesson.Fish: lessonText.text = "小鱼干代表剩余尝试次数，小心推理吧！"; break;
            }
            UpdateTutorialLayout();
            RefreshHintButton(TutorialAllowsHint);
        }

        void UpdateTutorialLayout()
        {
            if (!lessonLayer) return;
            int width = SelectedLevel.width;
            RectTransform first, last, pointer;
            if (lesson == Lesson.Fish) first = last = pointer = tryDisplay;
            else if (lesson == Lesson.Hint) first = last = pointer = (RectTransform)hintButton.transform;
            else if (lesson == Lesson.SplitRow && marking)
                first = last = pointer = (RectTransform)brushModeButton.transform;
            else if (lesson == Lesson.LeftColumn || lesson == Lesson.RightColumn)
            {
                int column = lesson == Lesson.LeftColumn ? 0 : width - 1;
                first = roundBoardLayout.GetColumnClueRect(column);
                last = tileRects[(SelectedLevel.height - 1) * width + column];
                pointer = first;
                if (!marking) pointer = (RectTransform)markModeButton.transform;
                else if (lesson == Lesson.LeftColumn)
                    for (int y = 0; y < SelectedLevel.height; y++)
                    {
                        int index = y * width + column;
                        if (round.GetTile(index) != NonogramRound.Tile.Blank) continue;
                        pointer = tileRects[index];
                        break;
                    }
            }
            else
            {
                int row = lesson == Lesson.FirstFill ? lessonRow : lesson == Lesson.SecondFill ? checkRow : 0;
                first = pointer = roundBoardLayout.GetRowClueRect(row);
                last = tileRects[row * width + width - 1];
                if (lesson == Lesson.FirstFill || lesson == Lesson.SecondFill)
                    for (int x = 0; x < width; x++)
                        if (round.GetTile(row * width + x) != NonogramRound.Tile.Filled)
                        {
                            pointer = tileRects[row * width + x];
                            break;
                        }
            }

            Rect a = TutorialBounds(first), b = TutorialBounds(last), target = TutorialBounds(pointer);
            Vector2 min = Vector2.Min(a.min, b.min) - Vector2.one * 6f;
            Vector2 max = Vector2.Max(a.max, b.max) + Vector2.one * 6f;
            Vector2 middle = (min + max) * .5f;
            SetTutorialRect(lessonFrame[0], new Vector2(middle.x, max.y), new Vector2(max.x - min.x + 6f, 6f));
            SetTutorialRect(lessonFrame[1], new Vector2(middle.x, min.y), new Vector2(max.x - min.x + 6f, 6f));
            SetTutorialRect(lessonFrame[2], new Vector2(min.x, middle.y), new Vector2(6f, max.y - min.y));
            SetTutorialRect(lessonFrame[3], new Vector2(max.x, middle.y), new Vector2(6f, max.y - min.y));

            Rect screen = lessonLayer.rect;
            const float paddingX = 34f;
            const float paddingY = 22f;
            if (measuredScreenWidth != screen.width)
            {
                measuredScreenWidth = screen.width;
                float maxTextWidth = Mathf.Max(1f, Mathf.Min(760f, screen.width - 40f) - paddingX * 2f);
                float textWidth = Mathf.Clamp(Mathf.Ceil(lessonText.GetPreferredValues(lessonText.text).x), 1f, maxTextWidth);
                float textHeight = Mathf.Ceil(lessonText.GetPreferredValues(lessonText.text, textWidth, Mathf.Infinity).y);
                measuredTextSize = new Vector2(textWidth, textHeight);
                SetTutorialRect(lessonText.rectTransform, Vector2.zero, measuredTextSize);
            }
            Vector2 cardSize = measuredTextSize + new Vector2(paddingX * 2f, paddingY * 2f);
            bool above = screen.yMax - max.y > cardSize.y + 40f || screen.yMax - max.y >= min.y - screen.yMin;
            float cardY = above ? max.y + cardSize.y * .5f + 22f : min.y - cardSize.y * .5f - 22f;
            float cardX = Mathf.Clamp(middle.x, screen.xMin + cardSize.x * .5f + 16f, screen.xMax - cardSize.x * .5f - 16f);
            Vector2 cardCenter = new Vector2(cardX, Mathf.Clamp(cardY,
                screen.yMin + cardSize.y * .5f + 12f, screen.yMax - cardSize.y * .5f - 12f));
            SetTutorialRect(lessonPanel,
                gameRoot.InverseTransformPoint(lessonLayer.TransformPoint(cardCenter)), cardSize);

            if (!lessonPaw) return;
            float pulse = .5f - .5f * Mathf.Cos(Time.unscaledTime * 8f);
            float direction = above ? -1f : 1f;
            float pawY = (above ? target.yMin : target.yMax) + direction * (47f - 21f * pulse);
            SetTutorialRect(lessonPaw, new Vector2(target.center.x, pawY), new Vector2(84f, 76f));
            lessonPaw.localRotation = Quaternion.Euler(0f, 0f, above ? 0f : 180f);
            lessonPaw.localScale = new Vector3(1f, 1f + .18f * pulse, 1f);
        }

        Rect TutorialBounds(RectTransform target)
        {
            target.GetWorldCorners(lessonCorners);
            var min = (Vector2)lessonLayer.InverseTransformPoint(lessonCorners[0]);
            var max = min;
            for (int i = 1; i < lessonCorners.Length; i++)
            {
                var corner = (Vector2)lessonLayer.InverseTransformPoint(lessonCorners[i]);
                min = Vector2.Min(min, corner);
                max = Vector2.Max(max, corner);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        static void SetTutorialRect(RectTransform rect, Vector2 center, Vector2 size)
        {
            rect.anchoredPosition = center;
            rect.sizeDelta = size;
        }

        bool TutorialAllowsFreePlay => lesson == Lesson.None || lesson == Lesson.Fish;
        bool TutorialAllowsHint => TutorialAllowsFreePlay || lesson == Lesson.Hint;

        bool AllowTutorialTap(int index)
        {
            if (TutorialAllowsFreePlay) return true;
            int width = SelectedLevel.width;
            switch (lesson)
            {
                case Lesson.FirstFill: return index / width == lessonRow;
                case Lesson.SecondFill: return index / width == checkRow;
                case Lesson.LeftColumn: return marking && index % width == 0;
                case Lesson.RightColumn: return marking && index % width == width - 1;
                case Lesson.SplitRow: return !marking && index / width == 0;
                default: return false;
            }
        }

        void AdvanceTutorialTap()
        {
            int width = SelectedLevel.width;
            if (lesson == Lesson.LeftColumn || lesson == Lesson.RightColumn)
            {
                int column = lesson == Lesson.LeftColumn ? 0 : width - 1;
                for (int y = 0; y < SelectedLevel.height; y++)
                    if (round.GetTile(y * width + column) == NonogramRound.Tile.Blank) return;
                SetLesson(lesson == Lesson.LeftColumn ? Lesson.RightColumn : Lesson.SplitRow);
                return;
            }
            if (lesson != Lesson.FirstFill && lesson != Lesson.SecondFill && lesson != Lesson.SplitRow) return;
            int row = lesson == Lesson.FirstFill ? lessonRow : lesson == Lesson.SecondFill ? checkRow : 0;
            for (int x = 0; x < width; x++)
            {
                int index = row * width + x;
                if (SelectedLevel.cells[index].state == CellState.Filled &&
                    round.GetTile(index) != NonogramRound.Tile.Filled) return;
            }
            if (lesson == Lesson.SplitRow && round.Outcome == NonogramRound.Result.Won)
                ShowFishLesson();
            else
                SetLesson(lesson == Lesson.FirstFill ? Lesson.SecondFill
                    : lesson == Lesson.SecondFill ? Lesson.LeftColumn : Lesson.Hint);
        }

        bool AllowTutorialMode(bool mark) => TutorialAllowsFreePlay ||
            (lesson == Lesson.LeftColumn && mark) || (lesson == Lesson.SplitRow && !mark);

        void AdvanceTutorialMode()
        {
            if (lesson == Lesson.LeftColumn || lesson == Lesson.SplitRow) SetLesson(lesson);
        }

        void TickTutorial()
        {
            if (lesson == Lesson.Fish && Time.unscaledTime >= fishTipEndsAt)
            {
                FinishTutorial();
                RefreshRoundStatus();
            }
            UpdateTutorialLayout();
        }

        void ShowFishLesson()
        {
            if (!tryDisplay)
            {
                FinishTutorial();
                return;
            }
            fishTipEndsAt = Time.unscaledTime + 4.5f;
            SetLesson(Lesson.Fish);
        }

        void FinishTutorial()
        {
            EndTutorial();
        }

        void EndTutorial()
        {
            lesson = Lesson.None;
            if (lessonPanel) lessonPanel.gameObject.SetActive(false);
            if (lessonLayer)
            {
                lessonLayer.gameObject.SetActive(false);
                Destroy(lessonLayer.gameObject);
            }
            lessonLayer = lessonPaw = lessonPanel = null;
            lessonText = null;
        }
    }
}
