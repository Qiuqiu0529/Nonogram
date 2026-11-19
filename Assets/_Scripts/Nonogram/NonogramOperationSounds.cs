using MoreMountains.Feedbacks;
using UnityEngine;

namespace Nonogram
{
    public sealed class NonogramOperationSounds : ScriptableObject
    {
        public MMF_Player fill;
        public MMF_Player mark;
        public MMF_Player unmark;
        public MMF_Player mistake;
        public MMF_Player blockedCell;
        public MMF_Player hint;
        public MMF_Player blockedButton;
        public MMF_Player brushMode;
        public MMF_Player markMode;
        public MMF_Player selectBrush;
        public MMF_Player lineComplete;
        public MMF_Player success;
        public MMF_Player failure;
    }
}
