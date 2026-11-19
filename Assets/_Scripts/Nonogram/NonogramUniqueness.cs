using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Nonogram
{
    /// <summary>Counts solutions from clues only. Blank cells and editor marks are not givens.</summary>
    public static class NonogramUniqueness
    {
        public enum Result { NoSolution, Unique, Multiple, Inconclusive }
        const int PatternBudget = 200000;

        public sealed class Report
        {
            public Result result;
            public bool[] differingRows;
            public bool[] differingColumns;
        }

        public static Result Check(int width, int height, NonogramLineClues clues)
        {
            return CheckDetailed(width, height, clues, null).result;
        }

        /// <summary>Compares an alternative solution with the authored cells when supplied.</summary>
        public static Report CheckDetailed(int width, int height, NonogramLineClues clues, int[] authored)
        {
            if (authored != null && authored.Length != width * height)
                throw new ArgumentException("Authored cell count must equal width × height.", nameof(authored));
            var search = new Search(width, height, clues, authored);
            try { return search.Run(); }
            catch (LimitReached) { return new Report { result = Result.Inconclusive }; }
        }

        sealed class LimitReached : Exception { }

        sealed class Search
        {
            readonly int width, height;
            readonly NonogramLineClues clues;
            readonly int[] authored;
            readonly Stopwatch clock = Stopwatch.StartNew();
            List<int[]>[] rows;
            int[][] selectedRows;
            int[] firstSolution, alternativeSolution;
            int patterns, solutions;

            public Search(int width, int height, NonogramLineClues clues, int[] authored)
            {
                this.width = width;
                this.height = height;
                this.clues = clues;
                this.authored = authored;
            }

            public Report Run()
            {
                rows = new List<int[]>[height];
                selectedRows = new int[height][];
                for (int y = 0; y < height; y++) rows[y] = Generate(width, clues.rows[y].runs);
                var columns = new List<int[]>[width];
                for (int x = 0; x < width; x++) columns[x] = Generate(height, clues.columns[x].runs);
                Visit(0, columns);
                var report = new Report
                {
                    result = solutions == 0 ? Result.NoSolution :
                        solutions == 1 ? Result.Unique : Result.Multiple
                };
                if (report.result != Result.Multiple) return report;
                var reference = authored ?? firstSolution;
                report.differingRows = new bool[height];
                report.differingColumns = new bool[width];
                for (int index = 0; index < alternativeSolution.Length; index++)
                {
                    if (reference[index] == alternativeSolution[index]) continue;
                    report.differingRows[index / width] = true;
                    report.differingColumns[index % width] = true;
                }
                return report;
            }

            void Guard()
            {
                if (clock.Elapsed.TotalSeconds > 8) throw new LimitReached();
            }

            List<int[]> Generate(int length, NonogramClueRun[] runs)
            {
                var result = new List<int[]>();
                var line = new int[length];
                PlaceRun(0, 0);
                return result;

                void PlaceRun(int runIndex, int start)
                {
                    Guard();
                    if (runIndex == runs.Length)
                    {
                        if (++patterns > PatternBudget) throw new LimitReached();
                        result.Add((int[])line.Clone());
                        return;
                    }
                    int needed = 0;
                    for (int i = runIndex; i < runs.Length; i++)
                    {
                        needed += runs[i].length;
                        if (i + 1 < runs.Length && runs[i].brushId == runs[i + 1].brushId) needed++;
                    }
                    var run = runs[runIndex];
                    for (int position = start; position <= length - needed; position++)
                    {
                        for (int i = 0; i < run.length; i++) line[position + i] = run.brushId + 1;
                        int gap = runIndex + 1 < runs.Length && run.brushId == runs[runIndex + 1].brushId ? 1 : 0;
                        PlaceRun(runIndex + 1, position + run.length + gap);
                        for (int i = 0; i < run.length; i++) line[position + i] = 0;
                    }
                }
            }

            void Visit(int y, List<int[]>[] columns)
            {
                Guard();
                if (y == height)
                {
                    var solution = new int[width * height];
                    for (int row = 0; row < height; row++)
                        Array.Copy(selectedRows[row], 0, solution, row * width, width);
                    if (authored != null)
                    {
                        if (!SameSolution(authored, solution))
                        {
                            alternativeSolution = solution;
                            solutions = 2;
                        }
                        else solutions = 1;
                    }
                    else if (firstSolution == null)
                    {
                        firstSolution = solution;
                        solutions = 1;
                    }
                    else
                    {
                        alternativeSolution = solution;
                        solutions = 2;
                    }
                    return;
                }
                foreach (var row in rows[y])
                {
                    Guard();
                    selectedRows[y] = row;
                    var filtered = new List<int[]>[width];
                    bool compatible = true;
                    for (int x = 0; x < width; x++)
                    {
                        var candidates = new List<int[]>();
                        foreach (var column in columns[x])
                        {
                            if (column[y] == row[x]) candidates.Add(column);
                        }
                        if (candidates.Count == 0) { compatible = false; break; }
                        filtered[x] = candidates;
                    }
                    if (!compatible) continue;
                    Visit(y + 1, filtered);
                    if (solutions >= 2) return;
                }
            }

            static bool SameSolution(int[] a, int[] b)
            {
                if (a.Length != b.Length) return false;
                for (int i = 0; i < a.Length; i++)
                    if (a[i] != b[i]) return false;
                return true;
            }
        }
    }
}
