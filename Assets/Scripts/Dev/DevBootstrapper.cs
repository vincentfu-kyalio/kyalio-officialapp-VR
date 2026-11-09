using Kyalio.Core;
using Kyalio.Dev;
using UnityEngine;

/// <summary>
/// Drop into the scene to control dev startup behavior.
///
/// Use Fake Data — bypasses all API calls; every page loads fake content.
///                 Works on device (Meta Quest) as well as in the Editor.
/// Start Page     — the page shown immediately on startup when fake data is on.
/// Dev Video Path — video the player page opens in fake-data mode when the
///                  episode has no local download (fake mode has no stream API).
///
/// When Use Fake Data is off the app starts normally (Login flow).
/// </summary>
public class DevBootstrapper : MonoBehaviour
{
    [SerializeField] private bool _useFakeData = true;
    [SerializeField] private PageType _startPage = PageType.ProjectInfo;

    [Tooltip("Relative to StreamingAssets, or an absolute path / URL. Empty = play nothing.")]
    [SerializeField] private string _devVideoPath = "AVProVideoSamples/Cones-360Mono-4K30-H264.mp4";

    private void Start()
    {
        DevFlags.UseFakeData = _useFakeData;
        DevFlags.DevVideoPath = _devVideoPath;

        if (_useFakeData)
        {
            FakeDataSeeder.Seed();
            UIManager.Instance.GoTo(_startPage);
        }
    }
}
