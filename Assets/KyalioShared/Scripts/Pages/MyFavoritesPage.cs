using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Kyalio.Components;
using Kyalio.Core;
using Kyalio.Dev;
using Kyalio.Models;
using Kyalio.Repositories;
using Kyalio.State;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Kyalio.Pages
{
    /// <summary>
    /// Shared panel for Favorites and Downloads lists. Set _mode in the Inspector.
    /// In fake data mode reads from FakeDataSeeder; otherwise calls the API / local state.
    ///
    /// Inspector:
    ///   _mode, titleText
    ///   editButton, deleteButton, cancelButton, selectAllToggle
    ///   projectCountText (Favorites), videoCountText + totalSizeText (Downloads)
    ///   listContainer, itemPrefab
    /// </summary>
    public class MyFavoritesPage : MonoBehaviour, IPageHandler
    {
        public enum ListMode { Favorites, Downloads }

        [Header("Mode")]
        [SerializeField] private ListMode _mode = ListMode.Favorites;

        [Header("Header")]
        [SerializeField] private TextMeshProUGUI titleText;

        [Header("Edit bar — normal mode")]
        [SerializeField] private Button editButton;

        [Header("Edit bar — edit mode")]
        [SerializeField] private Button deleteButton;
        [SerializeField] private Button cancelButton;
        [SerializeField] private Toggle selectAllToggle;

        [Header("Summary")]
        [SerializeField] private TextMeshProUGUI projectCountText;  // Favorites mode
        [SerializeField] private TextMeshProUGUI videoCountText;    // Downloads mode
        [SerializeField] private TextMeshProUGUI totalSizeText;     // Downloads mode

        [Header("List")]
        [SerializeField] private Transform listContainer;
        [SerializeField] private SelectableListItem itemPrefab;

        // ── Runtime state ─────────────────────────────────────────────

        private bool _isEditMode;
        private readonly List<string> _selectedIds = new();
        private readonly List<SelectableListItem> _items = new();
        private CancellationTokenSource _cts;

        // ── Unity lifecycle ───────────────────────────────────────────

        private void Awake()
        {
            editButton.onClick.AddListener(EnterEditMode);
            cancelButton.onClick.AddListener(ExitEditMode);
            deleteButton.onClick.AddListener(ShowConfirmPopup);
            selectAllToggle.onValueChanged.AddListener(OnSelectAllChanged);
        }

        // ── IPageHandler ──────────────────────────────────────────────

        public void OnEnter(object param)
        {
            if (titleText != null)
                titleText.text = _mode == ListMode.Favorites ? "Favorites" : "Downloads";

            if (projectCountText != null)
                projectCountText.gameObject.SetActive(_mode == ListMode.Favorites);
            if (videoCountText != null)
                videoCountText.gameObject.SetActive(_mode == ListMode.Downloads);
            if (totalSizeText != null)
                totalSizeText.gameObject.SetActive(_mode == ListMode.Downloads);

            ExitEditMode();

            if (DevFlags.UseFakeData)
            {
                if (_mode == ListMode.Favorites)
                    BindFavoriteItems(FakeDataSeeder.FakeFavorites);
                else
                    LoadDownloads();
                return;
            }

            // Favorites: skip reload when cache is fresh
            if (_mode == ListMode.Favorites &&
                !AppState.Instance.FavoritesDirty &&
                _items.Count > 0)
                return;

            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            LoadWithOverlayAsync(_cts.Token).Forget();
        }

        public void OnExit()
        {
            _cts?.Cancel();
        }

        // ── Load ──────────────────────────────────────────────────────

        private async UniTaskVoid LoadWithOverlayAsync(CancellationToken ct)
        {
            try   { await LoadListAsync(ct); }
            catch (OperationCanceledException) { }
            catch (Exception e) { Debug.LogError($"[MyFavoritesPage] Load failed: {e.Message}"); }
        }

        private async UniTask LoadListAsync(CancellationToken ct)
        {
            ClearList();

            if (_mode == ListMode.Favorites)
                await LoadFavoritesAsync(ct);
            else
                LoadDownloads();
        }

        private async UniTask LoadFavoritesAsync(CancellationToken ct)
        {
            var response = await ServiceLocator.Instance.FavoriteService.GetFavoritesAsync(ct);
            if (ct.IsCancellationRequested) return;

            var items = response?.Items;
            if (items == null) return;

            BindFavoriteItems(items);
            AppState.Instance.ClearFavoritesDirty();
        }

        private void BindFavoriteItems(List<FavoriteItem> items)
        {
            ClearList();
            int count = 0;
            foreach (var fav in items)
            {
                if (fav.ProjectId == null) continue;
                SpawnItem(fav.ProjectId, fav.ProjectName, fav.CategoryName,
                    fav.DrName, fav.ThumbnailUrl, fav.ProgramPicUrl, fav.VideoCount, true);
                count++;
            }
            if (projectCountText != null)
                projectCountText.text = $"{count} project{(count == 1 ? "" : "s")}";
        }

        private void LoadDownloads()
        {
            var records = DownloadedVideoState.Instance.Records;

            int totalVideos = records.Count;
            long totalBytes = records.Sum(r => r.SizeBytes);
            double gb = totalBytes / (1024.0 * 1024.0 * 1024.0);

            if (videoCountText != null)
                videoCountText.text = $"{totalVideos} video{(totalVideos == 1 ? "" : "s")}";
            if (totalSizeText != null)
                totalSizeText.text = $"{gb:F1} GB";

            var seen = new HashSet<string>();
            var sorted = records.OrderByDescending(r => r.DownloadedAt);

            foreach (var record in sorted)
            {
                if (!seen.Add(record.ProjectId)) continue;

                var p = ProjectCacheRepository.Instance.AllProjects
                    .Find(x => x.Id == record.ProjectId);
                if (p == null) continue;

                long projectBytes = records
                    .Where(r => r.ProjectId == p.Id)
                    .Sum(r => r.SizeBytes);

                SpawnItem(p.Id, p.Name, p.CategoryName,
                    FormatBytes(projectBytes), p.ThumbnailUrl, p.ProgramPicUrl, p.PlaylistCount, false);
            }
        }

        private void SpawnItem(string projectId, string title, string category,
            string drName, string thumbnailUrl, string programPicUrl, int videoCount, bool prefixWithDr)
        {
            var item = Instantiate(itemPrefab, listContainer);
            item.OnSelectionChanged = OnItemSelectionChanged;
            item.OnItemClicked = pid => UIManager.Instance.GoTo(PageType.ProjectInfo,
                new ProjectNavParam { ProjectId = pid, Source = "favorites" });
            item.Bind(projectId, title, category, drName, thumbnailUrl, programPicUrl, videoCount, prefixWithDr);
            item.SetEditMode(_isEditMode);
            _items.Add(item);
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes >= 1_073_741_824) return $"{bytes / 1_073_741_824.0:F1} GB";
            if (bytes >= 1_048_576)     return $"{bytes / 1_048_576.0:F0} MB";
            return bytes > 0 ? $"{bytes / 1024.0:F0} KB" : "0 KB";
        }

        private void ClearList()
        {
            foreach (Transform child in listContainer)
                Destroy(child.gameObject);
            _items.Clear();
            _selectedIds.Clear();
        }

        // ── Edit mode ─────────────────────────────────────────────────

        private void EnterEditMode()
        {
            _isEditMode = true;
            _selectedIds.Clear();

            editButton.gameObject.SetActive(false);
            deleteButton.gameObject.SetActive(true);
            cancelButton.gameObject.SetActive(true);
            selectAllToggle.gameObject.SetActive(true);
            selectAllToggle.SetIsOnWithoutNotify(false);

            foreach (var item in _items)
                item.SetEditMode(true);

            RefreshDeleteButton();
        }

        private void ExitEditMode()
        {
            _isEditMode = false;
            _selectedIds.Clear();

            editButton.gameObject.SetActive(true);
            deleteButton.gameObject.SetActive(false);
            cancelButton.gameObject.SetActive(false);
            selectAllToggle.gameObject.SetActive(false);

            foreach (var item in _items)
                item.SetEditMode(false);

            RefreshDeleteButton();
        }

        private void OnItemSelectionChanged(string projectId, bool selected)
        {
            if (selected) { if (!_selectedIds.Contains(projectId)) _selectedIds.Add(projectId); }
            else _selectedIds.Remove(projectId);

            bool allSelected = _items.Count > 0 && _selectedIds.Count == _items.Count;
            selectAllToggle.SetIsOnWithoutNotify(allSelected);

            RefreshDeleteButton();
        }

        private void OnSelectAllChanged(bool isOn)
        {
            _selectedIds.Clear();

            foreach (var item in _items)
            {
                item.SetSelected(isOn);
                if (isOn) _selectedIds.Add(item.ProjectId);
            }

            RefreshDeleteButton();
        }

        private void RefreshDeleteButton()
        {
            deleteButton.interactable = _selectedIds.Count > 0;
        }

        // ── Confirm popup ─────────────────────────────────────────────

        private void ShowConfirmPopup()
        {
            if (_selectedIds.Count == 0) return;

            string listName = _mode == ListMode.Favorites ? "favorites" : "downloads";
            PopupManager.Instance.ShowDeleteCancel(
                $"Are you sure to remove the selected video(s) from My {listName} list?",
                onDelete: () => OnConfirmRemoveAsync().Forget()
            );
        }

        private async UniTaskVoid OnConfirmRemoveAsync()
        {
            var toDelete = new List<string>(_selectedIds);
            ExitEditMode();

            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;

            try
            {
                if (_mode == ListMode.Favorites)
                {
                    var tasks = toDelete.Select(pid =>
                        ServiceLocator.Instance.FavoriteService.RemoveFavoriteAsync(pid, ct));
                    await UniTask.WhenAll(tasks);
                    if (ct.IsCancellationRequested) return;
                    UserLocalState.Instance.RemoveFavorites(toDelete);
                    AppState.Instance.MarkFavoritesDirty();
                }
                else
                {
                    foreach (var pid in toDelete)
                        DownloadedVideoState.Instance.RemoveAllForProject(pid);
                }

                await LoadListAsync(ct);
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                Debug.LogError($"[MyFavoritesPage] Delete failed: {e.Message}");
            }
        }
    }
}
