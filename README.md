GoRide

GoRide

## Local Development Setup (Frontend + Backend, Azure DB)

This walks a new developer through running `goride-identity-auth` (this repo) and
`goride-frontend` locally, both pointed at the **shared Azure MySQL database**
(not a local Docker database). Do this once per machine.

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js](https://nodejs.org/) 20+ and npm
- Both repos cloned as sibling folders, e.g.:
  ```
  System/
    goride-identity-auth/
    goride-frontend/
  ```
- The real Azure DB password and Asgardeo (WSO2) OIDC values for the `dev`
  environment — get these from a teammate or your password manager. **Never**
  commit real values for these into `appsettings.json`, `.env` files, or any
  other tracked file — they only ever go into `dotnet user-secrets` (backend)
  or an untracked `.env.local` (frontend).

### 1. Configure the backend's database connection

The backend reads its connection string via .NET's [user-secrets](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets)
store, which lives outside the repo. From the `src` folder:

```bash
cd goride-identity-auth/src
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=goride-db.mysql.database.azure.com;Port=3306;Database=identity_db;User=identity_svc;Password=<ask-a-teammate-for-the-password>;SslMode=Required;"
```

> Note: this is the **shared Azure server**, not the local MySQL container in
> `docker-compose.yml`. You do not need to run `docker compose up` for this
> workflow — the app talks to Azure directly.

### 2. Configure Asgardeo (WSO2) OIDC secrets

Still from `goride-identity-auth/src`, set each of the following (ask a
teammate / check the Asgardeo console for the real values):

```bash
dotnet user-secrets set "Asgardeo:ClientId" "<value>"
dotnet user-secrets set "Asgardeo:ClientSecret" "<value>"
dotnet user-secrets set "AsgardeoMgmt:ClientId" "<value>"
dotnet user-secrets set "AsgardeoMgmt:ClientSecret" "<value>"
dotnet user-secrets set "AsgardeoRoles:RiderRoleId" "<value>"
dotnet user-secrets set "AsgardeoRoles:DriverRoleId" "<value>"
```

Verify everything was saved (this only prints key names next to values, kept
on your machine — still avoid pasting the output anywhere shared):

```bash
dotnet user-secrets list
```

You should see 7 entries: the 6 `Asgardeo*` keys above plus
`ConnectionStrings:DefaultConnection`.

### 3. Trust the local HTTPS dev certificate

The backend runs on `https://localhost:7136` with a self-signed dev
certificate. The frontend's browser calls will fail TLS validation until this
certificate is trusted once per machine:

```bash
dotnet dev-certs https --trust
```

This opens a Windows confirmation dialog — accept it. (Changing ports does
**not** avoid this step; an untrusted cert is rejected on any port, and the
backend's session cookie requires HTTPS regardless.)

### 4. Configure the frontend environment

In `goride-frontend/.env.local` (create it if it doesn't exist — it's
gitignored):

```
NEXT_PUBLIC_API_URL=https://localhost:7136
NEXT_PUBLIC_APP_URL=http://localhost:3000
```

Then install dependencies once:

```bash
cd goride-frontend
npm install
```

### 5. Start the backend

```bash
cd goride-identity-auth/src
dotnet run --launch-profile https
```

Leave this terminal running. It listens on `https://localhost:7136`.

### 6. Start the frontend

In a separate terminal:

```bash
cd goride-frontend
npm run dev
```

Leave this terminal running. It listens on `http://localhost:3000`.

### 7. Health check — verify both are working

**Backend** (in a third terminal, or your browser):

```bash
curl -sk -o /dev/null -w "%{http_code}\n" https://localhost:7136/api/me
# expect: 401  (server is up; no session cookie yet — this is correct)

curl -sk -o /dev/null -w "%{http_code}\n" https://localhost:7136/login
# expect: 302  (redirects to Asgardeo's /oauth2/authorize — confirms OIDC secrets loaded)
```

If `/api/me` or `/login` instead return a 500 or the process crashes on
startup, re-check the connection string / Asgardeo secrets from steps 1–2.

**Frontend**: open [http://localhost:3000](http://localhost:3000) in a
browser — the app should load. Click through to log in; you should be
redirected to Asgardeo and back to the app afterward. If the browser shows a
certificate warning when the frontend calls the API, redo step 3.

### 8. Stop the backend and frontend

In each terminal, press `Ctrl+C` and confirm if prompted.

If a server was started in the background and you don't have its terminal
(e.g. it's still bound to the port from a previous session), find and stop it
by port instead:

```bash
# Windows (PowerShell or Git Bash)
netstat -ano | findstr :7136   # backend — note the PID in the last column
netstat -ano | findstr :3000   # frontend — note the PID in the last column
taskkill /PID <pid> /F
```