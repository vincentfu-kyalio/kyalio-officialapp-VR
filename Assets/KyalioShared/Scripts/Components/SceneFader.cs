using System.Collections;
using UnityEngine;

/// <summary>
/// 全場景 Fade In / Out，適用於 VR（含 3D、Skybox、UI）。
/// 掛在 FadeSphere GameObject 上，FadeSphere 需為 Main Camera 的子物件。
/// Material 使用 Kyalio/ScreenFade（ScreenFade.mat）：只畫球內側、最後一個 render queue、
/// 忽略深度，所以會蓋過 3D 場景、影片球與永遠置頂的 UI。
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

    /// <summary>先 Fade Out，callback 後再 Fade In</summary>
    public void FadeOutThenIn(System.Action onBlack, float duration = -1f)
        => FadeOutThenIn(onBlack, null, 0f, duration);

    /// <summary>
    /// 先 Fade Out，callback 後維持全黑直到 <paramref name="holdWhile"/> 回傳 false
    /// （最多 <paramref name="maxHold"/> 秒），再 Fade In。
    /// 用於新頁面還沒準備好（例如影片第一幀還沒出來）時，不讓使用者看到空白狀態。
    ///
    /// 轉場進行中再呼叫時會「併入」目前的轉場，而不是中斷它：每個 onBlack 都保證會在全黑時
    /// 依序執行（例如淡出途中按了登出，登出的切頁與清除狀態不會被吃掉）。
    ///   - 淡出中：排隊，到全黑時一起執行。
    ///   - 全黑等待中：立即執行，並加入等待條件。
    ///   - 淡入中或閒置：從目前透明度重新淡出。
    /// </summary>
    public void FadeOutThenIn(System.Action onBlack, System.Func<bool> holdWhile, float maxHold,
        float duration = -1f)
    {
        _pendingOnBlack.Add(onBlack);
        if (holdWhile != null) _holds.Add((holdWhile, Time.unscaledTime + maxHold));

        if (_phase == Phase.FadingOut) return;          // runs when the running fade-out reaches black
        if (_phase == Phase.Holding) { RunPending(); return; }

        // Idle or fading in: fade out again from where the screen is now.
        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = StartCoroutine(Transition(duration < 0f ? defaultDuration : duration));
    }

    // Disabled mid-transition, the coroutine is gone: still run what was queued for black
    // (a page switch, a logout) and clear the screen, so nothing is lost or left black.
    void OnDisable()
    {
        _fadeCoroutine = null;
        if (_phase == Phase.Idle) return;
        _phase = Phase.Idle;
        _holds.Clear();
        RunPending();
        SetAlpha(0f);
    }

    // ─── Internal ─────────────────────────────────────────────

    enum Phase { Idle, FadingOut, Holding, FadingIn }

    Phase _phase = Phase.Idle;
    float _alpha;
    readonly System.Collections.Generic.List<System.Action> _pendingOnBlack = new();
    readonly System.Collections.Generic.List<(System.Func<bool> holdWhile, float until)> _holds = new();

    IEnumerator Transition(float duration)
    {
        _phase = Phase.FadingOut;
        // Partial fade-outs take proportionally less time, so the speed stays constant.
        yield return FadeRoutine(_alpha, 1f, duration * (1f - _alpha), null);

        _phase = Phase.Holding;
        RunPending();
        while (AnyHolding())
        {
            yield return null;
            RunPending();
        }
        _holds.Clear();

        _phase = Phase.FadingIn;
        yield return FadeRoutine(1f, 0f, duration, null);
        _phase = Phase.Idle;
        _fadeCoroutine = null;
    }

    // Runs every queued callback in order, including ones queued by a callback.
    void RunPending()
    {
        while (_pendingOnBlack.Count > 0)
        {
            var action = _pendingOnBlack[0];
            _pendingOnBlack.RemoveAt(0);
            try { action?.Invoke(); }
            catch (System.Exception e) { Debug.LogException(e); }   // one failure must not leave the screen black
        }
    }

    bool AnyHolding()
    {
        foreach (var (holdWhile, until) in _holds)
        {
            bool waiting;
            try { waiting = holdWhile(); }
            catch (System.Exception e) { Debug.LogException(e); waiting = false; }
            if (waiting && Time.unscaledTime < until) return true;
        }
        return false;
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
        _alpha = alpha;

        // Fully clear: skip drawing. The sphere covers the whole view, so even an
        // invisible pass costs a full screen of fill rate on Quest every frame.
        _meshRenderer.enabled = alpha > 0f;

        _meshRenderer.GetPropertyBlock(_propBlock);
        _propBlock.SetColor(ColorId, new Color(0f, 0f, 0f, alpha));
        _meshRenderer.SetPropertyBlock(_propBlock);
    }
}
