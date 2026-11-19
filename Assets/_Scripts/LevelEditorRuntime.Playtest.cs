using System;
using Nonogram;
using UnityEngine;
using UnityEngine.UI;

public partial class LevelEditorRuntime
{
    RectTransform playtestOverlay, playtestBrushRoot;
    NonogramLevelData playtestLevel;
    NonogramRound playtestRound;
    Text playtestTimer, playtestStatus, playtestModeLabel, playtestHintLabel;
    Image[] playtestInside, playtestTags;
    Color[] playtestTagColors;
    float playtestStartedAt, playtestElapsed;
    int playtestBrush;
    bool playtestMarkMode, playtestFinished;

    void StartPlaytest()
    {
        if (level == null)
        {
            statusLabel.text = "请先新建关卡";
            return;
        }
        int targets = 0;
        foreach (var cell in level.cells)
            if (cell.state == CellState.Filled) targets++;
        if (targets == 0)
        {
            statusLabel.text = "请先绘制需要填充的格子";
            return;
        }

        ClosePlaytest(false);
        playtestLevel = NonogramLevelJson.Deserialize(NonogramLevelJson.Serialize(level, false));
        playtestRound = new NonogramRound(playtestLevel);
        level.playtest.attempts++;
        playtestBrush = 0;
        playtestMarkMode = false;
        playtestFinished = false;
        playtestElapsed = 0f;

        var background = Panel(root, "Playtest", new Color32(250, 246, 240, 255));
        playtestOverlay = background.GetComponent<RectTransform>();
        Stretch(playtestOverlay);
        playtestOverlay.SetAsLastSibling();
        var top = CreateRect("Playtest Header", playtestOverlay, Vector2.zero, new Vector2(850, 250));
        top.anchorMin = top.anchorMax = new Vector2(.5f, 1f);
        top.pivot = new Vector2(.5f, 1f);
        Label(top, "关卡试玩", 30, new Vector2(0, 88), new Vector2(420, 45), TextAnchor.MiddleCenter);
        playtestTimer = Label(top, "00:00.0", 27, new Vector2(0, 40), new Vector2(260, 40), TextAnchor.MiddleCenter);
        playtestStatus = Label(top, "根据数字填格子；错误会消耗鱼干", 17, new Vector2(0, -1), new Vector2(780, 38), TextAnchor.MiddleCenter);
        playtestModeLabel = Button(top, "当前：填充", new Vector2(-285, -49), new Vector2(170, 45), () =>
        {
            if (playtestFinished) return;
            playtestMarkMode = !playtestMarkMode;
            playtestModeLabel.text = playtestMarkMode ? "当前：标记 ×" : "当前：填充";
        });
        playtestHintLabel = Button(top, "提示 × 3", new Vector2(-90, -49), new Vector2(170, 45), UsePlaytestHint);
        Button(top, "重试", new Vector2(105, -49), new Vector2(150, 45), StartPlaytest);
        Button(top, "结束试玩", new Vector2(293, -49), new Vector2(170, 45), () => ClosePlaytest(true));
        playtestBrushRoot = CreateRect("Playtest Brushes", top, new Vector2(0, -112), new Vector2(760, 62));
        BuildPlaytestBrushes();

        var viewport = CreateRect("Playtest Board Viewport", playtestOverlay, Vector2.zero, Vector2.zero);
        Stretch(viewport);
        viewport.offsetMin = new Vector2(0, 30);
        viewport.offsetMax = new Vector2(0, -285);
        playtestInside = new Image[playtestLevel.cells.Length];
        playtestTags = new Image[playtestLevel.cells.Length];
        playtestTagColors = new Color[playtestLevel.cells.Length];
        var padding = new Vector4(backgroundTopPadding, backgroundLeftPadding,
            backgroundRightPadding, backgroundBottomPadding);
        var playtestBoardLayout = NonogramBoardLayout.Create(viewport, levelBackgroundPrefab, cellPrefab,
            clueFont ? clueFont : uiFont, playtestLevel, patternView, (cell, x, y) =>
            {
                int index = y * playtestLevel.width + x;
                var inside = cell.transform.Find("ImageInside").GetComponent<Image>();
                var tag = cell.transform.Find("ImageTag").GetComponent<Image>();
                inside.enabled = true;
                inside.gameObject.SetActive(true);
                inside.color = Color.white;
                inside.sprite = defaultInsideSprite;
                tag.gameObject.SetActive(false);
                playtestInside[index] = inside;
                playtestTags[index] = tag;
                playtestTagColors[index] = tag.color;
                cell.AddComponent<CellClick>().Init(() => TapPlaytest(index));
            }, padding, clueFontSize, clueFontStyle, boardWidthRatio,
            minFontSize: clueFontMinSize);
        playtestBoardLayout.SetSeparatorOpacity(1f);
        playtestStartedAt = Time.unscaledTime;
        RefreshPlaytestStatus();
    }

    void BuildPlaytestBrushes()
    {
        Clear(playtestBrushRoot);
        int count = playtestLevel.brushes.Length;
        for (int i = 0; i < count; i++)
        {
            int choice = i;
            var brush = playtestLevel.brushes[i];
            var go = Panel(playtestBrushRoot, "Playtest Brush " + (i + 1), Color.white);
            Place((RectTransform)go.transform, new Vector2((i - (count - 1) * .5f) * 86f, 0), new Vector2(68, 52));
            var image = go.GetComponent<Image>();
            if (patternView) image.sprite = GetShapeSprite(brush.shape);
            if (ColorUtility.TryParseHtmlString(brush.colorHex, out var color)) image.color = color;
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color32(105, 69, 54, 255);
            outline.effectDistance = new Vector2(3, -3);
            outline.enabled = i == playtestBrush;
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() =>
            {
                playtestBrush = choice;
                playtestMarkMode = false;
                playtestModeLabel.text = "当前：填充";
                BuildPlaytestBrushes();
            });
        }
    }

    void TapPlaytest(int index)
    {
        if (playtestFinished || playtestRound == null ||
            playtestRound.Outcome != NonogramRound.Result.Playing) return;
        int chances = playtestRound.Chances;
        if (!playtestRound.Tap(index, playtestMarkMode, playtestBrush)) return;
        PaintPlaytestCell(index);
        RefreshPlaytestStatus();
        if (!playtestFinished && playtestRound.Chances < chances)
            playtestStatus.text = "填错了 · 剩余鱼干：" + playtestRound.Chances + " / 5";
    }

    void PaintPlaytestCell(int index)
    {
        var inside = playtestInside[index];
        var tag = playtestTags[index];
        var tile = playtestRound.GetTile(index);
        if (tile == NonogramRound.Tile.Filled)
        {
            var brush = playtestLevel.brushes[playtestLevel.cells[index].brushId];
            inside.sprite = patternView ? GetShapeSprite(brush.shape) : defaultInsideSprite;
            inside.color = patternView ? new Color32(65, 52, 59, 255)
                : ColorUtility.TryParseHtmlString(brush.colorHex, out var color)
                    ? color : new Color32(65, 52, 59, 255);
        }
        else
        {
            inside.sprite = defaultInsideSprite;
            inside.color = Color.white;
        }
        tag.gameObject.SetActive(tile == NonogramRound.Tile.Marked || tile == NonogramRound.Tile.Mistake);
        tag.color = tile == NonogramRound.Tile.Mistake
            ? new Color32(175, 39, 53, 255) : playtestTagColors[index];
    }

    void UsePlaytestHint()
    {
        if (playtestFinished || playtestRound == null) return;
        int index = playtestRound.Reveal();
        if (index < 0) return;
        PaintPlaytestCell(index);
        RefreshPlaytestStatus();
    }

    void RefreshPlaytestStatus()
    {
        if (playtestRound == null) return;
        playtestHintLabel.text = "提示 × " + playtestRound.Hints;
        if (playtestRound.Outcome == NonogramRound.Result.Playing)
        {
            playtestStatus.text = "剩余鱼干：" + playtestRound.Chances + " / 5";
            return;
        }
        if (playtestFinished) return;
        playtestFinished = true;
        playtestElapsed = Time.unscaledTime - playtestStartedAt;
        playtestTimer.text = FormatPlaytestTime(playtestElapsed);
        if (playtestRound.Outcome == NonogramRound.Result.Won)
        {
            var stats = level.playtest;
            stats.clears++;
            stats.latestClearSeconds = playtestElapsed;
            stats.totalClearSeconds += playtestElapsed;
            if (stats.bestClearSeconds <= 0f || playtestElapsed < stats.bestClearSeconds)
                stats.bestClearSeconds = playtestElapsed;
            playtestStatus.text = "通关！用时 " + FormatPlaytestTime(playtestElapsed) +
                " · 最快 " + FormatPlaytestTime(stats.bestClearSeconds) +
                " · 平均 " + FormatPlaytestTime(stats.totalClearSeconds / stats.clears);
        }
        else playtestStatus.text = "挑战失败 · 用时 " + FormatPlaytestTime(playtestElapsed) + " · 可点击重试";
    }

    void Update()
    {
        if (playtestRound == null || playtestFinished || !playtestTimer) return;
        playtestTimer.text = FormatPlaytestTime(Time.unscaledTime - playtestStartedAt);
    }

    void ClosePlaytest(bool showSummary)
    {
        if (showSummary && level != null && statusLabel)
            statusLabel.text = PlaytestSummary() + " · 点击保存关卡写入 JSON";
        playtestRound = null;
        playtestLevel = null;
        playtestTimer = null;
        if (playtestOverlay)
        {
            playtestOverlay.gameObject.SetActive(false);
            Destroy(playtestOverlay.gameObject);
            playtestOverlay = null;
        }
    }

    string PlaytestSummary()
    {
        if (level?.playtest == null) return "暂无试玩记录";
        var stats = level.playtest;
        if (stats.clears == 0) return "试玩 " + stats.attempts + " 次，尚未通关";
        return "试玩 " + stats.attempts + " 次 · 通关 " + stats.clears + " 次 · 最近 " +
            FormatPlaytestTime(stats.latestClearSeconds) + " · 最快 " +
            FormatPlaytestTime(stats.bestClearSeconds) + " · 平均 " +
            FormatPlaytestTime(stats.totalClearSeconds / stats.clears);
    }

    static string FormatPlaytestTime(float seconds) =>
        ((int)(seconds / 60f)).ToString("00") + ":" + (seconds % 60f).ToString("00.0");
}
