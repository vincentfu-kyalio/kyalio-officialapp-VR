using UnityEngine;

namespace Kyalio.Components
{
    /// <summary>
    /// 影院模式：播放影片時隱藏 3D 場景並把相機天空盒改為純色背景，
    /// 影片結束或離開播放頁時還原。
    ///
    /// 變更發生在畫面「全黑的瞬間」，因此使用者不會看到場景被硬切掉。
    /// 全黑可以有兩個來源：
    ///   1. 由 <see cref="Kyalio.Core.UIManager"/> 切頁 fade 帶入時，呼叫 Enter/Exit(fade:false)
    ///      做「即時」切換（fade 已由 UIManager 負責，避免雙重淡入淡出）。
    ///   2. 沒有切頁時（例如影片自然播放結束），呼叫 Enter/Exit(fade:true)，
    ///      由本元件透過 <see cref="SceneFader"/> 自行 fade。
    ///
    /// 掛在常駐的空物件上（與相機同層級即可，不要放在會被切換的頁面底下）。
    /// Inspector 指定：
    ///   - Scene Root：3D 場景母物件（含所有子物件，會整個 SetActive(false)）
    ///   - Target Camera：主相機（留空則自動抓 Camera.main）
    ///   - Solid Color：影片播放時的純色背景（預設黑）
    /// </summary>
    public class CinemaModeController : MonoBehaviour
    {
        public static CinemaModeController Instance { get; private set; }

        [SerializeField] private GameObject _sceneRoot;
        [SerializeField] private Camera _targetCamera;
        [SerializeField] private Color _solidColor = Color.black;
        [SerializeField] private float _fadeDuration = 0.5f;

        private bool _active;
        private CameraClearFlags _savedClearFlags;
        private Color _savedBackgroundColor;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (_targetCamera == null) _targetCamera = Camera.main;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ─── Public API ───────────────────────────────────────────

        /// <summary>
        /// 進入影院模式：隱藏 3D 場景並把相機天空盒改純色。
        /// fade=true 時自帶 fade out/in；fade=false 時即時切換（用於已被 UIManager fade 包住的情況）。
        /// </summary>
        public void Enter(bool fade = true)
        {
            if (_active) return;
            _active = true;
            Run(ApplyCinema, fade);
        }

        /// <summary>
        /// 離開影院模式：還原 3D 場景與相機天空盒。
        /// fade=true 時自帶 fade out/in；fade=false 時即時切換。
        /// </summary>
        public void Exit(bool fade = true)
        {
            if (!_active) return;
            _active = false;
            Run(RestoreScene, fade);
        }

        // ─── Internal ─────────────────────────────────────────────

        private void Run(System.Action change, bool fade)
        {
            // 需要自帶 fade，且 SceneFader 存在時才走動畫；否則即時切換。
            if (fade && SceneFader.Instance != null)
                SceneFader.Instance.FadeOutThenIn(change, _fadeDuration);
            else
                change();
        }

        private void ApplyCinema()
        {
            if (_targetCamera != null)
            {
                _savedClearFlags = _targetCamera.clearFlags;
                _savedBackgroundColor = _targetCamera.backgroundColor;
                _targetCamera.clearFlags = CameraClearFlags.SolidColor;
                _targetCamera.backgroundColor = _solidColor;
            }

            if (_sceneRoot != null) _sceneRoot.SetActive(false);
        }

        private void RestoreScene()
        {
            if (_targetCamera != null)
            {
                _targetCamera.clearFlags = _savedClearFlags;
                _targetCamera.backgroundColor = _savedBackgroundColor;
            }

            if (_sceneRoot != null) _sceneRoot.SetActive(true);
        }
    }
}
