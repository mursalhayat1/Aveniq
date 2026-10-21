# Full Rust Application Build Prompt — Aveniq / Aveniq

> Copy and paste this prompt into the coding agent that will implement the Rust application. It describes the
> documented Aveniq/Aveniq product and its expected feature set. It is a build specification, not a claim that
> every feature below has already been validated in Rust or against every cloud provider/game.

## 1. Project Overview

Build a complete, production-quality Rust application that provides cloud-backed virtual storage for Windows and
intelligent caching/prefetching for locally executed PC games.

**Application description:** Aveniq (also referred to as Aveniq in the existing source and package names) mounts
cloud storage as a Windows drive letter. A game runs on the user's own PC; the application does not stream rendered
video or execute games in the cloud. File data is read through a virtual filesystem and cached sparsely on local
NTFS storage. The application learns file/block access patterns from real reads and can prepare likely startup data
before launch.

**Canonical product name:** `[FILL: confirm whether the user-facing name is Aveniq or Aveniq]`.
Preserve the existing Aveniq naming for technical identifiers, paths, and compatibility where required unless
the product owner explicitly approves a migration.

**Primary platform:** Windows 10 version 1809 or later and Windows 11, x64. Require an NTFS cache volume. The
application should run as the current user without routinely requiring administrator elevation. WinFsp is an
external prerequisite for virtual drive mounting. `[FILL: confirm whether additional operating systems are in
scope; do not claim cross-platform support without implementing and testing it.]`

**Product principles and non-goals**

- Execute the game on the user's local CPU/GPU; do not describe this as cloud gaming.
- Keep game reads (foreground demand) ahead of all speculative work.
- Report unavailable or unknown metrics as unavailable; never fabricate throughput, cache hits, readiness, or
  compatibility.
- Never delete cloud data or game save files as cache maintenance.
- Never bypass DRM, ownership checks, launchers, anti-cheat, or game security.
- Do not inject into games or modify protected executables. Any game-aware integration must use an authorized,
  documented plugin/modding/telemetry interface.
- Preserve user data, fail explicitly, and prefer safe, recoverable behavior over silently claiming success.

**Source-of-truth inputs:** Use this specification and any project documentation supplied by the user. Where two
sources conflict, state the conflict and use `[FILL: decision required]`; do not invent product behavior.

## 2. Feature List

Implement the following features as one coherent application. Keep the capability and validation distinctions
explicit: some provider integrations and commercial-game compatibility are documented as experimental or untested.

### Application, interface, and command line

- **Desktop application:** Provide a Windows desktop UI with Dashboard, Cloud Drives, Game Library, Accelerator,
  Smart Cache, Performance, Settings, and Diagnostics surfaces, plus dialogs for first run, connecting providers,
  adding drives/games, game settings, preparation, and Full Local approval/progress.
- **UI architecture:** Keep UI state separate from service/domain logic. Use asynchronous operations for network,
  disk, database, and process operations. Show progress, cancellation where safe, actionable errors, and honest
  unavailable states.
- **CLI:** Provide a `aveniq`-style command-line executable for supported maintenance, diagnostics, dependency/
  provider inspection, simulation/benchmark workflows, and automation. Define exact subcommands and their effects
  before implementation: `[FILL: final CLI command inventory and compatibility requirements]`.
- **First run and settings:** Collect cache root, cache budget, and expected network speed (Mbps); persist settings
  atomically and recover safely from malformed settings without silently discarding the original file.
- **Single-instance and ownership:** Prevent simultaneous processes from claiming the same cache/mount state.

### Cloud providers and virtual drives

- **rclone integration:** Discover the bundled or configured rclone executable; verify version/checksum where
  applicable; detect supported flags and capabilities; launch as a child process with safe argument construction,
  redirected/drained output, health checks, and explicit lifecycle state.
- **Google Drive:** Connect through browser-based OAuth using the user's own client credentials, with optional
  read-only scope and root-folder restriction. Keep OAuth and token handling local and secure. Never ask for or
  handle the user's Google password. The end-to-end provider flow requires credentials/accounts for validation.
- **TeraBox (experimental and optional):** Isolate its separately pinned community rclone backend and credential
  configuration from Google Drive. Treat session cookies as secrets. Clearly label the integration experimental;
  do not imply it is safe, stable, or end-to-end verified. A successful metadata check alone must not be reported
  as verified file streaming.
- **Other rclone remotes:** Do not claim Aveniq-specific support for OneDrive, Dropbox, S3, NAS, or other backends
  until each has explicit configuration guidance and validation. Allow rclone-compatible remotes only with
  accurately stated support levels.
- **Mount management:** Mount a selected remote folder at an available drive letter; show status and mount details;
  support unmount/remount, restart adoption, process crash recovery with bounded exponential backoff, and
  post-sleep health checks.
- **Virtual filesystem:** Use rclone VFS and WinFsp rather than inventing a second filesystem/cache layer. Support
  ordinary listing, Unicode/spaced filenames, whole-file/range/concurrent reads, and tested write-back behavior
  when the selected mount is writable. Surface known semantic differences from NTFS.
- **rclone remote control:** Bind the RC API to loopback only and use randomly generated per-session credentials.
  Never expose unauthenticated RC on a non-loopback interface.

### Cache and disk safety

- **Budget controls:** Support 10/20/30/40 GB presets and a custom size; account for multiple mounted drives,
  reserved free space for Windows, and a temporary overrun margin.
- **Cache inspection:** Report logical range/index usage separately from physical sparse-file allocation; use
  rclone's cache metadata/index as the source of truth for cached ranges.
- **Eviction and cleanup:** Respect rclone's eviction semantics and open-file constraints. Pause Aveniq prefetch
  under disk pressure. Only clear cache content inside the configured root and only when its ownership marker
  validates. Never remove cloud files, save games, or unrelated local data.
- **Pinning:** Provide best-effort file pinning, clearly communicating that rclone eviction and an undersized
  budget can still evict pinned content.
- **Cache reuse and remote changes:** Reuse valid cache data across restarts/remounts; detect stale remote content
  using backend/rclone mechanisms and never serve known-stale bytes as valid.
- **Disk safety:** Monitor free space, warn when the reserve is threatened, limit prefetch to safe capacity, and
  expose the reason when work is paused or refused.

### Game library, compatibility, and launch

- **Library:** Store the game library and profiles in SQLite. Add/remove game entries without deleting the user's
  cloud content. Support installed-game folder selection and per-game settings.
- **Executable discovery:** Find executable candidates, inspect PE headers/imports, identify architecture, and
  compare imported DLLs with game-local and Windows system DLLs. Report missing dependencies and suggestions
  without silently installing them.
- **Compatibility analysis:** Identify indicators for launchers, DRM/ownership dependencies, anti-cheat, installer
  packages, likely engines, file counts, and long paths. Explain the evidence and label the result as a heuristic.
  Kernel anti-cheat indicators must recommend local installation; never attempt to disable or evade them.
- **Three launch modes:**
  1. **Cloud Direct:** launch the selected executable from the mounted drive.
  2. **Hybrid Optimized:** construct a local mirrored game tree; copy executables, DLLs, configuration and small
     essential files (documented threshold: 2 MiB), while making symbolic links for large assets. Preserve
     user-modified local copies; communicate Developer Mode/admin requirements and the fact that writes through
     symlinked files still target the cloud.
  3. **Full Local:** after explicit user approval, perform a resumable/checksummed `rclone copy` to local storage.
     Show destination, required capacity, progress, and errors. Never describe it as complete before verification.
- **Launch monitoring:** Track the launched process tree using Windows job-object semantics without kill-on-close;
  collect exit status/NTSTATUS interpretation and relevant Windows application events; write sanitized launch
  diagnostics. Mark a mode/game Verified only after a monitored run has lasted at least 60 seconds without a
  known failure signature.
- **Compatibility truthfulness:** No commercial title is supported merely because a heuristic scan succeeds.
  The documented DRM-free Aveniq demo is the test application. Label other games untested until actually run and
  measured.

### Streaming, telemetry, and prediction

- **Read telemetry:** Parse rclone verbose logs into byte-exact access events (file-relative offset, length,
  cache hit/miss, wait duration, and errors). Handle partial lines, rotation, EOF, malformed records, and
  out-of-order/missing pairs robustly. Logging overhead must be communicated and profiling must be configurable.
- **Prefetch attribution:** Exclude Aveniq's own reads using registered range/time windows. Document the
  unavoidable attribution ambiguity if a game reads the same range during a prefetch window.
- **Profiles:** Persist per-game access history, sessions, prediction statistics, and profile metadata in SQLite.
  Make schema migrations explicit and resilient.
- **Predictors:** Implement explainable, deterministic startup, Markov/block-transition, and sequential-read
  predictors. The documented learning unit is a 1 MiB block relative to a game folder. Track useful versus wasted
  prefetch per predictor; after at least 64 samples, automatically disable predictors below 25% measured accuracy,
  persisting that state per game.
- **Priority queue:** Implement P0 game demand protection and P1/P2/P3 prefetch priorities; deduplicate and upgrade
  queued work, support cancellation by owner/tag, speculative TTL, per-session byte budget, and a dropped-work
  notification for every item that exits unfetched.
- **Scheduling/network governor:** Measure actual throughput and latency; choose concurrency using the measured
  bandwidth-delay product. While the game waits on the network, permit only P1 with one stream. Resume P2/P3 only
  after 750 ms without a foreground wait and limit them to the configured background share. Pause all prefetch
  under disk pressure. Do not claim prediction increases total link capacity.
- **Executor:** Fetch predicted ranges by reading through the mounted drive so rclone's own cache is populated.
- **Optimize & Prepare:** Scan the game, check budget/reserve, measure actual uncached throughput, load profile and
  compatibility data, identify essentials, run the selected safe preparation workflow, prefetch the startup set
  within budget, and verify completed coverage using rclone's on-disk range index after the applicable handle
  caching delay. Report Ready only when verification proves it. If there is no profile, report that startup data
  is unknown. Support cancellation without corrupting mount/cache state.
- **Metrics and monitor:** Display real throughput, cache hit/miss, read waits/stalls, prefetch accuracy, errors,
  retries, pending uploads, cache sizes, and operation state. Mark values unavailable when no real source exists
  (rclone does not distinguish upload/download speed in its aggregate stats).
- **Simulation and benchmarks:** Include a deterministic local throttled HTTP SimCloud test backend with
  bandwidth, latency, offline, HTTP fault, and dropped-connection controls. Keep simulation results separate from
  real-provider/game claims. Benchmark cold, prepared, and warm behavior with reproducible inputs.

### Optional Game-Aware Mode

- Only enable per-game when a supported/authorized plugin integration is configured and the game is launched by
  the application. No game plugin is assumed to exist.
- On Windows, receive newline-delimited UTF-8 JSON over a current-user-only named pipe named
  `\\.\pipe\aveniq-telemetry-<gameId>`, one client at a time, reconnectable, maximum 4096 bytes per line.
- Accept documented message kinds: `position` (game-supplied coordinates and region), `region`,
  `fasttravel`, and `loading`. Ignore unknown fields, count/drop malformed lines, and never infer region identity
  from coordinates by itself.
- Learn a block-to-region association only after at least two separate visits and at least 70% of the block's
  reads occurred in that region. Learn region transitions; schedule current-region blocks at P1 and likely-next
  regions at P2 only for observed probability >= 0.3. Cancel stale region work on movement and prioritize the
  destination after fast travel.
- Apply the universal P0, disk, bandwidth, and accuracy protections. Use only documented game interfaces; no
  injection, executable patching, anti-cheat bypass, or DRM circumvention.
- Label the mode experimental until a real authorized integration has been independently tested.

### Diagnostics, security, and release

- **Diagnostics:** Structured/file/in-memory logs with redaction on every path; produce a sanitized diagnostic
  archive excluding rclone config, tokens, client secrets, cookies, passwords, and private user data.
- **Secret storage:** Encrypt rclone config where configured. On Windows use current-user DPAPI protection for
  encryption keys and persisted RC credentials; pass secrets through protected environment variables or local
  HTTP request bodies, never command-line arguments or logs. Fail closed if protection/decryption fails.
- **Dependency checks:** Detect rclone and WinFsp, report versions/capabilities, and provide clear installation
  guidance. Do not silently install system components or elevate privileges.
- **Packaging:** Produce a self-contained per-user Windows release, install/uninstall scripts and shortcuts;
  retain settings/library/cache by default during uninstall and offer a clearly scoped user-controlled cleanup.
  Optional signed installer creation is `[FILL: signing and installer distribution requirements]`.
- **Documentation:** Include setup for OAuth, experimental provider warnings, troubleshooting, supported
  filesystem semantics, security handling, compatibility status, limitations, benchmark methodology, and
  developer build/test instructions.

## 3. Architecture Diagram

Use a Rust workspace with clear domain boundaries. UI and CLI should call the same application/service APIs;
the UI must not implement a separate copy of domain behavior.

```text
┌─────────────────────────────┐        ┌────────────────────────────┐
│ Desktop UI [FILL: framework]│        │ aveniq CLI                    │
│ Dashboard / Drives / Games  │        │ diagnostics / ops / bench  │
│ Cache / Accelerator / etc.  │        └─────────────┬──────────────┘
└──────────────┬──────────────┘                      │
               └─────────────────┬──────────────────┘
                                 ▼
                    ┌────────────────────────┐
                    │ Application / Use Cases│
                    │ settings, orchestration│
                    │ lifecycle, metrics     │
                    └──┬──────┬──────┬──────┘
                       │      │      │
         ┌─────────────┘      │      └──────────────────┐
         ▼                    ▼                         ▼
┌─────────────────┐  ┌──────────────────┐   ┌───────────────────┐
│ Mount / Provider│  │ Cache / Streaming│   │ Games / Launcher  │
│ rclone + WinFsp │  │ telemetry/predict│   │ scan/prepare/modes │
└───────┬─────────┘  └────────┬─────────┘   └─────────┬─────────┘
        │                     │                       │
        ▼                     ▼                       ▼
┌─────────────────┐  ┌──────────────────┐   ┌───────────────────┐
│ Cloud backend   │  │ SQLite profiles  │   │ Local game process│
│ + virtual drive │  │ and cache index  │   │ + Windows APIs    │
└─────────────────┘  └──────────────────┘   └───────────────────┘

Cross-cutting: Core types/settings/paths; diagnostics/redaction/secrets; storage/migrations;
SimCloud/test harness; installer/release tooling.
```

Use ports/traits at boundaries where required for deterministic tests (rclone process, clock, filesystem,
database, network probe). Avoid an over-generalized plugin framework. Keep provider-specific behavior isolated and
make support level explicit in metadata.

## 4. Dependencies

Create a Cargo workspace and pin compatible, maintained crate versions in `Cargo.lock`. Treat the entries below as
candidate crates and the intended dependency roles; confirm current Rust/MSRV and Windows compatibility before
selecting final versions. Do not add every optional crate blindly. In particular, choose exactly one desktop UI
framework and document that decision.

**Core/common (`crates/core` and application services):**

```toml
[dependencies]
serde = { version = "1", features = ["derive"] }
serde_json = "1"
thiserror = "2"
tokio = { version = "1", features = ["rt-multi-thread", "macros", "process", "sync", "time", "fs", "io-util", "net"] }
tracing = "0.1"
tracing-subscriber = { version = "0.3", features = ["env-filter", "fmt"] }
```

**Persistence, local HTTP, and data safety (include only where used):**

```toml
rusqlite = { version = "0.32", features = ["bundled"] }
reqwest = { version = "0.12", default-features = false, features = ["json", "rustls-tls"] } # Use loopback RC only.
uuid = { version = "1", features = ["v4", "serde"] }
sha2 = "0.10"
```

These are valid Cargo version requirements, but the implementation agent must check for newer compatible releases
and confirm the selected versions support the project's MSRV and Windows targets before locking them. If selecting
`sqlx` rather than `rusqlite`, specify its runtime/features and migration strategy instead of including both.

**Windows integration (Windows-only crate features should be target-gated):**

```toml
[target.'cfg(windows)'.dependencies]
windows = { version = "0.58", features = [
  "Win32_Foundation",
  "Win32_Security_Cryptography",
  "Win32_System_JobObjects",
  "Win32_System_Pipes",
  "Win32_Storage_FileSystem",
  "Win32_System_Threading",
  "Win32_System_Memory",
] }
```

Check for a newer compatible `windows` crate release and narrow the feature list to APIs actually used. Use the
Windows API crate for DPAPI, named pipes, job objects, drive/disk queries, and process/event facilities as required.
Prefer safe wrappers and narrowly scoped `unsafe` blocks; document every unavoidable unsafe invariant.
Evaluate a maintained WinFsp binding only if needed; otherwise supervise rclone's WinFsp-backed mount.

**Desktop UI:** `[FILL: choose and validate one Windows-capable Rust UI framework]`. Candidate options include
Tauri 2 (web UI plus Rust command boundary) or a Rust-native toolkit such as egui. Document the choice, accessibility,
packaging, asynchronous command, and UI test implications. Do not implement two UI stacks.

**Test/dev candidates:**

```toml
[dev-dependencies]
tempfile = "3"
assert_cmd = "2"
predicates = "3"
```

Use a test HTTP server/fault injector for SimCloud; select a compatible maintained crate only if it materially
simplifies tests. Avoid network-dependent tests in the default unit suite. Keep rclone/WinFsp integration tests
separately selectable and skip only with a clearly reported prerequisite reason.

## 5. Module Structure

Suggested initial layout; adjust only when the implementation gives a concrete reason. Keep public interfaces
small and avoid dependency cycles.

```text
Cargo.toml                         # Workspace manifest
Cargo.lock                         # Reproducible dependency resolution
crates/
  core/
    src/lib.rs                     # Shared domain types, settings, units, paths, errors
    src/settings.rs                # Atomic settings persistence and validation
    src/units.rs                   # Mbps/MBps and byte/block conversions
    src/paths.rs                   # Per-user app/cache/data locations
    src/models.rs                  # Mount/game/metric/provider states
  diagnostics/
    src/lib.rs
    src/logging.rs                 # Structured logging and redaction
    src/secrets.rs                 # DPAPI-backed secret protection
    src/report.rs                  # Sanitized diagnostic archive
    src/dependencies.rs            # rclone/WinFsp checks
  providers/
    src/lib.rs                     # Provider descriptors and support levels
    src/google_drive.rs            # OAuth/rclone configuration flow
    src/terabox.rs                 # Isolated experimental backend
  mount/
    src/lib.rs
    src/rclone.rs                  # Discovery, process runner, capabilities
    src/rc.rs                      # Authenticated loopback remote control
    src/flags.rs                   # Safe mount args and validation
    src/service.rs                 # Mount lifecycle, adoption, recovery
    src/log_tailer.rs              # Rotation-safe log following
    src/log_parser.rs              # Access event parsing and error classification
  cache/
    src/lib.rs
    src/index.rs                   # rclone range index inspection
    src/inspector.rs               # Logical/physical usage
    src/safety.rs                  # Budget, reserve, pressure, mount decisions
    src/maintenance.rs             # Marker-guarded cleanup and pinning
  streaming/
    src/lib.rs
    src/telemetry.rs               # Access events and prefetch attribution
    src/profiles.rs                # SQLite profile repository/migrations
    src/predictors/                 # Startup, Markov, sequential, region
    src/queue.rs                   # Priorities, dedupe, cancellation, TTL, budgets
    src/scheduler.rs               # P0 policy, governor and executor coordination
    src/engine.rs                  # Orchestration
    src/game_aware.rs              # Authorized named-pipe protocol
    src/simulator.rs               # Deterministic network/cache simulation
  games/
    src/lib.rs
    src/library.rs                 # SQLite game library
    src/scanner.rs                 # Executables, PE imports, dependencies
    src/compatibility.rs           # Evidence-based compatibility report
    src/launcher.rs                # Process tree, events, diagnostics
    src/hybrid.rs                  # Safe copy/symlink staging
    src/full_local.rs              # Explicitly approved resumable local copy
    src/prepare.rs                 # Optimize & Prepare workflow
  app/
    src/lib.rs                     # Shared application/use-case API and DI wiring
    src/metrics.rs                 # Live metrics aggregation
    src/lifecycle.rs               # Startup, shutdown and recovery coordination
  cli/
    src/main.rs                    # aveniq command-line entry point
  desktop/
    src/main.rs                    # Desktop entry point (if separate executable)
    src/                              # UI views, state and presentation adapters
  simcloud/
    src/lib.rs                     # Throttled local HTTP dataset server
tests/
  unit/                            # Domain tests (or colocated module tests)
  integration/                     # rclone + WinFsp and SQLite tests
  e2e/                             # Aveniq demo-game workflow and UI/CLI smoke tests
installer/                          # Windows install, uninstall and release scripts
docs/                               # Setup, security, troubleshooting, limitations
```

SQLite schema must cover settings as appropriate, library entries, profiles, access sessions, transitions,
predictor statistics, and migrations. Do not store secrets in SQLite unless encrypted and explicitly required.
Use structured application errors and explicit state types instead of success-shaped fallback values.

## 6. Implementation Details

Implement incrementally, keeping the repository buildable and behavior testable at each milestone. First inspect
the supplied source/docs, record confirmed requirements and unknowns, then implement. Do not replace unknowns with
assumptions.

### Milestone A — Workspace foundation and reliable state

1. Create the Cargo workspace, Windows target configuration, shared domain types, structured error model, tracing,
   settings model, per-user paths, and atomic settings writer.
2. Define units explicitly; network speed is Mbps, throughput is bytes/second or MB/s with clear conversion rules.
3. Add single-instance/data-folder ownership locking. Test concurrent startup and stale-lock recovery safely.
4. Define database schema/migrations and repository interfaces. Verify corrupt settings/database errors are surfaced
   while preserving recoverable source files.
5. Implement CLI skeleton and shared application API before UI-specific business logic.

Example domain types (illustrative; refine to match actual state transitions):

```rust
#[derive(Debug, Clone, Copy, PartialEq, Eq, serde::Serialize, serde::Deserialize)]
pub enum SupportLevel {
    Supported,
    Experimental,
    Untested,
}

#[derive(Debug, Clone, serde::Serialize, serde::Deserialize)]
pub struct NetworkSettings {
    /// User-entered link speed in megabits per second.
    pub expected_mbps: u32,
}
```

### Milestone B — rclone, providers, mounts, and drive recovery

1. Implement a process runner using argument arrays (never shell-concatenated commands); discover rclone and
   inspect version/available flags. Verify downloaded/bundled binaries against pinned checksums.
2. Generate only supported and allowlisted mount flags. Reject dangerous extra flags that can execute commands,
   change config/log/cache roots unexpectedly, or expose remote control.
3. Start rclone with stdout/stderr drained; pass secrets via protected environment variables or loopback HTTP
   request bodies. Bind RC to `127.0.0.1`, randomize port/credentials, and require authentication.
4. Define mount readiness as both a visible/ready drive and a successful authenticated RC health check.
5. Persist enough protected state for app restart adoption; never start duplicate mounts when a live owned mount
   can be safely adopted.
6. Health-check periodically; restart unexpected exits with bounded exponential backoff (at most five attempts in
   ten minutes). Revalidate mounts after sleep/resume.
7. On unmount, allow pending uploads to settle (maximum 60 seconds), wait for read activity and rclone cache-index
   persistence, request graceful quit, and use process termination only as a final bounded fallback. Surface
   incomplete writes/uploads.
8. Implement Google OAuth and optional restricted/read-only access using rclone's supported local flow. Keep
   configuration isolated in the app data directory. Implement TeraBox only behind explicit experimental status
   and separate config/binary handling.
9. Support directory listing, file reads/range reads and writable operations only when the mount mode/backend
   supports them. Document rclone/WinFsp semantics; never claim NTFS equivalence.

### Milestone C — cache inspection, budget, disk safety

1. Read rclone's VFS range metadata rather than guessing cached byte ranges. Compute physical sparse-file allocation
   separately from logical cache size using the appropriate Windows filesystem API.
2. Calculate safe mount/cache limits from budget, current usage, free space, configured reserve, overrun margin,
   and all active mounts.
3. Pause application-owned prefetch when disk pressure is detected; keep foreground game reads unaffected.
4. Implement cache cleanup only below the configured root and only when a valid ownership marker is present.
   Add tests proving cloud data, save files, and unrelated files remain untouched.
5. Treat pinning as best effort and surface when the pinned set exceeds capacity or rclone eviction wins.
6. Report that eviction is asynchronous, open files may not be evicted, and a brief budget overrun can occur.

### Milestone D — telemetry, storage, prediction and scheduling

1. Tail rclone's verbose log safely across rotation/truncation; parse read-start/range-cache/result lines into
   timestamped records and pair only valid events.
2. Track wait duration and exact offsets/lengths; classify parse errors, EOF, auth/rate-limit/network failures.
   Never convert missing telemetry into a cache hit or zero wait.
3. Attribute prefetch reads with a synchronized range/time registry and expose attribution limitations.
4. Persist profile sessions, 1 MiB game-relative block observations, transitions, accuracy, and per-predictor
   disablement in SQLite transactions.
5. Implement deterministic Startup, Markov, and Sequential predictors. Add explicit confidence values and useful/
   wasted accounting. Apply the stated auto-disable threshold only after the specified sample minimum.
6. Implement the P0/P1/P2/P3 queue with deduplication, in-place priority upgrade, cancellation, TTL, byte budgets,
   and an observable dropped event for all non-fetched exits.
7. Schedule P0 above speculative work. Enforce one-stream P1-only behavior during network-wait periods, 750 ms
   quiet period before P2/P3 restart, background share limits, measured bandwidth-delay concurrency, and disk
   pressure suspension.
8. Execute prefetch through mounted paths so rclone owns the on-disk cache and indexes.
9. Make all scheduling policies pure/testable where practical, inject clock/network state for deterministic tests,
   and ensure cancellation/shutdown leaves queue and mount state consistent.

### Milestone E — game library, compatibility and launching

1. Build a SQLite-backed game library and folder scanner. Parse PE headers/imports without executing candidate
   binaries. Check dependencies and distinguish evidence from conclusions.
2. Detect launcher/anti-cheat/installer/engine hints as heuristics. Emit user-readable reasons and a safe mode
   recommendation; never bypass any restriction.
3. Implement Cloud Direct launch and process-tree monitoring. Capture exit code, decoded NTSTATUS where applicable,
   relevant event log details, elapsed time, and a sanitized diagnostic JSON.
4. Implement Hybrid staging with stamp checks, preservation of user-edited local copies, documented copy threshold,
   and symbolic-link capability checks. Keep destructive operations out of synchronization.
5. Implement Full Local as a separate explicit-approval workflow, resumable copy, integrity verification, capacity
   checks, cancellation, and accurate completion status.
6. Only upgrade compatibility to Verified after >=60 seconds of monitored success. Preserve untested status for
   unsupported/unavailable game titles.

### Milestone F — Optimize & Prepare and Game-Aware mode

1. Build preparation as an observable state machine: scan → capacity/safety validation → uncached throughput
   measurement → profile/compatibility review → optional Hybrid/Full Local preparation → startup prefetch → wait
   for index persistence → verify ranges → report readiness.
2. Ensure no profile means “startup data unknown”; do not report Ready on planned or issued bytes alone. Reconcile
   requested ranges with rclone's actual range index.
3. Implement current-user-only named pipe transport, strict maximum line size, JSON parsing and message validation.
   Count and drop malformed input; reject inappropriate client concurrency.
4. Learn region associations only from the stated repeat-visit and dominant-read conditions. Use observed region
   transitions only; do not infer game world structure.
5. Cancel old region-tagged work on movement/fast travel, favor the current region, and keep all universal safety
   limits in force.
6. Keep Game-Aware disabled or experimental unless an authorized game plugin exists and has been tested.

### Milestone G — diagnostics, UI, packaging, and documentation

1. Implement redaction before log persistence/export; test tokens, OAuth client secrets, passwords, cookies, URLs,
   and RC credentials. Do not include config files in diagnostic bundles.
2. Implement the chosen desktop framework as a presentation layer over shared application services. Include all
   documented pages and dialogs, keyboard/accessibility states, progress, cancellation, empty/error/unavailable
   states, and responsive status updates.
3. Add a one-second metrics refresh or equivalent UI-safe stream; do not block rendering on filesystem/network
   work.
4. Build per-user Windows install/uninstall with no default admin requirement; detect WinFsp and explain missing
   prerequisites. Preserve user data on uninstall by default.
5. Write install, provider setup, troubleshooting, security, compatibility, limitations, benchmark, and contributor
   documentation. Mark experimental/unverified behavior in the relevant UI and docs.
6. Provide reproducible release instructions and checksum/third-party license inventory. Do not claim code signing
   unless the release is actually signed.

## 7. Testing Strategy

Test contracts and safety invariants, not only happy-path UI behavior. Use fake clocks, process adapters, and
network fixtures for unit tests. Use real rclone/WinFsp only in a separately marked Windows integration suite.

### Unit tests

- Settings serialization, atomic replace, corrupt-file retention, migration, and validation.
- Mbps/MBps and byte/block arithmetic, bounds, integer overflow, and 1 MiB block mapping.
- Redaction and fail-closed DPAPI wrapper behavior (Windows-specific tests where available).
- rclone version/flag parsing; command argument safety and refusal of dangerous flags.
- Mount readiness state machine, bounded restart backoff, activity settle timeout, and ownership-lock behavior.
- Log parser fixtures captured from the supported rclone version: range offsets, hit/miss, wait, EOF, malformed,
  rotation, auth, 429/503, dropped connections, and prefetch attribution.
- Cache index parsing; logical/physical usage distinction; reserve/overrun budget decisions; cleanup marker checks.
- Queue ordering, dedupe, upgrades, cancellation, TTL, session budget, and dropped notifications.
- P0 scheduling protection, disk-pressure pause, 750 ms wait recovery, background rate, and network concurrency.
- Predictor determinism, confidence, accuracy accounting, startup majority, Markov transitions, sequential runs,
  auto-disable boundary, and SQLite persistence.
- Game scanner/PE fixtures, dependency matching, heuristic compatibility labels, and safe recommendation rules.
- Hybrid copy/link planning and preservation of user-modified files; Full Local resume/checksum/cancel state.
- Optimize & Prepare readiness only when index coverage proves completion; no-profile/unknown case.
- Game-Aware malformed/oversized/unknown JSON, authorization scope, learning thresholds, region transitions,
  cancellation and fast-travel behavior.
- Simulator repeatability and cold/warm/prepared sanity invariants.

Illustrative async test shape (adapt to actual service interfaces):

```rust
#[tokio::test]
async fn foreground_demand_is_not_blocked_by_speculative_prefetch() {
    let scheduler = test_scheduler_with_pending_prefetch().await;

    let decision = scheduler.next_work(true).await.expect("scheduler decision");

    assert_eq!(decision.priority, Priority::P0);
}
```

### Integration tests

- Use a local deterministic HTTP dataset/SimCloud with configurable throughput, latency, offline state, 429/503,
  and connection drops.
- Mount through the actual bundled/pinned rclone and WinFsp on Windows. Verify drive visibility, Unicode/spaces,
  whole-file SHA-256, random range reads, offsets beyond 2 GiB, concurrent handles, memory-mapped reads, metadata,
  executable launch, cache reuse, read-only behavior, and supported write-back behavior.
- Verify rclone crash/restart and pending upload recovery; ensure uncached offline reads fail explicitly and cached
  reads remain correct; ensure remote changes do not return stale data.
- Exercise cache eviction/low-space behavior and prove user/cloud files are untouched.
- Test Google OAuth only with an authorized test account/client; never put credentials in fixtures/logs. Test
  TeraBox against a real account only with explicit authorization and without elevating its support label beyond
  the operations actually verified.
- Test concurrency with multiple mounts, app restart adoption, sleep/resume, unmount, and shutdown.

### End-to-end tests

- Use the DRM-free Aveniq demo game and a controlled SimCloud profile to exercise add game → compatibility report →
  first Cloud Direct launch → profile learning → Optimize & Prepare → verified readiness → subsequent launch.
- Test Hybrid and Full Local as separate workflows, including cancellation/restart and user-modified local files.
- Verify the UI can display all documented pages and dialogs, recover from service errors, and show “Unavailable”
  rather than synthetic live metrics.
- CLI smoke tests should verify exit codes, machine-readable output if supported, secrets absent from logs, and
  non-destructive defaults.
- Keep benchmark acceptance thresholds as `[FILL: specify target hardware/network and measurable thresholds]`.
  Report raw measured values and methodology; do not assert SSD-equivalent cold reads.

Example integration test should compare contents, not just successful return:

```rust
#[tokio::test]
async fn mounted_file_range_matches_source_bytes() {
    let fixture = SimCloudFixture::start().await;
    let mount = fixture.mount_read_only().await.expect("mount ready");

    let actual = mount.read_range("Games/demo.dat", 4096, 8192).await
        .expect("range read");
    let expected = fixture.source_range("Games/demo.dat", 4096, 8192);

    assert_eq!(actual, expected);
}
```

### Validation/reporting

- Run `cargo fmt --all -- --check`, `cargo clippy --workspace --all-targets -- -D warnings`, and
  `cargo test --workspace` for the platform-neutral suite.
- Run Windows-target compilation, UI build, and Windows-specific unit/integration tests on Windows CI.
- Keep external prerequisite skips explicit and count them as skipped, not passed.
- Attach test reports and reproducible benchmark details to releases. Never mark an unrun test as successful.

## 8. Build & Deployment

### Developer commands

Use the commands applicable to the chosen workspace/package names; replace placeholders after creating the workspace:

```powershell
cargo fmt --all -- --check
cargo clippy --workspace --all-targets -- -D warnings
cargo test --workspace
cargo build --workspace --release
cargo build -p [DESKTOP_PACKAGE] --release --target x86_64-pc-windows-msvc
cargo build -p [CLI_PACKAGE] --release --target x86_64-pc-windows-msvc
```

Install the MSVC Rust target/toolchain and Windows SDK prerequisites in documented developer setup. Integration
tests requiring WinFsp and rclone must detect prerequisites and provide setup instructions rather than silently
failing. Keep simulated and real-provider test modes separate.

### CI/CD outline

1. On pull requests: formatting, clippy, unit tests, dependency/license checks, secret scanning, and Windows
   x64 compilation.
2. On main/release tags: repeat checks; run Windows integration suite on a dedicated runner with pinned rclone
   and WinFsp; run Aveniq demo-game end-to-end tests where the runner permits it.
3. Generate self-contained artifacts, checksums, SBOM/third-party notices, and install/uninstall smoke tests.
4. Require explicit approval for publishing releases; sign only with a configured protected signing key and report
   unsigned status honestly.
5. Never put OAuth secrets, TeraBox cookies, tokens, or signing keys in CI logs or repository files.

### Packaging and deployment options

- Primary deployment is a per-user Windows x64 desktop installer/portable package.
- Bundle or fetch a pinned, checksum-verified official rclone binary. Keep any community TeraBox binary separate,
  optional, isolated, and conspicuously experimental.
- WinFsp is a system prerequisite and may require a separate official installer/elevation. Detect it; do not bundle
  an untrusted driver or silently elevate.
- Include required runtime assets for the chosen UI framework; package the CLI separately or alongside the desktop
  app.
- Default install/uninstall must preserve user settings, game library, and cache; offer explicit, scoped cleanup.
- `[FILL: release channel, signing, auto-update, telemetry/analytics, and support policy]`. Do not add analytics or
  auto-update without an explicit product decision and privacy/security design.

## 9. Future Enhancements

These are optional and must not be represented as current support until implemented and validated:

- Additional first-class cloud providers beyond Google Drive; provider-specific authorization, range reads,
  consistency, throttling, and write semantics must each be tested.
- Authorized Game-Aware plugins using officially supported APIs for individual titles.
- Better profile export/import, privacy-preserving profile portability, and opt-in aggregate predictor evaluation.
- Improved scheduling from measured network quality and user-configured time/bandwidth windows.
- More robust cross-version rclone capability adapters and supported-version compatibility tests.
- Additional UI localization/accessibility and documented keyboard workflows.
- Update/signing pipeline, clean-machine installer verification, and recoverable upgrade/rollback.
- Separate local cache/profile locations, multiple independent libraries, and additional drive filesystems only
  after explicit scope and safety review.
- Cross-platform support only after replacing Windows-specific DPAPI, WinFsp, drive letters, named pipes, job
  objects, installer assumptions, and filesystem compatibility behavior with tested platform equivalents.

## Missing Decisions / Placeholders to Resolve

Before calling the application complete, resolve each item with the product owner or preserve the placeholder and
report it as a blocker:

- `[FILL: canonical brand/name and whether legacy Aveniq paths/configuration must remain compatible]`
- `[FILL: desktop UI framework and packaging approach]`
- `[FILL: exact CLI subcommand/API compatibility requirements]`
- `[FILL: supported rclone versions and binary distribution/update policy]`
- `[FILL: whether TeraBox is included, excluded, or remains an experimental optional component]`
- `[FILL: cache default/custom limits, user-configurable reserve, and maximum allowed budget]`
- `[FILL: benchmark acceptance hardware, network, and thresholds]`
- `[FILL: signing, updates, telemetry/privacy, and release distribution policy]`
- `[FILL: supported target OS versions beyond Windows 10 1809+/Windows 11 x64]`

## Completion Criteria

The implementation is complete only when:

- All required desktop, CLI, mount, provider, cache, game, telemetry, prediction, preparation, diagnostics, and
  packaging workflows are implemented or explicitly marked blocked/experimental.
- Foreground reads are never starved by prefetch, and disk-pressure rules are enforced.
- Every readiness/compatibility/metrics claim is based on observed and verifiable evidence.
- Cache cleanup cannot escape the configured, marker-validated cache directory.
- Secrets never appear in command lines, logs, diagnostic bundles, or test output.
- Cloud files and game saves are never deleted by cache management or uninstall defaults.
- The test suites pass on the stated platforms, and all unavailable prerequisites/skips are clearly reported.
- Documentation states limitations, provider support level, filesystem differences, and verified test environment.
