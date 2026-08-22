# Government Worktime

SvelteKit + Tailwind CSS + daisyUI management interface for the school-government employee timekeeping system.

## Included routes

- `/login` — PocketBase `users` email/password login
- `/work` — search employees and clock them in or out
- `/sessions` — current active sessions with live durations
- `/employees` — create, edit, and delete employees
- `/logs` — paginated audit log with CSV export

## Configure

Edit `src/lib/connections.ts`:

```ts
export const POCKETBASE_URL = 'https://pocketbase.example.com';
export const SERVER_URL = 'https://api.example.com';
```

The worktime API requests are:

```http
POST /api/worktime/start-session
Authorisation: <PocketBase auth token>
Content-Type: application/json

{ "employee": "employee record id" }
```

and:

```http
POST /api/worktime/end-session
Authorisation: <PocketBase auth token>
Content-Type: application/json

{ "employee": "employee record id" }
```

`Authorisation` intentionally uses the spelling requested for your server. If your backend uses the standard HTTP header spelling, change it to `Authorization` in `src/lib/worktime.ts`.

## Install and run

```bash
npm install
npm run dev
```

For a production check:

```bash
npm run check
npm run build
```

## Notes about the supplied schema

- The employee collection requires `name`, `account`, `ministry`, and `salary`, so all four appear in the form.
- PocketBase rules automatically restrict ministry records and employee writes according to the logged-in user's `ministryAccess`.
- The UI is client-rendered (`ssr = false`) so PocketBase's browser auth store can be used consistently across routed pages.
