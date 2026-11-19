using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Nonogram
{
    public enum LevelDifficulty { Normal = 0, Difficult = 1, Super = 2 }
    public enum CellState { Blank = 0, Filled = 1, Marked = 2 }
    public enum FillKind { Color = 0, Shape = 1 }

    /// <summary>A single selectable fill type. Every brush carries both color and shape representations.</summary>
    [Serializable]
    public class NonogramBrush
    {
        public string id;
        public string name;
        public string colorHex;
        public string shape;
        public NonogramBrush() { }
        public NonogramBrush(string id, string name, string colorHex, string shape)
        { this.id = id; this.name = name; this.colorHex = colorHex; this.shape = shape; }
    }

    [Serializable]
    public class NonogramCell
    {
        public CellState state = CellState.Blank;
        /// <summary>Index into the level's brushes array. Relevant when state is Filled.</summary>
        public int brushId;
        public NonogramCell() { }
        public NonogramCell(CellState state, int brushId = 0) { this.state = state; this.brushId = brushId; }
    }

    [Serializable]
    public class NonogramClueRun
    {
        public int length;
        public int brushId;
        public NonogramClueRun() { }
        public NonogramClueRun(int length, int brushId) { this.length = length; this.brushId = brushId; }
    }

    [Serializable] public class NonogramClueLine { public NonogramClueRun[] runs = new NonogramClueRun[0]; }
    [Serializable] public class NonogramLineClues
    {
        public NonogramClueLine[] rows;
        public NonogramClueLine[] columns;
    }

    [Serializable]
    public class NonogramPlaytestStats
    {
        public int attempts;
        public int clears;
        public float latestClearSeconds;
        public float bestClearSeconds;
        public float totalClearSeconds;
    }

    /// <summary>JSON-ready level model. Cells are row-major: cells[y * width + x].</summary>
    [Serializable]
    public class NonogramLevelData
    {
        public int schemaVersion = 1;
        public string id;
        public string title;
        public LevelDifficulty difficulty;
        public int width;
        public int height;
        /// <summary>Select whether clue runs are presented with brush colors or shapes.</summary>
        public FillKind clueDisplay = FillKind.Color;
        /// <summary>Brush count equals brushes.Length. Each brush has a color and a shape.</summary>
        public NonogramBrush[] brushes = new NonogramBrush[0];
        public NonogramCell[] cells = new NonogramCell[0];
        public NonogramLineClues clues = new NonogramLineClues();
        /// <summary>Authoring-only timing data, saved with the level JSON.</summary>
        public NonogramPlaytestStats playtest = new NonogramPlaytestStats();

        public NonogramLevelData() { }
        public NonogramLevelData(int width, int height)
        {
            this.width = width; this.height = height;
            cells = new NonogramCell[width * height];
            for (int i = 0; i < cells.Length; i++) cells[i] = new NonogramCell();
            clues = new NonogramLineClues { rows = new NonogramClueLine[height], columns = new NonogramClueLine[width] };
        }

        public NonogramCell GetCell(int x, int y) { CheckCoordinates(x, y); return cells[y * width + x]; }
        public void SetCell(int x, int y, NonogramCell cell)
        { CheckCoordinates(x, y); cells[y * width + x] = cell ?? new NonogramCell(); }

        public void Validate()
        {
            if (width < 1 || height < 1) throw new InvalidDataException("Level width and height must be positive.");
            if (cells == null || cells.Length != width * height) throw new InvalidDataException("Cell count must equal width × height.");
            if (brushes == null) brushes = new NonogramBrush[0];
            if (playtest == null) playtest = new NonogramPlaytestStats();
            for (int i = 0; i < brushes.Length; i++)
                if (brushes[i] == null) throw new InvalidDataException("Brush entries cannot be null.");
            for (int i = 0; i < cells.Length; i++)
            {
                if (cells[i] == null) cells[i] = new NonogramCell();
                if (cells[i].state == CellState.Filled && (cells[i].brushId < 0 || cells[i].brushId >= brushes.Length))
                    throw new InvalidDataException("A filled cell references a missing brush.");
            }
        }

        public void RebuildClues()
        {
            Validate();
            clues = new NonogramLineClues { rows = new NonogramClueLine[height], columns = new NonogramClueLine[width] };
            for (int y = 0; y < height; y++) clues.rows[y] = new NonogramClueLine { runs = BuildLine(true, y) };
            for (int x = 0; x < width; x++) clues.columns[x] = new NonogramClueLine { runs = BuildLine(false, x) };
        }

        NonogramClueRun[] BuildLine(bool row, int index)
        {
            var runs = new List<NonogramClueRun>();
            int activeBrush = -1, length = row ? width : height, count = 0;
            for (int p = 0; p < length; p++)
            {
                NonogramCell cell = row ? GetCell(p, index) : GetCell(index, p);
                int brush = cell.state == CellState.Filled ? cell.brushId : -1;
                if (brush != activeBrush)
                {
                    if (count > 0) runs.Add(new NonogramClueRun(count, activeBrush));
                    activeBrush = brush; count = brush < 0 ? 0 : 1;
                }
                else if (brush >= 0) count++;
            }
            if (count > 0) runs.Add(new NonogramClueRun(count, activeBrush));
            return runs.ToArray();
        }

        void CheckCoordinates(int x, int y)
        {
            if (x < 0 || x >= width || y < 0 || y >= height) throw new ArgumentOutOfRangeException("Coordinates are outside the level board.");
            if (cells == null || cells.Length != width * height) throw new InvalidDataException("Level cell data is not initialized.");
        }
    }

    /// <summary>JSON serialization and UTF-8 file persistence for nonogram levels.</summary>
    public static class NonogramLevelJson
    {
        public static string Serialize(NonogramLevelData level, bool prettyPrint = true)
        {
            if (level == null) throw new ArgumentNullException("level");
            level.RebuildClues();
            return JsonUtility.ToJson(level, prettyPrint);
        }

        public static NonogramLevelData Deserialize(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("JSON cannot be empty.", "json");
            NonogramLevelData level = JsonUtility.FromJson<NonogramLevelData>(json);
            if (level == null) throw new InvalidDataException("JSON did not contain a level.");
            level.RebuildClues();
            return level;
        }

        public static void SaveFile(string path, NonogramLevelData level)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Path cannot be empty.", "path");
            string fullPath = Path.GetFullPath(path);
            string directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(fullPath, Serialize(level), new System.Text.UTF8Encoding(false));
        }

        public static NonogramLevelData LoadFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Path cannot be empty.", "path");
            return Deserialize(File.ReadAllText(path));
        }
    }
}
