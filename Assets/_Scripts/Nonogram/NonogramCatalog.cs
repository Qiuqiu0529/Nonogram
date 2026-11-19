using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace Nonogram
{
    public static class NonogramCatalog
    {
        public static string DirectoryPath => Path.Combine(Application.streamingAssetsPath, "Levels");

        public static IEnumerator LoadLevel(string name, Action<NonogramLevelData> onLoaded, Action<string> onError)
        {
            string file = name?.Trim();
            if (string.IsNullOrEmpty(file) || file == "." || file == ".." ||
                file.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                file.Contains("/") || file.Contains("\\") || file.Contains(":"))
            {
                onError($"Invalid level filename: '{name}'. Enter a filename from StreamingAssets/Levels.");
                yield break;
            }
            if (!file.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) file += ".json";
            yield return Read(file, json =>
            {
                NonogramLevelData level;
                try { level = NonogramLevelJson.Deserialize(json); }
                catch (Exception ex) when (ex is ArgumentException || ex is InvalidDataException)
                {
                    onError($"{file}: {ex.Message}");
                    return;
                }
                if (string.IsNullOrWhiteSpace(level.id)) level.id = Path.GetFileNameWithoutExtension(file);
                onLoaded(level);
            }, error => onError($"{file}: {error}"));
        }

        static IEnumerator Read(string name, Action<string> onLoaded, Action<string> onError)
        {
            string path = DirectoryPath;
            string url = path.Contains("://")
                ? path.TrimEnd('/') + "/" + Uri.EscapeDataString(name)
                : new Uri(Path.Combine(path, name)).AbsoluteUri;
            using (var request = UnityWebRequest.Get(url))
            {
                yield return request.SendWebRequest();
                if (request.result == UnityWebRequest.Result.Success) onLoaded(request.downloadHandler.text);
                else onError(request.error);
            }
        }
    }
}
