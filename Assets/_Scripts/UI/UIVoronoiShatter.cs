using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public sealed class UIVoronoiShatter : MonoBehaviour
{
    [SerializeField, Min(.01f)] float shakeDuration = .17f;
    [SerializeField, Min(0f)] float shakeDistance = 7f;
    [SerializeField, Min(.01f)] float duration = .65f;
    [SerializeField, Min(0f)] float scatter = .65f;
    [SerializeField, Min(0f)] float gravity = 1.6f;
    [SerializeField, Min(0f)] float rotation = 65f;

    Image image;
    Coroutine routine;
    UIShatterFragments fragments;
    Vector2 restingPosition;
    Quaternion restingRotation;

    void Awake() => image = GetComponent<Image>();

    public void Shatter() => TryShatter();

    public bool TryShatter(System.Action onComplete = null)
    {
        if (!isActiveAndEnabled || routine != null || !image || !image.overrideSprite) return false;
        restingPosition = image.rectTransform.anchoredPosition;
        restingRotation = image.rectTransform.localRotation;
        routine = StartCoroutine(Play(onComplete));
        return true;
    }

    IEnumerator Play(System.Action onComplete)
    {
        image.enabled = true;
        var rect = image.rectTransform;
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / shakeDuration)
        {
            float shake = Mathf.Sin(t * Mathf.PI * 8f) * Mathf.Sin(t * Mathf.PI);
            rect.anchoredPosition = restingPosition + new Vector2(shake * shakeDistance, 0f);
            rect.localRotation = restingRotation * Quaternion.Euler(0f, 0f, shake * 7f);
            yield return null;
        }
        RestorePose();

        var child = new GameObject("Fish Fragments", typeof(RectTransform), typeof(UIShatterFragments));
        child.layer = gameObject.layer;
        var childRect = (RectTransform)child.transform;
        childRect.SetParent(rect, false);
        childRect.anchorMin = Vector2.zero;
        childRect.anchorMax = Vector2.one;
        childRect.pivot = rect.pivot;
        childRect.offsetMin = childRect.offsetMax = Vector2.zero;
        fragments = child.GetComponent<UIShatterFragments>();
        fragments.Initialize(image, scatter, gravity, rotation);
        fragments.SetProgress(0f);
        image.enabled = false;
        yield return new WaitForSecondsRealtime(.07f);

        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / duration)
        {
            fragments.SetProgress(t);
            yield return null;
        }
        ClearFragments();
        routine = null;
        onComplete?.Invoke();
    }

    public void ResetEffect()
    {
        if (!image) return;
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
            RestorePose();
        }
        ClearFragments();
        image.enabled = true;
    }

    void OnDisable() => ResetEffect();

    void RestorePose()
    {
        image.rectTransform.anchoredPosition = restingPosition;
        image.rectTransform.localRotation = restingRotation;
    }

    void ClearFragments()
    {
        if (!fragments) return;
        fragments.gameObject.SetActive(false);
        Destroy(fragments.gameObject);
        fragments = null;
    }
}
