using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Graphic))]
public sealed class UIRewardRipple : BaseMeshEffect
{
    Coroutine animation;
    float offset;

    public void Play(float amplitude)
    {
        if (animation != null) StopCoroutine(animation);
        animation = StartCoroutine(Ripple(amplitude));
    }

    IEnumerator Ripple(float amplitude)
    {
        const float duration = .32f;
        for (float elapsed = 0; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            float t = elapsed / duration;
            offset = Mathf.Sin(t * Mathf.PI * 2f) * (1f - t) * amplitude;
            graphic.SetVerticesDirty();
            yield return null;
        }
        offset = 0;
        animation = null;
        graphic.SetVerticesDirty();
    }

    public override void ModifyMesh(VertexHelper vertices)
    {
        if (!IsActive()) return;
        var vertex = new UIVertex();
        for (int i = 0; i < vertices.currentVertCount; i++)
        {
            vertices.PopulateUIVertex(ref vertex, i);
            vertex.position.y += offset;
            vertices.SetUIVertex(vertex, i);
        }
    }

    protected override void OnDisable()
    {
        if (animation != null) StopCoroutine(animation);
        animation = null;
        offset = 0;
        base.OnDisable();
    }
}
