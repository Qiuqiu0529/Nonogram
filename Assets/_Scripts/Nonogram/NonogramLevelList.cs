using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Nonogram
{
    public sealed partial class NonogramLevelList : MonoBehaviour
    {
        [SerializeField] Button listButton;
        [SerializeField] Button levelButton;
        [SerializeField] RectTransform gameRoot;
        [SerializeField] GameObject mainRoot;
        [SerializeField] RectTransform backButtonRoot;
        [SerializeField] TMP_Text roundLevelNumber;
        [SerializeField] TMP_Text roundScoreLabel;
        [SerializeField] Button hintButton;
        [SerializeField] Image hintBulb;
        [SerializeField] TMP_Text hintCount;
        [SerializeField] Shader hintGrayscaleShader;
        [SerializeField] RectTransform tryDisplay;
        [SerializeField] RectTransform failPanel;
        [SerializeField] RectTransform replayButtonRoot;
        [SerializeField] RectTransform successPanel;
        [SerializeField] RectTransform nextButtonRoot;
        [SerializeField] GameObject roundBackgroundPrefab;
        [SerializeField] Font clueFont;
        [SerializeField, Range(.1f, 1f)] float gameBoardWidthRatio = .96f;
        [SerializeField] NonogramLevelListData listData;
        [SerializeField] UnityEvent<string> onLevelSelected = new UnityEvent<string>();

        static readonly Color Ink = new Color32(66, 49, 55, 255);
        static readonly Color Pink = new Color32(250, 229, 231, 255);
        readonly List<NonogramLevelData> levels = new List<NonogramLevelData>();
        readonly List<Text> rows = new List<Text>();
        readonly List<int> levelNumbers = new List<int>();
        RectTransform overlay, content, confirmation;
        Text status;
        Font font;
        Coroutine loading;

        public NonogramLevelData SelectedLevel { get; private set; }

        void Start()
        {
            font = Resources.Load<Font>("SourceHanSansCN-Medium");
            InitializeOperationSounds();
            listButton.onClick.AddListener(Open);
            levelButton.onClick.AddListener(PlayFromHome);
            NonogramProgress.Changed += RefreshProgress;
            SettingManager.PatternModeChanged += UpdateRoundPatternMode;
            RefreshHomeButton();
        }

        void OnDestroy()
        {
            if (listButton) listButton.onClick.RemoveListener(Open);
            if (levelButton) levelButton.onClick.RemoveListener(PlayFromHome);
            if (roundBackButton) roundBackButton.onClick.RemoveListener(ReturnHome);
            if (replayLevelButton) replayLevelButton.onClick.RemoveListener(ReplayFailedRound);
            if (nextLevelButton) nextLevelButton.onClick.RemoveListener(PlayFollowingRound);
            if (hintButtonBound && hintButton) hintButton.onClick.RemoveListener(RevealTile);
            if (brushModeButton) brushModeButton.onClick.RemoveListener(ChooseBrushMode);
            if (markModeButton) markModeButton.onClick.RemoveListener(ChooseMarkMode);
            StopHintMotion();
            if (hintGrayMaterial) Destroy(hintGrayMaterial);
            NonogramProgress.Changed -= RefreshProgress;
            SettingManager.PatternModeChanged -= UpdateRoundPatternMode;
            if (overlay) Destroy(overlay.gameObject);
            if (gameOverlay) Destroy(gameOverlay.gameObject);
        }

        void Open()
        {
            if (starting) return;
            if (!overlay) BuildUI();
            overlay.SetAsLastSibling();
            overlay.gameObject.SetActive(true);
            if (loading != null) StopCoroutine(loading);
            loading = StartCoroutine(LoadRows());
        }

        void Close()
        {
            if (loading != null) StopCoroutine(loading);
            loading = null;
            confirmation.gameObject.SetActive(false);
            overlay.gameObject.SetActive(false);
        }

        IEnumerator LoadRows()
        {
            foreach (var row in rows)
            {
                row.transform.parent.gameObject.SetActive(false);
                Destroy(row.transform.parent.gameObject);
            }
            rows.Clear();
            levels.Clear();
            levelNumbers.Clear();
            status.text = "正在读取关卡…";
            if (!listData)
            {
                status.text = "未配置关卡列表";
                loading = null;
                yield break;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            int skipped = 0;
            for (int index = 0; index < listData.Count; index++)
            {
                string file = listData.GetFile(index);
                NonogramLevelData level = null;
                yield return NonogramCatalog.LoadLevel(file, result => level = result,
                    error => Debug.LogWarning(file + ": " + error, this));
                if (level == null || !seen.Add(level.id))
                {
                    skipped++;
                    continue;
                }
                levels.Add(level);
                levelNumbers.Add(index + 1);
                var row = CreateRect("Level Row", content, Vector2.zero, Vector2.one);
                row.gameObject.AddComponent<LayoutElement>().preferredHeight = 116;
                int levelNumber = index + 1;
                var button = AddButton(row, "", () => Select(level, levelNumber));
                rows.Add(button.GetComponentInChildren<Text>());
            }
            RefreshProgress();
            status.text = levels.Count == 0 ? "暂无可用关卡" : $"共 {levels.Count} 关";
            if (skipped > 0) status.text += $"，跳过 {skipped} 个无效或重复文件";
            loading = null;
        }

        void Select(NonogramLevelData level, int levelNumber)
        {
            if (!ResolveRoundRoots())
            {
                status.text = "找不到 Game 或 Main 界面";
                return;
            }
            SelectedLevel = level;
            Close();
            StartRound(level, levelNumber);
            onLevelSelected.Invoke(level.id);
        }

        void RefreshProgress()
        {
            if (round == null && !starting) RefreshHomeButton();
            for (int i = 0; i < levels.Count; i++)
            {
                var level = levels[i];
                var progress = NonogramProgress.Get(level.id);
                string times = progress.completions == 0 ? "未通关"
                    : $"最近 {FormatTime(progress.lastClearSeconds)}  ·  最快 {FormatTime(progress.bestClearSeconds)}";
                if (progress.bestStars > 0) times += $"  ·  {progress.bestStars} 星";
                if (progress.bestScore > 0) times += $"  ·  最高 {progress.bestScore} 分";
                rows[i].text = $"{levelNumbers[i]}. {DisplayName(level)}   {level.width} × {level.height}\n" +
                    $"尝试 {progress.attempts} 次  ·  通关 {progress.completions} 次  ·  {times}";
            }
        }

        static string DisplayName(NonogramLevelData level) =>
            string.IsNullOrWhiteSpace(level.title) ? level.id : level.title;

        static string FormatTime(float seconds) => $"{(int)(seconds / 60):00}:{(int)(seconds % 60):00}";

        void BuildUI()
        {
            var canvas = listButton.GetComponentInParent<Canvas>().rootCanvas;
            overlay = CreateRect("Level List Overlay", canvas.transform, Vector2.zero, Vector2.one);
            AddImage(overlay, new Color(0f, 0f, 0f, .55f));
            var panel = CreateRect("Level List Panel", overlay, new Vector2(.06f, .10f), new Vector2(.94f, .90f));
            AddImage(panel, new Color32(255, 249, 242, 255));
            AddLabel(panel, "关卡列表", 42, new Vector2(.05f, .90f), new Vector2(.75f, .99f));
            AddButton(CreateRect("Close", panel, new Vector2(.79f, .91f), new Vector2(.96f, .98f)), "关闭", Close);
            status = AddLabel(panel, "", 28, new Vector2(.04f, .82f), new Vector2(.96f, .91f));

            var viewport = CreateRect("Level Scroll", panel, new Vector2(.05f, .13f), new Vector2(.95f, .82f));
            AddImage(viewport, new Color32(255, 253, 250, 255));
            viewport.gameObject.AddComponent<RectMask2D>();
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            content = CreateRect("Rows", viewport, new Vector2(0f, 1f), Vector2.one);
            content.pivot = new Vector2(.5f, 1f);
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 12;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            AddButton(CreateRect("Reset Progress", panel, new Vector2(.24f, .03f), new Vector2(.76f, .10f)),
                "重置关卡进度", () => confirmation.gameObject.SetActive(true));
            confirmation = CreateRect("Confirm Reset Progress", overlay, Vector2.zero, Vector2.one);
            AddImage(confirmation, new Color(0f, 0f, 0f, .62f));
            var dialog = CreateRect("Dialog", confirmation, new Vector2(.10f, .37f), new Vector2(.90f, .63f));
            AddImage(dialog, new Color32(255, 249, 242, 255));
            AddLabel(dialog, "清除所有关卡的尝试和通关记录？", 32, new Vector2(.06f, .38f), new Vector2(.94f, .94f));
            AddButton(CreateRect("Cancel", dialog, new Vector2(.08f, .10f), new Vector2(.46f, .38f)),
                "取消", () => confirmation.gameObject.SetActive(false));
            AddButton(CreateRect("Confirm", dialog, new Vector2(.54f, .10f), new Vector2(.92f, .38f)), "确认重置", () =>
            {
                NonogramProgress.ResetAll();
                confirmation.gameObject.SetActive(false);
                status.text = "关卡进度已重置";
            });
            confirmation.gameObject.SetActive(false);
        }

        static RectTransform CreateRect(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = parent.gameObject.layer;
            rect.SetParent(parent, false);
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        static Image AddImage(RectTransform target, Color color)
        {
            var image = target.gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        Text AddLabel(RectTransform parent, string value, int size, Vector2 min, Vector2 max)
        {
            var text = CreateRect("Text", parent, min, max).gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.color = Ink;
            text.alignment = TextAnchor.MiddleCenter;
            text.supportRichText = false;
            text.raycastTarget = false;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 16;
            text.resizeTextMaxSize = size;
            text.text = value;
            return text;
        }

        Button AddButton(RectTransform target, string value, UnityAction clicked)
        {
            var image = AddImage(target, Pink);
            var button = target.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(clicked);
            AddLabel(target, value, 30, new Vector2(.03f, .05f), new Vector2(.97f, .95f));
            return button;
        }
    }
}
