using System;
using System.IO;
using Nonogram;
using UnityEngine;
using UnityEngine.UI;

public partial class LevelEditorRuntime
{
    RectTransform loadLevelOverlay;
    Text loadLevelStatus;

    void OpenLoadLevelList()
    {
        CloseLoadLevelList();
        var overlay = Panel(root, "Load Level Overlay", new Color(0f, 0f, 0f, .58f));
        loadLevelOverlay = overlay.GetComponent<RectTransform>();
        Stretch(loadLevelOverlay);
        loadLevelOverlay.SetAsLastSibling();

        var dialog = Panel(loadLevelOverlay, "Saved Levels", new Color32(255, 249, 245, 255));
        Place((RectTransform)dialog.transform, Vector2.zero, new Vector2(770, 860));
        Label(dialog.transform, "加载已保存关卡", 29, new Vector2(-90, 385),
            new Vector2(470, 56), TextAnchor.MiddleCenter);
        Button(dialog.transform, "关闭", new Vector2(320, 385), new Vector2(110, 48), CloseLoadLevelList);
        loadLevelStatus = Label(dialog.transform, "正在读取关卡…", 16,
            new Vector2(0, -382), new Vector2(690, 46), TextAnchor.MiddleCenter);
        loadLevelStatus.horizontalOverflow = HorizontalWrapMode.Wrap;

        var viewportObject = Panel(dialog.transform, "Level Scroll View", Color.white);
        var viewport = viewportObject.GetComponent<RectTransform>();
        Place(viewport, new Vector2(0, -4), new Vector2(690, 675));
        viewportObject.AddComponent<RectMask2D>();
        var scroll = viewportObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;

        var content = CreateRect("Saved Level Rows", viewport, Vector2.zero, Vector2.zero);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = Vector2.one;
        content.pivot = new Vector2(.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;
        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(10, 10, 10, 10);
        layout.spacing = 8f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit =
            ContentSizeFitter.FitMode.PreferredSize;
        scroll.content = content;

        string[] files;
        try
        {
            string folder = NonogramCatalog.DirectoryPath;
            files = Directory.Exists(folder) ? Directory.GetFiles(folder, "*.json") : Array.Empty<string>();
            Array.Sort(files, (a, b) => StringComparer.OrdinalIgnoreCase.Compare(
                Path.GetFileName(b), Path.GetFileName(a)));
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            loadLevelStatus.text = "读取目录失败：" + exception.Message;
            return;
        }

        if (files.Length == 0)
        {
            loadLevelStatus.text = "StreamingAssets/Levels 中没有 JSON 关卡";
            return;
        }

        int unreadable = 0;
        foreach (string file in files)
        {
            string filename = Path.GetFileName(file);
            string description = filename;
            try
            {
                var entry = NonogramLevelJson.LoadFile(file);
                string name = string.IsNullOrWhiteSpace(entry.title) ? filename : entry.title;
                description = name + "  ·  " + entry.width + " × " + entry.height + "  ·  " +
                    DifficultyName(entry.difficulty) + "\n" + filename;
                if (entry.playtest != null && entry.playtest.clears > 0)
                    description += "  ·  最快 " + FormatPlaytestTime(entry.playtest.bestClearSeconds);
            }
            catch (Exception exception)
            {
                unreadable++;
                description = filename + "\n读取失败：" + exception.Message;
            }

            var row = Panel(content, "Level " + filename, new Color32(250, 231, 235, 255));
            row.AddComponent<LayoutElement>().preferredHeight = 86f;
            var button = row.AddComponent<Button>();
            button.targetGraphic = row.GetComponent<Image>();
            button.onClick.AddListener(() => SelectLevelToLoad(filename));
            var text = Label(row.transform, description, 17, Vector2.zero,
                new Vector2(630, 76), TextAnchor.MiddleLeft);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
        }
        loadLevelStatus.text = "共 " + files.Length + " 个关卡" +
            (unreadable > 0 ? "，其中 " + unreadable + " 个读取失败" : "");
    }

    static string DifficultyName(LevelDifficulty value) =>
        value == LevelDifficulty.Difficult ? "困难" :
        value == LevelDifficulty.Super ? "超难" : "普通";

    void ShowPendingSize()
    {
        if (level != null && (level.width != width || level.height != height))
            statusLabel.text = "尺寸待应用：点击「调整当前尺寸」；或点「新建关卡」重新开始。";
    }

    void ResizeCurrentLevel()
    {
        if (level == null)
        {
            statusLabel.text = "请先新建或加载关卡";
            return;
        }
        if (level.width == width && level.height == height)
        {
            statusLabel.text = "当前棋盘已经是 " + width + " × " + height;
            return;
        }

        ClearAmbiguity();

        int oldWidth = level.width, oldHeight = level.height;
        var resized = new NonogramCell[width * height];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            var source = x < oldWidth && y < oldHeight
                ? level.cells[y * oldWidth + x] : null;
            resized[y * width + x] = source == null
                ? new NonogramCell() : new NonogramCell(source.state, source.brushId);
        }
        level.width = width;
        level.height = height;
        level.cells = resized;
        level.playtest = new NonogramPlaytestStats();
        RenderBoard();
        statusLabel.text = "已调整为 " + width + " × " + height +
            "；保留左上角重叠区域，新增格子为空，试玩统计已重置。";
    }

    void SelectLevelToLoad(string filename)
    {
        if (level != null && savedLevelSnapshot != NonogramLevelJson.Serialize(level, false))
        {
            ShowReplaceConfirmation(() => LoadLevelFile(filename));
            return;
        }
        LoadLevelFile(filename);
    }

    void ShowReplaceConfirmation(Action confirm)
    {
        var overlay = Panel(loadLevelOverlay, "Confirm Replace", new Color(0f, 0f, 0f, .62f));
        Stretch((RectTransform)overlay.transform);
        var dialog = Panel(overlay.transform, "Confirmation", new Color32(255, 249, 245, 255));
        Place((RectTransform)dialog.transform, Vector2.zero, new Vector2(620, 245));
        Label(dialog.transform, "当前关卡有未保存的修改。加载其他关卡会丢失这些修改。", 21,
            new Vector2(0, 43), new Vector2(540, 95), TextAnchor.MiddleCenter);
        Button(dialog.transform, "取消", new Vector2(-145, -72), new Vector2(220, 55), () =>
        {
            overlay.SetActive(false);
            Destroy(overlay);
        });
        Button(dialog.transform, "继续加载", new Vector2(145, -72), new Vector2(220, 55), () =>
        {
            overlay.SetActive(false);
            Destroy(overlay);
            confirm();
        });
    }

    void LoadLevelFile(string filename)
    {
        try
        {
            string path = Path.Combine(NonogramCatalog.DirectoryPath, filename);
            var loaded = NonogramLevelJson.LoadFile(path);
            if (loaded.brushes == null || loaded.brushes.Length == 0)
                throw new InvalidDataException("关卡没有可用画笔");
            if (loaded.brushes.Length > Colors.Length)
                throw new InvalidDataException("此编辑器最多支持 " + Colors.Length + " 种画笔");
            if (loaded.width < 5 || loaded.width > 20 || loaded.height < 5 || loaded.height > 20)
                throw new InvalidDataException("编辑器仅支持 5–20 格的关卡");
            if (string.IsNullOrWhiteSpace(loaded.id))
                loaded.id = Path.GetFileNameWithoutExtension(filename);

            ClearAmbiguity();
            level = loaded;
            savedLevelFilename = filename;
            savedLevelSnapshot = NonogramLevelJson.Serialize(level, false);
            width = level.width;
            height = level.height;
            widthSlider.SetValueWithoutNotify(width);
            heightSlider.SetValueWithoutNotify(height);
            UpdateSizeLabels();
            difficulty = level.difficulty;
            difficultyLabel.text = "难度：" + DifficultyName(difficulty);
            patternView = level.clueDisplay == FillKind.Shape;
            displayButtonLabel.text = patternView ? "查看：图案" : "查看：颜色";
            markMode = false;
            modeButtonLabel.text = "当前：填充模式";
            activeBrush = 0;
            BuildBrushes();
            RenderBoard();
            CloseLoadLevelList();
            statusLabel.text = "已加载 " + filename + "。修改后点击「保存关卡」。";
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            if (loadLevelStatus) loadLevelStatus.text = "加载失败：" + exception.Message;
        }
    }

    void CloseLoadLevelList()
    {
        if (!loadLevelOverlay) return;
        loadLevelOverlay.gameObject.SetActive(false);
        Destroy(loadLevelOverlay.gameObject);
        loadLevelOverlay = null;
        loadLevelStatus = null;
    }
}
