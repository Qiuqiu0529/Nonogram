using UnityEngine;

namespace Nonogram
{
    [CreateAssetMenu(menuName = "Nonogram/Level List Data")]
    public sealed class NonogramLevelListData : ScriptableObject
    {
        [SerializeField, Tooltip("Filenames in StreamingAssets/Levels. The .json extension is optional. Element 0 is level 1.")]
        string[] levelFiles = System.Array.Empty<string>();

        public int Count => levelFiles.Length;
        public string GetFile(int index) => levelFiles[index];
    }
}
