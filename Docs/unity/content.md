# Content Endpoints

Endpoints clients use to discover, sync, and fetch project content: delta sync, progress sync, project batch fetch, home feed, search, and project detail.

Schemas used here: [`Project`](schemas.md#5-project), [`ProgressItem`](schemas.md#6-progressitem), [`ProgramSummary`](schemas.md#2-programsummary), [`EpisodeRef`](schemas.md#3-episoderef).

For when to invalidate and refetch, see [cache-rules.md](cache-rules.md).

---

## Data Synchronization (Delta Sync)

### `GET /api/me/sync`

Returns the member's current granted project state. This is the authoritative source for determining:

- which projects are currently granted
- which local project caches are outdated
- which local project caches should be removed
- which local thumbnail caches should be invalidated

Response:

```json
{
  "timestamp": "2026-04-23T08:00:00.000Z",
  "grantedProjects": {
    "prj_A1b2C3d4E5f6": {
      "projectVersion": 12,
      "thumbnailVersion": 5
    },
    "prj_B2c3D4e5F6g7": {
      "projectVersion": 5,
      "thumbnailVersion": 2
    }
  }
}
```

Client behavior:

- `grantedProjects` is the **authoritative set** — any project in local cache whose `projectId` is not a key here is no longer granted and must be purged:
  - local project cache
  - local thumbnail cache
  - any downloaded video files belonging to that project
- For each `projectId` key in `grantedProjects`:
  - If absent from local cache, fetch it via `POST /api/projects/batch`.
  - If `projectVersion` differs from cached, re-fetch the full `Project`.
  - If `thumbnailVersion` differs from cached, invalidate locally cached images for that project and fetch fresh project metadata before downloading thumbnails again.

---

### `GET /api/me/progress`

Returns member watch-progress state, scoped to granted projects only, independently from project metadata.

Query params:

| Param   | Required | Description                                                                |
| ------- | -------- | -------------------------------------------------------------------------- |
| `since` | No       | ISO timestamp. If provided, returns only progress updated after this time. |

Full snapshot response (`GET /api/me/progress`):

```json
{
  "timestamp": "2026-04-23T08:00:00.000Z",
  "items": [
    {
      "projectId": "prj_A1b2C3d4E5f6",
      "videoId": "vid_I9j0K1l2M3n4",
      "progressMs": 95000,
      "progressUpdatedAt": "2026-04-23T07:55:00.000Z",
      "lastDeviceType": "ios"
    }
  ]
}
```

Delta response (`GET /api/me/progress?since=...`):

```json
{
  "timestamp": "2026-04-23T08:30:00.000Z",
  "items": [
    {
      "projectId": "prj_A1b2C3d4E5f6",
      "videoId": "vid_I9j0K1l2M3n4",
      "progressMs": 120000,
      "progressUpdatedAt": "2026-04-23T08:20:00.000Z",
      "lastDeviceType": "quest"
    }
  ]
}
```

Notes:

- Without `since`, this endpoint returns the member's full progress snapshot, scoped to currently granted projects.
- With `since`, this endpoint returns only progress records updated after that timestamp, still scoped to granted projects.
- Progress is member state and is independent from `Project` content versions.
- Progress whose project is deleted or no longer granted is omitted from the response.

---

### `POST /api/projects/batch`

Fetches complete project details for the requested project IDs.

Request:

```json
{
  "projectIds": ["prj_A1b2C3d4E5f6", "prj_C3d4E5f6G7h8"]
}
```

Response:

```json
{
  "thumbnailsExpireAt": "2026-04-23T08:00:00.000Z",
  "items": [
    {
      "projectId": "prj_A1b2C3d4E5f6",
      "projectVersion": 12,
      "thumbnailVersion": 5,
      "projectName": "外科トレーニング",
      "description": "...",
      "surgeons": ["Dr. Wang", "Dr. Sato"],
      "institution": "Kyalio Institute",
      "specialtyId": "spc_F6g7H8i9J0k1",
      "roleId": "rol_H8i9J0k1L2m3",
      "programIds": ["prg_D4e5F6g7H8i9"],
      "thumbnailUrl": "/api/projects/prj_A1b2C3d4E5f6/thumbnail?w=768&v=5",
      "playlistCount": 3,
      "playlistDurationSeconds": 540,
      "totalSizeBytes": 24500000,
      "playlist": [
        {
          "title": "第1集：手術の基礎",
          "description": "...",
          "videoId": "vid_I9j0K1l2M3n4",
          "thumbnailUrl": "https://image.mux.com/<playback-id>/thumbnail.jpg?token=...",
          "durationMs": 180000,
          "sizeBytes": 12000000,
          "playbackMode": "vr180_sbs"
        }
      ]
    }
  ]
}
```

Notes:

- `items[]` is `Project[]`.
- A playlist item is considered updated when its `videoId` changes or any visible playlist metadata changes.
- Downloaded files are stale if the current playlist item no longer points to the previously downloaded `videoId`.
- Omitted items among the requested `projectIds[]` should be treated as unavailable to the current member and removed from local project cache.

---

## Home Feed

### `GET /api/me/home`

Returns the home-feed layout and normalized catalog. The actual project / playlist data is **not** returned here; clients resolve each `projectId` against their local `Project` cache populated by `POST /api/projects/batch`.

Home includes normalized program thumbnails in `filters.programs[]`. The response also includes `thumbnailsExpireAt` for the shared thumbnail envelope, but that field does not control program thumbnail freshness.

Response (`roles.displayMode = projects`):

```json
{
  "thumbnailsExpireAt": "2026-04-23T08:00:00.000Z",
  "latest": ["prj_A1b2C3d4E5f6", "prj_B2c3D4e5F6g7"],
  "recommended": ["prj_A1b2C3d4E5f6"],
  "roles": {
    "displayMode": "projects",
    "items": [
      {
        "name": "Resident",
        "description": "...",
        "projectIds": ["prj_A1b2C3d4E5f6"]
      }
    ]
  },
  "filters": {
    "specialties": [{ "id": "spc_F6g7H8i9J0k1", "name": "外科" }],
    "programs": [
      {
        "id": "prg_D4e5F6g7H8i9",
        "name": "Basic Program",
        "picUrl": "/api/programs/prg_D4e5F6g7H8i9/thumbnail?w=768&v=2026-03-19T10%3A00%3A00.000Z",
        "updatedAt": "2026-03-19T10:00:00.000Z"
      }
    ]
  }
}
```

Response (`roles.displayMode = episodes`, controlled by backend config `ROLE_CONTENT_MODE`):

```json
{
  "thumbnailsExpireAt": "2026-04-23T08:00:00.000Z",
  "latest": ["prj_A1b2C3d4E5f6"],
  "recommended": ["prj_A1b2C3d4E5f6"],
  "roles": {
    "displayMode": "episodes",
    "items": [
      {
        "name": "Resident",
        "description": "...",
        "episodes": [
          {
            "projectId": "prj_A1b2C3d4E5f6",
            "videoId": "vid_I9j0K1l2M3n4"
          }
        ]
      }
    ]
  },
  "filters": {
    "specialties": [{ "id": "spc_F6g7H8i9J0k1", "name": "外科" }],
    "programs": [
      {
        "id": "prg_D4e5F6g7H8i9",
        "name": "Basic Program",
        "picUrl": "/api/programs/prg_D4e5F6g7H8i9/thumbnail?w=768&v=2026-03-19T10%3A00%3A00.000Z",
        "updatedAt": "2026-03-19T10:00:00.000Z"
      }
    ]
  }
}
```

Notes:

- `latest[]`, `recommended[]`, and project references inside `roles.items[]` point to cached `Project` records populated via `POST /api/projects/batch`.
- `latest[]` size is decided by the server.
- `recommended[]` is returned in the server-chosen display order.
- `roles.displayMode` controls rendering behavior only: `projects` returns `projectIds[]`, while `episodes` returns `episodes[]` with exact `{projectId, videoId}` pairs chosen by the server.
- **Specialty grouping is client-side**: group the member's granted projects by `project.specialtyId` (from cached `Project`) and display sections in the order given by `filters.specialties[]`. No server-side specialty grouping is returned to avoid duplicating project references.
- `filters.specialties[]` and `filters.programs[]` enumerate the normalized catalog in the server-chosen display order, **scoped to the member's currently granted projects** — only specialties and programs that appear among the member's granted projects are included. These are the only source of specialty / program IDs accepted by `/api/projects/search`.
- Clients should overwrite their local program catalog with `filters.programs[]` on each response.

---

## Project Search

### `GET /api/projects/search`

Search active granted projects.

Query params:

| Param        | Required | Description                                                                            |
| ------------ | -------- | -------------------------------------------------------------------------------------- |
| `keyword`    | No       | Free-text search matched against project name, description, surgeons, and institution. |
| `specialty`  | No       | Filter by specialty ID(s). Supports repeated keys and comma-separated values.          |
| `program`    | No       | Filter by program ID(s). Supports repeated keys and comma-separated values.            |
| `deviceType` | No       | Deprecated; ignored by the server. The authenticated JWT `deviceType` is used for analytics. |
| `page`       | No       | 1-based page number. Default `1`.                                                      |
| `pageSize`   | No       | Requested page size. Server may cap it.                                                |

Response:

```json
{
  "searchEventId": "550e8400-e29b-41d4-a716-446655440000",
  "items": ["prj_A1b2C3d4E5f6"],
  "page": 1,
  "pageSize": 20,
  "hasMore": false
}
```

Notes:

- `items[]` contains only `projectId` strings.
- `searchEventId` is `null` when no filter is provided.
- When present, pass `searchEventId` as `sourceSearchEventId` to `project-page-sessions`.

---

## Project Detail

### `GET /api/projects/{projectId}`

Returns a single `Project`.

Response:

```json
{
  "thumbnailsExpireAt": "2026-04-23T08:00:00.000Z",
  "projectId": "prj_A1b2C3d4E5f6",
  "projectVersion": 12,
  "thumbnailVersion": 5,
  "projectName": "外科トレーニング",
  "description": "...",
  "surgeons": ["Dr. Wang", "Dr. Sato"],
  "institution": "Kyalio Institute",
  "specialtyId": "spc_F6g7H8i9J0k1",
  "roleId": "rol_H8i9J0k1L2m3",
  "programIds": ["prg_D4e5F6g7H8i9"],
  "thumbnailUrl": "/api/projects/prj_A1b2C3d4E5f6/thumbnail?w=768&v=5",
  "playlistCount": 3,
  "playlistDurationSeconds": 540,
  "totalSizeBytes": 24500000,
  "playlist": [
    {
      "title": "第1集：手術の基礎",
      "description": "...",
      "videoId": "vid_I9j0K1l2M3n4",
      "thumbnailUrl": "https://image.mux.com/<playback-id>/thumbnail.jpg?token=...",
      "durationMs": 180000,
      "sizeBytes": 12000000,
      "playbackMode": "vr180_sbs"
    }
  ]
}
```

Notes:

- If a playlist item is replaced with a different video, the item will expose the new `videoId`.
- Clients should treat any previously downloaded file for the old `videoId` as stale.
- Watch progress must be loaded separately via `GET /api/me/progress` or `GET /api/watch-history`.
