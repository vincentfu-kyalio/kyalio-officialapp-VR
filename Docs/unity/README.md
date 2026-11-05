# Unity API Documentation

The Unity integration contract for the Member Media API, split by topic.

## Files

- **[overview.md](overview.md)** — Base URL, authentication overview, error envelope, basic flows (Mobile / Quest), app version check, and the full endpoint inventory by auth class.
- **[schemas.md](schemas.md)** — Core shared DTO schemas: `Credential`, `ProgramSummary`, `EpisodeRef`, `PlaylistItem`, `Project`, `ProgressItem`, `PagedResponse<T>`, thumbnail envelope, and the public ID format.
- **[auth.md](auth.md)** — Auth endpoints: Quest pairing, Quest password login, mobile login, device replace / switch, token refresh, logout, forgot password.
- **[content.md](content.md)** — Content endpoints: delta sync, progress sync, project batch fetch, home feed, project search, project detail.
- **[media.md](media.md)** — Video stream URL and the R2 download lifecycle (request URL → complete / cancel / delete).
- **[engagement.md](engagement.md)** — Favorites, watch history, analytics reporting.
- **[cache-rules.md](cache-rules.md)** — When clients must re-fetch metadata, invalidate image caches, refresh the program catalog, or treat downloaded files as stale.

## Where to start

- First-time Unity integration: read **overview.md** end-to-end, then **schemas.md**, then the endpoint files in the order your flow needs them (typically auth → content → media → engagement).
- Looking up one endpoint: use the file map above. The auth class (`X-App-Key`, `X-Quest-Key`, Bearer, etc.) for every endpoint is summarized in [overview.md#endpoints-overview](overview.md#endpoints-overview).
- Debugging a stale local cache: see [cache-rules.md](cache-rules.md) for the invalidation triggers per resource type.

## Related docs (outside this folder)

- [`../password-reset-api.md`](../password-reset-api.md) — Web-based password reset flow (linked from `auth.md` forgot-password).
- [`../device-binding.md`](../device-binding.md) — Server-side device binding spec backing the `deviceId` / replace / switch flows.
- [`../api-changelog.md`](../api-changelog.md) — Cross-cutting API changelog.
- [`../cms-admin-api.md`](../cms-admin-api.md) — CMS admin API (not consumed by Unity clients).
- [`../api-design-notes.md`](../api-design-notes.md) — Backend-facing design principles and CMS contract assumptions (not part of the client contract).
