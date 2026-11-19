using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nonogram
{
    [Serializable]
    public sealed class LevelProgress
    {
        public string levelId;
        public int attempts;
        public int completions;
        public int bestStars;
        public int bestScore;
        public float lastClearSeconds;
        public float bestClearSeconds;
        public string lastClearUtc;
    }

    public static class NonogramProgress
    {
        const string SaveKey = "nonogram.progress.v1";

        [Serializable]
        sealed class ProgressSave
        {
            public List<LevelProgress> levels = new List<LevelProgress>();
        }

        public sealed class Attempt
        {
            internal string LevelId;
            internal int Generation;
            internal bool Completed;
        }

        static ProgressSave save;
        static int generation;
        public static event Action Changed;

        static ProgressSave Data
        {
            get
            {
                if (save != null) return save;
                try
                {
                    string json = PlayerPrefs.GetString(SaveKey, "");
                    if (json.Length > 0) save = JsonUtility.FromJson<ProgressSave>(json);
                }
                catch (ArgumentException ex) { Debug.LogWarning(ex.Message); }
                if (save?.levels == null) save = new ProgressSave();
                return save;
            }
        }

        public static LevelProgress Get(string levelId)
        {
            var record = Data.levels.Find(item => item != null && item.levelId == levelId);
            return record == null ? new LevelProgress { levelId = levelId } : new LevelProgress
            {
                levelId = record.levelId, attempts = record.attempts, completions = record.completions,
                bestStars = record.bestStars,
                bestScore = record.bestScore,
                lastClearSeconds = record.lastClearSeconds, bestClearSeconds = record.bestClearSeconds,
                lastClearUtc = record.lastClearUtc
            };
        }

        public static Attempt BeginAttempt(string levelId)
        {
            if (string.IsNullOrWhiteSpace(levelId)) throw new ArgumentException("A level ID is required.", nameof(levelId));
            var record = Get(levelId);
            record.attempts++;
            Store(record);
            return new Attempt { LevelId = levelId, Generation = generation };
        }

        public static bool CompleteAttempt(Attempt attempt, float elapsedSeconds, int stars = 0, int score = 0)
        {
            if (attempt == null || attempt.Completed || attempt.Generation != generation ||
                string.IsNullOrEmpty(attempt.LevelId) || elapsedSeconds < 0 ||
                float.IsNaN(elapsedSeconds) || float.IsInfinity(elapsedSeconds)) return false;
            attempt.Completed = true;
            var record = Get(attempt.LevelId);
            if (record.completions == 0 || elapsedSeconds < record.bestClearSeconds)
                record.bestClearSeconds = elapsedSeconds;
            record.completions++;
            record.bestStars = Mathf.Max(record.bestStars, Mathf.Clamp(stars, 0, 3));
            record.bestScore = Mathf.Max(record.bestScore, score);
            record.lastClearSeconds = elapsedSeconds;
            record.lastClearUtc = DateTime.UtcNow.ToString("O");
            Store(record);
            return true;
        }

        public static void ResetAll()
        {
            generation++;
            save = new ProgressSave();
            PlayerPrefs.DeleteKey(SaveKey);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        static void Store(LevelProgress record)
        {
            Data.levels.RemoveAll(item => item != null && item.levelId == record.levelId);
            Data.levels.Add(record);
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(Data));
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }
}
