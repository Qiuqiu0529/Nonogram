using System;
using System.IO;
using Nonogram;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

[CustomEditor(typeof(NonogramLevelListData))]
public sealed class NonogramLevelListDataEditor : Editor
{
    ReorderableList levels;

    void OnEnable()
    {
        var files = serializedObject.FindProperty("levelFiles");
        levels = new ReorderableList(serializedObject, files, true, true, true, true);
        levels.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "关卡文件与难度");
        levels.drawElementCallback = (rect, index, active, focused) =>
        {
            rect.y += 2f;
            rect.height = EditorGUIUtility.singleLineHeight;
            var file = files.GetArrayElementAtIndex(index);
            EditorGUI.LabelField(new Rect(rect.x, rect.y, 52f, rect.height), $"第 {index + 1} 关");
            var fileRect = new Rect(rect.x + 54f, rect.y, rect.width - 126f, rect.height);
            EditorGUI.PropertyField(fileRect, file, GUIContent.none);
            EditorGUI.LabelField(new Rect(rect.xMax - 67f, rect.y, 67f, rect.height),
                "难度 " + ReadDifficulty(file.stringValue));
        };
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.HelpBox("难度从关卡 JSON 读取：1 普通，2 困难，3 超难。", MessageType.Info);
        levels.DoLayoutList();
        serializedObject.ApplyModifiedProperties();
    }

    static string ReadDifficulty(string filename)
    {
        if (string.IsNullOrWhiteSpace(filename)) return "—";
        try
        {
            string file = filename.Trim();
            if (file != Path.GetFileName(file)) return "—";
            if (!file.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) file += ".json";
            string path = Path.Combine(Application.streamingAssetsPath, "Levels", file);
            if (!File.Exists(path)) return "缺失";
            var level = JsonUtility.FromJson<NonogramLevelData>(File.ReadAllText(path));
            int number = (int)level.difficulty + 1;
            return number >= 1 && number <= 3 ? number.ToString() : "无效";
        }
        catch (Exception)
        {
            return "无效";
        }
    }
}
