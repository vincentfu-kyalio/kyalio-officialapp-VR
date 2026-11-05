# Authentication Endpoints

This document covers all auth endpoints: Quest pairing, Quest password login, mobile login, device replace/switch, token refresh, logout, and forgot password.

For the overall auth model (which mechanism applies where, global error codes, app-version handling), see [overview.md](overview.md#authentication-overview). For the `Credential` schema, see [schemas.md](schemas.md#1-credential).

---

## Quest Pairing

### `POST /api/pair/request`

Required header:

```http
X-Quest-Key: <APP_KEY_QUEST>
X-App-Version: 2.0.0
```

Request body:

```json
{
  "deviceId": "quest-generated-stable-uuid",
  "model": "Meta Quest 3",
  "deviceOs": "Quest OS"
}
```

`deviceId` is required. It must be a stable client-generated identifier for the physical Quest. The server stores only a hash of this value and uses it to manage the active Quest session lease.

Response:

```json
{
  "code": "391847",
  "expiresAt": "2026-03-19T10:05:00.000Z"
}
```

Rate limited: 5 requests per IP per minute.

Common errors:

| Status | Code | Meaning |
| --- | --- | --- |
| `400` | `QUEST_DEVICE_BINDING_REQUIRED` | Missing `deviceId` |
| `403` | `PAIR_FORBIDDEN` | Missing or invalid `X-Quest-Key` |
| `429` | `PAIR_RATE_LIMITED` | Too many requests |

---

### `GET /api/pair/stream/{code}` (SSE)

Required header:

```http
X-Quest-Key: <APP_KEY_QUEST>
X-App-Version: 2.0.0
```

Event: `pending`

```text
event: pending
data: {"status":"pending"}
```

Event: `verified`

```text
event: verified
data: {"status":"verified","credential":{"accessToken":"<jwt-token>","tokenType":"Bearer","expiresAt":"2026-03-19T14:00:00.000Z"}}
```

The Quest access token is bound to an active server-side Quest session. When a new pairing is verified, the server revokes any existing active Quest session for that member and any existing active session for the same physical Quest before issuing the new credential.

The Quest credential's `expiresAt` is at most `QUEST_TOKEN_TTL_SECONDS` after pairing, and earlier if one of the member's contracts starts or expires sooner. Quest credentials are not refreshable; the headset pairs again to continue.

Event: `expired`

```text
event: expired
data: {"error":"Code expired"}
```

| HTTP Status | Meaning                                                |
| ----------- | ------------------------------------------------------ |
| `200`       | SSE stream opened                                      |
| `403`       | Missing or invalid X-Quest-Key                         |
| `404`       | Code not found or already expired before stream opened |
| `429`       | Too many failed attempts from this IP                  |

---

### `POST /api/pair/verify`

Called by **Mobile App** after the user enters the 6-digit code. Requires Bearer token.

Request:

```json
{ "code": "391847" }
```

On success, any replaced member session is revoked with `revokedReason: "replaced"`. If the physical Quest was active for another member, that previous session is revoked with `revokedReason: "account_switched"`.

| Status | Meaning                                      |
| ------ | -------------------------------------------- |
| `204`  | Pairing successful                           |
| `400`  | Invalid JSON body or missing `code` field    |
| `403`  | Bearer token is not a mobile device token    |
| `404`  | Code not found or expired                    |
| `409`  | Code already used                            |

---

## Quest Password Login

### `POST /api/auth/quest/login`

An alternative to 6-digit pairing: the member signs in on the Quest with the same email and password they use on mobile. Both methods produce the same kind of Quest credential, so the client can offer either one.

Required header:

```http
X-Quest-Key: <APP_KEY_QUEST>
X-App-Version: 2.0.0
```

Request:

```json
{
  "email": "ct1@abc.com",
  "password": "123456",
  "deviceId": "quest-generated-stable-uuid",
  "model": "Meta Quest 3",
  "deviceOs": "Quest OS"
}
```

`deviceId` is required and must be the same stable Quest identifier sent to `POST /api/pair/request`. `model` (or `deviceModelCode`) and `deviceOs` are optional.

Response:

```json
{
  "credential": {
    "accessToken": "<jwt-token>",
    "tokenType": "Bearer",
    "expiresAt": "2026-03-19T14:00:00.000Z"
  }
}
```

The credential is the same as the one in the pairing `verified` event:

- It is bound to a server-side Quest session. Signing in starts a new session and ends the member's previous session on this Quest, if there is one.
- `expiresAt` is at most `QUEST_TOKEN_TTL_SECONDS` after login, or earlier if one of the member's contracts starts or expires sooner.
- There is no `refreshToken`. When the credential expires, sign in again by password or pairing.
- Quest logout works the same as for a paired session. See [Logout](#logout).

Rate limits are shared with `POST /api/auth/login`: 20 attempts per IP per 15 minutes and 10 per email per hour, counted across both endpoints.

#### Confirmation before ending another session

If signing in would end another active Quest session, the API returns `409` with a short-lived `ticket` (10 minutes, single use) instead of a credential. Show a confirmation, then call `POST /api/auth/quest/login/confirm` with the ticket. If the user cancels, discard the ticket: nothing has changed on the server.

The error codes and body shape are the same as for mobile login conflicts, with `deviceKind: "quest"`, so the client can share the confirmation UI. Quest responses always include `ticketType`, and device context has `startedAt` (when that session began) instead of `lastSeenAt`.

| Code | `ticketType` | When | Device context |
| --- | --- | --- | --- |
| `DEVICE_REPLACE_REQUIRED` | `replace` | The account is signed in on another Quest | `existingDevice` |
| `DEVICE_ACCOUNT_SWITCH_REQUIRED` | `switch` | This Quest is signed in to another account | `existingDevice` |
| `DEVICE_REPLACE_SWITCH_REQUIRED` | `replace_switch` | Both | `replaceDevice`, `switchDevice` |

Example response:

```json
{
  "error": {
    "code": "DEVICE_ACCOUNT_SWITCH_REQUIRED",
    "message": "This Quest is already signed in to another account."
  },
  "ticket": "<device-confirm-ticket>",
  "ticketType": "switch",
  "deviceKind": "quest",
  "existingDevice": {
    "deviceModelCode": "Meta Quest 3",
    "deviceOs": "Quest OS",
    "startedAt": "2026-03-19T09:00:00.000Z"
  }
}
```

A member signing in again on the same Quest is not a conflict and gets a credential directly.

Common errors:

| Status | Code                             | Meaning |
| ------ | -------------------------------- | ------- |
| `400`  | `AUTH_MISSING_CREDENTIALS`       | Missing `email` or `password` |
| `400`  | `QUEST_DEVICE_BINDING_REQUIRED`  | Missing `deviceId` |
| `401`  | `AUTH_INVALID_CREDENTIALS`       | Wrong email or password |
| `403`  | `AUTH_FORBIDDEN`                 | Missing or invalid `X-Quest-Key` |
| `409`  | `DEVICE_REPLACE_REQUIRED`        | Confirmation required (see above) |
| `409`  | `DEVICE_ACCOUNT_SWITCH_REQUIRED` | Confirmation required (see above) |
| `409`  | `DEVICE_REPLACE_SWITCH_REQUIRED` | Confirmation required (see above) |
| `429`  | `AUTH_RATE_LIMITED`              | Too many requests |
| `502`  | `AUTH_SERVICE_ERROR`             | Authentication service (PlayFab) error |

---

### `POST /api/auth/quest/login/confirm`

Completes a Quest password login after `POST /api/auth/quest/login` returns one of the `409` confirmation codes. No password and no Bearer token are needed; the ticket proves the password was checked.

Required header:

```http
X-Quest-Key: <APP_KEY_QUEST>
X-App-Version: 2.0.0
```

Request:

```json
{
  "ticket": "<device-confirm-ticket>"
}
```

The success response is the same as `POST /api/auth/quest/login`: `{ "credential": { "accessToken", "tokenType", "expiresAt" } }`.

On success, the confirmed sessions are revoked: the member's session on the other Quest with `revokedReason: "replaced"`, and the other account's session on this Quest with `revokedReason: "account_switched"`. That headset's access token stops working on its next request.

If a new conflict appeared after the ticket was issued (for example, the member paired another Quest in the meantime), the endpoint returns a new `409` with a fresh ticket covering the current conflicts. Show the confirmation again and call this endpoint with the new ticket.

Common errors:

| Status | Code                    | Meaning |
| ------ | ----------------------- | ------- |
| `400`  | `DEVICE_TICKET_INVALID` | Missing `ticket` in body |
| `401`  | `DEVICE_TICKET_INVALID` | Ticket is invalid, expired, or already used |
| `403`  | `AUTH_FORBIDDEN`        | Missing or invalid `X-Quest-Key` |
| `409`  | `DEVICE_*_REQUIRED`     | A new conflict needs confirmation; retry with the new ticket |

For a forgotten password, the Quest app can call [`POST /api/auth/forgot-password`](#forgot-password) with `X-Quest-Key`.

---

## Login

### `POST /api/auth/login`

Required header:

```http
X-App-Key: <APP_KEY_IOS>
X-App-Version: 2.0.0
```

or

```http
X-App-Key: <APP_KEY_ANDROID>
X-App-Version: 2.0.0
```

Request:

```json
{
  "email": "ct1@abc.com",
  "password": "123456",
  "deviceId": "client-generated-uuid",
  "deviceKind": "phone",
  "deviceModelCode": "iPad13,18",
  "deviceOs": "iOS"
}
```

Response:

```json
{
  "credential": {
    "accessToken": "<jwt-token>",
    "tokenType": "Bearer",
    "expiresAt": "2026-03-12T09:00:00.000Z",
    "refreshToken": "<opaque-hex-string>",
    "refreshTokenExpiresAt": "2026-04-11T08:00:00.000Z"
  }
}
```

The returned JWT payload includes `deviceType: "ios"` or `deviceType: "android"`, `memberDeviceId`, plus the token issuance `appVersion`. Mobile clients must persist both `accessToken` and `refreshToken` securely.

`expiresAt` is usually one hour after issuance but can be earlier: the token carries the member's current package grants, so it expires when one of the member's contracts starts or expires. Schedule refresh from `expiresAt`, never from a fixed TTL. This applies to every response that returns a credential, including refresh and device replace/switch.

`deviceKind` must be `phone` or `tablet`. `deviceModelCode` and `deviceOs` are optional technical metadata fields. Do not send a user-supplied or custom device name; the server does not collect `deviceName`.

If the member already has a different active device for the same `deviceKind`, the API returns `409 DEVICE_REPLACE_REQUIRED` with a short-lived `ticket`. After user confirmation, call `POST /api/auth/devices/replace`.

Example response:

```json
{
  "error": {
    "code": "DEVICE_REPLACE_REQUIRED",
    "message": "This account is already linked to another phone."
  },
  "ticket": "<device-confirm-ticket>",
  "deviceKind": "phone",
  "existingDevice": {
    "deviceModelCode": "iPhone15,2",
    "deviceOs": "iOS",
    "lastSeenAt": "2026-05-03T01:00:00.000Z"
  }
}
```

If the submitted `deviceId` is actively linked to another member, the API returns `409 DEVICE_ACCOUNT_SWITCH_REQUIRED` with a short-lived `ticket`. After user confirmation, call `POST /api/auth/devices/switch`.

Example response:

```json
{
  "error": {
    "code": "DEVICE_ACCOUNT_SWITCH_REQUIRED",
    "message": "This device is already linked to another account."
  },
  "ticket": "<device-confirm-ticket>",
  "deviceKind": "phone",
  "existingDevice": {
    "deviceModelCode": "iPhone15,2",
    "deviceOs": "iOS",
    "lastSeenAt": "2026-05-03T02:00:00.000Z"
  }
}
```

If both conflicts exist at once, the API returns `409 DEVICE_REPLACE_SWITCH_REQUIRED` with `ticketType: "replace_switch"`. After user confirmation, call `POST /api/auth/devices/replace`. This confirms that the physical device may move from the previous member to the target member and replace the target member's existing same-kind device in one step.

Example response:

```json
{
  "error": {
    "code": "DEVICE_REPLACE_SWITCH_REQUIRED",
    "message": "This device is linked to another account, and this account is already linked to another phone."
  },
  "ticket": "<device-confirm-ticket>",
  "ticketType": "replace_switch",
  "deviceKind": "phone",
  "replaceDevice": {
    "deviceModelCode": "iPhone15,2",
    "deviceOs": "iOS",
    "lastSeenAt": "2026-05-03T01:00:00.000Z"
  },
  "switchDevice": {
    "deviceModelCode": "iPhone15,2",
    "deviceOs": "iOS",
    "lastSeenAt": "2026-05-03T02:00:00.000Z"
  }
}
```

---

### `POST /api/auth/devices/replace`

Completes a device replacement after `POST /api/auth/login` returns `DEVICE_REPLACE_REQUIRED` or `DEVICE_REPLACE_SWITCH_REQUIRED`.

No Bearer header is required. The endpoint uses the short-lived ticket returned by login. `X-App-Key` and `X-App-Version` are required.

Required header:

```http
X-App-Key: <APP_KEY_IOS>
X-App-Version: 2.0.0
```

or

```http
X-App-Key: <APP_KEY_ANDROID>
X-App-Version: 2.0.0
```

Request:

```json
{
  "ticket": "<device-confirm-ticket>"
}
```

Success response:

```json
{
  "credential": {
    "accessToken": "<jwt-token>",
    "tokenType": "Bearer",
    "expiresAt": "2026-03-12T09:00:00.000Z",
    "refreshToken": "<opaque-hex-string>",
    "refreshTokenExpiresAt": "2026-04-11T08:00:00.000Z"
  }
}
```

For a plain replace ticket, success revokes the member's old same-kind device binding and its attached refresh tokens, then creates or reuses the incoming device binding for the member.

For a `replace_switch` ticket, success confirms both actions in one step:

- move this physical device from the previous member to the target member
- replace the target member's existing same-kind device

The combined state transition revokes both old bindings and their attached refresh tokens before issuing the new device-bound credential.

If a plain replace ticket later discovers that the incoming physical device is now active on another member, the endpoint returns `409 DEVICE_REPLACE_SWITCH_REQUIRED` with a new `ticketType: "replace_switch"` ticket. The client should show the combined confirmation and call this endpoint again with the new ticket.

Common errors:

| Status | Code                             | Meaning |
| ------ | -------------------------------- | ------- |
| `400`  | `DEVICE_TICKET_INVALID`          | Missing `ticket` in body |
| `401`  | `DEVICE_TICKET_INVALID`          | Ticket is invalid, expired, already used, or for another app key |
| `403`  | `AUTH_FORBIDDEN`                 | Missing or invalid `X-App-Key` |
| `404`  | `DEVICE_NOT_FOUND`               | Referenced device binding is no longer active |
| `409`  | `DEVICE_REPLACE_SWITCH_REQUIRED` | Replacement also requires switching this physical device from another member |
| `500`  | `DEVICE_REPLACE_FAILED`          | Replacement failed |
| `500`  | `DEVICE_REPLACE_SWITCH_FAILED`   | Combined replace-switch failed |

---

### `POST /api/auth/devices/switch`

Completes a device account switch after `POST /api/auth/login` returns `DEVICE_ACCOUNT_SWITCH_REQUIRED`.

No Bearer header is required. The endpoint uses the short-lived ticket returned by login. `X-App-Key` and `X-App-Version` are required.

Required header:

```http
X-App-Key: <APP_KEY_IOS>
X-App-Version: 2.0.0
```

or

```http
X-App-Key: <APP_KEY_ANDROID>
X-App-Version: 2.0.0
```

Request:

```json
{
  "ticket": "<device-confirm-ticket>"
}
```

Success response:

```json
{
  "credential": {
    "accessToken": "<jwt-token>",
    "tokenType": "Bearer",
    "expiresAt": "2026-03-12T09:00:00.000Z",
    "refreshToken": "<opaque-hex-string>",
    "refreshTokenExpiresAt": "2026-04-11T08:00:00.000Z"
  }
}
```

On success, the server revokes the previous member's binding for this physical device and its attached refresh tokens, then creates a new binding for the target member and issues a new device-bound credential.

If the target member already has a different active device for the same `deviceKind`, the endpoint returns `409 DEVICE_REPLACE_SWITCH_REQUIRED` with a new `ticketType: "replace_switch"` ticket. The client should show the combined confirmation and call `POST /api/auth/devices/replace` with the new ticket.

Common errors:

| Status | Code                             | Meaning |
| ------ | -------------------------------- | ------- |
| `400`  | `DEVICE_TICKET_INVALID`          | Missing `ticket` in body |
| `401`  | `DEVICE_TICKET_INVALID`          | Ticket is invalid, expired, already used, or for another app key |
| `403`  | `AUTH_FORBIDDEN`                 | Missing or invalid `X-App-Key` |
| `404`  | `DEVICE_NOT_FOUND`               | Referenced device binding is no longer active |
| `409`  | `DEVICE_REPLACE_SWITCH_REQUIRED` | Account switch also requires replacing the target member's same-kind device |
| `500`  | `DEVICE_SWITCH_FAILED`           | Device switch failed |

---

## Token Refresh

### `POST /api/auth/refresh`

Exchanges a valid refresh token for a new access token and a new (rotated) refresh token. Mobile only.

No Bearer header is required. No `X-App-Key` header is required. `X-App-Version` is required.

```http
X-App-Version: 2.0.0
```

Request:

```json
{
  "refreshToken": "<opaque-hex-string>"
}
```

Response:

```json
{
  "credential": {
    "accessToken": "<jwt-token>",
    "tokenType": "Bearer",
    "expiresAt": "2026-03-12T10:00:00.000Z",
    "refreshToken": "<new-opaque-hex-string>",
    "refreshTokenExpiresAt": "2026-04-11T09:00:00.000Z"
  }
}
```

Rules:

- The presented refresh token is **revoked** on success; clients must replace their stored refresh token with the newly returned one.
- Only refresh tokens whose stored `device_type` is `ios` or `android` are accepted. Quest credentials are not refreshable.
- Mobile refresh tokens are bound to an active `memberDeviceId`. If the device binding is revoked, refresh returns `401 AUTH_INVALID_TOKEN`.
- Mobile protected API requests also require the JWT `memberDeviceId` to point to an active device binding. If an admin removes the device or a replace/switch flow revokes it, already-issued access tokens for that device return `401 AUTH_INVALID_TOKEN` on the next protected request, even if the JWT `exp` has not passed.
- Presenting an already-revoked refresh token is treated as token theft: **all** of that member's refresh tokens are revoked and `401 AUTH_INVALID_TOKEN` is returned. The user will need to re-login.
- Rate limited: 30 requests per IP per 15 minutes.

Common errors:

| Status | Code                  | Meaning                                    |
| ------ | --------------------- | ------------------------------------------ |
| `400`  | `INVALID_REQUEST`     | Missing `refreshToken` in body             |
| `401`  | `AUTH_INVALID_TOKEN`  | Refresh token is invalid, expired, or was revoked |
| `429`  | `AUTH_RATE_LIMITED`   | Too many requests                          |

---

## Logout

### `POST /api/auth/logout`

Revokes the given refresh token. Safe to call without an access token.

No Bearer header is required. `X-App-Version` is not required; logout is best-effort and will try to revoke the supplied refresh token even from unsupported app versions.

Request:

```json
{
  "refreshToken": "<opaque-hex-string>"
}
```

Response: `204 No Content` — always, even if the refresh token is missing, unknown, or already revoked (prevents token-existence probing).

Rate limited: 20 requests per IP per 15 minutes.

Notes:

- For mobile, this only invalidates the refresh token; the current access token remains valid until it naturally expires (up to 1 hour later). Clients should also discard the access token locally.
- For Quest, send the Quest Bearer token on logout. The API revokes the active Quest session, so the current Quest access token stops working on the next protected request.
- There is no dedicated "revoke all sessions" endpoint. Logging out on each device individually is the current flow.

---

## Forgot Password

### `POST /api/auth/forgot-password`

Available to the mobile apps and the Quest app. Required header:

```http
X-App-Key: <APP_KEY_IOS>
X-App-Version: 2.0.0
```

or

```http
X-App-Key: <APP_KEY_ANDROID>
X-App-Version: 2.0.0
```

or

```http
X-Quest-Key: <APP_KEY_QUEST>
X-App-Version: 2.0.0
```

The reset email and reset page are the same for every app. The member finishes the reset in a browser (for example on their phone), then signs in on the Quest with the new password.

Request:

```json
{
  "email": "ct1@abc.com"
}
```

Success response:

```json
{
  "message": "If the account exists, a password reset email has been sent."
}
```

Rate limited: 5 attempts per email per hour, 10 per IP per 15 minutes, 60-second cooldown per email after each dispatch.

During the PlayFab→D1 migration, the recovery email and reset page are PlayFab-hosted — the Worker only triggers `Admin/SendAccountRecoveryEmail`. Unity has nothing to do beyond calling this endpoint. See [`../password-reset-api.md`](../password-reset-api.md) for the current flow and the parked Kyalio-hosted contract.
