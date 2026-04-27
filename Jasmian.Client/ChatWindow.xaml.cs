using Jasmian.Client.Helpers;
using Jasmian.Client.Models;
using Jasmian.Client.Network;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Jasmian.Client
{
    public partial class ChatWindow : Window
    {
        private NetworkClient _network;

        private ObservableCollection<ChatMessage> _messages = new ObservableCollection<ChatMessage>();
        private ObservableCollection<ChatListItem> _chatList = new ObservableCollection<ChatListItem>();
        private ObservableCollection<SearchResultItem> _searchResults = new ObservableCollection<SearchResultItem>();
        private ObservableCollection<ProfilePhotoItem> _myPhotos = new ObservableCollection<ProfilePhotoItem>();

        private Dictionary<string, string> _publicKeys = new Dictionary<string, string>();
        private List<ImageSource> _userPhotos = new List<ImageSource>();
        private DispatcherTimer _typingTimer;
        private DateTime _lastTypingTime;

        private string _myUsername;
        private bool _isLoggingOut = false;
        private string _pendingAttachedImageBase64 = null;
        private string _viewingUsername;
        private int _currentUserPhotoIndex = 0;
        private int _currentMyPhotoIndex = 0;
        private int _editingMessageId = 0;
        private string _myPrivateKey = "";

        public ChatWindow(NetworkClient network, string myUsername)
        {
            InitializeComponent();
            _network = network;
            _myUsername = myUsername;
            Title = $"Jasmian - {_myUsername}";

            if (System.IO.File.Exists("private_key.xml"))
            {
                _myPrivateKey = System.IO.File.ReadAllText("private_key.xml");
            }

            ChatHistory.ItemsSource = _messages;
            ChatsList.ItemsSource = _chatList;
            SearchResultsList.ItemsSource = _searchResults;

            _typingTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _typingTimer.Tick += (s, e) =>
            {
                _typingTimer.Stop();
                ActiveChatStatus.Text = "в сети";
                ActiveChatStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#32CD32"));
            };

            _network.OnCommandReceived += Network_OnCommandReceived;
            _network.OnConnectionLost += Network_OnConnectionLost;
            _network.OnReconnected += Network_OnReconnected;

            _ = _network.SendAsync("GET_CHATS");

            MyInitials.Text = _myUsername[0].ToString().ToUpper();
            _ = _network.SendAsync($"GET_FULL_PROFILE|{_myUsername}");
        }

        private void Network_OnCommandReceived(string[] parts)
        {
            string command = parts[0];

            Dispatcher.Invoke(() =>
            {
                if (command == "HISTORY_START")
                {
                    _messages.Clear();
                    ActiveChatName.Text = parts[2];
                    ActiveChatName.Tag = parts[1];

                    string avatarBase64 = parts.Length > 3 ? parts[3] : "NONE";

                    var chatInList = _chatList.FirstOrDefault(c => c.Username == parts[1]);
                    if (chatInList != null)
                    {
                        chatInList.UnreadCount = 0;
                        ChatsList.Items.Refresh();
                    }

                    if (avatarBase64 != "NONE")
                    {
                        ActiveChatAvatarBrush.ImageSource = GetImageFromBase64(avatarBase64);
                        ActiveChatAvatar.Visibility = Visibility.Visible;
                        ActiveChatInitialsBorder.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        ActiveChatInitials.Text = parts[1][0].ToString().ToUpper();
                        ActiveChatAvatar.Visibility = Visibility.Collapsed;
                        ActiveChatInitialsBorder.Visibility = Visibility.Visible;
                    }
                }
                else if (command == "MSG" || command == "NEW_MSG")
                {
                    string senderUsername = parts[1];
                    string senderDName = parts[2];
                    string encryptedPayload = parts[3];                     string time = parts[4];
                    string serverAttachedImage = parts[5];                     int msgId = parts.Length > 6 ? int.Parse(parts[6]) : 0;
                    bool isEdited = parts.Length > 7 && bool.Parse(parts[7]);

                                        string decryptedText = encryptedPayload;
                    string decryptedImageBase64 = "NONE";

                                                            string decrypted = Helpers.CryptoHelper.DecryptMessage(encryptedPayload, _myPrivateKey);

                                        if (decrypted.Contains("♦"))
                    {
                        var payloadParts = decrypted.Split('♦');
                        decryptedText = payloadParts[0];
                        decryptedImageBase64 = payloadParts[1];
                    }
                    else
                    {
                        decryptedText = decrypted;                     }
                    
                    bool isChatActive = (ActiveChatName.Tag as string) == senderUsername;

                    if (command == "MSG" || (command == "NEW_MSG" && isChatActive))
                    {
                        var newMessage = new ChatMessage
                        {
                            Id = msgId,
                            Sender = senderDName,
                            Text = (decryptedText == " " && decryptedImageBase64 != "NONE") ? null : decryptedText,
                            Time = time,
                            IsMe = senderUsername == _myUsername,
                            AttachedImage = GetImageFromBase64(decryptedImageBase64),
                            IsEdited = isEdited,
                            Base64Data = decryptedImageBase64
                        };

                        _messages.Add(newMessage);

                        if (ChatHistory.Items.Count > 0)
                            ChatHistory.ScrollIntoView(ChatHistory.Items[ChatHistory.Items.Count - 1]);
                    }

                    if (command == "NEW_MSG")
                    {
                        System.Media.SystemSounds.Asterisk.Play();
                        _ = _network.SendAsync("GET_CHATS");

                        if (isChatActive)
                        {
                            _ = _network.SendAsync($"MARK_READ|{senderUsername}");
                            Task.Run(async () => { await Task.Delay(300); await _network.SendAsync("GET_CHATS"); });
                        }
                    }
                }
                else if (command == "CHAT_LIST_START")
                {
                    _chatList.Clear();
                }
                else if (command == "CHAT_STATUS")
                {
                    string target = parts[1];
                    bool isOnline = bool.Parse(parts[2]);
                                        if ((ActiveChatName.Tag as string) == target)
                    {
                        ActiveChatStatus.Text = isOnline ? "в сети" : "был(а) недавно";
                        ActiveChatStatus.Foreground = isOnline ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#32CD32")) : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#AAA"));
                    }
                }
                else if (command == "STATUS")
                {
                    string targetUser = parts[1];
                    bool isOnline = bool.Parse(parts[2]);

                    foreach (var item in _chatList)
                    {
                        if (item.Username == targetUser)
                        {
                            item.IsOnline = isOnline;
                            break;
                        }
                    }
                    ChatsList.Items.Refresh();

                                        if ((ActiveChatName.Tag as string) == targetUser)
                    {
                        ActiveChatStatus.Text = isOnline ? "в сети" : "был(а) недавно";
                        ActiveChatStatus.Foreground = isOnline ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#32CD32")) : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#AAA"));
                    }
                }
                else if (command == "SEND_SUCCESS")
                {
                    if (parts.Length > 1 && int.TryParse(parts[1], out int newId))
                    {
                        var lastMe = _messages.LastOrDefault(m => m.IsMe && m.Id == 0);
                        if (lastMe != null) lastMe.Id = newId;
                    }
                }
                else if (command == "TYPING_EVENT")
                {
                    string typist = parts[1];
                                        if ((ActiveChatName.Tag as string) == typist)
                    {
                        ActiveChatStatus.Text = "печатает...";
                        ActiveChatStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF1493"));
                        _typingTimer.Stop();
                        _typingTimer.Start();
                    }
                }
                else if (command == "READ_RECEIPT")
                {
                    string readerName = parts[1];
                                        if ((ActiveChatName.Tag as string) == readerName)
                    {
                        foreach (var msg in _messages)
                        {
                            if (msg.IsMe && !msg.IsRead)
                            {
                                msg.IsRead = true;
                            }
                        }
                    }
                }
                else if (command == "ERROR")
                {
                    ShowToast(parts[1]);
                }
                else if (command == "PROFILE_INFO_FULL")
                {
                    string username = parts[1];

                                        if (username == _myUsername)
                    {
                        DisplayNameBox.Text = parts[2];
                        PhoneBox.Text = parts[3] == "Не указан" ? "" : parts[3];
                        BioBox.Text = parts[4] == "О себе" ? "" : parts[4];
                        if (DateTime.TryParse(parts[5], out DateTime bday))
                            BirthdayPicker.SelectedDate = bday;

                        _myPhotos.Clear();
                        _currentMyPhotoIndex = 0;

                                                int photoCount = parts.Length > 6 ? int.Parse(parts[6]) : 0;
                        if (photoCount == 0)
                        {
                                                        MyAvatar.Visibility = Visibility.Collapsed;
                            MyInitialsBorder.Visibility = Visibility.Visible;

                                                        MainAvatarImage.Visibility = Visibility.Collapsed;
                            MainAvatarBorder.Visibility = Visibility.Visible;
                        }
                    }
                                        else if (username == _viewingUsername)
                    {
                        UserDisplayNameText.Text = parts[2];
                        UserPhoneText.Text = parts[3];
                        UserBioText.Text = parts[4];
                        UserUsernameText.Text = "@" + parts[1];
                    }
                }
                else if (command == "PROFILE_PHOTO")
                {
                    string username = parts[1];
                    int photoId = int.Parse(parts[2]);
                    var img = GetImageFromBase64(parts[3]);

                    if (img != null)
                    {
                                                if (username == _myUsername)
                        {
                            _myPhotos.Add(new ProfilePhotoItem { Id = photoId, Image = img });

                                                        if (_myPhotos.Count == 1)
                            {
                                                                MainAvatarBrush.ImageSource = img;
                                MainAvatarImage.Visibility = Visibility.Visible;
                                MainAvatarBorder.Visibility = Visibility.Collapsed;

                                                                MyAvatarBrush.ImageSource = img;
                                MyAvatar.Visibility = Visibility.Visible;
                                MyInitialsBorder.Visibility = Visibility.Collapsed;
                            }
                        }
                                                else if (username == _viewingUsername)
                        {
                            _userPhotos.Add(img);
                            if (_userPhotos.Count == 1)
                            {
                                ProfileImageBrush.ImageSource = _userPhotos[0];
                                _currentUserPhotoIndex = 0;
                            }
                        }
                    }
                }
                
                else if (command == "PROFILE_UPDATED")
                {
                    _ = _network.SendAsync("GET_CHATS");

                    if (ActiveChatName.Tag as string == parts[1])
                    {
                        _ = _network.SendAsync($"HISTORY|{parts[1]}");
                    }

                    if (parts[1] == _myUsername)
                    {
                        MyAvatar.Visibility = Visibility.Collapsed;
                        _ = _network.SendAsync($"GET_FULL_PROFILE|{_myUsername}");
                    }
                }
                else if (command == "CHAT_ITEM")
                {
                    string username = parts[1];
                    string displayName = parts[2];
                    string lastMsgEncrypted = parts[3];

                                        string decryptedPreview = Helpers.CryptoHelper.DecryptMessage(lastMsgEncrypted, _myPrivateKey);

                                        if (decryptedPreview.Contains("♦"))
                    {
                        decryptedPreview = decryptedPreview.Split('♦')[0];
                    }

                                        string finalPreview = decryptedPreview.Length > 25 ? decryptedPreview.Substring(0, 25) + "..." : decryptedPreview;

                    _chatList.Add(new ChatListItem
                    {
                        Username = username,
                        DisplayName = displayName,
                        LastMessage = finalPreview,
                        Time = parts[4],
                        IsOnline = bool.Parse(parts[5]),
                        AvatarImage = GetImageFromBase64(parts[6]),
                        UnreadCount = parts.Length > 7 ? int.Parse(parts[7]) : 0
                    });
                }
                else if (command == "CHAT_LIST_END")
                {
                    string activeUser = ActiveChatName.Tag as string;
                    if (!string.IsNullOrEmpty(activeUser))
                    {
                        var itemToSelect = _chatList.FirstOrDefault(c => c.Username == activeUser);
                        if (itemToSelect != null)
                        {
                            ChatsList.SelectedItem = itemToSelect;
                        }
                    }
                }
                else if (command == "SEARCH_RESULTS")
                {
                    var users = parts.Skip(1).Where(u => !string.IsNullOrWhiteSpace(u)).ToList();
                    _searchResults.Clear();
                    foreach (var u in users)
                    {
                        var data = u.Split('~');
                        _searchResults.Add(new SearchResultItem
                        {
                            Username = data[0],
                            DisplayName = data[1],                             AvatarImage = GetImageFromBase64(data.Length > 2 ? data[2] : "NONE")
                        });
                    }
                    SearchResultsList.Visibility = _searchResults.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
                }
                else if (command == "UPDATE_PROFILE_SUCCESS")
                {
                    ShowToast("Профиль обновлен!");
                    CloseOverlay_Click(null, null);                 }
                else if (command == "ADD_PHOTO_SUCCESS" || command == "REMOVE_PHOTO_SUCCESS")
                {
                    _myPhotos.Clear();
                    _ = _network.SendAsync($"GET_FULL_PROFILE|{_myUsername}");
                }
                else if (command == "MSG_EDITED")
                {
                    int msgId = int.Parse(parts[1]);
                    string encryptedPayload = parts[2]; 
                                        var msg = _messages.FirstOrDefault(m => m.Id == msgId);
                    if (msg != null)
                    {
                                                string decrypted = Helpers.CryptoHelper.DecryptMessage(encryptedPayload, _myPrivateKey);
                        string finalMsgText = decrypted;
                        string finalImageBase64 = "NONE";

                                                if (decrypted.Contains("♦"))
                        {
                            var payloadParts = decrypted.Split('♦');
                            finalMsgText = payloadParts[0];
                            finalImageBase64 = payloadParts[1];
                        }

                                                msg.Text = (finalMsgText == " " && finalImageBase64 != "NONE") ? null : finalMsgText;
                        msg.IsEdited = true;

                        msg.AttachedImage = GetImageFromBase64(finalImageBase64);
                        msg.Base64Data = finalImageBase64;
                    }

                                        _ = _network.SendAsync("GET_CHATS");
                }
                else if (command == "MSG_DELETED")
                {
                    int msgId = int.Parse(parts[1]);

                                        var msg = _messages.FirstOrDefault(m => m.Id == msgId);
                    if (msg != null)
                    {
                        _messages.Remove(msg);
                    }

                                        _ = _network.SendAsync("GET_CHATS");
                }
                else if (command == "PUBLIC_KEY" && parts.Length >= 3)
                {
                    string targetUsername = parts[1];
                    string pubKey = parts[2];

                                        _publicKeys[targetUsername] = pubKey;
                }
            });
        }

        private void Network_OnConnectionLost()
        {
            Dispatcher.Invoke(() =>
            {
                ReconnectBanner.Visibility = Visibility.Visible;
            });
        }

        private void Network_OnReconnected()
        {
            Dispatcher.Invoke(async () =>
            {
                ReconnectBanner.Visibility = Visibility.Collapsed;
                ShowToast("Соединение восстановлено!");

                var session = SessionManager.LoadSession();
                if (session != null)
                {
                    await _network.SendAsync($"LOGIN|{session.Username}|{session.Password}");

                    await Task.Delay(500);
                    await _network.SendAsync("GET_CHATS");
                }
            });
        }
        private ImageSource GetImageFromBase64(string base64)
        {
            if (string.IsNullOrWhiteSpace(base64) || base64 == "NONE") return null;
            try
            {
                byte[] imageBytes = Convert.FromBase64String(base64);
                using (MemoryStream ms = new MemoryStream(imageBytes))
                {
                    BitmapImage bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = ms;
                    bitmap.EndInit();
                    return bitmap;
                }
            }
            catch { return null; }
        }
        private void OpenOverlay(FrameworkElement panel)
        {
            OverlayContainer.Visibility = Visibility.Visible;
            MyProfilePanel.Visibility = Visibility.Collapsed;
            UserProfilePanel.Visibility = Visibility.Collapsed;

            panel.Visibility = Visibility.Visible;

                        var blurAnim = new System.Windows.Media.Animation.DoubleAnimation(0, 15, TimeSpan.FromMilliseconds(250));
            MainBlur.BeginAnimation(System.Windows.Media.Effects.BlurEffect.RadiusProperty, blurAnim);

                        var fadeAnim = new System.Windows.Media.Animation.DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(250));
            panel.BeginAnimation(UIElement.OpacityProperty, fadeAnim);
        }

        private void CloseOverlay_Click(object sender, RoutedEventArgs e)
        {
                        var blurAnim = new System.Windows.Media.Animation.DoubleAnimation(15, 0, TimeSpan.FromMilliseconds(200));
            MainBlur.BeginAnimation(System.Windows.Media.Effects.BlurEffect.RadiusProperty, blurAnim);

                        var fadeAnim = new System.Windows.Media.Animation.DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200));
            fadeAnim.Completed += (s, ev) => OverlayContainer.Visibility = Visibility.Collapsed;

            if (MyProfilePanel.Visibility == Visibility.Visible)
                MyProfilePanel.BeginAnimation(UIElement.OpacityProperty, fadeAnim);
            else if (UserProfilePanel.Visibility == Visibility.Visible)
                UserProfilePanel.BeginAnimation(UIElement.OpacityProperty, fadeAnim);
        }

        private void PreventClose_MouseDown(object sender, MouseButtonEventArgs e) => e.Handled = true;

        private async void Send_Click(object sender, RoutedEventArgs e)
        {
            string msg = MessageInput.Text;
            string target = ActiveChatName.Tag as string;

            if (string.IsNullOrEmpty(target) || (string.IsNullOrWhiteSpace(msg) && _pendingAttachedImageBase64 == null))
            {
                ShowToast("Выберите чат и введите текст или выберите картинку.");
                return;
            }

                        if (!_publicKeys.ContainsKey(target))
            {
                ShowToast("🔑 Обмен ключами... Нажмите отправить еще раз.");
                await _network.SendAsync($"GET_PUBLIC_KEY|{target}");
                return;
            }

            string targetPublicKey = _publicKeys[target];
            string safeMsg = string.IsNullOrWhiteSpace(msg) ? " " : msg;
            string attachedData = _pendingAttachedImageBase64 ?? "NONE";

                        string payload = $"{safeMsg}♦{attachedData}";

                        string encryptedPayload = Helpers.CryptoHelper.EncryptMessage(payload, targetPublicKey);

                        if (_editingMessageId != 0)
            {
                                await _network.SendAsync($"EDIT_MSG|{_editingMessageId}|{encryptedPayload}");
                CancelEdit_Click(null, null);
                return;
            }

                                    await _network.SendAsync($"SEND|{target}|{encryptedPayload}|NONE");

                        _messages.Add(new ChatMessage
            {
                Id = 0,
                Sender = _myUsername,
                Text = string.IsNullOrWhiteSpace(msg) ? null : msg,
                Time = DateTime.Now.ToString("HH:mm"),
                IsMe = true,
                AttachedImage = GetImageFromBase64(attachedData),
                IsEdited = false,
                Base64Data = attachedData
            });

            ChatHistory.ScrollIntoView(_messages[_messages.Count - 1]);

            MessageInput.Clear();
            RemoveAttach_Click(null, null);

            _ = _network.SendAsync("GET_CHATS");
        }

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            _isLoggingOut = true;
            SessionManager.ClearSession();
            _network.Disconnect(intentional: true);

            var loginWindow = new LoginWindow();
            loginWindow.Show();

            this.Close();
        }

        private void Profile_Click(object sender, RoutedEventArgs e)
        {
            OpenOverlay(MyProfilePanel);
            AvatarInitials.Text = _myUsername[0].ToString().ToUpper();
            _ = _network.SendAsync($"GET_FULL_PROFILE|{_myUsername}");
        }

        private void ChatHeader_Click(object sender, MouseButtonEventArgs e)
        {
            string target = ActiveChatName.Tag as string;
            if (string.IsNullOrEmpty(target)) return;

            _viewingUsername = target;             OpenOverlay(UserProfilePanel);
            _userPhotos.Clear();
            _ = _network.SendAsync($"GET_FULL_PROFILE|{target}");
        }

        private void Attach_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Title = "Выберите картинку для отправки",
                Filter = "Изображения|*.jpg;*.jpeg;*.png;*.bmp",
                Multiselect = false
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                                        BitmapImage bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(openFileDialog.FileName);
                                        bitmap.DecodePixelWidth = 800;
                    bitmap.EndInit();

                                        JpegBitmapEncoder encoder = new JpegBitmapEncoder { QualityLevel = 80 };
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));

                    using (MemoryStream ms = new MemoryStream())
                    {
                        encoder.Save(ms);
                        byte[] imageBytes = ms.ToArray();
                        _pendingAttachedImageBase64 = Convert.ToBase64String(imageBytes);
                    }

                                        PreviewImage.Source = bitmap;
                    AttachPreview.Visibility = Visibility.Visible;
                }
                catch (Exception ex)
                {
                    ShowToast("Ошибка при загрузке картинки: " + ex.Message);
                }
            }
        }

        private void RemoveAttach_Click(object sender, RoutedEventArgs e)
        {
            _pendingAttachedImageBase64 = null;
            AttachPreview.Visibility = Visibility.Collapsed;
            PreviewImage.Source = null;
        }

        private async void SaveProfile_Click(object sender, RoutedEventArgs e)
        {
            string dName = string.IsNullOrWhiteSpace(DisplayNameBox.Text) ? _myUsername : DisplayNameBox.Text;
            string phone = string.IsNullOrWhiteSpace(PhoneBox.Text) ? "Не указан" : PhoneBox.Text;
            string bio = string.IsNullOrWhiteSpace(BioBox.Text) ? "О себе" : BioBox.Text;
            string bday = BirthdayPicker.SelectedDate?.ToShortDateString() ?? "Не указана";

            await _network.SendAsync($"UPDATE_PROFILE_BASIC|{dName}|{phone}|{bio}|{bday}");
        }

        private async void AddPhoto_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog { Filter = "Images|*.jpg;*.png;*.jpeg" };
            if (ofd.ShowDialog() == true)
            {
                byte[] bytes = File.ReadAllBytes(ofd.FileName);
                string base64 = Convert.ToBase64String(bytes);
                await _network.SendAsync($"ADD_PHOTO|{base64}");
            }
        }

        private void NextPhoto_Click(object sender, RoutedEventArgs e)
        {
            if (_userPhotos.Count <= 1) return;
            _currentUserPhotoIndex = (_currentUserPhotoIndex + 1) % _userPhotos.Count;
            ProfileImageBrush.ImageSource = _userPhotos[_currentUserPhotoIndex];
        }

        private void NextMyPhoto_Click(object sender, RoutedEventArgs e)
        {
            if (_myPhotos.Count <= 1) return;
            _currentMyPhotoIndex = (_currentMyPhotoIndex + 1) % _myPhotos.Count;
            MainAvatarBrush.ImageSource = _myPhotos[_currentMyPhotoIndex].Image;
        }

        private void PrevMyPhoto_Click(object sender, RoutedEventArgs e)
        {
            if (_myPhotos.Count <= 1) return;
            _currentMyPhotoIndex = (_currentMyPhotoIndex - 1 + _myPhotos.Count) % _myPhotos.Count;
            MainAvatarBrush.ImageSource = _myPhotos[_currentMyPhotoIndex].Image;
        }

        private async void RemoveCurrentPhoto_Click(object sender, RoutedEventArgs e)
        {
            if (_myPhotos.Count == 0) return;
            int photoId = _myPhotos[_currentMyPhotoIndex].Id;
            await _network.SendAsync($"REMOVE_PHOTO|{photoId}");
        }

        private void PrevPhoto_Click(object sender, RoutedEventArgs e)
        {
            if (_userPhotos.Count <= 1) return;
            _currentUserPhotoIndex = (_currentUserPhotoIndex - 1 + _userPhotos.Count) % _userPhotos.Count;
            ProfileImageBrush.ImageSource = _userPhotos[_currentUserPhotoIndex];
        }
        private async void ChatsList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (ChatsList.SelectedItem is ChatListItem selectedChat)
            {
                if (ActiveChatName.Tag as string == selectedChat.Username) return;

                ActiveChatName.Text = selectedChat.DisplayName;
                ActiveChatName.Tag = selectedChat.Username;
                await _network.SendAsync($"HISTORY|{selectedChat.Username}");
                await _network.SendAsync($"GET_PUBLIC_KEY|{selectedChat.Username}");
            }
        }

        private async void MessageInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (ActiveChatName.Text == "Выберите собеседника слева") return;

            if ((DateTime.Now - _lastTypingTime).TotalSeconds > 2)
            {
                _lastTypingTime = DateTime.Now;
                await _network.SendAsync($"TYPING|{ActiveChatName.Text}");
            }
        }

        private void MessageInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Send_Click(sender, e);
            }
        }
        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (SearchResultsList == null || SearchBox == null) return;

            string query = SearchBox.Text;
            if (string.IsNullOrWhiteSpace(query) || query == "Поиск...")
            {
                SearchResultsList.Visibility = Visibility.Collapsed;
                return;
            }

            _ = _network.SendAsync($"SEARCH|{query}");
        }

        private void SearchBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (SearchBox.Text == "Поиск...") SearchBox.Text = "";
        }

        private void SearchBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(SearchBox.Text)) SearchBox.Text = "Поиск...";
        }

        private async void SearchResultsList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (SearchResultsList.SelectedItem is SearchResultItem selectedUser)
            {
                SearchBox.Text = "Поиск...";
                SearchResultsList.Visibility = Visibility.Collapsed;
                ActiveChatName.Text = selectedUser.DisplayName;
                ActiveChatName.Tag = selectedUser.Username;
                await _network.SendAsync($"HISTORY|{selectedUser.Username}");
                await _network.SendAsync($"GET_PUBLIC_KEY|{selectedUser.Username}");
            }
        }

        private async void ShowToast(string message)
        {
            NotificationText.Text = message;
            NotificationToast.Visibility = Visibility.Visible;
            await Task.Delay(3000);
            NotificationToast.Visibility = Visibility.Collapsed;
        }
        private ChatMessage GetMessageFromMenuItem(RoutedEventArgs e)
        {
            if (e.Source is MenuItem menuItem && menuItem.Parent is ContextMenu contextMenu)
            {
                if (contextMenu.PlacementTarget is Border border)
                {
                    return border.DataContext as ChatMessage;
                }
            }
            return null;
        }

        private void CopyMessage_Click(object sender, RoutedEventArgs e)
        {
            var msg = GetMessageFromMenuItem(e);
            if (msg != null && !string.IsNullOrWhiteSpace(msg.Text))
            {
                Clipboard.SetText(msg.Text);
                ShowToast("Текст скопирован!");
            }
        }

        private void EditMessage_Click(object sender, RoutedEventArgs e)
        {
            var msg = GetMessageFromMenuItem(e);

            if (msg != null && msg.IsMe && msg.Id != 0)
            {
                _editingMessageId = msg.Id;

                EditOldMessageText.Text = string.IsNullOrWhiteSpace(msg.Text) ? "[Только фото]" : msg.Text;
                MessageInput.Text = msg.Text;

                if (!string.IsNullOrEmpty(msg.Base64Data) && msg.Base64Data != "NONE")
                {
                    _pendingAttachedImageBase64 = msg.Base64Data;
                    PreviewImage.Source = msg.AttachedImage;
                    AttachPreview.Visibility = Visibility.Visible;
                }
                else
                {
                    RemoveAttach_Click(null, null);
                }

                EditPreviewPanel.Visibility = Visibility.Visible;
                MessageInput.Focus();
                MessageInput.CaretIndex = MessageInput.Text.Length;
            }
        }

        private async void DeleteMessage_Click(object sender, RoutedEventArgs e)
        {
            var msg = GetMessageFromMenuItem(e);
            if (msg != null && msg.IsMe && msg.Id != 0)
            {
                if (MessageBox.Show("Вы уверены, что хотите удалить это сообщение?", "Удаление", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    await _network.SendAsync($"DELETE_MSG|{msg.Id}");
                }
            }
        }

        private void CancelEdit_Click(object sender, RoutedEventArgs e)
        {
            _editingMessageId = 0;
            EditPreviewPanel.Visibility = Visibility.Collapsed;
            MessageInput.Clear();
            RemoveAttach_Click(null, null);
        }

        protected override void OnClosed(EventArgs e)
        {
            if (!_isLoggingOut)
            {
                _network.Disconnect(intentional: true);
                Application.Current.Shutdown();
            }
            base.OnClosed(e);
        }
    }
}