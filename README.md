# GoRide Identity & Auth

ASP.NET Core (.NET 10) backend that fronts the WSO2 Identity Server (Asgardeo)
tenant for GoRide: OIDC login with a cookie session, role selection, SCIM2
profile maintenance, driver profiles and account deactivation.

## Run locally

```bash
cd src
dotnet user-secrets set "Asgardeo:ClientId" "..."
dotnet user-secrets set "Asgardeo:ClientSecret" "..."
dotnet user-secrets set "AsgardeoMgmt:ClientId" "..."
dotnet user-secrets set "AsgardeoMgmt:ClientSecret" "..."
dotnet user-secrets set "AsgardeoRoles:RiderRoleId" "..."
dotnet user-secrets set "AsgardeoRoles:DriverRoleId" "..."
dotnet run
```

The API listens on `https://localhost:7136`. Sign in through
`https://localhost:7136/login`; the OpenAPI document is at `/openapi/v1.json`
in Development.

Database schema is managed with EF Core migrations:

```bash
dotnet ef database update --project src/GoRide.IdentityAuth.csproj
```

## Configuration keys

| Key | Purpose |
| --- | --- |
| `Asgardeo:BaseUrl` | Tenant base URL, `https://api.asgardeo.io/t/goride`. Every OIDC/SCIM2 endpoint is derived from it. |
| `Asgardeo:ClientId` / `ClientSecret` | The GoRide web application (authorization-code login). |
| `AsgardeoMgmt:ClientId` / `ClientSecret` | Machine-to-machine application used for SCIM2 management calls. |
| `AsgardeoRoles:RiderRoleId` / `DriverRoleId` | Role ids assigned during onboarding. |
| `Frontend:BaseUrl` | Frontend origin for CORS and post-login/logout redirects (default `http://localhost:3000`). |
| `TripService:BaseUrl` / `ApiKey` / `ActiveTripPath` | Trip service used to block deactivation while a trip is in progress. Leave `BaseUrl` empty until the trip service exists; the check is then skipped and logged. |
| `ConnectionStrings:DefaultConnection` | MySQL connection string. |

Environment variables use `__` as the separator (`Asgardeo__BaseUrl`), as in
`docker-compose.yml` and `src/infra/main.bicep`.

## Account deactivation (SCRUM-35)

`POST /api/account/deactivate` with body `{ "confirm": true }`, authenticated
with the session cookie.

| Status | Meaning |
| --- | --- |
| 200 | Account disabled in the Identity Server, local row soft-deleted, session cookie cleared. |
| 400 | `confirm` was not `true`. |
| 401 | No session. |
| 409 | The trip service reports a trip that is not in a terminal state. |
| 502 | The Identity Server refused the SCIM2 disable; nothing was changed. |
| 503 | The trip service is configured but could not answer; nothing was changed. |

What happens on success:

1. SCIM2 `PATCH /scim2/Users/{id}` sets `urn:scim:wso2:schema.accountDisabled`
   to `true` using the M2M app (scope `internal_user_mgt_update`).
2. `user_accounts.status` becomes `deactivated` with `deactivated_at` set; a
   `driverprofiles` row, if any, gets `status = deactivated`. Rows are never
   deleted so trip history keeps the opaque user id.
3. The cookie session is ended. Any other live session for that user is
   rejected on its next request, and a later login attempt is refused both by
   Asgardeo and by the API's own check.

Re-enabling an account is an Identity Server admin action (Console → Users →
account → Enable) plus flipping the `user_accounts` row back to `active`.

### Identity Server prerequisites

Both are captured by `scripts/fetch-asgardeo-config.sh` into `asgardeo-config/`
and should be committed whenever they change:

- **Account Disable** connector enabled (Console → Login & Registration →
  Account Management → Account Disable). Without it the flag is stored but
  logins are not refused. Snapshot: `asgardeo-config/account-disable-config.json`.
- The M2M application must be authorised for the SCIM2 Users API with the
  `internal_user_mgt_update` scope (in addition to the existing
  `internal_user_mgt_view` and `internal_role_mgt_users_update`). The token
  provider logs a warning when Asgardeo grants fewer scopes than requested.

```bash
CLIENT_ID=<m2m client id> CLIENT_SECRET=<m2m secret> ORG_NAME=goride \
  bash scripts/fetch-asgardeo-config.sh
```

## Tests

```bash
dotnet test
```

Unit tests live in `tests/GoRide.IdentityAuth.Tests` (xUnit, EF Core
in-memory provider, fake HTTP handlers for Asgardeo and the trip service).
CI runs them with coverage on every pull request into `dev` and `main`.
