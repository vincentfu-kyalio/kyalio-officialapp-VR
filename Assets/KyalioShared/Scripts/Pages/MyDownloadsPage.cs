using Kyalio.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Kyalio.Pages
{
    /// <summary>
    /// My Downloads page: UI preserved, functionality deferred.
    /// </summary>
    public class MyDownloadsPage : MonoBehaviour, IPageHandler
    {
        [SerializeField] private Button backButton;

        private void Awake()
        {
            backButton.onClick.AddListener(() => UIManager.Instance.GoBack());
        }

        public void OnEnter(object param)
        {
            // TODO: Download functionality to be implemented
        }

        public void OnExit() { }
    }
}
