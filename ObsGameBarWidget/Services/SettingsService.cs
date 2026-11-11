using System;
using Windows.Storage;
using ObsGameBarWidget.Models;

namespace ObsGameBarWidget.Services
{
    public class SettingsService
    {
        private const string KeyHost = "ObsHost";
        private const string KeyPort = "ObsPort";
        private const string KeyPassword = "ObsPassword";
        private const string KeyAutoConnect = "ObsAutoConnect";

        private readonly ApplicationDataContainer _settings;

        public SettingsService()
        {
            _settings = ApplicationData.Current.LocalSettings;
        }

        public ObsServerConfig LoadConfig()
        {
            var config = new ObsServerConfig();

            if (_settings.Values.TryGetValue(KeyHost, out var hostVal) && hostVal is string host)
            {
                config.Host = host;
            }

            if (_settings.Values.TryGetValue(KeyPort, out var portVal) && portVal is int port)
            {
                config.Port = port;
            }

            if (_settings.Values.TryGetValue(KeyPassword, out var passVal) && passVal is string pass)
            {
                config.Password = pass;
            }

            if (_settings.Values.TryGetValue(KeyAutoConnect, out var autoVal) && autoVal is bool auto)
            {
                config.AutoConnect = auto;
            }

            return config;
        }

        public void SaveConfig(ObsServerConfig config)
        {
            if (config == null) return;

            _settings.Values[KeyHost] = config.Host ?? "127.0.0.1";
            _settings.Values[KeyPort] = config.Port > 0 ? config.Port : 4455;
            _settings.Values[KeyPassword] = config.Password ?? "";
            _settings.Values[KeyAutoConnect] = config.AutoConnect;
        }
    }
}
