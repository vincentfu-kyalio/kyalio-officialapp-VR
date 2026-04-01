using Kyalio.Core;
using Kyalio.Dev;
using UnityEngine;

/// <summary>
/// Drop into the scene to control dev startup behavior.
///
/// Use Fake Data — bypasses all API calls; every page loads fake content.
///                 Works on device (Meta Quest) as well as in the Editor.
/// Start Page     — the page shown immediately on startup when fake data is on.
///
/// When Use Fake Data is off the app starts normally (Login flow).
/// </summary>
public class DevBootstrapper : MonoBehaviour
{
    [SerializeField] private bool _useFakeData = true;
    [SerializeField] private PageType _startPage = PageType.ProjectInfo;

    private void Start()
    {
        DevFlags.UseFakeData = _useFakeData;

        if (_useFakeData)
        {
            FakeDataSeeder.Seed();
            UIManager.Instance.GoTo(_startPage);
        }
    }
}
