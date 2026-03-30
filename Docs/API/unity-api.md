# Unity API Document

This document is the Unity integration contract.

Base URL:

- Local: `http://127.0.0.1:8787`
- Production: `<your-worker-domain>`

---

## Authentication Overview

| Client | Endpoint group | Auth mechanism |
|--------|---------------|----------------|
| Mobile App | `POST /api/auth/login`, `POST /api/auth/forgot-password` | `X-App-Key` header (no Bearer) |
| Quest App | `POST /api/pair/request`, `GET /api/pair/poll/{code}` | `X-Quest-Key` header (no Bearer) |
| Mobile & Quest | All other `/api/*` | `Authorization: Bearer <token>` |

```http
Authorization: Bearer <token>
```

---

## Basic Flow — Mobile App

1. Call `POST /api/auth/login` with `X-App-Key` → store `credential.token` and `credential.expiresAt`
2. Call `GET /api/subscriptions` → store subscription packages and project list
3. Before each subsequent API call, check if token is expired (`expiresAt` vs current UTC). If expired, re-login.
4. All other `/api/*` calls use `Authorization: Bearer <token>`

---

## Basic Flow — Quest App

1. Call `POST /api/pair/request` with `X-Quest-Key` → display the 6-digit `code` on screen
2. Poll `GET /api/pair/poll/{code}` every 3 seconds with `X-Quest-Key`
3. When `status` becomes `verified`, store `credential.token` and `credential.expiresAt` — stop polling
4. Call `GET /api/subscriptions` with the new Bearer token → store subscription packages and project list
5. Before each subsequent API call, check if token is expired. If expired, return to step 1 (re-pair).

---

## Quest Pairing

### `POST /api/pair/request`

Required header:

```http
X-Quest-Key: <QUEST_APP_API_KEY>
```

Request body (all fields optional):

```json
{
  "model": "Meta Quest 3",
  "serial": "1PASH...",
  "appVersion": "1.2.0"
}
```

Response:

```json
{
  "code": "391847",
  "expiresAt": "2026-03-19T10:05:00.000Z"
}
```

Rate limited: 5 requests per IP per minute.

---

### `GET /api/pair/poll/{code}`

Required header:

```http
X-Quest-Key: <QUEST_APP_API_KEY>
```

Response while waiting:

```json
{ "status": "pending" }
```

Response when Mobile has verified (one-time — entry is deleted immediately after this response):

```json
{
  "status": "verified",
  "credential": {
    "token": "<jwt-token>",
    "tokenType": "Bearer",
    "expiresAt": "2026-03-19T22:00:00.000Z"
  }
}
```

| Status | Meaning |
|--------|---------|
| `200 pending` | Not yet verified — keep polling |
| `200 verified` | Credential ready — stop polling, store token |
| `404` | Code expired or already picked up |
| `429` | Rate limited (60 requests per IP per minute) |

---

### `POST /api/pair/verify`

Called by **Mobile App** after the user enters the 6-digit code. Requires Bearer token.

Request:

```json
{ "code": "391847" }
```

| Status | Meaning |
|--------|---------|
| `204` | Pairing successful |
| `404` | Code not found or expired |
| `409` | Code already used |

---

## Login

### `POST /api/auth/login`

Required header:

```http
X-App-Key: <MOBILE_APP_API_KEY>
```

Request:

```json
{
  "email": "ct1@abc.com",
  "password": "123456"
}
```

Response:

```json
{
  "credential": {
    "token": "<jwt-token>",
    "tokenType": "Bearer",
    "expiresAt": "2026-03-12T08:00:00.000Z"
  }
}
```

Common errors:

```json
{ "error": "Forbidden" }
```
_(missing or invalid X-App-Key)_

```json
{ "error": "Too many requests" }
```
_(rate limit: 20/IP/15min or 10/email/hour)_

```json
{ "error": "email and password are required" }
```

```json
{ "error": "Invalid email or password" }
```

```json
{ "error": "Authentication service error" }
```

---

## Forgot Password

### `POST /api/auth/forgot-password`

Required header:

```http
X-App-Key: <MOBILE_APP_API_KEY>
```

Request:

```json
{
  "email": "ct1@abc.com"
}
```

Response (always 200 to prevent account enumeration):

```json
{
  "message": "If the account exists, a password reset email has been sent."
}
```

Rate limited: 5 attempts per email per hour, 10 per IP per 15 minutes. 60-second cooldown per email after each dispatch.

---

## Endpoints

### Quest Key only (no Bearer)

- `POST /api/pair/request`
- `GET /api/pair/poll/{code}`

### Bearer Required (Mobile + Quest)

- `POST /api/pair/verify` _(Mobile only)_
- `GET /api/subscriptions`
- `GET /api/projects/latest?limit=5`
- `GET /api/projects/search`
- `GET /api/projects/recommended`
- `GET /api/roles/projects`
- `GET /api/roles/content`
- `GET /api/projects/{projectId}`
- `GET /api/projects/{projectId}/thumbnail`
- `GET /api/projects/{projectId}/contents/{projectVideoId}/thumbnail`
- `GET /api/projects/{projectId}/videos/{videoId}/thumbnail`
- `GET /api/projects/{projectId}/videos/{videoId}/stream`
- `GET /api/projects/{projectId}/videos/{videoId}/download`
- `GET /api/watch-history`
- `PATCH /api/watch-history/{mediaVideoId}`
- `POST /api/analytics/project-page-sessions`
- `POST /api/analytics/view-sessions`
- `GET /api/favorites`
- `POST /api/favorites/{projectId}`
- `DELETE /api/favorites/{projectId}`

---

## Subscriptions

### `GET /api/subscriptions`

Call this immediately after login (Mobile) or after pairing (Quest). Returns subscription packages with granted projects and categories.

```json
{
  "items": [
    {
      "id": "subscription_basic",
      "name": "Basic",
      "description": "Basic package",
      "active": 1,
      "projects": [
        {
          "id": "project_a",
          "name": "外科トレーニング",
          "categoryName": "外科",
          "programPicUrl": "https://...",
          "thumbnailUrl": "https://<worker>/api/projects/project_a/thumbnail",
          "playlistCount": 3,
          "playlistDurationSeconds": 540
        }
      ],
      "categories": [
        {
          "id": "cat_surgery",
          "name": "外科",
          "picUrl": "https://..."
        }
      ]
    }
  ]
}
```

`items` is empty if the user has no valid subscription packages. Projects are listed in grant order; categories are sorted alphabetically by name.

---

## Roles Content Mode

`GET /api/roles/content` response is controlled by backend config `ROLES_CONTENT_MODE`.

- `projects` mode:

```json
{
  "mode": "projects",
  "items": [
    {
      "id": "role_1",
      "name": "Resident",
      "description": "...",
      "projects": []
    }
  ]
}
```

- `episodes` mode:

```json
{
  "mode": "episodes",
  "items": [
    {
      "id": "role_1",
      "name": "Resident",
      "description": "...",
      "episodes": [
        {
          "id": 1,
          "title": "Episode 1",
          "description": "...",
          "mediaVideoId": "video_1",
          "thumbnailUrl": "https://<your-worker>/api/projects/project_a/contents/1/thumbnail",
          "videoName": "Main Procedure",
          "durationMs": 132000,
          "sizeBytes": 24500000,
          "projectionType": "vr180",
          "stereoLayout": "sbs",
          "eyeOrder": "LR",
          "projectId": "project_a",
          "projectName": "Project A",
          "progressMs": 0,
          "serverUpdatedAt": null
        }
      ]
    }
  ]
}
```

---

## Project Search

### `GET /api/projects/search`

Search active projects. All query parameters are optional and composable.

| Param | Required | Description |
|-------|----------|-------------|
| `keyword` | No | Free-text search matched against `name`, `description`, `dr_name`, and `institution` (case-insensitive). |
| `category` | No | Filter by category ID(s). Supports repeated keys (`?category=cat_a&category=cat_b`) and comma-separated values (`?category=cat_a,cat_b`). |
| `program` | No | Filter by program ID(s). Supports repeated keys (`?program=prog_a&program=prog_b`) and comma-separated values (`?program=prog_a,prog_b`). |
| `deviceType` | No | Platform the user is searching from (e.g. `ios`, `android`, `web`, `quest`). Stored for analytics. |

Examples:

```
GET /api/projects/search?keyword=brain
GET /api/projects/search?category=cat_surgery
GET /api/projects/search?keyword=tokyo&program=prog_basic&category=cat_surgery&deviceType=ios
GET /api/projects/search?category=cat_surgery&category=cat_neuro&program=prog_basic,prog_advanced
```

Response:

```json
{
  "searchEventId": "550e8400-e29b-41d4-a716-446655440000",
  "results": [
    {
      "id": "project_a",
      "name": "外科トレーニング",
      "description": "...",
      "programId": "prog_basic",
      "programName": "Basic Program",
      "programPicUrl": "https://...",
      "categoryId": "cat_surgery",
      "categoryName": "外科",
      "categoryPicUrl": "https://...",
      "roleId": "role_1",
      "roleName": "Resident",
      "thumbnailUrl": "https://<worker>/api/projects/project_a/thumbnail",
      "playlistCount": 3,
      "playlistDurationSeconds": 540,
      "size": 24500000
    }
  ]
}
```

`results` is an empty array `[]` when no projects match. **Store `searchEventId`** — pass it as `sourceSearchEventId` when reporting a project page session that originated from this search.

---

## Project Detail

### `GET /api/projects/{projectId}`

Returns project metadata and the full playlist. Each playlist item includes the user's watch progress.

```json
{
  "id": "project_a",
  "name": "外科トレーニング",
  "programPicUrl": "https://...",
  "categoryName": "外科",
  "playlistCount": 3,
  "playlistDurationSeconds": 540,
  "playlist": [
    {
      "id": 42,
      "ordinal": 1,
      "title": "第1集：手術の基礎",
      "mediaVideoId": "video_1",
      "thumbnailUrl": "https://<worker>/api/projects/project_a/contents/42/thumbnail",
      "durationMs": 180000,
      "progressMs": 95000,
      "serverUpdatedAt": "2026-03-19T10:00:00.000Z"
    },
    {
      "id": 43,
      "ordinal": 2,
      "title": "第2集：応用編",
      "mediaVideoId": "video_2",
      "thumbnailUrl": "https://<worker>/api/projects/project_a/contents/43/thumbnail",
      "durationMs": 200000,
      "progressMs": 0,
      "serverUpdatedAt": null
    }
  ]
}
```

`progressMs: 0` and `serverUpdatedAt: null` mean the video has never been watched.

---

## Favorites

### `GET /api/favorites`

Returns all favorited projects sorted by most recently added. Each item includes
`projectName`, `drName`, `categoryName`, `programPicUrl`, `thumbnailUrl`,
`playlistDurationSeconds`, `videoCount`, and `createdAt`.

```json
{
  "items": [
    {
      "projectId": "project_a",
      "projectName": "外科トレーニング",
      "drName": "Dr. Sato",
      "categoryName": "外科",
      "programPicUrl": "https://...",
      "thumbnailUrl": "https://<worker>/api/projects/project_a/thumbnail",
      "playlistDurationSeconds": 540,
      "videoCount": 3,
      "createdAt": "2026-03-19T08:00:00.000Z"
    }
  ]
}
```

Content fields are `null` if the project no longer exists in the CMS.

---

### `POST /api/favorites/{projectId}`

Adds a project to favorites. Idempotent.

Response: `204 No Content`

---

### `DELETE /api/favorites/{projectId}`

Removes a project from favorites. Idempotent.

Response: `204 No Content`

---

## Watch History

### `GET /api/watch-history`

Returns the user's watch progress enriched with content metadata, sorted by most recently updated. Items are omitted if the video no longer exists in the CMS or if `durationMs`/`ordinal` are unavailable (project mode only).

Query params:

| Param | Required | Description |
|-------|----------|-------------|
| `mode` | No | `video` (default) — one item per episode. `project` — one item per project (most recently watched episode). |
| `limit` | No | Max items per page. Capped at 500. Default: 500. |
| `page` | No | Page number (1-based). Default: 1. |

**Video mode response** (`mode=video` or omitted):

```json
{
  "items": [
    {
      "mediaVideoId": "video_1",
      "projectId": "project_a",
      "progressMs": 80000,
      "lastDeviceType": "quest",
      "serverUpdatedAt": "2026-03-17T10:05:00.000Z",
      "title": "第1集：手術の基礎",
      "thumbnailUrl": "https://<worker>/api/projects/project_a/contents/42/thumbnail",
      "projectName": "外科トレーニング",
      "categoryName": "外科",
      "categoryPicUrl": "https://...",
      "programPicUrl": "https://..."
    }
  ],
  "page": 1,
  "hasMore": false
}
```

**Project mode response** (`mode=project`):

```json
{
  "items": [
    {
      "projectId": "project_a",
      "projectName": "外科トレーニング",
      "thumbnailUrl": "https://<worker>/api/projects/project_a/thumbnail",
      "categoryName": "外科",
      "categoryPicUrl": "https://...",
      "programPicUrl": "https://...",
      "latestEpisode": {
        "mediaVideoId": "video_3",
        "title": "第3集：応用編",
        "thumbnailUrl": "https://<worker>/api/projects/project_a/contents/44/thumbnail",
        "progressMs": 120000,
        "durationMs": 45000,
        "ordinal": 3,
        "serverUpdatedAt": "2026-03-17T10:05:00.000Z"
      }
    }
  ],
  "page": 1,
  "hasMore": false
}
```

`hasMore: true` means there is a next page — request again with `page` incremented.

In project mode, an entry is omitted entirely if the CMS does not provide `durationMs` or `ordinal` for the latest episode. When present, both fields are always non-null integers.

---

### `PATCH /api/watch-history/{mediaVideoId}`

Updates watch progress for a video.

Request:

```json
{
  "progressMs": 45000,
  "projectId": "project_a",
  "deviceType": "mobile",
  "knownServerUpdatedAt": "2026-03-17T09:00:00.000Z"
}
```

`knownServerUpdatedAt`: the `serverUpdatedAt` from the client's last successful sync. Send `null` or omit on first sync.

Responses:

| Status | Condition |
|--------|-----------|
| `200` | Accepted |
| `409` | Stale update — server has newer data |
| `400` | Missing / invalid fields |

Both `200` and `409` return the same body — the current server record:

```json
{
  "mediaVideoId": "video_1",
  "projectId": "project_a",
  "progressMs": 45000,
  "serverUpdatedAt": "2026-03-17T10:00:01.000Z",
  "lastDeviceType": "mobile"
}
```

On `409`: discard the pending update and update local state from the response.

On first sync (`knownServerUpdatedAt` is `null`) when the server already has a record: server keeps the higher `progressMs` to avoid regressing progress.

---

## Analytics

### `POST /api/analytics/project-page-sessions`

Report a project page session when the user **leaves** a project page. Captures dwell time, whether a video was started, and the entry source.

| Field | Required | Description |
|-------|----------|-------------|
| `projectId` | Yes | The project the user visited. |
| `source` | Yes | How the user arrived. One of: `search`, `latest`, `recommended`, `favorites`, `roles_content`, `category`, `direct`. |
| `startedAt` | Yes | Client UTC timestamp when the user entered the page. |
| `durationMs` | No | Time spent on the page in milliseconds. Default `0`. |
| `videoStarted` | No | Whether the user started playing any video. Default `false`. |
| `sourceSearchEventId` | No | Required when `source` is `search`. The `searchEventId` from the search response. |
| `deviceType` | No | Platform the user is on (e.g. `ios`, `android`, `web`, `quest`). Stored for analytics. |

Request:

```json
{
  "projectId": "project_a",
  "source": "search",
  "sourceSearchEventId": "550e8400-e29b-41d4-a716-446655440000",
  "durationMs": 12000,
  "videoStarted": false,
  "startedAt": "2026-03-23T08:30:00.000Z",
  "deviceType": "ios"
}
```

Response: `204 No Content`

**When to send:**
- Fire this event as the user navigates away from the project page (or when the app is backgrounded/closed while on the page).
- If `videoStarted` is `true`, a `view_sessions` event will typically follow with the actual watch data.

---

### `POST /api/analytics/view-sessions`

Batch upload of completed view sessions. Idempotent via `id` — duplicate uploads are safely ignored.

Max **50 items** per request.

Request body (array):

```json
[
  {
    "id": "uuid-v4",
    "mediaVideoId": "video_1",
    "projectId": "project_a",
    "videoTitle": "Episode 1",
    "startedAt": "2026-03-17T10:00:00.000Z",
    "flatWatchMs": 30000,
    "cardboardWatchMs": 60000,
    "totalWatchMs": 90000,
    "finalProgressMs": 90000,
    "durationMs": 132000,
    "completed": false,
    "deviceType": "mobile"
  }
]
```

Responses:

| Status | Condition |
|--------|-----------|
| `204` | Accepted |
| `400` | Body is not an array, or exceeds 50 items |
