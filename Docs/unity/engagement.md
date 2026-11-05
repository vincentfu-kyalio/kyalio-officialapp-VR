# Engagement Endpoints

Favorites, watch-history, and analytics reporting.

For `ProgressItem`, see [schemas.md](schemas.md#6-progressitem).

---

## Favorites

### `GET /api/favorites`

Returns favorited projects sorted by most recently added.

Response:

```json
{
  "items": [
    {
      "projectId": "prj_A1b2C3d4E5f6",
      "favoritedAt": "2026-03-19T08:00:00.000Z"
    }
  ]
}
```

Notes:

- `items[]` contains only project references plus `favoritedAt`.
- Favorites whose project is deleted or no longer granted are omitted from the response, even if the underlying favorite record remains stored.

---

### `POST /api/favorites/{projectId}`

Adds a project to favorites.

Response: `204 No Content`

---

### `DELETE /api/favorites/{projectId}`

Removes a project from favorites.

Response: `204 No Content`

---

## Watch History

### `GET /api/watch-history`

Query params:

| Param   | Required | Description                        |
| ------- | -------- | ---------------------------------- |
| `mode`  | No       | `video` (default) or `project`     |
| `limit` | No       | Max items per page. Capped at 500. |
| `page`  | No       | 1-based page number. Default `1`.  |

#### Video mode response

```json
{
  "items": [
    {
      "videoId": "vid_I9j0K1l2M3n4",
      "projectId": "prj_A1b2C3d4E5f6",
      "progressMs": 80000,
      "lastDeviceType": "quest",
      "progressUpdatedAt": "2026-03-17T10:05:00.000Z"
    }
  ],
  "page": 1,
  "pageSize": 500,
  "hasMore": false
}
```

#### Project mode response

```json
{
  "items": [
    {
      "projectId": "prj_A1b2C3d4E5f6",
      "videoId": "vid_K1l2M3n4O5p6",
      "progressMs": 120000,
      "lastDeviceType": "quest",
      "progressUpdatedAt": "2026-03-17T10:05:00.000Z"
    }
  ],
  "page": 1,
  "pageSize": 500,
  "hasMore": false
}
```

Notes:

- Video mode returns one record per watched video.
- Project mode returns the most recently watched valid video per project.
- Entries whose project is deleted or no longer granted are omitted from the response.

---

### `PATCH /api/watch-history/{videoId}`

Updates watch progress for a video.

Request:

```json
{
  "progressMs": 45000,
  "projectId": "prj_A1b2C3d4E5f6",
  "knownProgressUpdatedAt": "2026-03-17T09:00:00.000Z"
}
```

Responses:

- `200` accepted
- `409` stale update — server has newer data
- `400` missing / invalid fields

Both `200` and `409` return:

```json
{
  "videoId": "vid_I9j0K1l2M3n4",
  "projectId": "prj_A1b2C3d4E5f6",
  "progressMs": 45000,
  "progressUpdatedAt": "2026-03-17T10:00:01.000Z",
  "lastDeviceType": "ios"
}
```

Rules:

- On `409`, client must discard the pending write and update local state from the response.
- On first sync (`knownProgressUpdatedAt = null`) when the server already has data, the server keeps the higher `progressMs`.

---

## Analytics

### `POST /api/analytics/project-page-sessions`

Report a project page session when the user leaves a project page.

Request:

```json
{
  "projectId": "prj_A1b2C3d4E5f6",
  "source": "search",
  "sourceSearchEventId": "550e8400-e29b-41d4-a716-446655440000",
  "durationMs": 12000,
  "videoStarted": false,
  "startedAt": "2026-03-23T08:30:00.000Z"
}
```

Allowed `source` values:

| Value         | Meaning                                                                 |
| ------------- | ----------------------------------------------------------------------- |
| `latest`      | Opened from the "Latest" section on the home screen.                    |
| `recommended` | Opened from the "Recommended" section on the home screen.               |
| `roles`       | Opened from the role-grouped section on the home screen.                |
| `specialty`   | Opened from a client-side specialty-grouped section on the home screen. |
| `search`      | Opened from a search result. `sourceSearchEventId` should be set.       |
| `favorites`   | Opened from the favorites list.                                         |
| `direct`      | Opened via deep link, push notification, or other programmatic routing. |

Tapping a specialty or program badge from a project card is treated as `search` — the badge tap triggers a filtered search that yields a `searchEventId`, and `source` is set accordingly.

Response: `204 No Content`

---

### `POST /api/analytics/view-sessions`

Batch upload completed view sessions.

Max 50 items per request.

Request:

```json
[
  {
    "id": "uuid-v4",
    "videoId": "vid_I9j0K1l2M3n4",
    "projectId": "prj_A1b2C3d4E5f6",
    "videoTitle": "Episode 1",
    "startedAt": "2026-03-17T10:00:00.000Z",
    "flatWatchMs": 30000,
    "cardboardWatchMs": 60000,
    "totalWatchMs": 90000,
    "finalProgressMs": 90000,
    "durationMs": 132000,
    "completed": false
  }
]
```

Response: `204 No Content`
