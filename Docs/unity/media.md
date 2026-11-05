# Project Media Endpoints

Stream playback and direct R2 download lifecycle (request URL → complete / cancel / delete).

For the `Project` and `PlaylistItem` schemas, see [schemas.md](schemas.md#5-project).

---

## `GET /api/projects/{projectId}/videos/{videoId}/stream`

Returns a signed Mux playback URL for the selected video.

```json
{
  "streamUrl": "https://stream.mux.com/abcd1234.m3u8?token=<mux-signed-playback-token>&rendition_order=desc",
  "expiresAt": "2026-03-19T22:00:00.000Z",
  "playbackMode": "vr180_sbs"
}
```

Common errors:

- `NOT_FOUND` — project does not exist or is not granted to this member
- `CONTENT_VIDEO_NOT_FOUND`
- `MUX_PLAYBACK_NOT_READY`
- `MUX_SIGNING_FAILED`

---

## `GET /api/projects/{projectId}/videos/{videoId}/download`

Returns a time-limited R2 presigned URL for direct download. The client downloads the file directly from R2 without going through the Worker.

Response:

```json
{
  "downloadUrl": "https://<account>.r2.cloudflarestorage.com/kyalio-video-cms/<key>?X-Amz-Signature=...&X-Amz-Expires=7200&...",
  "expiresAt": "2026-04-25T14:00:00.000Z"
}
```

Notes:

- `downloadUrl` is valid for 2 hours (`expiresAt`). After expiry, R2 returns `403`.
- The client must download directly from `downloadUrl` — no Authorization header is required for the R2 request itself.
- `downloadUrl` supports `Range` requests. To resume an interrupted download, call this endpoint again to get a fresh URL, then issue a `Range: bytes=<offset>-` request to the new URL.
- Client should track `expiresAt` and refresh the URL before it expires if the download is still in progress.
- When download tracking is enabled, this endpoint records a download URL request only. It does not mark the video as fully downloaded on the member's device.

Common errors:

- `NOT_FOUND` — project does not exist or is not granted to this member
- `CONTENT_VIDEO_NOT_FOUND`
- `CONTENT_VIDEO_NOT_DOWNLOADABLE`
- `CONTENT_SOURCE_MISSING`
- `DOWNLOAD_LIMIT_REACHED`
- `CONTENT_DOWNLOAD_FETCH_FAILED`

---

## `POST /api/projects/{projectId}/videos/{videoId}/download/complete`

Marks a direct R2 download as complete after the client has fully downloaded the file and safely written it to local device storage.

Response: `204 No Content`

Notes:

- Call this only after the local file is complete and ready for offline playback.
- When download tracking is enabled, the API records a `completed` event and upserts the server-side downloaded-file record for this member, video, and device kind.
- This endpoint does not download, verify, or read the R2 file. Completion is a client-reported state.

Common errors:

- `NOT_FOUND` — project does not exist or is not granted to this member
- `CONTENT_VIDEO_NOT_FOUND`
- `CONTENT_VIDEO_NOT_DOWNLOADABLE`
- `CONTENT_SOURCE_MISSING`

---

## `POST /api/projects/{projectId}/videos/{videoId}/download/cancel`

Records that an in-progress direct R2 download was canceled, failed, or abandoned before the client stored a complete local file.

Response: `204 No Content`

Notes:

- Call this when the user cancels a download or the client gives up after a failed/interrupted download.
- This endpoint records a `canceled` event only. It does not create or delete a server-side downloaded-file record.
- A previously completed local download remains recorded until the client deletes that local file and calls `DELETE /download`.
- The presigned R2 URL cannot be individually revoked by this API; it remains usable until `expiresAt`.

Common errors:

- `NOT_FOUND` — project does not exist or is not granted to this member
- `CONTENT_VIDEO_NOT_FOUND`

---

## `DELETE /api/projects/{projectId}/videos/{videoId}/download`

Clears the server-side download record after the client deletes the local downloaded file.

Response: `204 No Content`

Notes:

- Call this after the local file has been removed from device storage.
- The endpoint is idempotent. If the member has no recorded download for that `videoId` and device kind, the API still returns `204`.
- When download tracking is enabled, the API records a `deleted` event only if the member previously had a matching download record.
- This does not delete media from R2 or change CMS project/video data.
