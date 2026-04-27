using System.ComponentModel;
using System.Windows;

namespace Jasmian.Client.Models
{
    public class ChatMessage : INotifyPropertyChanged
    {
                public int Id { get; set; }
        public string Sender { get; set; }

        private string _text;
        public string Text
        {
            get => _text;
            set { _text = value; OnPropertyChanged(); OnPropertyChanged(nameof(TextVisibility)); }
        }
        public string Base64Data { get; set; }

        public string Time { get; set; }
        public bool IsMe { get; set; }
        public System.Windows.Media.ImageSource AttachedImage { get; set; }

                private bool _isEdited;
        public bool IsEdited
        {
            get => _isEdited;
            set { _isEdited = value; OnPropertyChanged(); OnPropertyChanged(nameof(EditedVisibility)); }
        }

        public Visibility EditedVisibility => IsEdited ? Visibility.Visible : Visibility.Collapsed;
        public Visibility ImageVisibility => AttachedImage != null ? Visibility.Visible : Visibility.Collapsed;
        public Visibility TextVisibility => !string.IsNullOrWhiteSpace(Text) ? Visibility.Visible : Visibility.Collapsed;

        private bool _isRead;
        public bool IsRead
        {
            get => _isRead;
            set { _isRead = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    public class ChatListItem : System.ComponentModel.INotifyPropertyChanged
    {
        public string Username { get; set; }
        public string DisplayName { get; set; }
        public string LastMessage { get; set; }
        public string Time { get; set; }
        public bool IsOnline { get; set; }

        public string StatusColor => IsOnline ? "#32CD32" : "Transparent";
        public System.Windows.Visibility StatusVisibility => IsOnline ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

                private int _unreadCount;
        public int UnreadCount
        {
            get => _unreadCount;
            set
            {
                _unreadCount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(UnreadVisibility));             }
        }

        public System.Windows.Visibility UnreadVisibility => UnreadCount > 0 ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

        public System.Windows.Media.ImageSource AvatarImage { get; set; }
        public System.Windows.Visibility AvatarVisibility => AvatarImage != null ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        public System.Windows.Visibility InitialsVisibility => AvatarImage == null ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
        }
    }

    public class SearchResultItem
    {
        public string Username { get; set; }
        public string DisplayName { get; set; }         public System.Windows.Media.ImageSource AvatarImage { get; set; }
        public System.Windows.Visibility AvatarVisibility => AvatarImage != null ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        public System.Windows.Visibility InitialsVisibility => AvatarImage == null ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
    }
    public class ProfilePhotoItem
    {
        public int Id { get; set; }
        public System.Windows.Media.ImageSource Image { get; set; }
    }
}
