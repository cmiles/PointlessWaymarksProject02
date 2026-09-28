# PointlessWaymarks.SiteViewerMaui

A self-contained **.NET MAUI (Android)** app that is the mobile equivalent of the desktop
`PointlessWaymarks.SiteViewerGui` **CloudViewer**: it stores S3 / S3-compatible bucket connection
details securely on the device and renders the bucket's published site as web pages inside the app.

This project intentionally **does not reference** any other solution projects. The small amount of
S3 contract/logic it needs is copied in (see `S3/`) so the app is fully independent.

## Requirements / Prerequisites

- .NET SDK 10 with the MAUI **android** workload installed (`dotnet workload install android` /
  `maui-android`).
- An Android emulator or a device with USB debugging for a sideloaded build.
- The project ships its own `Directory.Build.props` so it is **not** affected by the repository-root
  props (which pin a Windows-only target for the desktop projects).

## Build

```
dotnet build .\PointlessWaymarks.SiteViewerMaui\PointlessWaymarks.SiteViewerMaui.csproj -c Debug
```

## Running in JetBrains Rider

Rider can build, deploy and debug this project on an Android emulator or device.

1. **Prerequisites (one time):**
   - Install the .NET **android** workload (`dotnet workload install android`).
   - .NET MAUI / Android support is **built into Rider** — there is no separate ".NET MAUI"
     plugin to install from the Marketplace or to enable under **Settings > Plugins**. (If you want
     to double-check what ships with your build, the relevant bundled plugins are listed under
     **Settings > Plugins > Installed** — e.g. *.NET MAUI* / *Android* — and are enabled by default;
     you do not add them from the Marketplace.)
   - In Rider, open **Settings > Build, Execution, Deployment > MAUI** and make sure the Android
     SDK / JDK are detected (Rider can install a missing SDK from here).
2. **Open the solution:** open `PointlessWaymarks.slnx`. Because this app is `net10.0-android` and
   builds as `AnyCPU`, the solution now maps the solution's `x64` platform to the project's `AnyCPU`
   platform so Rider resolves a valid `Debug` build for it (the other, Windows, projects stay on
   `x64`).
3. **Pick the run configuration:** once the solution finishes loading, Rider automatically creates a
   run configuration named **PointlessWaymarks.SiteViewerMaui** for the app. Select it in the
   configurations drop-down. A device selector appears next to it — choose a running emulator or a
   connected device (use **Tools > Android > AVD Manager** to create/start an emulator if needed).
4. **Run / Debug:** press **Run** (or **Debug**). Rider builds the app, deploys the APK and launches
   it on the selected device.

> If the app does not appear in the run-configuration drop-down, use **Edit Configurations… > Add
> New Configuration** and check for a MAUI/Android entry, or reload the solution after the android
> workload/SDK is installed — Rider only offers Android deployment once it detects the workload.

You can also build/deploy from Rider's terminal exactly as documented in the smoke test below
(`dotnet build -t:Run -f net10.0-android ...`).

## How it works

- **Secrets** (access key, secret and, for non-Amazon providers, the service URL) are stored only in
  MAUI `SecureStorage` (Android KeyStore-backed), keyed by each profile's `Guid`
  (`Storage/SecureCredentialStore.cs`). Non-secret profile metadata is persisted as JSON in the app
  data directory (`Storage/ProfileRepository.cs`).
- **Providers**: Amazon (service URL derived from the region) and S3-compatible providers
  (Cloudflare / Wasabi, custom service URL), mirroring the desktop provider model.
- **Rendering**: the viewer uses a `WebView` whose Android `WebViewClient`
  (`Platforms/Android/S3WebViewClient.cs`) intercepts every request to the in-app virtual host
  `pw.local` and returns bytes fetched directly from S3 (`Services/S3ContentService.cs`). There is
  **no localhost server** and the only permission requested is `INTERNET`.

  > Note: the request-interception is implemented in the Android handler layer (a custom
  > `WebViewClient`) behind a cross-platform "virtual host" abstraction. `HybridWebView` was not used
  > for the final render surface because it exposes neither navigation events nor dynamic remote byte
  > serving, both of which the CloudViewer navigation rules require.

- **Navigation rules** reproduce the desktop `SitePreviewControl`/`SitePreviewContext`
  (`Services/ViewerNavigation.cs`): in-site links are rewritten to stay on the virtual host, external
  links are opened in the system browser, and the toolbar provides Back / Forward / Home / Refresh
  plus the current address.

## Automated tests

The pure, deterministic logic is covered by `PointlessWaymarks.SiteViewerMauiTests` (link-compiled
from this project because a test project cannot reference the `net10.0-android` app):

```
dotnet test .\PointlessWaymarks.SiteViewerMauiTests\PointlessWaymarks.SiteViewerMauiTests.csproj
```

Covered: profile → `IS3AccountInformation` mapping, provider/service-URL selection, connection
validation, path → S3 key mapping, domain rewriting, content-type resolution, and the navigation
decisions.

## Manual emulator smoke test (end-to-end S3 rendering)

The WebView interception and real S3 fetch cannot be exercised headlessly, so verify them manually:

1. Launch an Android emulator (API 24+), then deploy:
   `dotnet build -t:Run -f net10.0-android .\PointlessWaymarks.SiteViewerMaui\PointlessWaymarks.SiteViewerMaui.csproj`.
2. On the **Connections** screen tap **Add** and enter a real connection:
   - **Amazon**: set Region (e.g. `us-east-1`), Bucket, Site Domain, Access Key and Secret.
   - **Cloudflare/Wasabi**: set the Service URL, Bucket, Site Domain, Access Key and Secret.
3. Save. Confirm the profile appears in the list and that re-opening the editor shows the stored
   secrets (proving `SecureStorage` round-trips).
4. Tap the profile to open the viewer. Confirm the site's `index.html` renders and that CSS/JS/images
   load (content-type + domain rewriting working).
5. Tap an in-site link and confirm it stays inside the app; tap an external link and confirm it opens
   in the system browser. Exercise Back / Forward / Home / Refresh and watch the address update.
6. Delete the profile and confirm it disappears (its secrets are also removed from `SecureStorage`).
