using UnityEngine;
using UnityEngine.UI;

// Renders a regular ParticleSystem through a camera into a RawImage. Scene
// cameras and textures make the effect visible in edit mode too.
public sealed class UIResultParticles : MonoBehaviour
{
    const int ParticleLayer = 31;
    static readonly Vector3 StagePosition = new Vector3(10000f, 10000f, 0f);
    RenderTexture texture;
    GameObject stage;
    GameObject spawnedEffect;
    Camera particleCamera;
    bool ownsStage;
    bool ownsTexture;
    bool ownsImage;

    public static UIResultParticles Play(RectTransform panel, GameObject prefab, float cameraSize,
        RawImage image, Camera camera)
    {
        bool createImage = !image || !camera;
        if (createImage)
        {
            if (!prefab) return null;
            var rect = new GameObject("Result UI Particles", typeof(RectTransform), typeof(RawImage))
                .GetComponent<RectTransform>();
            rect.SetParent(panel, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            image = rect.GetComponent<RawImage>();
            camera = null;
        }

        image.raycastTarget = false;
        image.rectTransform.SetAsLastSibling();
        var player = image.GetComponent<UIResultParticles>();
        if (!player) player = image.gameObject.AddComponent<UIResultParticles>();
        player.ownsImage = createImage;

        if (!camera)
        {
            player.stage = new GameObject("Result Particle Camera");
            camera = player.stage.AddComponent<Camera>();
            camera.transform.position = StagePosition + Vector3.back * 10f;
            camera.cullingMask = 1 << ParticleLayer;
            player.ownsStage = true;
            int width = 512;
            int height = Mathf.Clamp(Mathf.RoundToInt(width * (float)Screen.height /
                Mathf.Max(1, Screen.width)), 512, 1024);
            player.texture = new RenderTexture(width, height, 16, RenderTextureFormat.ARGB32);
            player.texture.Create();
            camera.targetTexture = player.texture;
            player.ownsTexture = true;
        }
        else
        {
            player.stage = camera.gameObject;
            player.texture = camera.targetTexture;
        }

        player.particleCamera = camera;
        camera.orthographic = true;
        camera.orthographicSize = cameraSize;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.clear;
        camera.depth = -100f;
        camera.nearClipPlane = .1f;
        camera.farClipPlane = 100f;
        camera.enabled = true;
        image.texture = player.texture;

        Transform effectParent = camera.transform.Find("Particle Stage");
        if (!effectParent) effectParent = camera.transform;
        if (prefab)
        {
            Vector3 position = camera.transform.position + camera.transform.forward * 10f;
            player.spawnedEffect = Instantiate(prefab, position, Quaternion.identity, effectParent);
            player.spawnedEffect.name = prefab.name;
            if (createImage) SetLayerRecursively(player.spawnedEffect.transform, ParticleLayer);
            player.spawnedEffect.SetActive(true);
        }
        foreach (var particle in effectParent.GetComponentsInChildren<ParticleSystem>())
        {
            particle.Clear();
            particle.Play();
        }
        return player;
    }

    public void StopEffect()
    {
        if (particleCamera) particleCamera.enabled = false;
        if (spawnedEffect) Destroy(spawnedEffect);
        spawnedEffect = null;
        if (ownsImage)
            Destroy(gameObject);
        else if (particleCamera)
            foreach (var particle in particleCamera.GetComponentsInChildren<ParticleSystem>())
                particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    static void SetLayerRecursively(Transform root, int layer)
    {
        root.gameObject.layer = layer;
        foreach (Transform child in root) SetLayerRecursively(child, layer);
    }

    void OnDestroy()
    {
        if (ownsStage && stage) Destroy(stage);
        if (ownsTexture && texture)
        {
            texture.Release();
            Destroy(texture);
        }
    }
}
