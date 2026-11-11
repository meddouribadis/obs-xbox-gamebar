using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.Gaming.XboxGameBar;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using ObsGameBarWidget.Models;
using ObsGameBarWidget.Services;

namespace ObsGameBarWidget
{
    public sealed partial class MainPage : Page, INotifyPropertyChanged
    {
        private XboxGameBarWidget _gameBarWidget;
        private ObsWebSocketService _obsService;
        private SettingsService _settingsService;
        private DispatcherTimer _clockTimer;
        private DispatcherTimer _toastTimer;

        // Timers state
        private bool _isRecording;
        private bool _isRecordPaused;
        private TimeSpan _recordDuration = TimeSpan.Zero;

        private bool _isStreaming;
        private TimeSpan _streamDuration = TimeSpan.Zero;

        private bool _isVirtualCamActive;

        // Observable collections
        public ObservableCollection<ObsSceneItem> ScenesList { get; } = new ObservableCollection<ObsSceneItem>();
        public ObservableCollection<ObsAudioInput> AudioInputsList { get; } = new ObservableCollection<ObsAudioInput>();

        // Dynamic Brushes
        // IMPORTANT : les objets XAML (SolidColorBrush...) sont liés au thread UI qui les crée.
        // Ne JAMAIS les mettre dans des champs static partagés (RPC_E_WRONG_THREAD) :
        // on en crée un nouveau à chaque lecture, toujours depuis le thread UI.
        private static SolidColorBrush NewBrush(byte r, byte g, byte b) => new SolidColorBrush(Color.FromArgb(255, r, g, b));

        private static SolidColorBrush BrushDisconnected => NewBrush(209, 52, 56);   // Red
        private static SolidColorBrush BrushConnecting => NewBrush(255, 185, 0);     // Orange
        private static SolidColorBrush BrushConnected => NewBrush(16, 124, 65);      // Green

        private static SolidColorBrush BrushCardDark => NewBrush(37, 40, 48);
        private static SolidColorBrush BrushRecordActive => NewBrush(180, 24, 28);
        private static SolidColorBrush BrushStreamActive => NewBrush(100, 65, 165);  // Twitch purple
        private static SolidColorBrush BrushAccentBlue => NewBrush(0, 120, 215);

        // Binding Properties
        public SolidColorBrush StatusBrush =>
            _obsService?.State == ObsConnectionState.Connected ? BrushConnected :
            _obsService?.State == ObsConnectionState.Connecting ? BrushConnecting : BrushDisconnected;

        public string StatusText =>
            _obsService?.State == ObsConnectionState.Connected ? "Connecté à OBS" :
            _obsService?.State == ObsConnectionState.Connecting ? "Connexion..." : "Déconnecté";

        public string ConnectButtonText =>
            _obsService?.State == ObsConnectionState.Connected ? "Déconnecter" : "Connecter";

        public SolidColorBrush RecordBackgroundBrush => _isRecording ? BrushRecordActive : BrushCardDark;
        public SolidColorBrush RecordIconBrush => _isRecording ? new SolidColorBrush(Colors.White) : BrushDisconnected;
        public string RecordButtonLabel => _isRecording ? "Arrêter REC" : "ENREGISTRER";
        public string RecordTimecodeText => _isRecording ? _recordDuration.ToString(@"hh\:mm\:ss") : "--:--:--";
        public Visibility RecordPauseVisibility => _isRecording ? Visibility.Visible : Visibility.Collapsed;
        public string RecordPauseLabel => _isRecordPaused ? "Reprendre" : "Pause";

        public SolidColorBrush StreamBackgroundBrush => _isStreaming ? BrushStreamActive : BrushCardDark;
        public SolidColorBrush StreamIconBrush => _isStreaming ? new SolidColorBrush(Colors.White) : new SolidColorBrush(Color.FromArgb(255, 140, 145, 155));
        public string StreamButtonLabel => _isStreaming ? "Arrêter LIVE" : "DIFFUSER";
        public string StreamTimecodeText => _isStreaming ? _streamDuration.ToString(@"hh\:mm\:ss") : "--:--:--";

        public SolidColorBrush VirtualCamBackgroundBrush => _isVirtualCamActive ? BrushAccentBlue : BrushCardDark;
        public string VirtualCamButtonLabel => _isVirtualCamActive ? "Caméra Active" : "Caméra Virtuelle";

        public MainPage()
        {
            try
            {
                App.Log("MainPage constructor start");
                this.InitializeComponent();
                App.Log("MainPage InitializeComponent done");

                _settingsService = new SettingsService();
                _obsService = new ObsWebSocketService();
                _obsService.Config = _settingsService.LoadConfig();

                // Setup clock timer for duration counters
                _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                _clockTimer.Tick += OnClockTimerTick;
                _clockTimer.Start();

                // Setup toast timer
                _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
                _toastTimer.Tick += (s, e) =>
                {
                    NotificationBanner.Visibility = Visibility.Collapsed;
                    _toastTimer.Stop();
                };

                // Wire OBS Events
                _obsService.ConnectionStateChanged += OnObsConnectionStateChanged;
                _obsService.RecordStateChanged += OnObsRecordStateChanged;
                _obsService.StreamStateChanged += OnObsStreamStateChanged;
                _obsService.VirtualCamStateChanged += OnObsVirtualCamStateChanged;
                _obsService.ReplayBufferSaved += OnObsReplayBufferSaved;
                _obsService.CurrentSceneChanged += OnObsCurrentSceneChanged;
                _obsService.SceneListChanged += OnObsSceneListChanged;
                _obsService.InputMuteChanged += OnObsInputMuteChanged;
                _obsService.StatusMessage += OnObsStatusMessage;

                // Load settings into UI
                HostInput.Text = _obsService.Config.Host ?? "127.0.0.1";
                PortInput.Text = _obsService.Config.Port.ToString();
                PasswordInput.Password = _obsService.Config.Password ?? "";
                AutoConnectCheck.IsChecked = _obsService.Config.AutoConnect;

                this.Loaded += MainPage_Loaded;
                this.SizeChanged += (s, a) => App.Log($"MainPage SizeChanged: {a.NewSize.Width}x{a.NewSize.Height}");
                App.Log("MainPage constructor completed");
            }
            catch (Exception ex)
            {
                App.Log($"ERREUR dans MainPage constructor: {ex}");
            }
        }

        private async void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            App.Log($"MainPage_Loaded: ActualSize={ActualWidth}x{ActualHeight}, AutoConnect={_obsService?.Config.AutoConnect}");
            try
            {
                if (_obsService != null && _obsService.Config.AutoConnect)
                {
                    await _obsService.ConnectAsync();
                }
            }
            catch (Exception ex)
            {
                App.Log($"ERREUR ConnectAsync: {ex}");
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            App.Log($"MainPage.OnNavigatedTo called. Parameter: {e.Parameter?.GetType().Name}");

            // Récupère l'instance XboxGameBarWidget déjà créée dans App.xaml.cs
            _gameBarWidget = e.Parameter as XboxGameBarWidget;
            if (_gameBarWidget != null)
            {
                App.Log("XboxGameBarWidget attaché à MainPage");
                _gameBarWidget.SettingsClicked += async (s, a) =>
                {
                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        SettingsOverlay.Visibility = SettingsOverlay.Visibility == Visibility.Visible
                            ? Visibility.Collapsed
                            : Visibility.Visible;
                    });
                };
            }
        }

        private void OnClockTimerTick(object sender, object e)
        {
            if (_isRecording && !_isRecordPaused)
            {
                _recordDuration = _recordDuration.Add(TimeSpan.FromSeconds(1));
                NotifyBindings(nameof(RecordTimecodeText));
            }

            if (_isStreaming)
            {
                _streamDuration = _streamDuration.Add(TimeSpan.FromSeconds(1));
                NotifyBindings(nameof(StreamTimecodeText));
            }
        }

        #region OBS Event Handlers

        private async void OnObsConnectionStateChanged(object sender, ObsConnectionState state)
        {
            await RunOnUi(() =>
            {
                NotifyBindings(
                    nameof(StatusBrush),
                    nameof(StatusText),
                    nameof(ConnectButtonText));

                if (state != ObsConnectionState.Connected)
                {
                    _isRecording = false;
                    _isStreaming = false;
                    _isVirtualCamActive = false;
                    ScenesList.Clear();
                    AudioInputsList.Clear();
                    NotifyAllControls();
                }
            });
        }

        private async void OnObsRecordStateChanged(object sender, (bool isActive, bool isPaused, string state) e)
        {
            await RunOnUi(() =>
            {
                _isRecording = e.isActive;
                _isRecordPaused = e.isPaused;
                if (!_isRecording) _recordDuration = TimeSpan.Zero;

                NotifyBindings(
                    nameof(RecordBackgroundBrush),
                    nameof(RecordIconBrush),
                    nameof(RecordButtonLabel),
                    nameof(RecordTimecodeText),
                    nameof(RecordPauseVisibility),
                    nameof(RecordPauseLabel));
            });
        }

        private async void OnObsStreamStateChanged(object sender, (bool isActive, string state) e)
        {
            await RunOnUi(() =>
            {
                _isStreaming = e.isActive;
                if (!_isStreaming) _streamDuration = TimeSpan.Zero;

                NotifyBindings(
                    nameof(StreamBackgroundBrush),
                    nameof(StreamIconBrush),
                    nameof(StreamButtonLabel),
                    nameof(StreamTimecodeText));
            });
        }

        private async void OnObsVirtualCamStateChanged(object sender, bool isActive)
        {
            await RunOnUi(() =>
            {
                _isVirtualCamActive = isActive;
                NotifyBindings(
                    nameof(VirtualCamBackgroundBrush),
                    nameof(VirtualCamButtonLabel));
            });
        }

        private async void OnObsReplayBufferSaved(object sender, string path)
        {
            await RunOnUi(() =>
            {
                NotificationText.Text = "Clip sauvegardé dans OBS !";
                NotificationBanner.Visibility = Visibility.Visible;
                _toastTimer?.Stop();
                _toastTimer?.Start();
            });
        }

        private async void OnObsCurrentSceneChanged(object sender, string currentScene)
        {
            await RunOnUi(() =>
            {
                foreach (var scene in ScenesList)
                {
                    scene.IsCurrent = string.Equals(scene.Name, currentScene, StringComparison.OrdinalIgnoreCase);
                }
            });
        }

        private async void OnObsSceneListChanged(object sender, System.Collections.Generic.List<string> scenes)
        {
            await RunOnUi(() =>
            {
                ScenesList.Clear();
                foreach (var sc in scenes)
                {
                    ScenesList.Add(new ObsSceneItem { Name = sc, IsCurrent = false });
                }
            });
        }

        private async void OnObsInputMuteChanged(object sender, (string inputName, bool isMuted) e)
        {
            await RunOnUi(() =>
            {
                var input = AudioInputsList.FirstOrDefault(a => string.Equals(a.Name, e.inputName, StringComparison.OrdinalIgnoreCase));
                if (input != null)
                {
                    input.IsMuted = e.isMuted;
                }
                else
                {
                    AudioInputsList.Add(new ObsAudioInput { Name = e.inputName, IsMuted = e.isMuted });
                }
            });
        }

        private async void OnObsStatusMessage(object sender, string message)
        {
            await RunOnUi(() =>
            {
                NotificationText.Text = message;
                NotificationBanner.Visibility = Visibility.Visible;
                _toastTimer?.Stop();
                _toastTimer?.Start();
            });
        }

        #endregion

        #region UI Clicks

        private async void OnConnectToggleClicked(object sender, RoutedEventArgs e)
        {
            if (_obsService == null) return;

            if (_obsService.State == ObsConnectionState.Connected || _obsService.State == ObsConnectionState.Connecting)
            {
                _obsService.Disconnect();
            }
            else
            {
                await _obsService.ConnectAsync();
            }
        }

        private void OnSettingsToggleClicked(object sender, RoutedEventArgs e)
        {
            SettingsOverlay.Visibility = SettingsOverlay.Visibility == Visibility.Visible
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        private void OnSettingsCloseClicked(object sender, RoutedEventArgs e)
        {
            SettingsOverlay.Visibility = Visibility.Collapsed;
        }

        private async void OnSaveSettingsClicked(object sender, RoutedEventArgs e)
        {
            if (_obsService == null) return;

            int.TryParse(PortInput.Text, out int port);
            if (port <= 0) port = 4455;

            var newConfig = new ObsServerConfig
            {
                Host = string.IsNullOrWhiteSpace(HostInput.Text) ? "127.0.0.1" : HostInput.Text.Trim(),
                Port = port,
                Password = PasswordInput.Password ?? "",
                AutoConnect = AutoConnectCheck.IsChecked ?? true
            };

            _settingsService.SaveConfig(newConfig);
            _obsService.Config = newConfig;

            SettingsOverlay.Visibility = Visibility.Collapsed;

            _obsService.Disconnect();
            await _obsService.ConnectAsync();
        }

        private async void OnRecordClicked(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_obsService != null) await _obsService.ToggleRecordAsync();
            }
            catch (Exception ex)
            {
                ShowToast($"Erreur Record : {ex.Message}");
            }
        }

        private async void OnRecordPauseClicked(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_obsService != null) await _obsService.ToggleRecordPauseAsync();
            }
            catch (Exception ex)
            {
                ShowToast($"Erreur Pause : {ex.Message}");
            }
        }

        private async void OnStreamClicked(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_obsService != null) await _obsService.ToggleStreamAsync();
            }
            catch (Exception ex)
            {
                ShowToast($"Erreur Stream : {ex.Message}");
            }
        }

        private async void OnVirtualCamClicked(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_obsService != null) await _obsService.ToggleVirtualCamAsync();
            }
            catch (Exception ex)
            {
                ShowToast($"Erreur Cam : {ex.Message}");
            }
        }

        private async void OnSaveReplayClicked(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_obsService != null)
                {
                    await _obsService.SaveReplayBufferAsync();
                    ShowToast("Demande de clip envoyée à OBS...");
                }
            }
            catch (Exception ex)
            {
                ShowToast($"Erreur Replay : {ex.Message}");
            }
        }

        private async void OnRefreshScenesClicked(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_obsService != null) await _obsService.RefreshSceneListAsync();
            }
            catch { }
        }

        private async void OnSceneItemClicked(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is ObsSceneItem item)
            {
                try
                {
                    if (_obsService != null) await _obsService.SetCurrentProgramSceneAsync(item.Name);
                }
                catch (Exception ex)
                {
                    ShowToast($"Erreur Scène : {ex.Message}");
                }
            }
        }

        private async void OnAudioMuteClicked(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is ObsAudioInput item)
            {
                try
                {
                    if (_obsService != null) await _obsService.ToggleInputMuteAsync(item.Name);
                }
                catch (Exception ex)
                {
                    ShowToast($"Erreur Audio : {ex.Message}");
                }
            }
        }

        #endregion

        #region Helpers

        private void ShowToast(string message)
        {
            NotificationText.Text = message;
            NotificationBanner.Visibility = Visibility.Visible;
            _toastTimer?.Stop();
            _toastTimer?.Start();
        }

        private void NotifyAllControls()
        {
            NotifyBindings(
                nameof(RecordBackgroundBrush),
                nameof(RecordIconBrush),
                nameof(RecordButtonLabel),
                nameof(RecordTimecodeText),
                nameof(RecordPauseVisibility),
                nameof(StreamBackgroundBrush),
                nameof(StreamIconBrush),
                nameof(StreamButtonLabel),
                nameof(StreamTimecodeText),
                nameof(VirtualCamBackgroundBrush),
                nameof(VirtualCamButtonLabel));
        }

        private async Task RunOnUi(Action action)
        {
            if (Dispatcher.HasThreadAccess)
            {
                action();
            }
            else
            {
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => action());
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void NotifyBindings(params string[] propertyNames)
        {
            foreach (var prop in propertyNames)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
            }
        }

        #endregion
    }
}
