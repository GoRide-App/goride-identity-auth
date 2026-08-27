# GoRide Identity & Auth — QA test pack

Postman collection covering every SCRUM ticket currently in flight, one folder
per ticket.

| File | Purpose |
| --- | --- |
| `GoRide-IdentityAuth.postman_collection.json` | 29 requests, 67 assertions |
| `GoRide-Local.postman_environment.json` | Variables for a local run |

## Setup

1. **Import both files** into Postman (File → Import).
2. **Turn off SSL verification** — Settings → General → *SSL certificate
   verification* → off. The API runs on the .NET dev certificate.
3. Select the **GoRide - Local** environment (top-right dropdown).
4. Start the API on the branch you are testing:

   ```bash
   git checkout SCRUM-33-Update-personal-profile-details-via-SCIM2
   cd src
   dotnet run
   ```

5. Run the **00 - Preflight** folder. If it fails, the server configuration is
   wrong and no other result is meaningful.

## Getting a session

The API authenticates with an **HttpOnly session cookie**, not a bearer token,
so Postman cannot perform the login itself. Do it once in a browser:

1. Open <https://localhost:7136/login> in Chrome and sign in.
2. `F12` → **Application** → **Cookies** → `https://localhost:7136`.
3. Copy the **value** of the `app_session` cookie.
4. Paste it into the `sessionCookie` environment variable.

The cookie is valid for 8 hours. When requests start returning 401, repeat.

## What each folder covers

| Folder | Covers | Notes |
| --- | --- | --- |
| `00 - Preflight` | Server is up, config is bound | Run first |
| `SCRUM-28` | Tenant provisioned, app registered, auth enforced | No session needed |
| `SCRUM-30` | OIDC login, claims, role selection, CORS | Sign in as a **Rider**, not an Admin |
| `SCRUM-31` | Forgot-password OTP | **Not implemented** — expected to fail |
| `SCRUM-33` | SCIM2 profile read/update | Runs in order, restores original values |
| `SCRUM-38` | Refresh-token rotation | Needs a 120s access-token lifetime |

`SCRUM-33` is a superset of `SCRUM-30` and `SCRUM-38`, so running the whole
collection against that branch exercises all three at once.

### SCRUM-31 will fail — that is the result

The `SCRUM-31` branch is byte-identical to `SCRUM-30`; no recovery code was
written. Those requests document the expected contract and return 404 today.
Report it as *feature not delivered*, not as a broken test. Confirm the final
route names with the developer before sign-off.

### SCRUM-38 needs a short token lifetime

In the Asgardeo console, set the application's access token lifetime to **120
seconds**. Then run *Baseline*, wait out the lifetime, and run the rest. The
rotated cookie is captured back into `sessionCookie` automatically.

## Values to request from the developer

None of these are needed by CI — they are only for running the API locally.
They are secrets: get them over a private channel, never in a ticket or chat
thread, and never commit them.

| Setting | Where it comes from |
| --- | --- |
| `Asgardeo:ClientId` | Asgardeo → the GoRide web application |
| `Asgardeo:ClientSecret` | Same application |
| `AsgardeoMgmt:ClientId` | The **management** (M2M) application |
| `AsgardeoMgmt:ClientSecret` | Same management application |
| `AsgardeoRoles:RiderRoleId` | Asgardeo → Roles → Rider → UUID |
| `AsgardeoRoles:DriverRoleId` | Asgardeo → Roles → Driver → UUID |

Plus a **test account** (email + password) that is *not* an Admin, and
confirmation that the console has:

- Redirect URI `https://localhost:7136/signin-oidc`
- Allowed origin `http://localhost:3000`
- Scopes `openid`, `email`, `profile`, `roles`, `offline_access`, `internal_login`
- Management app scope `internal_role_mgt_users_update`

Load them with user-secrets so they never touch the repo:

```bash
cd src
dotnet user-secrets set "Asgardeo:ClientId"          "<value>"
dotnet user-secrets set "Asgardeo:ClientSecret"      "<value>"
dotnet user-secrets set "AsgardeoMgmt:ClientId"      "<value>"
dotnet user-secrets set "AsgardeoMgmt:ClientSecret"  "<value>"
dotnet user-secrets set "AsgardeoRoles:RiderRoleId"  "<value>"
dotnet user-secrets set "AsgardeoRoles:DriverRoleId" "<value>"
```

## Running from the command line

```bash
npm install -g newman
newman run GoRide-IdentityAuth.postman_collection.json \
  -e GoRide-Local.postman_environment.json \
  --env-var "sessionCookie=<paste cookie value>" \
  --insecure
```

## Defects the collection is written to catch

- `POST /api/onboarding/select-role` lets any signed-in user assign themselves
  the **Driver** role. Drivers should be vetted.
- `PATCH /api/profile` with an all-null body builds an empty SCIM `Operations`
  array; that should be a 400, not a 500.
- If the `roles` claim never arrives, `[Authorize(Roles=...)]` silently never
  matches. The *Claims include the roles claim* test is the canary.
- `/api/onboarding/debug-claims` dumps every claim and must be removed before
  production.
