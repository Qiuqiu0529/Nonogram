using System.Collections;
using MoreMountains.Feedbacks;
using UnityEngine;
using UnityEngine.UI;

namespace Nonogram
{
    public sealed partial class NonogramLevelList
    {
        [Header("Round result feedback")]
        [SerializeField] MMF_Player successSoundFB;
        [SerializeField] MMF_Player failureSoundFB;
        [SerializeField] GameObject successParticlePrefab;
        [SerializeField] GameObject failureParticlePrefab;
        [SerializeField] RawImage successParticleImage;
        [SerializeField] RawImage failureParticleImage;
        [SerializeField] Camera successParticleCamera;
        [SerializeField] Camera failureParticleCamera;
        [SerializeField, Range(2f, 15f)] float resultParticleCameraSize = 6f;
        [SerializeField, Range(.05f, 1f)] float resultBackdropFadeDuration = .20f;
        [SerializeField, Range(.1f, 2f)] float resultTitleDuration = .64f;
        [SerializeField, Range(.1f, 1.5f)] float resultButtonDuration = .38f;
        [SerializeField, Range(0f, 60f)] float resultTitleRise = 18f;
        [SerializeField, Range(.5f, 1f)] float resultButtonStartScale = .78f;

        Coroutine resultMotion;
        CanvasGroup resultCanvasGroup;
        Text resultTitle;
        string resultTitleText;
        RectTransform resultTitleRect;
        Vector2 resultTitlePosition;
        Vector3 resultTitleScale;
        Quaternion resultTitleRotation;
        RectTransform resultButtonRect;
        Vector3 resultButtonScale;
        CanvasGroup resultButtonGroup;
        float resultButtonAlpha;
        bool resultButtonInteractable, resultButtonBlocksRaycasts;
        UIResultParticles resultParticles;

        void PlayResultPresentation(RectTransform panel, bool won)
        {
            StopResultPresentation();
            if (!panel.TryGetComponent(out resultCanvasGroup))
                resultCanvasGroup = panel.gameObject.AddComponent<CanvasGroup>();
            resultCanvasGroup.alpha = 0f;

            resultTitleRect = panel.Find("Text (Legacy)") as RectTransform;
            resultTitle = resultTitleRect ? resultTitleRect.GetComponent<Text>() : null;
            if (resultTitle)
            {
                resultTitleText = resultTitle.text;
                resultTitlePosition = resultTitleRect.anchoredPosition;
                resultTitleScale = resultTitleRect.localScale;
                resultTitleRotation = resultTitleRect.localRotation;
                resultTitle.text = "";
                resultTitleRect.anchoredPosition = resultTitlePosition + Vector2.down * resultTitleRise;
                resultTitleRect.localScale = resultTitleScale * .88f;
            }

            resultButtonRect = won ? nextButtonRoot : replayButtonRoot;
            if (resultButtonRect)
            {
                resultButtonScale = resultButtonRect.localScale;
                if (!resultButtonRect.TryGetComponent(out resultButtonGroup))
                    resultButtonGroup = resultButtonRect.gameObject.AddComponent<CanvasGroup>();
                resultButtonAlpha = resultButtonGroup.alpha;
                resultButtonInteractable = resultButtonGroup.interactable;
                resultButtonBlocksRaycasts = resultButtonGroup.blocksRaycasts;
                resultButtonGroup.alpha = 0f;
                resultButtonGroup.interactable = false;
                resultButtonGroup.blocksRaycasts = false;
                resultButtonRect.localScale = resultButtonScale * resultButtonStartScale;
            }

            var particlePrefab = won ? successParticlePrefab : failureParticlePrefab;
            var particleImage = won ? successParticleImage : failureParticleImage;
            var particleCamera = won ? successParticleCamera : failureParticleCamera;
            if (particlePrefab || (particleCamera &&
                particleCamera.GetComponentInChildren<ParticleSystem>(true)))
                resultParticles = UIResultParticles.Play(panel, particlePrefab, resultParticleCameraSize,
                    particleImage, particleCamera);
            resultMotion = StartCoroutine(AnimateResult(won));
        }

        IEnumerator AnimateResult(bool won)
        {
            for (float elapsed = 0f; elapsed < resultBackdropFadeDuration; elapsed += Time.unscaledDeltaTime)
            {
                if (resultCanvasGroup)
                    resultCanvasGroup.alpha = Mathf.SmoothStep(0f, 1f,
                        Mathf.Clamp01(elapsed / resultBackdropFadeDuration));
                yield return null;
            }
            if (resultCanvasGroup) resultCanvasGroup.alpha = 1f;

            if (resultTitle)
            {
                for (float elapsed = 0f; elapsed < resultTitleDuration; elapsed += Time.unscaledDeltaTime)
                {
                    float t = Mathf.Clamp01(elapsed / resultTitleDuration);
                    int visible = Mathf.Clamp(Mathf.CeilToInt(t * resultTitleText.Length),
                        0, resultTitleText.Length);
                    resultTitle.text = resultTitleText.Substring(0, visible);
                    float settle = 1f - Mathf.Pow(1f - t, 3f);
                    resultTitleRect.anchoredPosition = Vector2.LerpUnclamped(
                        resultTitlePosition + Vector2.down * resultTitleRise, resultTitlePosition, settle);
                    resultTitleRect.localScale = resultTitleScale *
                        (1f - .12f * Mathf.Exp(-7f * t) * Mathf.Cos(13f * t));
                    if (!won)
                    {
                        float angle = 2.5f * Mathf.Sin(24f * t) * (1f - t);
                        resultTitleRect.localRotation = resultTitleRotation * Quaternion.Euler(0f, 0f, angle);
                    }
                    yield return null;
                }
                resultTitle.text = resultTitleText;
                resultTitleRect.anchoredPosition = resultTitlePosition;
                resultTitleRect.localScale = resultTitleScale;
                resultTitleRect.localRotation = resultTitleRotation;
            }

            if (resultButtonRect)
            {
                for (float elapsed = 0f; elapsed < resultButtonDuration; elapsed += Time.unscaledDeltaTime)
                {
                    float t = Mathf.Clamp01(elapsed / resultButtonDuration);
                    resultButtonGroup.alpha = resultButtonAlpha * Mathf.SmoothStep(0f, 1f, t);
                    resultButtonRect.localScale = resultButtonScale *
                        (1f - (1f - resultButtonStartScale) * Mathf.Exp(-7f * t) * Mathf.Cos(12f * t));
                    yield return null;
                }
                resultButtonRect.localScale = resultButtonScale;
                resultButtonGroup.alpha = resultButtonAlpha;
                resultButtonGroup.interactable = resultButtonInteractable;
                resultButtonGroup.blocksRaycasts = resultButtonBlocksRaycasts;
            }
            resultMotion = null;
        }

        void StopResultPresentation()
        {
            if (resultMotion != null) StopCoroutine(resultMotion);
            resultMotion = null;
            if (resultTitle)
            {
                resultTitle.text = resultTitleText;
                resultTitleRect.anchoredPosition = resultTitlePosition;
                resultTitleRect.localScale = resultTitleScale;
                resultTitleRect.localRotation = resultTitleRotation;
            }
            resultTitle = null;
            resultTitleRect = null;
            if (resultButtonRect)
            {
                resultButtonRect.localScale = resultButtonScale;
                if (resultButtonGroup)
                {
                    resultButtonGroup.alpha = resultButtonAlpha;
                    resultButtonGroup.interactable = resultButtonInteractable;
                    resultButtonGroup.blocksRaycasts = resultButtonBlocksRaycasts;
                }
            }
            resultButtonRect = null;
            resultButtonGroup = null;
            if (resultCanvasGroup) resultCanvasGroup.alpha = 1f;
            resultCanvasGroup = null;
            if (resultParticles) resultParticles.StopEffect();
            resultParticles = null;
        }
    }
}
