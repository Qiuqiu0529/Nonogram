using MoreMountains.Feedbacks;
using UnityEngine;

namespace Nonogram
{
    public sealed partial class NonogramLevelList
    {
        [Header("Operation soundFB prefabs")]
        [SerializeField] MMF_Player levelOpenSoundFB;
        [SerializeField] MMF_Player fillSoundFB;
        [SerializeField] MMF_Player markSoundFB;
        [SerializeField] MMF_Player unmarkSoundFB;
        [SerializeField] MMF_Player mistakeSoundFB;
        [SerializeField] MMF_Player blockedCellSoundFB;
        [SerializeField] MMF_Player hintSoundFB;
        [SerializeField] MMF_Player blockedButtonSoundFB;
        [SerializeField] MMF_Player brushModeSoundFB;
        [SerializeField] MMF_Player markModeSoundFB;
        [SerializeField] MMF_Player selectBrushSoundFB;
        [SerializeField] MMF_Player lineCompleteSoundFB;
        [SerializeField] MMF_Player niceSoundFB;
        [SerializeField] MMF_Player greatSoundFB;
        [SerializeField] MMF_Player fantasticSoundFB;
        readonly System.Collections.Generic.Dictionary<MMF_Player, MMF_Player> operationSounds =
            new System.Collections.Generic.Dictionary<MMF_Player, MMF_Player>();
        UICellFeedback[] cellFeedbacks;

        void InitializeOperationSounds()
        {
            var defaults = Resources.Load<NonogramOperationSounds>("OperationSounds");
            if (defaults)
            {
                if (!levelOpenSoundFB) levelOpenSoundFB = defaults.levelOpen;
                if (!fillSoundFB) fillSoundFB = defaults.fill;
                if (!markSoundFB) markSoundFB = defaults.mark;
                if (!unmarkSoundFB) unmarkSoundFB = defaults.unmark;
                if (!mistakeSoundFB) mistakeSoundFB = defaults.mistake;
                if (!blockedCellSoundFB) blockedCellSoundFB = defaults.blockedCell;
                if (!hintSoundFB) hintSoundFB = defaults.hint;
                if (!blockedButtonSoundFB) blockedButtonSoundFB = defaults.blockedButton;
                if (!brushModeSoundFB) brushModeSoundFB = defaults.brushMode;
                if (!markModeSoundFB) markModeSoundFB = defaults.markMode;
                if (!selectBrushSoundFB) selectBrushSoundFB = defaults.selectBrush;
                if (!lineCompleteSoundFB) lineCompleteSoundFB = defaults.lineComplete;
                if (!niceSoundFB) niceSoundFB = defaults.nice;
                if (!greatSoundFB) greatSoundFB = defaults.great;
                if (!fantasticSoundFB) fantasticSoundFB = defaults.fantastic;
                if (!successSoundFB) successSoundFB = defaults.success;
                if (!failureSoundFB) failureSoundFB = defaults.failure;
            }
            foreach (var prefab in new[] { fillSoundFB, markSoundFB, unmarkSoundFB,
                         mistakeSoundFB, blockedCellSoundFB, hintSoundFB, blockedButtonSoundFB,
                         brushModeSoundFB, markModeSoundFB, selectBrushSoundFB, lineCompleteSoundFB })
            {
                if (prefab) GetOperationSound(prefab);
            }
        }

        MMF_Player GetOperationSound(MMF_Player prefab)
        {
            if (operationSounds.TryGetValue(prefab, out var player) && player) return player;
            var instance = Instantiate(prefab.gameObject, transform);
            instance.name = prefab.name;
            instance.SetActive(true);
            player = instance.GetComponent<MMF_Player>();
            player.InitializationMode = MMFeedbacks.InitializationModes.Script;
            player.Initialization();
            operationSounds[prefab] = player;
            var sound = player.GetFeedbackOfType<MMF_MMSoundManagerSound>();
            if (sound == null)
                Debug.LogWarning($"{prefab.name} has no MMSoundManager Sound feedback.", this);
            else if (!sound.Sfx && (sound.RandomSfx == null || sound.RandomSfx.Length == 0) &&
                     !sound.AudioResourceToPlay && !sound.SoundDataSO)
                Debug.LogWarning($"{prefab.name} has no audio clip configured.", this);
            return player;
        }

        void PlayOperationSound(MMF_Player prefab)
        {
            if (!prefab)
            {
                Debug.LogWarning("Operation soundFB is not assigned.", this);
                return;
            }
            var player = GetOperationSound(prefab);
            if (!player.gameObject.activeInHierarchy)
            {
                Debug.LogWarning($"{player.name} cannot play because its parent is inactive.", this);
                return;
            }
            var manager = MoreMountains.Tools.MMSoundManager.Instance;
            if (!manager.isActiveAndEnabled)
            {
                Debug.LogWarning("MMSoundManager is disabled.", this);
                return;
            }
            player.PlayFeedbacks();
        }

        void AnimateTileChange(int index, NonogramRound.Tile previous)
        {
            switch (round.GetTile(index))
            {
                case NonogramRound.Tile.Filled:
                    cellFeedbacks[index].Play(tileImages[index], fill: true);
                    PlayOperationSound(fillSoundFB);
                    break;
                case NonogramRound.Tile.Marked:
                    cellFeedbacks[index].Play(tags[index]);
                    PlayOperationSound(markSoundFB);
                    break;
                case NonogramRound.Tile.Blank:
                    if (previous != NonogramRound.Tile.Marked) break;
                    cellFeedbacks[index].Play(tags[index], disappear: true);
                    PlayOperationSound(unmarkSoundFB);
                    break;
            }
        }

        void RejectHint()
        {
            PlayHintMotion(true);
            PlayOperationSound(blockedButtonSoundFB);
        }
    }
}
