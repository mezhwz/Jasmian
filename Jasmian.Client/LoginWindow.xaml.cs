using System.Threading.Tasks;
using System.Windows;
using Jasmian.Client.Network;
using Jasmian.Client.Helpers;
using System.IO;

namespace Jasmian.Client
{
    public partial class LoginWindow : Window
    {
        private NetworkClient _network;
        private string _attemptingUsername;
        private string _attemptingPassword;

        public LoginWindow()
        {
            InitializeComponent();
            _network = new NetworkClient();

            string ipAddress = "127.0.0.1";             string configFile = "server_ip.txt";

            try
            {
                if (File.Exists(configFile))
                {
                    ipAddress = File.ReadAllText(configFile).Trim();
                }
                else
                {
                    File.WriteAllText(configFile, ipAddress);
                }
            }
            catch
            {

            }

            if (!_network.Connect(ipAddress, 8888))
            {
                ShowToast("Не удалось подключиться к серверу.");
            }
            else
            {
                _network.OnCommandReceived += Network_OnCommandReceived;
                TryAutoLogin();
            }
        }

        private async void TryAutoLogin()
        {
            var session = SessionManager.LoadSession();
            if (session != null)
            {
                _attemptingUsername = session.Username;
                _attemptingPassword = session.Password;
                string myPublicKey = GetOrGeneratePublicKey();
                await _network.SendAsync($"LOGIN|{session.Username}|{session.Password}|{myPublicKey}");
            }
        }

        private void Network_OnCommandReceived(string[] parts)
        {
            string command = parts[0];

            Dispatcher.Invoke(() =>
            {
                if (command == "REGISTER_SUCCESS")
                {
                    ShowToast("Регистрация успешна! Теперь вы можете войти.");
                }
                else if (command == "LOGIN_SUCCESS")
                {
                    SessionManager.SaveSession(_attemptingUsername, _attemptingPassword);

                    _network.OnCommandReceived -= Network_OnCommandReceived;

                    var chatWindow = new ChatWindow(_network, _attemptingUsername);
                    chatWindow.Show();

                    this.Close();
                }
                else if (command == "ERROR")
                {
                    ShowToast(parts[1]);
                    SessionManager.ClearSession();
                }
            });
        }

        private async void Register_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(LoginBox.Text) || string.IsNullOrWhiteSpace(PasswordBox.Password)) return;
            string myPublicKey = GetOrGeneratePublicKey();
            await _network.SendAsync($"REGISTER|{LoginBox.Text}|{PasswordBox.Password}|{myPublicKey}");
        }

        private string GetOrGeneratePublicKey()
        {
            string pubFile = "public_key.xml";
            string privFile = "private_key.xml";

            if (File.Exists(pubFile) && File.Exists(privFile))
            {
                                return File.ReadAllText(pubFile);
            }
            else
            {
                                var keys = Helpers.CryptoHelper.GenerateRSAKeys();

                                File.WriteAllText(pubFile, keys.PublicKey);
                File.WriteAllText(privFile, keys.PrivateKey);

                                File.SetAttributes(privFile, FileAttributes.Hidden);

                return keys.PublicKey;
            }
        }

        private async void Login_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(LoginBox.Text) || string.IsNullOrWhiteSpace(PasswordBox.Password)) return;

            _attemptingUsername = LoginBox.Text;
            _attemptingPassword = PasswordBox.Password;

            string myPublicKey = GetOrGeneratePublicKey();
            await _network.SendAsync($"LOGIN|{_attemptingUsername}|{_attemptingPassword}|{myPublicKey}");
        }

        private async void ShowToast(string message)
        {
            NotificationText.Text = message;
            NotificationToast.Visibility = Visibility.Visible;
            await Task.Delay(3000);
            NotificationToast.Visibility = Visibility.Collapsed;
        }
    }
}