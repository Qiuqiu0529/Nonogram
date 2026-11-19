using System.Collections;
using UnityEngine;

public sealed class UIIconPop : MonoBehaviour
{
    Coroutine animation;
    Vector3 restingScale;

    public void Play()
    {
        StopPop();
        restingScale = transform.localScale;
        animation = StartCoroutine(Pop());
    }

    IEnumerator Pop()
    {
        const float duration = .28f;
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            float t = elapsed / duration;
            float amount = t < .3f ? Mathf.SmoothStep(0f, 1f, t / .3f)
                : Mathf.SmoothStep(1f, 0f, (t - .3f) / .7f);
            transform.localScale = restingScale * (1f + .22f * amount);
            yield return null;
        }
        animation = null;
        transform.localScale = restingScale;
    }

    void StopPop()
    {
        if (animation == null) return;
        StopCoroutine(animation);
        animation = null;
        transform.localScale = restingScale;
    }

    void OnDisable() => StopPop();
}
