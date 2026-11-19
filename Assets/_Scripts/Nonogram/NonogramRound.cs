using System;

namespace Nonogram
{
    public sealed class NonogramRound
    {
        public enum Tile { Blank, Marked, Filled, Mistake }
        public enum Result { Playing, Won, Lost }

        public const int MaxChances = 5;
        public const int MaxHints = 3;
        readonly NonogramLevelData level;
        readonly Tile[] tiles;
        readonly int[] rowTargets, columnTargets;
        int remainingTargets;

        public int Chances { get; private set; } = MaxChances;
        public int Hints { get; private set; } = MaxHints;
        public int Score { get; private set; }
        public Result Outcome => Chances == 0 ? Result.Lost : remainingTargets == 0 ? Result.Won : Result.Playing;
        public int Stars => Outcome != Result.Won ? 0 : Chances == 5 ? 3 : Chances >= 3 ? 2 : 1;

        public NonogramRound(NonogramLevelData level)
        {
            this.level = level ?? throw new ArgumentNullException(nameof(level));
            level.RebuildClues();
            tiles = new Tile[level.cells.Length];
            rowTargets = new int[level.height];
            columnTargets = new int[level.width];
            for (int index = 0; index < level.cells.Length; index++)
            {
                if (level.cells[index].state != CellState.Filled) continue;
                rowTargets[index / level.width]++;
                columnTargets[index % level.width]++;
                remainingTargets++;
            }
        }

        public Tile GetTile(int index) => tiles[index];
        public bool IsRowComplete(int row) => level.clues.rows[row].runs.Length > 0 && rowTargets[row] == 0;
        public bool IsColumnComplete(int column) => level.clues.columns[column].runs.Length > 0 && columnTargets[column] == 0;

        public bool Tap(int index, bool mark, int brushId = 0)
        {
            Tile tile = tiles[index];
            if (Outcome != Result.Playing || tile == Tile.Filled || tile == Tile.Mistake) return false;
            if (mark || tile == Tile.Marked)
            {
                tiles[index] = tile == Tile.Marked ? Tile.Blank : Tile.Marked;
            }
            else if (level.cells[index].state == CellState.Filled)
            {
                if (level.cells[index].brushId == brushId) Fill(index);
                else Chances--;
            }
            else
            {
                tiles[index] = Tile.Mistake;
                Chances--;
            }
            return true;
        }

        public int Reveal()
        {
            if (Outcome != Result.Playing || Hints == 0) return -1;
            for (int index = 0; index < tiles.Length; index++)
            {
                if (level.cells[index].state != CellState.Filled || tiles[index] == Tile.Filled) continue;
                Hints--;
                Fill(index);
                return index;
            }
            return -1;
        }

        void Fill(int index)
        {
            tiles[index] = Tile.Filled;
            Score += 10;
            if (--rowTargets[index / level.width] == 0) Score += 30;
            if (--columnTargets[index % level.width] == 0) Score += 30;
            remainingTargets--;
            if (remainingTargets == 0) Score += 100 + Chances * 50;
        }
    }
}
