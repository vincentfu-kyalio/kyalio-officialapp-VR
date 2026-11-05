# Core Shared Schemas

These schemas are the canonical DTO models for client integration. They are referenced from every endpoint document in this folder.

See also: [overview.md](overview.md) for auth, error envelope, and basic flows; [cache-rules.md](cache-rules.md) for when to refetch.

---

## 1. Credential

Mobile credential — returned by `POST /api/auth/login` and `POST /api/auth/refresh`:

```json
{
  "accessToken": "<jwt-token>",
  "tokenType": "Bearer",
  "expiresAt": "2026-03-12T09:00:00.000Z",
  "refreshToken": "<opaque-hex-string>",
  "refreshTokenExpiresAt": "2026-04-11T08:00:00.000Z"
}
```

Quest credential — delivered via the `verified` SSE event on `GET /api/pair/stream/{code}`, or returned by `POST /api/auth/quest/login` and `POST /api/auth/quest/login/confirm` as `{ "credential": { ... } }`:

```json
{
  "accessToken": "<jwt-token>",
  "tokenType": "Bearer",
  "expiresAt": "2026-03-12T10:00:00.000Z"
}
```

Token lifetimes:

| Token           | Device type | TTL     | Refreshable       |
| --------------- | -------- | ------- | ----------------- |
| `accessToken`   | Mobile   | 1 hour  | via `/api/auth/refresh` |
| `refreshToken`  | Mobile   | 30 days | rotated on every refresh |
| `accessToken`   | Quest    | 2 hours | no — sign in again on expiry |

Rules:

- `accessToken` is the JWT sent in the `Authorization: Bearer <accessToken>` header.
- `expiresAt` is the access-token expiry (ISO-8601 UTC).
- `refreshToken` is opaque to clients; do not parse it.
- `refreshTokenExpiresAt` is the refresh-token expiry. If it has passed, the user must re-login.
- Refresh tokens are **rotated** on every successful refresh — clients must persist the newly returned `refreshToken` and discard the previous one.
- Quest does not receive a refresh token; on access-token expiry the Quest app must sign in again, by pairing or by email and password.

---

## 2. ProgramSummary

Used in the normalized program catalog returned from `GET /api/me/home` → `filters.programs[]`.

```json
{
  "id": "prg_D4e5F6g7H8i9",
  "name": "Basic Program",
  "picUrl": "/api/programs/prg_D4e5F6g7H8i9/thumbnail?w=768&v=2026-03-19T10%3A00%3A00.000Z",
  "updatedAt": "2026-03-19T10:00:00.000Z"
}
```

Notes:

- `id` is always included.
- `picUrl` may be `null`.
- `picUrl` is a relative, auth-gated proxy URL. Clients should resolve it against the API base URL and send `Authorization: Bearer <accessToken>` when loading it.
- `picUrl` includes `w=768` for a 768px max-edge thumbnail and a `v` value derived from `updatedAt` for URL-based image cache keys.
- `updatedAt` changes when program metadata changes, including name or picture updates.
- Program metadata is versioned independently from `Project` and does **not** affect `projectVersion` or `thumbnailVersion`.
- Program thumbnail freshness is controlled by `ProgramSummary.updatedAt` and the thumbnail endpoint's HTTP cache policy. It is **not** controlled by `thumbnailsExpireAt`.

---

## 3. EpisodeRef

```json
{
  "projectId": "prj_A1b2C3d4E5f6",
  "videoId": "vid_I9j0K1l2M3n4"
}
```

Notes:

- Used by home-role sections when `displayMode = episodes`.
- The `videoId` is the exact episode the server wants the client to render for that role slot.

---

## 4. PlaylistItem

Used inside `Project.playlist`.

```json
{
  "title": "第1集：手術の基礎",
  "description": "...",
  "videoId": "vid_I9j0K1l2M3n4",
  "thumbnailUrl": "https://image.mux.com/<playback-id>/thumbnail.jpg?token=...",
  "durationMs": 180000,
  "sizeBytes": 12000000,
  "playbackMode": "vr180_sbs"
}
```

Notes:

- `videoId` is the current published video used by this playlist item.
- If content is updated, the playlist item points to a new `videoId` instead of versioning the old file.
- `sizeBytes` is the downloadable file size for the authenticated device type.
- Playlist items do **not** include watch-progress state. Member watch progress is synced separately via `GET /api/me/progress` and updated via `PATCH /api/watch-history/{videoId}`.
- `playbackMode` possible values:
  - `flat`
  - `vr180_sbs` (most)
  - `vr360_mono`

---

## 5. Project

```json
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
```

Notes:

- This is the only full project DTO used by clients. It is returned by `POST /api/projects/batch` and `GET /api/projects/{projectId}`.
- `thumbnailUrl` is a relative, auth-gated proxy URL. Clients should resolve it against the API base URL and send `Authorization: Bearer <accessToken>` when loading it.
- `thumbnailUrl` includes `w=768` for a 768px max-edge thumbnail and `v={thumbnailVersion}` so HTTP/disk cache keys move with project thumbnail invalidation.
- `projectVersion` changes only when CMS-managed project content changes. Member watch progress must never affect `projectVersion`.
- `thumbnailVersion` changes whenever any image displayed within the project payload changes, including:
  - project thumbnail
  - any playlist-item thumbnail
- `specialtyId` may be `null`.
- `roleId` may be `null`.
- `programIds[]` may be empty.
- `thumbnailUrl` may be `null`.
- `playlist[]` order is the canonical episode order.

### Thumbnail size URLs

Use the 768px URLs returned by the API for list/card imagery:

- Project thumbnail: `/api/projects/{projectId}/thumbnail?w=768&v={thumbnailVersion}`
- Program logo: `/api/programs/{programId}/thumbnail?w=768&v={updatedAt}`
- Video thumbnail proxy, when needed: `/api/projects/{projectId}/videos/{videoId}/thumbnail?w=768`

The proxy clamps `w` to a maximum edge of 768px, preserves aspect ratio, and does not upscale smaller R2 image sources. Mux playlist thumbnail URLs are already signed for a 768px max-edge thumbnail; `thumbnailsExpireAt` still controls when clients must refresh those signed URLs.

---

## 6. ProgressItem

Used in `GET /api/me/progress`, `GET /api/watch-history`, and `PATCH /api/watch-history/{videoId}` responses.

```json
{
  "projectId": "prj_A1b2C3d4E5f6",
  "videoId": "vid_I9j0K1l2M3n4",
  "progressMs": 95000,
  "progressUpdatedAt": "2026-03-19T10:00:00.000Z",
  "lastDeviceType": "ios"
}
```

Notes:

- Progress is member state, not CMS content.
- `progressUpdatedAt` is the server timestamp for optimistic concurrency and delta sync.
- `lastDeviceType` uses client device identifiers such as `ios`, `android`, or `quest`.

---

## 7. PagedResponse<T>

All pageable list endpoints follow the same envelope:

```json
{
  "items": [],
  "page": 1,
  "pageSize": 20,
  "hasMore": false
}
```

Notes:

- `items` contains the current page data.
- `page` is 1-based.
- `pageSize` is the actual applied page size.
- `hasMore` indicates whether a next page exists.

---

## 8. Thumbnail Envelope Fields

Endpoints that return thumbnail URLs include:

```json
{
  "thumbnailsExpireAt": "2026-04-23T08:00:00.000Z"
}
```

Rules:

- `thumbnailsExpireAt` controls the validity of **`playlist[].thumbnailUrl`** only — these are Mux signed thumbnail URLs that expire.
- `Project.thumbnailUrl` and `ProgramSummary.picUrl` are relative, auth-gated proxy URLs (`/api/.../thumbnail`). They do **not** expire and require `Authorization: Bearer <accessToken>` to load.
- Mux signed thumbnail URLs are signed with 768px max-edge resize parameters in the token claims. The URL should contain only `token=...`; clients must not append `width`, `height`, or `time` to signed Mux thumbnail URLs.
- `thumbnailsExpireAt` does **not** represent image content versioning.
- `thumbnailVersion` controls local image cache invalidation.
- `ProgramSummary.picUrl` is not governed by `thumbnailsExpireAt`; program thumbnail freshness is controlled by `ProgramSummary.updatedAt` and the thumbnail endpoint's HTTP cache policy.
- Clients may keep displaying already-downloaded local thumbnail files after `thumbnailsExpireAt`, but must refresh metadata before re-downloading a playlist thumbnail from a signed URL.
- Clients must not assume a fixed 2-hour or 4-hour TTL. The response value is the only contractually reliable expiry.
- If `thumbnailVersion` changes, clients must fetch fresh metadata from `POST /api/projects/batch` or `GET /api/projects/{projectId}` before downloading project or playlist thumbnails again.

---

## 9. Public ID Format

All entity IDs exposed by the API follow a uniform format:

```
<prefix>_<nanoid12>
```

- `<prefix>` is a 3-letter tag identifying the entity type (see table below).
- `<nanoid12>` is a 12-character NanoID from the URL-safe alphabet `A-Za-z0-9_-`.
- Total length: **16 characters** including underscore.

| Prefix | Entity               |
| ------ | -------------------- |
| `prj_` | Project              |
| `pkg_` | Package              |
| `prg_` | Program              |
| `spc_` | Specialty            |
| `rol_` | Role                 |
| `vid_` | Media (type = video) |
| `img_` | Media (type = image) |
| `usr_` | Member               |
| `fav_` | Favorite record      |
| `whs_` | Watch-history record |

Rules:

- IDs are **stable** — they do not change over an entity's lifetime.
- IDs are **opaque** — clients must treat them as strings and must not parse, sort, or derive meaning from them.
- IDs are **non-time-sortable** — ordering by ID does not correspond to any real-world ordering.
- These are **public IDs**. The CMS may use different internal primary keys for joins / analytics; clients only ever see the public form.
- Member device binding IDs, including the JWT `memberDeviceId` claim and CMS admin `{deviceId}` path value, are opaque base64url strings without a semantic prefix.
- Analytics records (`view_session`, `project_page_session`) have internal PKs only — no public ID is exposed because clients never reference them back.
