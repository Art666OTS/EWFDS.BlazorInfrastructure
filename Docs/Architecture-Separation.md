# EWFDS.BlazorInfrastructure — Architecture Separation Review & Design

> Status: **Design proposal (review only — no code has been moved).**
> Decision recorded: keep a **single project**; separate by **folders + namespaces** only.
> Author aid: this document is the reference for a future incremental migration.

## 1. Purpose of this project

`EWFDS.BlazorInfrastructure` is a shared infrastructure library that supports other
applications built on **EWFDS** (the `EWFDSBL8` database/business library). Its code is
separated by **which consumer application needs it**, not by technical dependency:

- **Blazor** — only an interactive Blazor app needs it: Razor components, Telerik UI, JS
  interop, SignalR circuits, the Blazor auth-state provider, and Blazor-specific host wiring
  (even when that wiring is ASP.NET Core middleware).
- **API-only** — only a *pure* Web API app (never a Blazor app) needs it. **In the current
  codebase this bucket is effectively empty** (see §4.2).
- **Common** — **both** app types need it. This deliberately includes ASP.NET Core
  *server-host* code (middleware, endpoints, `HttpContext`), because a Blazor Server app is
  itself an ASP.NET Core host and uses the same pipeline a Web API host does.

> Important framing note: "uses `HttpContext` / middleware / endpoints" does **not** make code
> API-specific. Both a Blazor Server host and a Web API host are ASP.NET Core hosts. The only
> question that decides the bucket is: *would a Blazor app use this, a pure API app, or both?*

**Goal:** make the boundaries explicit so a new app can reason about (and eventually only pull
in) the parts of the infrastructure it needs.

## 2. Current state (what the review found)

### 2.1 The good news — composition already models the split
`Extensions/ServiceCollectionExtensions.cs` already exposes the intended layering:

| Method | Meaning |
| --- | --- |
| `AddEwfdsCoreInfrastructure(services, config)` | Services shared by **both** API and Blazor hosts. |
| `AddEwfdsBlazorUi(services)` | **Blazor-only** services (theming, user state, circuit handler, auth state provider). |
| `AddEwfdsBlazorInfrastructure(services, config)` | Convenience aggregate = Common + Blazor UI. |

Note there are only **two** consumer buckets here — shared ("Core") and Blazor-only. There is
no API-only registration group, which is the first strong signal that no genuinely API-only
code exists. The problem is purely **organizational**: the files themselves are grouped by
*technical concern* (Authorization, FileStorage, ...) rather than by *consumption boundary*
(Common / Blazor), so it is not visually obvious which files a non-Blazor app may safely
reference.

### 2.2 The friction points
- Blazor-only types live next to shared types inside `Services/Authorization`
  (e.g. `PersistingAuthenticationStateProvider`, `ComponentBaseWithAuth`,
  `LayoutComponentBaseWithAuth`, `UserAuthorised` sit beside the host-agnostic
  `ActivityTokenValidator`, `TokenBasedAuthService`, `UserInfo`).
- Shared server-host middleware/endpoints (`GlobalExceptionHandlerMiddleware`,
  `SharedAppEndpointsExtensions`, the `/health` + logging endpoints in
  `InfrastructureExtensions`) are not visually distinguishable from Blazor-only middleware
  (`BlazorCookieLoginMiddleware`) — even though the former is shared and the latter is
  Blazor-only. The deciding factor is the *consumer*, not the fact that both use `HttpContext`.
- `FileStorage` mixes settings and implementations, but both the Azure Blob and the
  HTTP-client `FileApiStorageService` are explicitly "generic / any application" — so both are
  Common.

## 3. Proposed folder & namespace layout

Keep one project (`EWFDS.BlazorInfrastructure`) but reorganize into three top-level areas.
Namespaces mirror folders so `using` statements advertise the boundary being crossed.

```
EWFDS.BlazorInfrastructure/
+-- Common/                         // namespace EWFDS.BlazorInfrastructure.Common.*
|   |                               //   used by BOTH API and Blazor hosts
|   +-- Configuration/              // IApplicationConfig
|   +-- Environment/                // AppEnvironment, IAppEnvironment
|   +-- Identity/                   // Application_User(+I), ApplicationUserIdentity(+I),
|   |                               //   Company(+I), IdentityRedirectManager, UserInfo(Claims)
|   +-- Authorization/              // ActivityTokenValidator, TokenBasedAuthService,
|   |                               //   UserAuthService, Application_User_Actions, IApplication_User_Actions,
|   |                               //   ILoadApplicationUser, LoadApplicationUser
|   +-- Authentication/             // LoginService, ILoginService (shared cookie sign-in)
|   +-- Email/                      // MailGunEmailService (+ IEmailService)
|   +-- FileStorage/                // AzureBlobStorageService, AzureBlobStorageSettings,
|   |                               //   IFileApiStorageService, FileApiStorageService, FileApiSettings
|   +-- FileSystem/                 // ImageService(+I), VirtualDirectoryService
|   +-- Security/                   // LoginRateLimiter
|   +-- ErrorHandling/              // GlobalErrorHandler (+ IGlobalErrorHandler),
|   |                               //   GlobalExceptionHandlerMiddleware  (server-host, shared)
|   +-- Hosting/                    // SharedAppEndpointsExtensions, /health + log endpoints,
|                                   //   Serilog / generic host startup helpers
|
+-- Api/                            // namespace EWFDS.BlazorInfrastructure.Api.*
|                                   //   API-only (no Blazor). Currently EMPTY — kept as a
|                                   //   placeholder folder so future API-only code has a home.
|
+-- Blazor/                         // namespace EWFDS.BlazorInfrastructure.Blazor.*
|                                   //   used ONLY by interactive Blazor apps
|   +-- Components/                  // all Shared/** razor + dd/* selects, Telerik helpers
|   +-- Theming/                     // ThemeService(+I), TelerikVersionProvider  (JS interop, Telerik CDN)
|   +-- State/                       // UserStateService (+I)  (circuit-scoped)
|   +-- Authorization/               // ComponentBaseWithAuth, LayoutComponentBaseWithAuth,
|   |                               //   PersistingAuthenticationStateProvider, UserAuthorised
|   +-- Authentication/              // BlazorCookieLoginMiddleware, BlazorCookieAuthExtensions
|                                   //   (server-host middleware, but Blazor-only by design)
|   +-- Circuits/                    // CircuitHandlerService
|   +-- wwwroot/js/                  // themeService.js
|
+-- Extensions/                     // Composition roots (kept, but split per area — see 4.4)
```

## 4. File-by-file classification

### 4.1 Common (shared by both API and Blazor hosts)
- `Services/Configuration/IApplicationConfig.cs`
- `Services/Environment/AppEnvironment.cs`
- `Services/Identity/*` (all)
- `Services/Authorization/ActivityTokenValidator.cs`, `TokenBasedAuthService.cs`,
  `UserAuthService.cs`, `Application_User_Actions.cs`, `IApplication_User_Actions.cs`,
  `ILoadApplicationUser.cs`, `LoadApplicationUser.cs`, `UserInfo.cs`, `UserInfoClaims.cs`
- `Services/Authentication/LoginService.cs`, `ILoginService.cs` (cookie sign-in via
  `HttpContext` — server-host, but usable by any ASP.NET Core host)
- `Services/Email/MailGunEmailService.cs`
- `Services/FileStorage/AzureBlobStorageService.cs`, `AzureBlobStorageSettings.cs`,
  `IFileApiStorageService.cs`, `FileApiStorageService.cs`, `FileApiSettings.cs`
  (both implementations are documented as "generic / any application")
- `Services/FileSystem/ImageService.cs`, `IImageService.cs`, `VirtualDirectoryService.cs`
- `Services/Security/LoginRateLimiter.cs`
- `Services/ErrorHandling/GlobalErrorHandler.cs`, `IGlobalErrorHandler.cs`
- `Services/ErrorHandling/GlobalExceptionHandlerMiddleware.cs` (server-host middleware; used by
  both host types)
- `Services/Startup/SharedAppEndpointsExtensions.cs` and the `/health` + log-status endpoints
  in `Services/Startup/InfrastructureExtensions.cs` (shared server-host endpoints)

> Key point: server-host code (`HttpContext`, `IMiddleware`, endpoint mapping) lives in Common
> whenever **both** app types use it. A Blazor Server app is an ASP.NET Core host too, so this
> is not "API-specific" code.

### 4.2 API-only (pure Web API, never Blazor)
**Currently empty.** A file-by-file review found nothing that only a pure Web API host would
use and a Blazor host would not:
- `FileApiStorageService` — documented "generic implementation that can be used by any
  application" → Common.
- `GlobalExceptionHandlerMiddleware`, `SharedAppEndpointsExtensions`, `/health` → used by both
  host types → Common.
- `LoginService` / `TokenBasedAuthService` → used by both → Common.
- All middleware/endpoints that *are* host-specific (`BlazorCookieLoginMiddleware`,
  `BlazorCookieAuthExtensions`) are documented as **Blazor-only** → Blazor.

This matches the existing composition root, which has no API-only registration group. The `Api`
folder/namespace is kept as an empty placeholder so future pure-API code has an obvious home
without another redesign.

### 4.3 Blazor
- `Components/**` — every `.razor` and `.razor.cs` (Telerik `dd/*` selects,
  `SelectComponentBase`, `GlobalErrorBoundary`, `StatusMessage`, `Models/AlertType`)
- `Services/Blazor/CircuitHandlerService.cs` (`CircuitHandler`)
- `Services/Theming/*` (`IJSRuntime`, Telerik CDN)
- `Services/State/UserStateService.cs`, `IUserStateService.cs` (circuit-scoped)
- `Services/Authorization/ComponentBaseWithAuth.cs`, `LayoutComponentBaseWithAuth.cs`,
  `PersistingAuthenticationStateProvider.cs`, `UserAuthorised.cs`
  (`ComponentBase`, `AuthenticationStateProvider`, `RenderMode`)
- `Services/Authentication/BlazorCookieLoginMiddleware.cs`,
  `Extensions/BlazorCookieAuthExtensions.cs`

### 4.4 Composition roots (keep, but re-home)
- `AddEwfdsCoreInfrastructure` -> registers **Common** types (shared by both host types).
- `AddEwfdsBlazorUi` -> registers only **Blazor** types.
- Split `ServiceCollectionExtensions.cs` into partial files per area
  (`ServiceCollectionExtensions.Common.cs`, `.Blazor.cs`) so each area owns its
  registrations while keeping the same public API surface. Add a `.Api.cs` only if/when
  genuinely API-only code appears.

## 5. Conventions to enforce the boundaries

1. **Namespace = folder = boundary.** `...Common.*`, `...Api.*`, `...Blazor.*`.
2. **Dependency direction:** `Blazor -> Common` and `Api -> Common`. Common must never `using`
   a `Blazor.*` or `Api.*` namespace. `Blazor` and `Api` do not reference each other.
3. **No Telerik / `Microsoft.AspNetCore.Components*` / `IJSRuntime` / circuits outside
   `Blazor`.** This — not `HttpContext` — is the real test for Blazor-only code.
4. **Server-host code (`HttpContext`, `IMiddleware`, endpoints) is allowed in `Common`** when
   both host types use it; it only belongs in `Blazor`/`Api` when a single host type needs it
   (e.g. `BlazorCookieLoginMiddleware`).
5. Registration extension methods live in the same area as the services they register.

## 6. Suggested migration order (when you choose to execute)

1. Create the `Common` and `Blazor` top-level folders (and an empty `Api` placeholder). Move
   **leaf** Common files first (Email, Security, FileSystem, Environment, Configuration) —
   lowest coupling, lowest risk.
2. Move Identity + Common Authorization/Authentication types; fix namespaces.
3. Move the shared server-host code into `Common/ErrorHandling` and `Common/Hosting`
   (`GlobalExceptionHandlerMiddleware`, endpoints, `FileApiStorageService`).
4. Move Blazor area last (components, theming, state, circuit handler, auth state provider,
   `BlazorCookieLoginMiddleware`, `BlazorCookieAuthExtensions`).
5. Split the composition roots into per-area partials; keep the public
   `AddEwfds*` method names unchanged so consuming apps do not break.
6. Add an architecture test / analyzer (optional) to assert the dependency direction in rule 2.

## 7. Future option (not chosen now)

The only way to *physically* prevent a new app from referencing Blazor code is to split into
separate assemblies. Given the findings above, that realistically means **two** assemblies
(`...Common` and `...Blazor`) — an `...Api` assembly is not warranted until genuinely API-only
code exists. Because the folder/namespace layout above already mirrors those boundaries, a
later project split becomes a low-friction "lift each folder into its own csproj" exercise
rather than a redesign. This document is structured so that path stays open.
