using System.Collections;
using UnityEngine;

/// <summary>
/// 全場景 Fade In / Out，適用於 VR（含 3D、Skybox、UI）。
/// 掛在 FadeSphere GameObject 上，FadeSphere 需為 Main Camera 的子物件。
/// Material 使用 URP Unlit + Transparent，Render Face = Back。
/// </summary>
public class SceneFader : MonoBehaviour
{
    public static SceneFader Instance { get; private set; }

    [SerializeField] float defaultDuration = 0.5f;

    static readonly int ColorId = Shader.PropertyToID("_BaseColor");

    MeshRenderer _meshRenderer;
    MaterialPropertyBlock _propBlock;
    Coroutine _fadeCoroutine;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        _meshRenderer = GetComponent<MeshRenderer>();
        _propBlock = new MaterialPropertyBlock();

        SetAlpha(0f);
    }

    // ─── Public API ───────────────────────────────────────────

    /// <summary>畫面漸黑（播放影片前呼叫）</summary>
    public void FadeOut(float duration = -1f, System.Action onComplete = null)
    {
        StartFade(0f, 1f, duration < 0f ? defaultDuration : duration, onComplete);
    }

    /// <summary>畫面漸亮（影片開始後呼叫）</summary>
    public void FadeIn(float duration = -1f, System.Action onComplete = null)
    {
        StartFade(1f, 0f, duration < 0f ? defaultDuration : duration, onComplete);
    }

    /// <summary>先 Fade Out，callback 後再 Fade In</summary>
    public void FadeOutThenIn(System.Action onBlack, float duration = -1f)
    {
        float d = duration < 0f ? defaultDuration : duration;
        StartFade(0f, 1f, d, () =>
        {
            onBlack?.Invoke();
            StartFade(1f, 0f, d, null);
        });
    }

    // ─── Internal ─────────────────────────────────────────────

    void StartFade(float from, float to, float duration, System.Action onComplete)
    {
        if (_fadeCoroutine != null)
            StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = StartCoroutine(FadeRoutine(from, to, duration, onComplete));
    }

    IEnumerator FadeRoutine(float from, float to, float duration, System.Action onComplete)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            SetAlpha(Mathf.Lerp(from, to, elapsed / duration));
            yield return null;
        }
        SetAlpha(to);
        onComplete?.Invoke();
    }

    void SetAlpha(float alpha)
    {
        _meshRenderer.GetPropertyBlock(_propBlock);
        _propBlock.SetColor(ColorId, new Color(0f, 0f, 0f, alpha));
        _meshRenderer.SetPropertyBlock(_propBlock);
    }
}
