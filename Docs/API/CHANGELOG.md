# Changelog

## [0.7.4] - 2026-03-25

### Changed

- `GET /api/projects/search` — `category` and `program` now support multi-value filtering via both repeated query keys (for example `?category=cat_a&category=cat_b`) and comma-separated values (for example `?category=cat_a,cat_b`). Filtering logic is OR within each field and AND across fields.

## [0.7.3] - 2026-03-24

### Added

- `GET /api/projects/search` — new optional query param `deviceType`. Stored in `search_events` for analytics (tracks which platform the user is searching from).
- `POST /api/analytics/project-page-sessions` — new optional body field `deviceType`. Stored in `project_page_sessions` for analytics (tracks which platform the user is on when visiting a project page).

### Infrastructure

- New migration `0006_add_device_type_to_search_and_page_sessions.sql`: adds `device_type TEXT NOT NULL DEFAULT ''` column to both `search_events` and `project_page_sessions` tables in `kyalio-member-media-app-analytics-db`.

---

## [0.7.2] - 2026-03-23

### Changed

- `GET /api/watch-history` (project mode) — entries are now omitted when the CMS does not provide `durationMs` or `ordinal` for the latest episode. Previously these fields were returned as `null`. Both fields are now guaranteed non-null when an entry is present.

---

## [0.7.1] - 2026-03-23

### Added

- `GET /api/watch-history` (project mode) — `latestEpisode` now includes `durationMs` (video duration in ms, `null` if not in CMS) and `ordinal` (current episode position in project, `null` if not in CMS).

### Changed

- `GET /api/watch-history` — pagination changed from cursor-based (`after` / `afterId`) to page-based. New query param: `page` (integer, 1-based, default `1`). Response now returns `page` and `hasMore` instead of `nextAfter` and `nextAfterId`. **Breaking change.**
- `GET /api/watch-history` — both `video` and `project` modes now omit items whose `media_video_id` no longer exists in the CMS. Previously, video mode returned all records with CMS fields set to `null`.

---

## [0.7.0] - 2026-03-23

### Added

- `GET /api/roles/content` (episodes mode) — each episode now includes `progressMs` (milliseconds, `0` if never watched) and `serverUpdatedAt` (`null` if never watched), fetched from the user's watch progress.

---

## [0.6.1] - 2026-03-23

### Added

- `GET /api/projects/search` — search active projects by optional `keyword` (matched against name, description, dr_name, institution), `category` (category ID), and `program` (program ID). Returns a `searchEventId` UUID to be passed as `sourceSearchEventId` when reporting a project page session that originated from this search.
- `POST /api/analytics/project-page-sessions` — reports a project page session on leaving the page. Fields: `projectId`, `source` (one of `search`, `latest`, `recommended`, `favorites`, `roles_content`, `category`, `direct`), `startedAt`, `durationMs`, `videoStarted`, `sourceSearchEventId`. Stored in `kyalio-member-media-app-analytics-db`.
- `GET /api/watch-history?mode=project` — new mode that returns one item per project (the most recently watched episode), with a nested `latestEpisode` object containing `mediaVideoId`, `title`, `thumbnailUrl`, `progressMs`, and `serverUpdatedAt`.

---

## [0.6.0] - 2026-03-19

### Added

- `POST /api/pair/request` — Quest App requests a 6-digit pairing code (requires `X-Quest-Key`). Code expires in 5 minutes. Rate limited: 5/IP/min.
- `GET /api/pair/poll/{code}` — Quest App polls for pairing status (requires `X-Quest-Key`). Returns `pending` or `verified` with a JWT credential. Entry is deleted on first successful pickup. Rate limited: 60/IP/min.
- `POST /api/pair/verify` — Mobile App submits the 6-digit code to complete pairing (requires Bearer JWT). Issues a Quest JWT with the same `sub`, `email`, and `subscriptionIds` as the caller's token.
- `GET /api/subscriptions` — returns the subscription packages the user is entitled to, each with granted projects and a deduplicated category list. Replaces the `subscriptionProjects` field that was previously included in the login response.
- `GET /api/projects/{projectId}` — playlist items now include `progressMs` (milliseconds, `0` if never watched) and `serverUpdatedAt` (null if never watched). Both fields are fetched in parallel with the playlist query.
- `POST /api/favorites/{projectId}` — adds a project to the authenticated user's favorites. Idempotent; returns `204` even if already favorited.
- `DELETE /api/favorites/{projectId}` — removes a project from favorites. Idempotent; returns `204` even if not favorited.
- `GET /api/favorites` — returns all favorited projects sorted by most recently added (`createdAt DESC`). Each item includes `projectName`, `drName`, `categoryName`, `programPicUrl`, `thumbnailUrl`, `playlistDurationSeconds`, and `videoCount`. Content fields are `null` if the project no longer exists in the CMS.

### Infrastructure

- New `member_favorites` table in `kyalio-member-media-app-db`. Primary key: `(member_id, project_id)`.

---

## [0.5.0] - 2026-03-17

### Added

- `GET /api/watch-history` — returns the user's watch progress list sorted by most recently updated (`serverUpdatedAt DESC`). Supports `limit` (max 500) and `after` cursor for pagination. Each video appears at most once.
- `PATCH /api/watch-history/{mediaVideoId}` — upserts watch progress for a video. Implements optimistic locking via `knownServerUpdatedAt`: returns `409` with the current server record when a stale update is detected. On first sync (`knownServerUpdatedAt` omitted), uses `max(progressMs)` to avoid regressing progress.
- `POST /api/analytics/view-sessions` — batch upload of view sessions (max 50 per request). Idempotent via client-generated UUID `id`. Server injects `userId` from JWT.
- Member records are now written to `kyalio-member-media-app-db` on every successful login. Stores `playFabId`, `email`, and a PBKDF2-SHA256 password hash (310,000 iterations). Enables future migration away from PlayFab.

### Infrastructure

- Two new Cloudflare D1 databases: `kyalio-member-media-app-db` (watch progress, members) and `kyalio-member-media-app-analytics-db` (view sessions).
- Auth middleware now extracts and sets `userId` and `subscriptionIds` from JWT payload into Hono context for use in all protected endpoints.

---

## [0.4.0] - 2026-03-13

### Breaking Changes

- **`POST /api/auth/login` now requires `X-App-Key` header.**
  All clients must include `X-App-Key: <MOBILE_APP_API_KEY>` in every login request.
  Missing or invalid key returns `403 Forbidden`.

### Added

- `POST /api/auth/login`: rate limiting — 20 attempts per IP per 15 minutes, 10 per email per hour. Returns `429 Too Many Requests` when exceeded.
- JWT payload now includes `iss: "kyalio-member-media-api"`. Tokens issued before this release are no longer accepted; users must log in again.

### Documentation

- `docs/unity-api.md`: added `X-App-Key` requirement for login, added forgot-password section, updated Basic Flow.
- `openapi.yaml`: version bumped to `0.4.0`, login endpoint updated with `X-App-Key` parameter and `403`/`429` responses, JWT scheme description added.

---

## [0.3.0] - initial release
