using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Nonogram
{
    public sealed partial class NonogramLevelList
    {
        static readonly Color ErrorRed = new Color32(175, 39, 53, 255);
        RectTransform gameOverlay;
        Text roundStatus;
        Button roundBackButton, retryButton;
        bool hintButtonBound;
        CanvasGroup hintGroup;
        float hintRestAlpha;
        Material hintGrayMaterial, hintButtonMaterial, hintBulbMaterial;
        Coroutine hintMotion;
        RectTransform movingHint;
        Vector2 hintRestPosition;
        Vector3 hintRestScale;
        Button replayLevelButton;
        Button nextLevelButton;
        TMPro.TMP_Text nextLevelLabel;
        Coroutine nextLevelLoad;
        NonogramLevelData followingLevel;
        int followingLevelNumber;
        bool victoryShown;
        Coroutine scorePop;
        Vector3 scoreRestScale;
        int displayedScore;
        UIVoronoiShatter[] lifeFish = System.Array.Empty<UIVoronoiShatter>();
        Vector3[] fishIntroScales;
        int currentLevelNumber;
        NonogramRound round;
        NonogramProgress.Attempt attempt;
        float startedAt;
        bool marking, patternMode, starting;
        Coroutine boardIntro;
        NonogramBoardLayout roundBoardLayout;
        bool revealingBoard;
        RectTransform[] tileRects;
        Image[] tileImages;
        Sprite[] originalTileSprites;
        Color[] originalColors;
        Image[] tags;
        Color[] originalTagColors;
        bool[] tileShaking;

        void PlayFromHome()
        {
            if (!starting && ResolveRoundRoots()) StartCoroutine(LoadNextRound(true));
        }

        void RefreshHomeButton()
        {
            if (levelButton && !starting && round == null)
                StartCoroutine(LoadNextRound(false));
        }

        void ReturnHome()
        {
            CloseRound();
            RefreshHomeButton();
        }

        bool ResolveRoundRoots()
        {
            if (gameRoot && mainRoot) return true;

            // A scene already open in Unity may still have the older component data.
            // Transform.Find also searches inactive children, so Game can start hidden.
            var canvas = levelButton ? levelButton.GetComponentInParent<Canvas>(true) : null;
            if (canvas)
            {
                var root = canvas.rootCanvas.transform;
                if (!gameRoot) gameRoot = root.Find("Game") as RectTransform;
                if (!mainRoot)
                {
                    var main = root.Find("Main");
                    if (main) mainRoot = main.gameObject;
                }
            }

            if (gameRoot && mainRoot) return true;
            Debug.LogError("NonogramLevelList requires the Game and Main UI roots. Assign them in the Inspector.", this);
            return false;
        }

        IEnumerator LoadNextRound(bool enterRound)
        {
            starting = true;
            levelButton.interactable = false;
            NonogramLevelData last = null;
            NonogramLevelData next = null;
            int lastNumber = 0, nextNumber = 0;
            for (int i = 0; listData && i < listData.Count && next == null; i++)
            {
                yield return NonogramCatalog.LoadLevel(listData.GetFile(i), level =>
                {
                    last = level;
                    lastNumber = i + 1;
                    if (NonogramProgress.Get(level.id).completions == 0)
                    {
                        next = level;
                        nextNumber = i + 1;
                    }
                }, error => Debug.LogWarning(error, this));
            }
            starting = false;
            levelButton.interactable = true;
            if (next == null)
            {
                next = last;
                nextNumber = lastNumber;
            }
            SetDifficultySkin(levelButton, next?.difficulty ?? LevelDifficulty.Normal);
            var homeLevelLabel = levelButton.GetComponentInChildren<TMPro.TMP_Text>(true);
            if (homeLevelLabel)
                homeLevelLabel.text = next != null ? $"第 {nextNumber} 关" : "暂无关卡";
            if (!enterRound) yield break;
            if (next != null)
            {
                StartRound(next, nextNumber);
                onLevelSelected.Invoke(next.id);
            }
            else
            {
                BuildRoundPanel(0);
                roundStatus.text = "请检查关卡列表 Data";
            }
        }

        void StartRound(NonogramLevelData level, int levelNumber)
        {
            if (!ResolveRoundRoots()) return;
            var prefab = Resources.Load<GameObject>("Grid");
            if (!roundBackgroundPrefab) roundBackgroundPrefab = Resources.Load<GameObject>("LevelBackground");
            BuildRoundPanel(levelNumber);
            if (!prefab || !roundBackgroundPrefab)
            {
                roundStatus.text = "无法加载棋盘资源";
                return;
            }

            SelectedLevel = level;
            currentLevelNumber = levelNumber;
            round = new NonogramRound(level);
            SetDifficultySkin(replayLevelButton, level.difficulty);
            foreach (var fish in lifeFish) fish.ResetEffect();
            marking = false;
            patternMode = SettingManager.PatternModeEnabled;
            activeRoundBrush = 0;
            BindBrushControls();
            BuildRoundBrushes();
            var panel = (RectTransform)roundStatus.transform.parent;
            BuildRoundBoard(panel, prefab, level);
            retryButton = AddButton(CreateRect("Retry", panel, new Vector2(.70f, .10f), new Vector2(.96f, .17f)),
                "再玩一次", () => StartRound(level, levelNumber));
            if (round.Outcome == NonogramRound.Result.Playing)
                attempt = NonogramProgress.BeginAttempt(level.id);
            revealingBoard = true;
            RefreshRoundStatus();
            if (levelOpenSoundFB) PlayOperationSound(levelOpenSoundFB);
            boardIntro = StartCoroutine(PlayBoardIntro());
        }

        void BuildRoundPanel(int levelNumber)
        {
            CloseRound();
            mainRoot.SetActive(false);
            gameRoot.gameObject.SetActive(true);
            BindRoundHeader();
            BindHintButton();
            ResetScoreDisplay();
            BindRoundFailureUI();
            BindRoundSuccessUI();
            if (roundLevelNumber)
                roundLevelNumber.text = levelNumber > 0
                    ? levelNumber.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    : "";
            gameOverlay = CreateRect("Nonogram Round", gameRoot, Vector2.zero, Vector2.one);
            roundStatus = AddLabel(gameOverlay, "", 30, new Vector2(.04f, .71f), new Vector2(.96f, .75f));
        }

        static void SetDifficultySkin(Button button, LevelDifficulty difficulty)
        {
            if (!button) return;
            string selected = difficulty == LevelDifficulty.Difficult ? "Image (1)Difficult"
                : difficulty == LevelDifficulty.Super ? "Image (2)Super" : "ImageNormal";
            foreach (string name in new[] { "ImageNormal", "Image (1)Difficult", "Image (2)Super" })
            {
                var child = button.transform.Find(name);
                if (child) child.gameObject.SetActive(name == selected);
            }
        }

        void BindRoundSuccessUI()
        {
            if (!successPanel) successPanel = gameRoot.Find("ImageSuccess") as RectTransform;
            if (!successPanel) return;
            successPanel.gameObject.SetActive(false);
            if (!nextButtonRoot) nextButtonRoot = successPanel.Find("NextLevel") as RectTransform;
            if (!nextLevelButton && nextButtonRoot)
            {
                nextLevelButton = nextButtonRoot.GetComponentInChildren<Button>(true);
                if (nextLevelButton)
                {
                    nextLevelButton.onClick.AddListener(PlayFollowingRound);
                    nextLevelLabel = nextLevelButton.GetComponentInChildren<TMPro.TMP_Text>(true);
                }
            }
        }

        IEnumerator ShowVictory()
        {
            yield return null;
            followingLevel = null;
            for (int i = currentLevelNumber; listData && i < listData.Count && followingLevel == null; i++)
            {
                int number = i + 1;
                yield return NonogramCatalog.LoadLevel(listData.GetFile(i), level =>
                {
                    followingLevel = level;
                    followingLevelNumber = number;
                }, error => Debug.LogWarning(error, this));
            }
            while (scorePop != null || activeLineRewards > 0) yield return null;
            yield return RevealVictoryPattern();
            if (nextLevelButton)
            {
                SetDifficultySkin(nextLevelButton, followingLevel?.difficulty ?? LevelDifficulty.Normal);
                if (nextLevelLabel)
                    nextLevelLabel.text = followingLevel != null ? $"第 {followingLevelNumber} 关" : "主界面";
            }
            if (successPanel && !successPanel.gameObject.activeSelf)
            {
                successPanel.SetAsLastSibling();
                successPanel.gameObject.SetActive(true);
                if (successSoundFB) PlayOperationSound(successSoundFB);
                PlayResultPresentation(successPanel, true);
            }
            nextLevelLoad = null;
        }

        IEnumerator RevealVictoryPattern()
        {
            if (roundBoardLayout == null || round == null || tileRects == null) yield break;

            var hiddenTiles = new CanvasGroup[tileRects.Length];
            for (int i = 0; i < tileRects.Length; i++)
            {
                if (round.GetTile(i) == NonogramRound.Tile.Filled) continue;
                if (!tileRects[i].TryGetComponent(out CanvasGroup group))
                    group = tileRects[i].gameObject.AddComponent<CanvasGroup>();
                hiddenTiles[i] = group;
            }

            float duration = Mathf.Max(.01f, victoryRevealFadeDuration);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float opacity = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                roundBoardLayout.SetClueOpacity(opacity);
                roundBoardLayout.SetSeparatorOpacity(opacity);
                foreach (var group in hiddenTiles)
                    if (group) group.alpha = opacity;
                yield return null;
            }

            roundBoardLayout.SetClueOpacity(0f);
            roundBoardLayout.SetSeparatorOpacity(0f);
            foreach (var group in hiddenTiles)
                if (group) group.alpha = 0f;

            // Remove the normal two-pixel cell gaps and the inset on filled artwork.
            // The board is rebuilt for every round, so these reveal-only changes do not
            // affect the next level's playable grid.
            roundBoardLayout.SetVictoryCellSpacing();
            for (int i = 0; i < tileImages.Length; i++)
            {
                if (round.GetTile(i) != NonogramRound.Tile.Filled) continue;
                var art = tileImages[i].rectTransform;
                art.anchorMin = Vector2.zero;
                art.anchorMax = Vector2.one;
                art.offsetMin = Vector2.zero;
                art.offsetMax = Vector2.zero;
            }

            if (victoryRevealHoldDuration > 0f)
                yield return new WaitForSecondsRealtime(victoryRevealHoldDuration);
        }

        void PlayFollowingRound()
        {
            var level = followingLevel;
            if (round?.Outcome != NonogramRound.Result.Won) return;
            if (level == null)
            {
                ReturnHome();
                return;
            }
            StartRound(level, followingLevelNumber);
            onLevelSelected.Invoke(level.id);
        }

        void BindRoundFailureUI()
        {
            if (!tryDisplay) tryDisplay = gameRoot.Find("ImageTry") as RectTransform;
            if (tryDisplay)
            {
                lifeFish = tryDisplay.GetComponentsInChildren<UIVoronoiShatter>(true);
                System.Array.Sort(lifeFish, (a, b) =>
                    tryDisplay.InverseTransformPoint(a.transform.position).x.CompareTo(
                        tryDisplay.InverseTransformPoint(b.transform.position).x));
            }
            if (!failPanel) failPanel = gameRoot.Find("ImageFail") as RectTransform;
            if (!failPanel) return;
            failPanel.gameObject.SetActive(false);
            if (!replayButtonRoot) replayButtonRoot = failPanel.Find("ReplayLevel") as RectTransform;
            if (!replayLevelButton && replayButtonRoot)
            {
                replayLevelButton = replayButtonRoot.GetComponentInChildren<Button>(true);
                if (replayLevelButton) replayLevelButton.onClick.AddListener(ReplayFailedRound);
            }
        }

        void ReplayFailedRound()
        {
            if (round?.Outcome == NonogramRound.Result.Lost)
                StartRound(SelectedLevel, currentLevelNumber);
        }

        void LoseFish()
        {
            bool last = round.Outcome == NonogramRound.Result.Lost;
            // Chances has already been decremented, so it is the index of the spent slot.
            if (round.Chances < lifeFish.Length &&
                lifeFish[round.Chances].TryShatter(last ? ShowFailurePanel : null)) return;
            if (last) ShowFailurePanel();
        }

        void ShowFailurePanel()
        {
            if (round?.Outcome != NonogramRound.Result.Lost || !failPanel ||
                failPanel.gameObject.activeSelf) return;
            failPanel.SetAsLastSibling();
            failPanel.gameObject.SetActive(true);
            if (failureSoundFB) PlayOperationSound(failureSoundFB);
            PlayResultPresentation(failPanel, false);
        }

        void BindRoundHeader()
        {
            if (!backButtonRoot) backButtonRoot = gameRoot.Find("Up/BackButton") as RectTransform;
            if (!roundBackButton && backButtonRoot && backButtonRoot.TryGetComponent(out roundBackButton))
                roundBackButton.onClick.AddListener(ReturnHome);
            if (roundLevelNumber) return;
            foreach (var label in gameRoot.GetComponentsInChildren<TMPro.TMP_Text>(true))
            {
                if (label.name != "Text (TMP) Level (2)") continue;
                roundLevelNumber = label;
                break;
            }
        }

        void CloseRound()
        {
            StopResultPresentation();
            StopLineRewards();
            EndTutorial();
            StopScorePop();
            StopHintMotion();
            SetHintOpacity(1f);
            if (boardIntro != null) StopCoroutine(boardIntro);
            if (nextLevelLoad != null) StopCoroutine(nextLevelLoad);
            nextLevelLoad = null;
            followingLevel = null;
            victoryShown = false;
            boardIntro = null;
            RestoreFishIntroScale();
            revealingBoard = false;
            if (gameOverlay)
            {
                gameOverlay.gameObject.SetActive(false);
                Destroy(gameOverlay.gameObject);
            }
            if (gameRoot) gameRoot.gameObject.SetActive(false);
            if (brushPalette) brushPalette.gameObject.SetActive(false);
            if (failPanel) failPanel.gameObject.SetActive(false);
            if (successPanel) successPanel.gameObject.SetActive(false);
            if (mainRoot) mainRoot.SetActive(true);
            gameOverlay = null;
            round = null;
            attempt = null;
        }

        void BuildRoundBoard(RectTransform panel, GameObject prefab, NonogramLevelData level)
        {
            var area = CreateRect("Board Area", panel, new Vector2(0f, .19f), new Vector2(1f, .715f));
            tileRects = new RectTransform[level.cells.Length];
            tileImages = new Image[level.cells.Length];
            originalTileSprites = new Sprite[level.cells.Length];
            originalColors = new Color[level.cells.Length];
            tags = new Image[level.cells.Length];
            originalTagColors = new Color[level.cells.Length];
            tileShaking = new bool[level.cells.Length];
            rewardedRows = new bool[level.height];
            rewardedColumns = new bool[level.width];
            cellFeedbacks = new UICellFeedback[level.cells.Length];
            roundBoardLayout = NonogramBoardLayout.Create(area, roundBackgroundPrefab, prefab,
                clueFont ? clueFont : font, level, patternMode,
                (cell, x, y) =>
                {
                    int index = y * level.width + x;
                    var rect = (RectTransform)cell.transform;
                    tileRects[index] = rect;
                    cellFeedbacks[index] = cell.AddComponent<UICellFeedback>();
                    var inside = rect.Find("ImageInside").GetComponent<Image>();
                    inside.gameObject.SetActive(true);
                    inside.enabled = true;
                    inside.raycastTarget = true;
                    tileImages[index] = inside;
                    originalTileSprites[index] = inside.sprite;
                    originalColors[index] = inside.color;
                    var tag = rect.Find("ImageTag").GetComponent<Image>();
                    tags[index] = tag;
                    originalTagColors[index] = tag.color;
                    tag.raycastTarget = false;
                    tag.gameObject.SetActive(false);
                    var button = cell.GetComponent<Button>();
                    if (!button) button = cell.AddComponent<Button>();
                    button.targetGraphic = inside;
                    button.transition = Selectable.Transition.None;
                    button.onClick.AddListener(() => TapTile(index));
                }, new Vector4(18f, 18f, 48f, 60f),
                fontSize: clueFontMaxSize, widthRatio: gameBoardWidthRatio,
                minFontSize: clueFontMinSize);
        }

        IEnumerator PlayBoardIntro()
        {
            SetHintOpacity(0f);
            const float duration = .28f;
            const float fishPopDuration = .32f;
            const float fishStagger = .075f;
            int width = SelectedLevel.width;
            int lastWave = width + SelectedLevel.height - 2;
            float stagger = Mathf.Min(.065f, .8f / Mathf.Max(1, lastWave));
            var rotations = new Quaternion[tileRects.Length];
            var groups = new CanvasGroup[tileRects.Length];
            var finalAlphas = new float[tileRects.Length];
            fishIntroScales = new Vector3[lifeFish.Length];
            for (int i = 0; i < lifeFish.Length; i++)
            {
                var fish = lifeFish[i].transform;
                fishIntroScales[i] = fish.localScale;
                fish.localScale = Vector3.zero;
            }
            roundBoardLayout.SetClueOpacity(0f);
            roundBoardLayout.SetSeparatorOpacity(0f);
            for (int i = 0; i < tileRects.Length; i++)
            {
                var tile = tileRects[i];
                rotations[i] = tile.localRotation;
                if (!tile.TryGetComponent<CanvasGroup>(out var group))
                    group = tile.gameObject.AddComponent<CanvasGroup>();
                groups[i] = group;
                finalAlphas[i] = group.alpha;
                group.alpha = 0f;
                tile.gameObject.SetActive(false);
            }

            float elapsed = 0;
            float total = lastWave * stagger + duration;
            if (lifeFish.Length > 0)
                total = Mathf.Max(total, (lifeFish.Length - 1) * fishStagger + fishPopDuration);
            while (elapsed < total)
            {
                yield return null;
                elapsed += Time.unscaledDeltaTime;
                for (int i = 0; i < lifeFish.Length; i++)
                {
                    float t = Mathf.Clamp01((elapsed - i * fishStagger) / fishPopDuration);
                    float tail = t - 1f;
                    float pop = 1f + 2.9f * tail * tail * tail + 1.9f * tail * tail;
                    lifeFish[i].transform.localScale = fishIntroScales[i] * Mathf.Max(0f, pop);
                }
                for (int i = 0; i < tileRects.Length; i++)
                {
                    int wave = i % width + i / width;
                    float localTime = elapsed - wave * stagger;
                    if (localTime < 0f) continue;
                    var tile = tileRects[i];
                    float t = Mathf.Clamp01(localTime / duration);
                    float decay = 1f - t;
                    float angle = -9f * Mathf.Cos(t * Mathf.PI * 2f) * decay * decay;
                    tile.localRotation = rotations[i] * Quaternion.Euler(0f, 0f, angle);
                    groups[i].alpha = Mathf.SmoothStep(0f, finalAlphas[i], Mathf.Clamp01(t / .75f));
                    if (!tile.gameObject.activeSelf) tile.gameObject.SetActive(true);
                }
            }

            RestoreFishIntroScale();
            const float clueFadeDuration = .24f;
            float clueTime = 0f;
            while (clueTime < clueFadeDuration)
            {
                yield return null;
                clueTime += Time.unscaledDeltaTime;
                float opacity = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(clueTime / clueFadeDuration));
                roundBoardLayout.SetClueOpacity(opacity);
                roundBoardLayout.SetSeparatorOpacity(opacity);
                SetHintOpacity(opacity);
            }
            boardIntro = null;
            revealingBoard = false;
            startedAt = Time.unscaledTime;
            BeginTutorial();
            RefreshRoundStatus();
        }

        void RestoreFishIntroScale()
        {
            if (fishIntroScales == null) return;
            for (int i = 0; i < lifeFish.Length; i++)
                if (lifeFish[i]) lifeFish[i].transform.localScale = fishIntroScales[i];
            fishIntroScales = null;
        }

        void TapTile(int index)
        {
            if (revealingBoard || tileShaking[index]) return;
            if (round.Outcome != NonogramRound.Result.Playing) return;
            var previous = round.GetTile(index);
            if (!AllowTutorialTap(index) || previous == NonogramRound.Tile.Mistake ||
                previous == NonogramRound.Tile.Filled)
            {
                PlayOperationSound(blockedCellSoundFB);
                StartCoroutine(ShakeMistake(index, flash: false));
                return;
            }
            int chancesBefore = round.Chances;
            if (!round.Tap(index, marking, activeRoundBrush)) return;
            cellFeedbacks[index].Restore();
            PaintTile(index);
            AdvanceTutorialTap();
            if (round.Chances < chancesBefore)
            {
                ResetPraiseStreak();
                PlayOperationSound(mistakeSoundFB);
                StartCoroutine(ShakeMistake(index));
                LoseFish();
            }
            else AnimateTileChange(index, previous);
            RefreshRoundStatus(round.GetTile(index) == NonogramRound.Tile.Filled);
        }

        void RevealTile()
        {
            if (revealingBoard) return;
            if (round?.Outcome != NonogramRound.Result.Playing || !TutorialAllowsHint || round.Hints == 0)
            {
                RejectHint();
                return;
            }
            int index = round.Reveal();
            if (index < 0) { RejectHint(); return; }
            cellFeedbacks[index].Restore();
            PaintTile(index);
            cellFeedbacks[index].Play(tileImages[index], fill: true);
            PlayOperationSound(hintSoundFB);
            PlayHintMotion(false);
            if (lesson == Lesson.Hint) ShowFishLesson();
            RefreshRoundStatus();
        }

        void BindHintButton()
        {
            if (!hintButton)
            {
                var root = gameRoot.Find("HintButton");
                if (root) hintButton = root.GetComponent<Button>();
            }
            if (!hintButton || hintButtonBound) return;
            if (!hintBulb)
            {
                var bulb = hintButton.transform.Find("ImageBulb");
                if (bulb) hintBulb = bulb.GetComponent<Image>();
            }
            if (!hintCount)
                foreach (var label in hintButton.GetComponentsInChildren<TMPro.TMP_Text>(true))
                    if (label.name == "Text (TMP)Count") { hintCount = label; break; }

            if (hintButton.targetGraphic) hintButtonMaterial = hintButton.targetGraphic.material;
            if (hintBulb) hintBulbMaterial = hintBulb.material;
            if (!hintGrayscaleShader) hintGrayscaleShader = Shader.Find("UI/HintGrayscale");
            if (hintGrayscaleShader) hintGrayMaterial = new Material(hintGrayscaleShader);
            if (!hintButton.TryGetComponent(out hintGroup))
                hintGroup = hintButton.gameObject.AddComponent<CanvasGroup>();
            hintRestAlpha = hintGroup.alpha;
            hintButton.onClick.AddListener(RevealTile);
            hintButtonBound = true;
        }

        void RefreshHintButton(bool canPlay)
        {
            if (hintCount) hintCount.text = round.Hints.ToString();
            if (!hintButton) return;
            // Keep the Button's normal visual state while the whole group fades.
            hintButton.interactable = true;
            if (hintGroup) hintGroup.blocksRaycasts = !revealingBoard;
            bool spent = round.Hints == 0 && hintGrayMaterial;
            if (hintButton.targetGraphic)
                hintButton.targetGraphic.material = spent ? hintGrayMaterial : hintButtonMaterial;
            if (hintBulb) hintBulb.material = spent ? hintGrayMaterial : hintBulbMaterial;
        }

        void SetHintOpacity(float opacity)
        {
            if (hintGroup) hintGroup.alpha = hintRestAlpha * opacity;
        }

        void PlayHintMotion(bool shake)
        {
            StopHintMotion();
            movingHint = shake || !hintBulb ? (RectTransform)hintButton.transform : hintBulb.rectTransform;
            hintRestPosition = movingHint.anchoredPosition;
            hintRestScale = movingHint.localScale;
            hintMotion = StartCoroutine(AnimateHint(shake));
        }

        IEnumerator AnimateHint(bool shake)
        {
            const float duration = .32f;
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                float t = elapsed / duration;
                if (shake)
                    movingHint.anchoredPosition = hintRestPosition +
                        Vector2.right * (Mathf.Sin(t * Mathf.PI * 6f) * (1f - t) * 13f);
                else
                {
                    float amount = t < .3f ? Mathf.SmoothStep(0f, 1f, t / .3f)
                        : Mathf.SmoothStep(1f, 0f, (t - .3f) / .7f);
                    movingHint.localScale = hintRestScale * (1f + .26f * amount);
                }
                yield return null;
            }
            hintMotion = null;
            RestoreHintPose();
        }

        void StopHintMotion()
        {
            if (hintMotion != null) StopCoroutine(hintMotion);
            hintMotion = null;
            RestoreHintPose();
        }

        void RestoreHintPose()
        {
            if (!movingHint) return;
            movingHint.anchoredPosition = hintRestPosition;
            movingHint.localScale = hintRestScale;
            movingHint = null;
        }

        void PaintTile(int index)
        {
            var state = round.GetTile(index);
            var image = tileImages[index];
            var tag = tags[index];
            tag.color = originalTagColors[index];
            tag.gameObject.SetActive(state == NonogramRound.Tile.Marked);
            image.color = originalColors[index];
            if (state == NonogramRound.Tile.Filled)
            {
                PaintFilledTile(index);
            }
        }

        void PaintFilledTile(int index)
        {
            var brush = SelectedLevel.brushes[SelectedLevel.cells[index].brushId];
            var image = tileImages[index];
            image.sprite = patternMode ? LevelEditorRuntime.GetShapeSprite(brush.shape) : originalTileSprites[index];
            image.color = patternMode ? Ink : BrushColor(brush);
        }

        void UpdateRoundPatternMode(bool enabled)
        {
            if (round == null || !roundBoardLayout || patternMode == enabled) return;
            patternMode = enabled;
            roundBoardLayout.SetPatternMode(enabled);
            if (lesson == Lesson.ColorGreen) SetLesson(lesson);
            RefreshBrushControls();
            for (int index = 0; index < tileImages.Length; index++)
                if (round.GetTile(index) == NonogramRound.Tile.Filled)
                {
                    cellFeedbacks[index].Restore();
                    PaintFilledTile(index);
                }
        }

        IEnumerator ShakeMistake(int index, bool flash = true)
        {
            tileShaking[index] = true;
            var tile = tileRects[index];
            var image = tileImages[index];
            var tag = tags[index];
            if (flash) image.color = new Color32(246, 162, 164, 255);
            if (flash && round.GetTile(index) == NonogramRound.Tile.Blank)
            {
                tag.color = ErrorRed;
                tag.gameObject.SetActive(true);
            }
            Vector2 origin = tile.anchoredPosition;
            const float duration = .24f;
            for (float elapsed = 0; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                if (!tile || !tile.gameObject.activeInHierarchy) yield break;
                float t = elapsed / duration;
                float offset = Mathf.Sin(t * Mathf.PI * 6f) * (1f - t) * 6f;
                tile.anchoredPosition = origin + Vector2.right * offset;
                yield return null;
            }
            if (!tile || !tile.gameObject.activeInHierarchy) yield break;
            tile.anchoredPosition = origin;
            tileShaking[index] = false;
            if (!flash) yield break;
            if (round.GetTile(index) != NonogramRound.Tile.Filled)
            {
                image.color = originalColors[index];
                bool locked = round.GetTile(index) == NonogramRound.Tile.Mistake;
                tag.color = locked ? ErrorRed : originalTagColors[index];
                tag.gameObject.SetActive(locked);
            }
        }

        void RefreshRoundStatus(bool manualFill = false)
        {
            RewardCompletedLines(manualFill);
            RefreshScoreDisplay();
            bool canPlay = !revealingBoard && round.Outcome == NonogramRound.Result.Playing;
            RefreshBrushControls();
            RefreshHintButton(canPlay && TutorialAllowsHint);
            retryButton.gameObject.SetActive(!revealingBoard &&
                ((round.Outcome == NonogramRound.Result.Won && (!successPanel || !nextLevelButton)) ||
                 (round.Outcome == NonogramRound.Result.Lost && (!failPanel || !replayLevelButton))));
            if (revealingBoard || round.Outcome != NonogramRound.Result.Won || victoryShown) return;
            if (attempt != null)
            {
                NonogramProgress.CompleteAttempt(attempt, Time.unscaledTime - startedAt, round.Stars, round.Score);
                attempt = null;
            }
            if (lesson == Lesson.Hint) ShowFishLesson();
            if (lesson == Lesson.Fish) return;
            victoryShown = true;
            if (successPanel && nextLevelButton) nextLevelLoad = StartCoroutine(ShowVictory());
        }

        void ResetScoreDisplay()
        {
            if (!roundScoreLabel)
            {
                foreach (var label in gameRoot.GetComponentsInChildren<TMPro.TMP_Text>(true))
                {
                    if (label.name != "Text (TMP) Score1") continue;
                    roundScoreLabel = label;
                    break;
                }
            }
            displayedScore = 0;
            if (!roundScoreLabel) return;
            scoreRestScale = roundScoreLabel.rectTransform.localScale;
            roundScoreLabel.text = "0";
        }

        void RefreshScoreDisplay()
        {
            int gain = round.Score - displayedScore;
            if (gain <= 0 || !roundScoreLabel) return;
            displayedScore = round.Score;
            roundScoreLabel.text = displayedScore.ToString(System.Globalization.CultureInfo.InvariantCulture);

            var scale = roundScoreLabel.rectTransform.localScale;
            if (scorePop != null) StopCoroutine(scorePop);
            scorePop = StartCoroutine(PlayScorePop(gain, scale));
        }

        IEnumerator PlayScorePop(int gain, Vector3 startScale)
        {
            float strength = Mathf.Sqrt(Mathf.InverseLerp(10f, 400f, gain));
            Vector3 peak = scoreRestScale * Mathf.Lerp(1.10f, 1.44f, strength);
            float duration = Mathf.Lerp(.26f, .58f, strength);
            var rect = roundScoreLabel.rectTransform;
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                float t = elapsed / duration;
                rect.localScale = t < .25f
                    ? Vector3.Lerp(startScale, peak, Mathf.SmoothStep(0f, 1f, t / .25f))
                    : Vector3.Lerp(peak, scoreRestScale, Mathf.SmoothStep(0f, 1f, (t - .25f) / .75f));
                yield return null;
            }
            rect.localScale = scoreRestScale;
            scorePop = null;
        }

        void StopScorePop()
        {
            if (scorePop == null) return;
            StopCoroutine(scorePop);
            scorePop = null;
            if (roundScoreLabel) roundScoreLabel.rectTransform.localScale = scoreRestScale;
        }
    }
}
