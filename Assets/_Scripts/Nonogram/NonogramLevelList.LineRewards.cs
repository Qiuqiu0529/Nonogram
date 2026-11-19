using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Nonogram
{
    public sealed partial class NonogramLevelList
    {
        bool[] rewardedRows, rewardedColumns;
        readonly List<Coroutine> lineRewards = new List<Coroutine>();
        int activeLineRewards;

        void RewardCompletedLines()
        {
            for (int y = 0; y < rewardedRows.Length; y++)
            {
                if (rewardedRows[y] || !round.IsRowComplete(y)) continue;
                rewardedRows[y] = true;
                activeLineRewards++;
                lineRewards.Add(StartCoroutine(PlayLineReward(true, y)));
            }
            for (int x = 0; x < rewardedColumns.Length; x++)
            {
                if (rewardedColumns[x] || !round.IsColumnComplete(x)) continue;
                rewardedColumns[x] = true;
                activeLineRewards++;
                lineRewards.Add(StartCoroutine(PlayLineReward(false, x)));
            }
        }

        static void RippleGraphic(Graphic graphic, float amplitude)
        {
            if (!graphic || !graphic.isActiveAndEnabled) return;
            if (!graphic.TryGetComponent<UIRewardRipple>(out var ripple))
                ripple = graphic.gameObject.AddComponent<UIRewardRipple>();
            ripple.Play(amplitude);
        }

        IEnumerator PlayLineReward(bool row, int line)
        {
            var clue = row ? roundBoardLayout.GetRowClueRect(line) : roundBoardLayout.GetColumnClueRect(line);
            RippleGraphic(clue.GetComponent<Graphic>(), 12f);
            PlayOperationSound(lineCompleteSoundFB);
            var layer = CreateRect("Line Reward Stars", gameOverlay, Vector2.zero, Vector2.one);
            int count = row ? SelectedLevel.width : SelectedLevel.height;
            var stars = new List<Image>();
            var origins = new List<Vector2>();
            var launches = new List<float>();
            var velocities = new List<Vector2>();
            float interval = Mathf.Min(.055f, .65f / count);
            float elapsed = 0f;
            int next = 0;
            const float delay = .10f;
            const float lifetime = .48f;
            float total = delay + (count - 1) * interval + lifetime;
            while (elapsed < total)
            {
                while (next < count && elapsed >= delay + next * interval)
                {
                    int index = row ? line * SelectedLevel.width + next : next * SelectedLevel.width + line;
                    RippleGraphic(tileImages[index], Mathf.Min(16f, tileRects[index].rect.height * .15f));
                    if (tags[index].gameObject.activeSelf) RippleGraphic(tags[index], 10f);
                    Vector2 origin = layer.InverseTransformPoint(tileRects[index].TransformPoint(tileRects[index].rect.center));
                    for (int i = 0; i < 2; i++)
                    {
                        var rect = CreateRect("Star", layer, Vector2.one * .5f, Vector2.one * .5f);
                        float side = Random.Range(13f, 21f);
                        rect.sizeDelta = Vector2.one * side;
                        var image = AddImage(rect, new Color32(255, 190, 66, 255));
                        image.sprite = LevelEditorRuntime.GetShapeSprite("star");
                        image.raycastTarget = false;
                        stars.Add(image);
                        origins.Add(origin);
                        launches.Add(elapsed);
                        velocities.Add(new Vector2(Random.Range(-70f, 70f), Random.Range(90f, 145f)));
                    }
                    next++;
                }
                for (int i = 0; i < stars.Count; i++)
                {
                    float age = elapsed - launches[i];
                    float t = Mathf.Clamp01(age / lifetime);
                    var rect = stars[i].rectTransform;
                    rect.anchoredPosition = origins[i] + velocities[i] * age + Vector2.down * (120f * age * age);
                    rect.localRotation = Quaternion.Euler(0, 0, age * 160f);
                    rect.localScale = Vector3.one * Mathf.Lerp(1f, .4f, t);
                    var color = stars[i].color;
                    color.a = 1f - Mathf.SmoothStep(.25f, 1f, t);
                    stars[i].color = color;
                }
                yield return null;
                elapsed += Time.unscaledDeltaTime;
            }
            Destroy(layer.gameObject);
            activeLineRewards--;
        }

        void StopLineRewards()
        {
            foreach (var routine in lineRewards) StopCoroutine(routine);
            lineRewards.Clear();
            activeLineRewards = 0;
        }
    }
}
