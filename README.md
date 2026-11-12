# OBS Game Bar Widget

A lightweight Windows Xbox Game Bar (`Win + G`) widget to control and monitor OBS Studio directly in-game without needing to Alt+Tab.

Built with UWP and C#, designed to match the native Windows Game Bar design system.

---

## Features

- **Recording**: Start, stop, pause, and resume recording with an active duration timer.
- **Streaming**: Start and stop stream broadcasts with live uptime.
- **Replay Buffer**: Save instant gameplay replay clips in one click.
- **Virtual Camera**: Toggle the OBS virtual camera on or off.
- **Scene Switching**: Real-time synchronized scene list with active scene indicator and one-tap switching.
- **Audio Controls**: Mute and unmute toggles for desktop audio and microphone.
- **In-Game Overlay**: Pin the widget over any game, position it anywhere, and adjust opacity via native Game Bar settings.

---

## Requirements

- **Windows 10 (version 2004+) or Windows 11** with Xbox Game Bar installed.
- **OBS Studio 28+** (uses the built-in obs-websocket v5 protocol).
- **Visual Studio 2022** (if building from source) with the **Universal Windows Platform development** workload.

---

## Setup

### 1. Enable WebSocket Server in OBS Studio

1. Open OBS Studio.
2. In the top menu, go to **Tools** > **WebSocket Server Settings**.
3. Check **Enable WebSocket server**.
4. Confirm the port is set to **4455** (default).
5. If authentication is enabled, note your server password.
6. Click **Apply**, then **OK**.

### 2. Build and Deploy

1. Open `Solution1.slnx` (or `Solution1.sln`) in Visual Studio.
2. Set configuration to `Release` (or `Debug`) and platform to `x64` (or `ARM64`).
3. Right-click the `ObsGameBarWidget` project in Solution Explorer and click **Deploy** (or press `F5`).
4. Windows registers the widget extension automatically with the Xbox Game Bar system.

### 3. Grant Localhost Loopback Exemption

Windows UWP apps run inside an AppContainer isolation sandbox that blocks loopback network traffic (`localhost` / `127.0.0.1`) by default. To let the widget connect to OBS on port 4455, run this script once in PowerShell:

```powershell
powershell -ExecutionPolicy Bypass -File .\ObsGameBarWidget\Enable-LoopbackExemption.ps1
```

*(Note: The widget must be deployed at least once before running this script so Windows can identify its package name).*

---

## Usage

1. Press `Win + G` to bring up the Xbox Game Bar overlay.
2. Open the **Widget Menu** (top bar icon) and select **OBS Controller** (click the star to pin it to your favorites bar).
3. The widget connects to OBS Studio automatically if OBS is running.
4. Click the gear icon (`⚙`) to configure a custom host, port, or password if needed.
5. Click the **Pin** icon on the widget's title bar to keep it permanently visible over your games.

---

## Project Structure

```
Solution1/
├── ObsGameBarWidget/
│   ├── Assets/                       # Application icons and branding assets
│   ├── Converters/
│   │   └── ValueConverters.cs        # XAML binding converters (colors, scene state, mute)
│   ├── Models/
│   │   └── ObsModels.cs              # Data contracts for scenes, audio sources, and status
│   ├── Services/
│   │   ├── ObsWebSocketService.cs    # OBS WebSocket v5 client (SHA-256 auth, RPC, events)
│   │   └── SettingsService.cs        # Local persistent configuration
│   ├── App.xaml / App.xaml.cs        # Game Bar protocol activation (ms-gamebarwidget)
│   ├── MainPage.xaml / .cs           # Fluent UI layout and Game Bar interaction logic
│   ├── Package.appxmanifest          # UWP manifest and Game Bar extension declarations
│   ├── Enable-LoopbackExemption.ps1  # One-time loopback exemption helper
│   └── ObsGameBarWidget.csproj       # Project file
├── Solution1.slnx                    # Visual Studio solution (XML format)
└── Solution1.sln                     # Visual Studio solution (legacy format)
```
