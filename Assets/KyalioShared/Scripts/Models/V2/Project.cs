using System.Collections.Generic;
using Newtonsoft.Json;

namespace Kyalio.Models.V2
{
    /// <summary>
    /// Playback layout. Replaces the legacy projectionType + stereoLayout + eyeOrder triple.
    /// </summary>
    public static class PlaybackMode
    {
        public const string Flat       = "flat";
        public const string Vr180Sbs   = "vr180_sbs";
        public const string Vr360Mono  = "vr360_mono";
    }

    /// <summary>
    /// Canonical project DTO returned by POST /api/projects/batch and GET /api/projects/{projectId}.
    /// </summary>
    public class Project
    {
        [JsonProperty("projectId")]
        public string ProjectId;

        [JsonProperty("projectVersion")]
        public int ProjectVersion;

        [JsonProperty("thumbnailVersion")]
        public int ThumbnailVersion;

        [JsonProperty("projectName")]
        public string ProjectName;

        [JsonProperty("description")]
        public string Description;

        [JsonProperty("surgeons")]
        public List<string> Surgeons;

        [JsonProperty("institution")]
        public string Institution;

        [JsonProperty("specialtyId")]
        public string SpecialtyId;

        [JsonProperty("roleId")]
        public string RoleId;

        [JsonProperty("programIds")]
        public List<string> ProgramIds;

        /// <summary>
        /// Relative auth-gated proxy URL (e.g. /api/projects/{id}/thumbnail?w=768&amp;v={thumbnailVersion}).
        /// Resolve against API base URL and send Bearer when loading.
        /// </summary>
        [JsonProperty("thumbnailUrl")]
        public string ThumbnailUrl;

        [JsonProperty("playlistCount")]
        public int PlaylistCount;

        [JsonProperty("playlistDurationSeconds")]
        public int PlaylistDurationSeconds;

        [JsonProperty("totalSizeBytes")]
        public long TotalSizeBytes;

        [JsonProperty("playlist")]
        public List<PlaylistItem> Playlist;
    }

    /// <summary>
    /// Single video inside a project's playlist. Watch progress is NOT carried here —
    /// load from /api/me/progress and merge client-side.
    /// </summary>
    public class PlaylistItem
    {
        [JsonProperty("title")]
        public string Title;

        [JsonProperty("description")]
        public string Description;

        [JsonProperty("videoId")]
        public string VideoId;

        /// <summary>
        /// Signed Mux thumbnail URL (768px max-edge baked in). Validity controlled by
        /// the response-level thumbnailsExpireAt. Do not append width/height/time.
        /// </summary>
        [JsonProperty("thumbnailUrl")]
        public string ThumbnailUrl;

        [JsonProperty("durationMs")]
        public int DurationMs;

        [JsonProperty("sizeBytes")]
        public long SizeBytes;

        /// <summary>One of <see cref="PlaybackMode"/> constants.</summary>
        [JsonProperty("playbackMode")]
        public string PlaybackMode;

        public bool IsSBS => PlaybackMode == V2.PlaybackMode.Vr180Sbs;
    }
}
