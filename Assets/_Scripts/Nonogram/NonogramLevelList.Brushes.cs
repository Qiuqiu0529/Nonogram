using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Nonogram
{
    public sealed partial class NonogramLevelList
    {
        Button brushModeButton, markModeButton;
        CanvasGroup brushIcon, markIcon;
        Image activeBrushColor;
        RectTransform brushPalette;
        RectTransform brushOptionsContent;
        [SerializeField, Range(.1f, 1f)] float brushSizeHeightRatio = .8f;
        [SerializeField, Min(0f)] float brushOptionSpacing = 12f;
        readonly List<Button> roundBrushButtons = new List<Button>();
        readonly List<Image> roundBrushIcons = new List<Image>();
        int activeRoundBrush;

        void BindBrushControls()
        {
            if (brushModeButton) return;
            var root = gameRoot.Find("Brush");
            if (!root) return;

            var brush = root.Find("brush");
            var mark = root.Find("mark");
            if (brush)
            {
                brushModeButton = brush.GetComponent<Button>();
                if (brushModeButton) brushModeButton.onClick.AddListener(ChooseBrushMode);
                var icon = brush.Find("ImageBrush");
                brushIcon = GetIconGroup(icon);
                var color = icon ? icon.Find("ImageColor") : null;
                if (color) activeBrushColor = color.GetComponent<Image>();
            }
            if (mark)
            {
                markModeButton = mark.GetComponent<Button>();
                if (markModeButton) markModeButton.onClick.AddListener(ChooseMarkMode);
                markIcon = GetIconGroup(mark.Find("ImageMark"));
            }
            brushPalette = root.Find("ImageBrushs") as RectTransform;
            if (brushPalette)
            {
                var viewport = CreateRect("Brush Options Viewport", brushPalette, Vector2.zero, Vector2.one);
                AddImage(viewport, Color.clear);
                viewport.gameObject.AddComponent<RectMask2D>();
                brushOptionsContent = CreateRect("Brush Options", viewport, Vector2.zero, Vector2.one);
                brushOptionsContent.anchorMin = new Vector2(0f, .5f);
                brushOptionsContent.anchorMax = new Vector2(0f, .5f);
                brushOptionsContent.pivot = new Vector2(0f, .5f);
                var scroll = viewport.gameObject.AddComponent<ScrollRect>();
                scroll.viewport = viewport;
                scroll.content = brushOptionsContent;
                scroll.horizontal = true;
                scroll.vertical = false;
                scroll.movementType = ScrollRect.MovementType.Clamped;
            }
        }

        static CanvasGroup GetIconGroup(Transform icon)
        {
            if (!icon) return null;
            if (!icon.TryGetComponent<CanvasGroup>(out var group))
                group = icon.gameObject.AddComponent<CanvasGroup>();
            return group;
        }

        void ChooseBrushMode() => SetMarking(false);
        void ChooseMarkMode() => SetMarking(true);

        void SetMarking(bool enabled)
        {
            if (revealingBoard) return;
            if (round?.Outcome != NonogramRound.Result.Playing || !AllowTutorialMode(enabled))
            {
                PlayOperationSound(blockedButtonSoundFB);
                return;
            }
            if (marking == enabled) return;
            marking = enabled;
            AdvanceTutorialMode();
            RefreshBrushControls();
            PopBrushIcon(enabled ? markIcon?.transform : brushIcon?.transform);
            PlayOperationSound(enabled ? markModeSoundFB : brushModeSoundFB);
        }

        void BuildRoundBrushes()
        {
            foreach (var button in roundBrushButtons)
            {
                if (!button) continue;
                button.gameObject.SetActive(false);
                Destroy(button.gameObject);
            }
            roundBrushButtons.Clear();
            roundBrushIcons.Clear();
            if (!brushPalette || SelectedLevel.brushes.Length < 2) return;

            int count = SelectedLevel.brushes.Length;
            for (int i = 0; i < count; i++)
            {
                int index = i;
                var tile = CreateRect("Brush " + (index + 1), brushOptionsContent,
                    new Vector2(0f, .5f), new Vector2(0f, .5f));
                var background = AddImage(tile, Color.white);
                var button = tile.gameObject.AddComponent<Button>();
                button.targetGraphic = background;
                // Selection colors are managed below, including during the board intro.
                button.transition = Selectable.Transition.None;
                button.onClick.AddListener(() => SelectRoundBrush(index));
                var icon = AddImage(CreateRect("Brush Icon", tile, new Vector2(.13f, .13f), new Vector2(.87f, .87f)),
                    Color.white);
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                roundBrushButtons.Add(button);
                roundBrushIcons.Add(icon);
            }
            brushOptionsContent.anchoredPosition = Vector2.zero;
            LayoutBrushOptions();
            RefreshBrushControls();
        }

        void LateUpdate()
        {
            if (brushPalette && brushPalette.gameObject.activeInHierarchy)
                LayoutBrushOptions();
            TickTutorial();
        }

        void LayoutBrushOptions()
        {
            int count = roundBrushButtons.Count;
            if (count == 0 || !brushOptionsContent) return;
            float height = brushPalette.rect.height;
            float side = Mathf.Max(1f, height * brushSizeHeightRatio);
            float inset = Mathf.Max(0f, (height - side) * .5f);
            float rowWidth = count * side + (count - 1) * brushOptionSpacing;
            float contentWidth = Mathf.Max(brushPalette.rect.width, rowWidth + inset * 2f);
            brushOptionsContent.sizeDelta = new Vector2(contentWidth, height);
            float start = (contentWidth - rowWidth) * .5f;
            for (int i = 0; i < count; i++)
            {
                var rect = (RectTransform)roundBrushButtons[i].transform;
                rect.sizeDelta = new Vector2(side, side);
                rect.anchoredPosition = new Vector2(start + side * .5f + i * (side + brushOptionSpacing), 0f);
            }
        }

        void SelectRoundBrush(int index)
        {
            if (revealingBoard || marking || round?.Outcome != NonogramRound.Result.Playing) return;
            if (!AllowTutorialBrush(index))
            {
                PlayOperationSound(blockedButtonSoundFB);
                return;
            }
            if (activeRoundBrush == index) return;
            activeRoundBrush = index;
            AdvanceTutorialBrush();
            RefreshBrushControls();
            PopBrushIcon(roundBrushIcons[index].transform);
            PopBrushIcon(brushIcon ? brushIcon.transform : null);
            PlayOperationSound(selectBrushSoundFB);
        }

        static void PopBrushIcon(Transform icon)
        {
            if (!icon) return;
            if (!icon.TryGetComponent<UIIconPop>(out var pop))
                pop = icon.gameObject.AddComponent<UIIconPop>();
            pop.Play();
        }

        void RefreshBrushControls()
        {
            bool playing = !revealingBoard && round?.Outcome == NonogramRound.Result.Playing;
            if (brushModeButton) brushModeButton.interactable = playing;
            if (markModeButton) markModeButton.interactable = playing;
            if (brushIcon) brushIcon.alpha = marking ? .5f : 1f;
            if (markIcon) markIcon.alpha = marking ? 1f : .5f;

            var brushes = SelectedLevel.brushes;
            if (brushPalette) brushPalette.gameObject.SetActive(!marking && brushes.Length > 1);
            if (activeBrushColor && brushes.Length > 0)
                activeBrushColor.color = BrushColor(brushes[activeRoundBrush]);

            for (int i = 0; i < roundBrushButtons.Count; i++)
            {
                var button = roundBrushButtons[i];
                var icon = roundBrushIcons[i];
                button.interactable = playing;
                button.targetGraphic.color = i == activeRoundBrush
                    ? new Color32(105, 69, 54, 255) : new Color32(255, 244, 234, 255);
                icon.sprite = patternMode ? LevelEditorRuntime.GetShapeSprite(brushes[i].shape) : null;
                icon.color = patternMode ? (i == activeRoundBrush ? Color.white : Ink) : BrushColor(brushes[i]);
            }
        }

        static Color BrushColor(NonogramBrush brush) =>
            ColorUtility.TryParseHtmlString(brush.colorHex, out var color) ? color : Ink;
    }
}
