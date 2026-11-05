# Unity API — Overview

This document defines the Unity integration contract for the Member Media API, with a focus on **shared response schemas**, **client cache behavior**, and **stable DTO design** for Android / iOS App and Quest App.

Back-end stack context:

- Cloudflare Worker (Member Media API)
- D1 / SQLite-like relational storage for content and member data
- Pages (CMS)
- R2 (downloadable source files)
- Mux (streaming playback + signed thumbnails)

Front-end clients:

- Android / iOS App (Unity Engine)
- Quest App (Unity Engine)

This folder is split by topic. See [README](README.md) for the full index.

---

## Base URL

- Local: `http://127.0.0.1:8787`
- Production: `<your-worker-domain>`

---

## Authentication Overview

| Client               | Endpoint group                                           | Auth mechanism                             |
| -------------------- | -------------------------------------------------------- | ------------------------------------------ |
| Mobile App (iOS)     | `POST /api/auth/login`, `POST /api/auth/forgot-password` | `X-App-Key: <APP_KEY_IOS>` + `X-App-Version` (no Bearer) |
| Mobile App (Android) | `POST /api/auth/login`, `POST /api/auth/forgot-password` | `X-App-Key: <APP_KEY_ANDROID>` + `X-App-Version` (no Bearer) |
| Mobile App           | `POST /api/auth/refresh`                                 | Refresh token in JSON body + `X-App-Version` (no Bearer) |
| Mobile App           | `POST /api/auth/logout`                                  | Refresh token in JSON body (no Bearer; no version check) |
| Quest App            | `POST /api/pair/request`, `GET /api/pair/stream/{code}`, `POST /api/auth/quest/login`, `POST /api/auth/forgot-password` | `X-Quest-Key` + `X-App-Version` (no Bearer) |
| Mobile & Quest       | All other `/api/*`                                       | `Authorization: Bearer <accessToken>` + `X-App-Version` |

Each device type has its own app key (`APP_KEY_IOS` / `APP_KEY_ANDROID` / `APP_KEY_QUEST`). The server resolves the header to a `deviceType` and rejects unknown keys with `403 Forbidden`.

All non-admin Unity client endpoints except `POST /api/auth/logout` require `X-App-Version` in `major.minor.patch` format. The default minimum version is `2.0.0`, with optional device-type-specific server overrides.

> **Global auth errors** — all protected `/api/*` endpoints (those requiring Bearer) will return the following before reaching handler logic:
>
> | Status | Error code            | Meaning                              |
> | ------ | --------------------- | ------------------------------------ |
> | `401`  | `AUTH_MISSING_TOKEN`  | `Authorization` header absent or empty |
> | `401`  | `AUTH_INVALID_TOKEN`  | Token is malformed, expired, or has an unrecognised `deviceType` claim |

> **Global app-version errors** — all non-admin Unity client endpoints except `POST /api/auth/logout` may return the following before handler logic:
>
> | Status | Error code                | Meaning                              |
> | ------ | ------------------------- | ------------------------------------ |
> | `400`  | `APP_VERSION_MISSING`     | `X-App-Version` header is absent or empty |
> | `400`  | `APP_VERSION_INVALID`     | `X-App-Version` is not `major.minor.patch` |
> | `426`  | `APP_VERSION_UNSUPPORTED` | App version is below the device type minimum |

```http
Authorization: Bearer <accessToken>
```

Content APIs choose media files by the JWT `deviceType` claim:

- `ios`
- `android`
- `quest`

iOS and Android both use CMS `mobile` media variants. Quest uses CMS `quest` media variants. The JWT also records `appVersion`, but enforcement uses the current request's `X-App-Version`.

For the actual auth endpoints (login, refresh, logout, forgot password, Quest pairing, Quest password login, device replace/switch), see [auth.md](auth.md).

---

## Error Response Schema

All business error responses follow this shape:

```json
{
  "error": {
    "code": "AUTH_INVALID_CREDENTIALS",
    "message": "Invalid email or password"
  }
}
```

Rules:

- `code` is stable for programmatic handling.
- `message` is for logs / debugging / fallback display.
- Do not use the raw `message` as the primary branch condition in clients.

---

## Basic Flow — Mobile App

1. Send `X-App-Version` on every API request except logout.
2. On launch, optionally call `GET /api/app/version-check` with `X-App-Key` and `X-App-Version` before refresh/login UI work.
3. If any endpoint returns `426 APP_VERSION_UNSUPPORTED`, show the forced-update UI and stop normal API flow.
4. Call `POST /api/auth/login` with `X-App-Key` when the user needs to sign in.
5. Store `credential.accessToken`, `credential.expiresAt`, `credential.refreshToken`, and `credential.refreshTokenExpiresAt` securely.
6. Call `GET /api/me/sync`.
7. Diff local cache state against server state.
8. Call `POST /api/projects/batch` for missing / outdated granted projects.
9. Call `GET /api/me/progress` for full progress sync on cold start, or `GET /api/me/progress?since=...` on resume.
10. Call `GET /api/me/home` to refresh the home layout and normalized catalog.
11. Before each subsequent API call, check if the access token is expired (`expiresAt` vs current UTC). If expired:
   - If `refreshTokenExpiresAt` is still in the future, call `POST /api/auth/refresh` with the stored `refreshToken`. Replace both the access token and the refresh token with the newly returned values. Retry the original request.
   - If the refresh call returns `401 AUTH_INVALID_TOKEN`, or if `refreshTokenExpiresAt` has already passed, clear stored credentials and redirect to the login screen.
12. On user-initiated logout, call `POST /api/auth/logout` with the stored `refreshToken`, then clear local credentials.

---

## Basic Flow — Quest App

1. Send `X-App-Version` on every API request.
2. On launch, optionally call `GET /api/app/version-check` with `X-Quest-Key` and `X-App-Version`.
3. If any endpoint returns `426 APP_VERSION_UNSUPPORTED`, show the forced-update UI and stop normal API flow.
4. Sign in with either method:
   - **Pairing:** call `POST /api/pair/request` with `X-Quest-Key`, display the 6-digit `code`, and open an SSE connection to `GET /api/pair/stream/{code}` with `X-Quest-Key`. On `verified`, close the connection.
   - **Email and password:** call `POST /api/auth/quest/login` with `X-Quest-Key`. If it returns `409` with a `ticket`, show the confirmation and call `POST /api/auth/quest/login/confirm` with the ticket.
5. Store `credential.accessToken` and `credential.expiresAt`. Both methods return the same credential.
6. Call `GET /api/me/sync`.
7. Fetch required projects via `POST /api/projects/batch`.
8. Call `GET /api/me/progress` for full progress sync on cold start, or `GET /api/me/progress?since=...` on resume.
9. Call `GET /api/me/home`.
10. If the access token is expired (2-hour TTL), return to step 4. Quest has no refresh-token flow.

---

## App Version Check

### `GET /api/app/version-check`

Optional bootstrap endpoint for showing update UI before login, refresh, or pairing. Enforcement also happens globally, so clients must still handle `426 APP_VERSION_UNSUPPORTED` from any non-admin endpoint.

Required headers for iOS / Android:

```http
X-App-Key: <APP_KEY_IOS or APP_KEY_ANDROID>
X-App-Version: 2.0.0
```

Required headers for Quest:

```http
X-Quest-Key: <APP_KEY_QUEST>
X-App-Version: 2.0.0
```

Success response:

```json
{
  "deviceType": "ios",
  "currentVersion": "2.0.0",
  "minimumVersion": "2.0.0",
  "updateRequired": false,
  "storeUrl": "https://apps.apple.com/app/..."
}
```

Unsupported version response:

```json
{
  "error": {
    "code": "APP_VERSION_UNSUPPORTED",
    "message": "Please update the app to continue."
  },
  "deviceType": "android",
  "currentVersion": "1.9.9",
  "minimumVersion": "2.0.0",
  "storeUrl": "https://play.google.com/store/apps/details?id=..."
}
```

---

## Endpoints Overview

All non-admin Unity client endpoints except `POST /api/auth/logout` require `X-App-Version`.

### App version check (no Bearer)

- `GET /api/app/version-check`

### App Key only (no Bearer)

- `POST /api/auth/login`
- `POST /api/auth/devices/replace`
- `POST /api/auth/devices/switch`
- `POST /api/auth/forgot-password` _(also accepts `X-Quest-Key`)_

### Refresh token only (no Bearer)

- `POST /api/auth/refresh`
- `POST /api/auth/logout`

### Quest Key only (no Bearer)

- `POST /api/pair/request`
- `GET /api/pair/stream/{code}`
- `POST /api/auth/quest/login`
- `POST /api/auth/quest/login/confirm`

### Bearer Required (Mobile + Quest)

- `POST /api/pair/verify` _(Mobile only)_
- `GET /api/me/sync`
- `GET /api/me/progress`
- `POST /api/projects/batch`
- `GET /api/me/home`
- `GET /api/projects/search`
- `GET /api/projects/{projectId}`
- `GET /api/projects/{projectId}/thumbnail`
- `GET /api/projects/{projectId}/videos/{videoId}/thumbnail`
- `GET /api/programs/{programId}/thumbnail`
- `GET /api/projects/{projectId}/videos/{videoId}/stream`
- `GET /api/projects/{projectId}/videos/{videoId}/download`
- `POST /api/projects/{projectId}/videos/{videoId}/download/complete`
- `POST /api/projects/{projectId}/videos/{videoId}/download/cancel`
- `DELETE /api/projects/{projectId}/videos/{videoId}/download`
- `GET /api/watch-history`
- `PATCH /api/watch-history/{videoId}`
- `POST /api/analytics/project-page-sessions`
- `POST /api/analytics/view-sessions`
- `GET /api/favorites`
- `POST /api/favorites/{projectId}`
- `DELETE /api/favorites/{projectId}`
