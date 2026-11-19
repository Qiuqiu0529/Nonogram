using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public sealed class UICellFeedback : MonoBehaviour
{
    Coroutine animation;
    Image target;
    Vector3 restingScale;
    Color restingColor;
    bool hideOnFinish;

    public void Play(Image image, bool disappear = false, bool fill = false)
    {
        Restore();
        target = image;
        restingScale = image.rectTransform.localScale;
        restingColor = image.color;
        hideOnFinish = disappear;
        image.gameObject.SetActive(true);
        animation = StartCoroutine(Animate(fill, disappear));
    }

    IEnumerator Animate(bool fill, bool disappear)
    {
        float duration = disappear ? .16f : .24f;
        for (float elapsed = 0; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = Mathf.SmoothStep(0, 1, t);
            float scale;
            if (disappear) scale = 1f - eased;
            else
            {
                float tail = t - 1;
                float pop = 1 + 2.7f * tail * tail * tail + 1.7f * tail * tail;
                scale = Mathf.LerpUnclamped(fill ? .8f : .15f, 1f, pop);
            }
            target.rectTransform.localScale = restingScale * scale;
            Color color = restingColor;
            color.a *= disappear ? 1f - eased : eased;
            target.color = color;
            yield return null;
        }
        animation = null;
        Restore();
    }

    public void Restore()
    {
        if (animation != null) StopCoroutine(animation);
        animation = null;
        if (!target) return;
        target.rectTransform.localScale = restingScale;
        target.color = restingColor;
        if (hideOnFinish) target.gameObject.SetActive(false);
        target = null;
    }

    void OnDisable() => Restore();
}
