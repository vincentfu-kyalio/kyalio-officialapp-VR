using System.Collections.Generic;
using Kyalio.Models.V2;

namespace Kyalio.State
{
    /// <summary>
    /// Current video playback state.
    /// Written by VideoPlayerController; read by VideoPlayerUI.
    /// </summary>
    public class PlaybackState
    {
        private static PlaybackState _instance;
        public static PlaybackState Instance => _instance ??= new PlaybackState();

        public string ProjectId { get; private set; }
        public PlaylistItem CurrentItem { get; private set; }
        public StreamResponse CurrentStream { get; private set; }

        // ── Playlist queue (set when Play All is used) ───────────────
        public List<PlaylistItem> Playlist { get; private set; }
        public int PlaylistIndex { get; private set; }
        public bool HasNext => Playlist != null && PlaylistIndex < Playlist.Count - 1;
        public PlaylistItem NextItem => HasNext ? Playlist[PlaylistIndex + 1] : null;
        public bool HasPrev => Playlist != null && PlaylistIndex > 0;
        public PlaylistItem PrevItem => HasPrev ? Playlist[PlaylistIndex - 1] : null;

        /// <summary>
        /// Whether the current video is a Side-by-Side stereo video (determined by the PlaylistItem).
        /// </summary>
        public bool IsSBS => CurrentItem?.IsSBS ?? false;

        /// <summary>
        /// Whether the user has activated Cardboard mode (only meaningful when IsSBS == true).
        /// </summary>
        public bool IsCardboardActive { get; private set; } = false;

        /// <summary>
        /// Current playback position in milliseconds; periodically updated by VideoPlayerController.
        /// </summary>
        public long CurrentPositionMs { get; set; }

        public void SetPlayback(string projectId, PlaylistItem item, StreamResponse stream)
        {
            ProjectId = projectId;
            CurrentItem = item;
            CurrentStream = stream;
            IsCardboardActive = false;
            CurrentPositionMs = 0;
        }

        public void SetCardboardActive(bool active)
        {
            // Cardboard mode can only be activated for SBS videos
            IsCardboardActive = IsSBS && active;
        }

        /// <summary>
        /// Sets the continuous playback playlist; VideoPlayerController automatically calls AdvancePlaylist when each item finishes.
        /// </summary>
        public void SetPlaylist(List<PlaylistItem> playlist, int startIndex = 0)
        {
            Playlist = playlist;
            PlaylistIndex = System.Math.Max(0, System.Math.Min(startIndex, playlist.Count - 1));
        }

        public void AdvancePlaylist()
        {
            if (HasNext) PlaylistIndex++;
        }

        public void RewindPlaylist()
        {
            if (HasPrev) PlaylistIndex--;
        }

        public void ClearPlaylist()
        {
            Playlist = null;
            PlaylistIndex = 0;
        }

        public void Clear()
        {
            ProjectId = null;
            CurrentItem = null;
            CurrentStream = null;
            IsCardboardActive = false;
            CurrentPositionMs = 0;
            Playlist = null;
            PlaylistIndex = 0;
        }

        public static void Reset() => _instance = new PlaybackState();
    }
}
