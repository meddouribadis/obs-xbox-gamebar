using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ObsGameBarWidget.Models
{
    public enum ObsConnectionState
    {
        Disconnected,
        Connecting,
        Connected,
        AuthenticationFailed,
        Error
    }

    public class ObsSceneItem : INotifyPropertyChanged
    {
        private string _name;
        private bool _isCurrent;

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public bool IsCurrent
        {
            get => _isCurrent;
            set { _isCurrent = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class ObsAudioInput : INotifyPropertyChanged
    {
        private string _name;
        private bool _isMuted;

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public bool IsMuted
        {
            get => _isMuted;
            set { _isMuted = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class ObsServerConfig
    {
        public string Host { get; set; } = "127.0.0.1";
        public int Port { get; set; } = 4455;
        public string Password { get; set; } = "";
        public bool AutoConnect { get; set; } = true;
    }
}
