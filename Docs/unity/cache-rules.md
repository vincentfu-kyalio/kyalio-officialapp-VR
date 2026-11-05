# Cache and Update Rules

When clients must re-fetch project metadata, invalidate local image caches, refresh the program catalog, or treat downloaded files as stale.

Related: [content.md](content.md) for `/api/me/sync` and `/api/projects/batch`; [schemas.md](schemas.md) for `projectVersion`, `thumbnailVersion`, and `thumbnailsExpireAt` semantics.

---

## Project cache invalidation

Clients must re-fetch full `Project` when:

- local project is missing
- `projectVersion` changed

`projectVersion` is content-only. Member watch-progress updates must never change it.

---

## Thumbnail cache invalidation

Clients must invalidate locally cached images for a project when:

- `thumbnailVersion` changed

This includes:

- project thumbnail
- playlist item thumbnails

After invalidation, clients must fetch fresh project metadata before downloading any project or playlist thumbnails again.

---

## Program catalog refresh

Clients should refresh normalized program metadata from `GET /api/me/home` → `filters.programs[]`.

- `ProgramSummary.updatedAt` changes independently from any `Project` version.
- Program thumbnail URLs are auth-gated proxy URLs governed by `ProgramSummary.updatedAt` and the thumbnail endpoint's HTTP cache policy, not by `thumbnailsExpireAt`.
- Clients should overwrite their local program catalog snapshot on each home response.

---

## Downloaded video staleness

Clients should treat a downloaded video file as stale when:

- its `projectId` no longer appears as a key in `/api/me/sync` → `grantedProjects`
- the playlist item that previously referenced its `videoId` now references a different `videoId`
