using UnityEngine;

namespace Kyalio.Components
{
    /// <summary>
    /// 影片球體置中：把球心搬到使用者眼睛的位置，並把影片正面轉到使用者當下的朝向。
    ///
    /// VR180/360 的影像貼在球體內面，正確視點只有球心一處。場景的 Tracking Origin 是
    /// Floor Level（OVRManager），相機高度等於使用者實際身高，而球體擺在地板高度 (y=0)，
    /// 於是眼睛落在球心上方 → 地平線掉到視線下方，看起來就像「相機比影片高」。
    /// 進入播放頁時呼叫 <see cref="Recenter"/> 把球心對到眼睛即可拉平。
    ///
    /// 只取相機的 yaw，不取 pitch/roll，避免使用者進場時剛好低頭而讓地平線歪掉；
    /// 場景中原本擺好的旋轉（球體的 Y 180 翻面）會被保留，只在其上疊加 yaw。
    ///
    /// 掛在帶有影片 Mesh 的物件上（AVPro/ApplyToMesh），由
    /// <see cref="Kyalio.Pages.PlayVideoPage"/> 在 OnEnter 時呼叫。
    /// 這是一次性校正：使用者若在播放中從坐姿改站姿，高度會再次跑掉，
    /// 需要時再從 UI 呼叫一次 <see cref="Recenter"/> 即可。
    /// </summary>
    public class VideoSphereRecenter : MonoBehaviour
    {
        [Tooltip("使用者的眼睛位置（OVRCameraRig 的 CenterEyeAnchor）。留空則自動抓 Camera.main。")]
        [SerializeField] private Transform _targetCamera;

        [Tooltip("關閉時只對齊高度／位置，不把影片正面轉向使用者。")]
        [SerializeField] private bool _alignYaw = true;

        // 場景中作者擺好的旋轉，yaw 對齊時以它為基準疊加。
        private Quaternion _authoredRotation;
        private bool _authoredRotationCaptured;

        private void Awake() => CaptureAuthoredRotation();

        /// <summary>球心對到相機位置，並（可選）把影片正面轉向使用者當下的朝向。</summary>
        public void Recenter()
        {
            var cam = ResolveCamera();
            if (cam == null)
            {
                Debug.LogWarning("[VideoSphereRecenter] No camera found; skipping recenter.");
                return;
            }

            CaptureAuthoredRotation();

            transform.position = cam.position;

            if (_alignYaw)
                transform.rotation = Quaternion.Euler(0f, cam.eulerAngles.y, 0f) * _authoredRotation;
        }

        private Transform ResolveCamera()
        {
            if (_targetCamera != null) return _targetCamera;
            var main = Camera.main;
            if (main != null) _targetCamera = main.transform;
            return _targetCamera;
        }

        // Awake 只會在物件第一次啟用時跑，且 yaw 對齊會覆寫 rotation，
        // 所以只在第一次取得原始旋轉，之後重複呼叫都以它為基準。
        private void CaptureAuthoredRotation()
        {
            if (_authoredRotationCaptured) return;
            _authoredRotation = transform.rotation;
            _authoredRotationCaptured = true;
        }
    }
}
