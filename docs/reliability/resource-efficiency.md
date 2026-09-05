# Resource efficiency — 2026-09-06

Display-only work now follows demand: the process list samples only while its
panel is visible, widgets receive only the fields they render, and a busy UI
keeps one pending metrics callback containing the latest sample. Hardware,
thermal protection and power automation keep their existing sampling cadence.

Clock widgets wake at minute boundaries; calendars refresh at local midnight.
Power widgets avoid overlapping reads and poll less frequently in gaming and
critical profiles. Closing widgets releases their WebViews and subscriptions;
recreating the main bridge detaches its old subscriptions.

Taskbar minimize suspends the renderer while preserving the document. Tray
parking retains the existing delayed page teardown. Restoring an already hidden
window cannot resume it, and a pending suspend cannot leave a restored window
asleep. Live widgets prevent the parked-process working-set trim.
[WebView2 requires the controller to be invisible before suspension](https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.core.corewebview2.trysuspendasync).

Decorative title, navigation and status animations now follow the existing
`data-fx="rich"` opt-in and reduced-motion settings. Loading indicators remain
independent.

## Local UI experiment

Baseline: `502c4bb`. A temporary WPF/WebView2 harness loaded each frontend in a
fresh isolated browser profile, supplied the same simulated telemetry and RPC
responses, and reproduced the old/new widget delivery cadence. WebView2 runtime
was `152.0.4191.62`; the machine had 16 logical processors. This isolates UI work;
it does **not** measure the complete app's hardware service or automation, real
game FPS, battery drain, or performance on other hardware.

Each phase ran for about 15 seconds after startup; overview included counter
initialization (~20 seconds total), and hidden ran for 10 seconds. One run per
variant is indicative, not a statistical guarantee. CPU is the combined harness
and WebView process time normalized over all 16 processors. Private memory is
committed process memory, not physical RAM; shared working sets were not used to
claim memory savings. GPU is the sampled busiest engine summed over those
processes, not whole-system GPU use.

| Scenario | CPU before → after | GPU engine before → after | Private MiB before → after |
|---|---:|---:|---:|
| Overview | 0.95% → 0.36% | 0.52% → 0.03% | 183.5 → 183.2 |
| Hardware monitoring | 0.68% → 0.28% | 0.74% → 0.08% | 184.0 → 182.3 |
| Six widgets, full profile | 1.71% → 1.22% | 1.21% → 0.70% | 214.7 → 212.3 |
| Six widgets, gaming profile | 1.30% → 1.07% | 1.56% → 0.49% | 215.6 → 212.0 |
| Six widgets, critical profile | 1.14% → 0.13% | 0.88% → 0.03% | 216.0 → 211.2 |
| Hidden, widgets closed | 0.15% → 0.12% | 0% → 0% | 201.7 → 198.9 |

The main gains are CPU and repaint reduction; the small memory differences are
within short-run variability. A separate hardware-rendering trial, before the
decorative-animation fix, added roughly 100–125 MiB of private memory and used
more GPU with mixed CPU results. The existing renderer flags were retained.

Navigating away from the process panel produced zero process RPCs during the
6.5-second observation (baseline: one). Native WebView checks also passed for
minimize/suspend, retained document state, 20 rapid suspend/restore cycles, and
reload after `about:blank`, with no reported script or renderer errors. These
API checks used the harness, not a full installed-app lifecycle test.

## Regression checks

- Release solution build: zero warnings/errors; 199 .NET tests passed.
- JavaScript: 45 tests passed, plus syntax checks for all application scripts.
- Tests cover concurrent and failed process sampling, PID reuse, bounded UI
  delivery, scan reentrancy, widget payloads, clock/calendar timing, visibility,
  polling overlap, and repeated language changes.
- The uninstall-event test now uses a unique test event, so running the suite
  cannot signal an installed VoltManager instance to exit.

Run `dotnet build VoltManager.sln -c Release`,
`dotnet test tests/VoltManager.Tests/VoltManager.Tests.csproj -c Release`,
`node --test tests/*.test.mjs`, and `./build.ps1 -SkipInstaller` on Windows.
