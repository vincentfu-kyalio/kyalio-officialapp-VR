# API Changelog

Changes affecting Unity and CMS clients. For the current API contract, see [`unity/README.md`](unity/README.md), [`cms-admin-api.md`](cms-admin-api.md), and [`password-reset-api.md`](password-reset-api.md).

## Format

Each release gets a `## [version] - YYYY-MM-DD` heading. Use these subsections, omitting any that are empty:

- **Breaking** — clients must change to continue working.
- **Added** — new endpoint, new field, or new error code.
- **Changed** — semantics or shape adjusted compatibly.
- **Removed** — endpoint or field no longer served.

Each entry should link to the relevant spec file (`unity/*.md`, `cms-admin-api.md`, or `password-reset-api.md`) rather than restating the spec.

---

## [Unreleased]

### Breaking

- `PUT /api/admin/members/{id}/packages` is removed; packages now belong to member contracts. Use `PUT /api/admin/members/{id}/contracts`. See [`cms-admin-api.md`](cms-admin-api.md#replace-member-contracts).
- `POST /api/admin/members` no longer accepts the top-level `packages` field (returns `400`); send `contracts[].packages`. See [`cms-admin-api.md`](cms-admin-api.md#create-member).

### Added

- `POST /api/auth/quest/login`: Quest sign-in with email and password, as an alternative to 6-digit pairing. It returns the same session-bound, non-refreshable Quest credential and shares rate limits with `POST /api/auth/login`. If it would end another active Quest session, it returns `409` with a ticket; confirm with `POST /api/auth/quest/login/confirm`. See [`unity/auth.md`](unity/auth.md#quest-password-login).
- `POST /api/auth/forgot-password` now also accepts `X-Quest-Key`, so the Quest app can request a password reset email. See [`unity/auth.md`](unity/auth.md#forgot-password).
- `fullName` on `POST /api/admin/members` and `full_name` on `PATCH /api/admin/members/{id}` (D1 only, not synced to PlayFab). See [`cms-admin-api.md`](cms-admin-api.md#update-member-display-name).
- Bulk member email: `GET /api/admin/email-templates`, `POST /api/admin/email-jobs` (+ `/preview`, `/test`), `GET /api/admin/email-jobs[/{id}]`, `POST /api/admin/email-jobs/{id}/retry-failed`. Each recipient gets an individual message. See [`cms-admin-api.md`](cms-admin-api.md#bulk-email).
- `GET` / `PUT /api/admin/members/{id}/contracts`, and `contracts` on `POST /api/admin/members` (response adds `contractCount`). See [`cms-admin-api.md`](cms-admin-api.md#member-contracts) and [`member-db-schema.md`](member-db-schema.md).

### Changed

- Package grants now come from the member's contracts: packages of a contract before its start date or after its expiry date are no longer granted. Access-token and Quest-credential `expiresAt` can be earlier than the usual TTL when a contract starts or expires sooner. See [`unity/auth.md`](unity/auth.md).

- `POST /api/auth/forgot-password` now defers to PlayFab's hosted account-recovery flow (default email + default reset page). The request/response shape and rate limits are unchanged. See [`password-reset-api.md`](password-reset-api.md).
- `POST /api/admin/members` now creates the PlayFab account before writing the D1 member row, uses the PlayFab ID as the D1 member ID, queues PlayFab account deletion if D1 insert fails, and returns `playFabId` on success. See [`cms-admin-api.md`](cms-admin-api.md).
- `POST /api/auth/login` now validates credentials against PlayFab as the authoritative source during the PlayFab→D1 migration; the D1 `password_hash` mirror is no longer consulted for verification. See [`unity/auth.md`](unity/auth.md).
