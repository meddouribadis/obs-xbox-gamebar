# AGENT.md

Guidance for AI coding agents working in this repository.

## Project overview

**OBS Game Bar Widget** is a UWP (C#, XAML) widget for the Windows Xbox Game Bar (`Win + G`). It controls OBS Studio from inside a game over the OBS WebSocket v5 protocol (default `ws://127.0.0.1:4455`). It can start and stop recording, streaming, the replay buffer and the virtual camera. It can also switch scenes and mute audio inputs.

- Solution: `Solution1.slnx` (and legacy `Solution1.sln`). It contains a single project, `ObsGameBarWidget/`.
- Target: UAP `10.0.26100.0`, min `10.0.19041.0`. Platforms are x64, x86 and ARM64, and x64 is the default.
- Project style: classic non-SDK `.csproj`. Files are listed explicitly (see "Adding files").
- Packages: `Microsoft.NETCore.UniversalWindowsPlatform` 6.2.14, `Microsoft.Gaming.XboxGameBar` 7.3.x, `Newtonsoft.Json` 13.0.3.
- The UI language is **French**. User-facing strings, toasts, log messages and manifest descriptions are in French. Keep new strings French and consistent with the existing ones.

## Repository layout

```
Solution1/
├── README.md                       # User-facing setup/usage docs
├── Solution1.slnx / Solution1.sln
└── ObsGameBarWidget/
    ├── App.xaml(.cs)               # Activation: Game Bar widget activation, rootFrame, file logger (App.Log)
    ├── MainPage.xaml(.cs)          # Whole UI + code-behind (INotifyPropertyChanged, no MVVM framework)
    ├── Services/
    │   ├── ObsWebSocketService.cs  # OBS WebSocket v5 client: handshake/auth, requests, events
    │   └── SettingsService.cs      # ApplicationData.LocalSettings persistence (host/port/password/autoconnect)
    ├── Models/ObsModels.cs         # ObsConnectionState, ObsSceneItem, ObsAudioInput, ObsServerConfig
    ├── Converters/ValueConverters.cs # XAML IValueConverters
    ├── Package.appxmanifest        # Identity, Game Bar extension (microsoft.gameBarUIExtension), capabilities
    ├── GameBar/Icons/              # Game Bar icons (referenced by PublicFolder="GameBar")
    ├── Assets/                     # App/tile/store logos
    ├── Deploy-Widget.ps1           # CLI build + deploy
    ├── Enable-LoopbackExemption.ps1# UWP loopback exemption (reversible with -Remove)
    ├── Register-ObsProtocol.ps1    # HKCU obs:// protocol registration (reversible with -Unregister)
    └── Read-WidgetLog.ps1          # Prints the widget's widget_debug.log
```

`bin/`, `obj/`, `AppPackages/` and `.vs/` are build output and are git-ignored. Never edit or commit them. `*.log`, `*.msix` and `*.appxsym` are ignored too.

## Build and deploy

Windows only. It needs Visual Studio 2022 or newer with the **Universal Windows Platform development** workload. There is no `dotnet build` path, because this is a classic UWP project that needs full MSBuild.

```powershell
# Build (Debug|x64) + deploy to Windows (register package if manifest changed)
powershell -ExecutionPolicy Bypass -File .\ObsGameBarWidget\Deploy-Widget.ps1

# Other configuration/platform
powershell -ExecutionPolicy Bypass -File .\ObsGameBarWidget\Deploy-Widget.ps1 -Configuration Release -Platform x64

# Read runtime debug logs from the installed package
powershell -ExecutionPolicy Bypass -File .\ObsGameBarWidget\Read-WidgetLog.ps1
```

- Plain MSBuild does **not** refresh `bin\<Platform>\<Configuration>\AppX`. `Deploy-Widget.ps1` copies the output there itself.
- If `Package.appxmanifest` changes, `Deploy-Widget.ps1` uninstalls and re-registers the package. This **wipes saved widget settings** (host, port, password). Warn the user before making manifest changes.
- After deploying, the widget has to be closed and reopened in Game Bar.
- `Enable-LoopbackExemption.ps1` must be run after the first deploy. The AppContainer blocks localhost traffic otherwise.
- There are no automated tests and no linter config. Validate changes by building. Runtime verification needs Game Bar and a running OBS with its WebSocket server enabled, so do not claim runtime behaviour was verified unless it was.

## Architecture notes

- **Activation flow**: `App.OnActivated` builds an `XboxGameBarWidget` from `XboxGameBarWidgetActivatedEventArgs` and navigates to `MainPage`, passing the widget as the navigation parameter. `OnLaunched` is only a fallback for non-widget launches.
- **`ObsWebSocketService`** (OBS WebSocket v5):
  - Message ops handled: `0` Hello, `1` Identify, `2` Identified, `5` Event, `6` Request, `7` RequestResponse.
  - Authentication: `base64(sha256(base64(sha256(password+salt)) + challenge))`.
  - Event subscription mask: `1|2|4|8|64`, meaning General, Config, Scenes, Inputs and Outputs.
  - Requests are correlated by a GUID `requestId` through `_pendingRequests`, with a 5 s timeout.
  - Everything the UI needs is exposed as C# events (`RecordStateChanged`, `StreamStateChanged`, `CurrentSceneChanged`, `InputMuteChanged`, `StatusMessage`, ...). The service must not touch UI.
  - After connecting, `InitializeObsStateAsync` syncs record, stream, virtual cam, scenes and special audio inputs.
- **`MainPage`** subscribes to those events. It marshals to the UI thread with `RunOnUi`, and it updates bindings by calling `NotifyBindings(nameof(...))`. Binding properties are computed from private state fields, so after changing a field you must notify every dependent property (see `NotifyAllControls`).
- Record and stream timers are driven locally by a 1 s `DispatcherTimer`. They reset when the OBS state reports inactive.
- **Settings** persist in `ApplicationData.Current.LocalSettings`. The password is stored in plain text there. Do not log it.
- **Logging**: use `App.Log(...)`. It appends to `widget_debug.log` in the package `LocalState` folder and never throws.

## Conventions and pitfalls

- **Never store XAML objects (`SolidColorBrush`, etc.) in `static` fields or shared state.** They are bound to the creating UI thread and cause `RPC_E_WRONG_THREAD`. The code deliberately creates a new brush on every property read (`NewBrush(...)`, and the converters' `ActiveBrush => new ...`). Follow that pattern.
- Raise `PropertyChanged` for every affected binding when you change state. A missing notification is the most common cause of a stale UI.
- Wrap OBS actions from click handlers in `try/catch` and surface errors with `ShowToast(...)`, as the existing handlers do.
- OBS events arrive on a background thread (the receive loop). Always use `RunOnUi` before touching `ObservableCollection`s or controls.
- `ObsWebSocketService.Disconnect()` is called from the receive loop's `finally` and from error paths. Keep it idempotent.
- Keep code UWP-compatible (`NETFX_CORE` / `WINDOWS_UWP`). Don't use APIs outside the UWP surface, and don't add packages that aren't UWP/`uap10.0` compatible.
- Match the existing C# style: 4-space indentation, Allman braces, `_camelCase` private fields, `#region` blocks in `MainPage`, and C# 7-era syntax. Don't rely on newer language features the UWP toolchain may not support.
- `Package.appxmanifest` declares the capabilities `internetClient` and `privateNetworkClientServer`, which OBS access needs. It also declares the Game Bar extension. Be careful editing it (see the manifest warning above).

## Adding files

The `.csproj` is **not** SDK-style, so new files are not picked up automatically. Add them explicitly:

- New `.cs` file: add `<Compile Include="path\File.cs" />` to `ObsGameBarWidget.csproj`.
- New XAML page: add a `<Page Include="...">` entry, plus a `<Compile Include="...xaml.cs"><DependentUpon>...</DependentUpon></Compile>` entry.
- New asset or icon: add a `<Content Include="..." />` entry.

## Adding an OBS feature (typical flow)

1. Add the request wrapper to `ObsWebSocketService` (`SendRequestAsync("<RequestType>", data)`), and a new event plus a `HandleEvent` case if OBS emits state changes.
2. Seed the initial state in `InitializeObsStateAsync` if it needs syncing on connect.
3. Add the UI to `MainPage.xaml`, the binding properties and handler in `MainPage.xaml.cs`, and wire the event in the constructor.
4. Reset the new state in `OnObsConnectionStateChanged` when the connection drops (and add it to `NotifyAllControls`).
5. Update `README.md` (Features and Project Structure) if the change is user-visible.

## Things not to do

- Don't commit build output, `.vs/`, logs or packages.
- Don't hard-code credentials or log the OBS password.
- Don't change the package identity (`OBSStudio.GameBarWidget`) or `Id="ObsControllerWidget"` casually. It breaks existing installs and the loopback and log scripts, which look up the package by name.
- Don't introduce a UI or MVVM framework without being asked. The project is intentionally small and dependency-light.
