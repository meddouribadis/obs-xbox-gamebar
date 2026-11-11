using System;
using System.IO;
using Microsoft.Gaming.XboxGameBar;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace ObsGameBarWidget
{
    sealed partial class App : Application
    {
        private XboxGameBarWidget _widget = null;

        public static void Log(string message)
        {
            try
            {
                string folder = ApplicationData.Current.LocalFolder.Path;
                string file = Path.Combine(folder, "widget_debug.log");
                File.AppendAllText(file, $"[{DateTime.Now:HH:mm:ss.fff}] {message}\r\n");
            }
            catch { }
        }

        public App()
        {
            this.InitializeComponent();
            this.Suspending += OnSuspending;
            this.UnhandledException += App_UnhandledException;
            Log("App started");
        }

        private void App_UnhandledException(object sender, Windows.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            Log($"EXCEPTION NON GEREE: {e.Message}\r\n{e.Exception}");
            e.Handled = true;
        }

        protected override void OnActivated(IActivatedEventArgs args)
        {
            Log($"OnActivated: Kind={args.Kind}, PrevState={args.PreviousExecutionState}");

            XboxGameBarWidgetActivatedEventArgs widgetArgs = args as XboxGameBarWidgetActivatedEventArgs;

            if (widgetArgs == null && args.Kind == ActivationKind.Protocol)
            {
                var protocolArgs = args as IProtocolActivatedEventArgs;
                if (protocolArgs != null && protocolArgs.Uri != null && protocolArgs.Uri.Scheme.Equals("ms-gamebarwidget", StringComparison.OrdinalIgnoreCase))
                {
                    widgetArgs = args as XboxGameBarWidgetActivatedEventArgs;
                }
            }

            Log($"WidgetArgs: isNull={widgetArgs == null}, IsLaunch={widgetArgs?.IsLaunchActivation}, AppExtId={widgetArgs?.AppExtensionId}");

            var rootFrame = Window.Current.Content as Frame;
            if (rootFrame == null)
            {
                rootFrame = new Frame();
                rootFrame.NavigationFailed += OnNavigationFailed;
                Window.Current.Content = rootFrame;
            }

            if (widgetArgs != null)
            {
                if (widgetArgs.IsLaunchActivation || _widget == null)
                {
                    try
                    {
                        _widget = new XboxGameBarWidget(
                            widgetArgs,
                            Window.Current.CoreWindow,
                            rootFrame);
                        Log("XboxGameBarWidget instancié avec succès");
                    }
                    catch (Exception ex)
                    {
                        Log($"Erreur instanciation XboxGameBarWidget: {ex}");
                    }

                    rootFrame.Navigate(typeof(MainPage), _widget);
                }
                else if (rootFrame.Content == null)
                {
                    rootFrame.Navigate(typeof(MainPage), _widget);
                }

                Window.Current.Activate();
                Log("Window.Current.Activate() appelée");
            }
            else
            {
                // Fallback direct
                Log("Activation fallback (non-widget)");
                if (rootFrame.Content == null)
                {
                    rootFrame.Navigate(typeof(MainPage), null);
                }
                Window.Current.Activate();
            }

            base.OnActivated(args);
        }

        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            Log($"OnLaunched: PrelaunchActivated={e.PrelaunchActivated}");

            Frame rootFrame = Window.Current.Content as Frame;
            if (rootFrame == null)
            {
                rootFrame = new Frame();
                rootFrame.NavigationFailed += OnNavigationFailed;
                Window.Current.Content = rootFrame;
            }

            if (e.PrelaunchActivated == false)
            {
                if (rootFrame.Content == null)
                {
                    rootFrame.Navigate(typeof(MainPage), null);
                }
                Window.Current.Activate();
            }
        }

        void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            Log($"Echec Navigation: {e.SourcePageType?.FullName} - {e.Exception}");
            throw new Exception("Echec de chargement de la page " + e.SourcePageType?.FullName);
        }

        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            Log("App Suspending");
            var deferral = e.SuspendingOperation.GetDeferral();
            deferral.Complete();
        }
    }
}
