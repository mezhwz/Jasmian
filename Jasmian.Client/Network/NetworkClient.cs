using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Jasmian.Client.Network
{
    public class NetworkClient
    {
        private TcpClient _client;
        private StreamReader _reader;
        private StreamWriter _writer;

        private string _ip;
        private int _port;
        private bool _isIntentionalDisconnect = false;

        public event Action<string[]> OnCommandReceived;
        public event Action OnConnectionLost;
        public event Action OnReconnected;

        public bool Connect(string ip, int port)
        {
            _ip = ip;
            _port = port;
            _isIntentionalDisconnect = false;

            try
            {
                _client = new TcpClient(ip, port);
                var stream = _client.GetStream();
                _reader = new StreamReader(stream, Encoding.UTF8);
                _writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

                _ = Task.Run(() => ReceiveLoopAsync());
                return true;
            }
            catch
            {
                return false;
            }
        }

        private async Task ReceiveLoopAsync()
        {
            try
            {
                while (true)
                {
                    string message = await _reader.ReadLineAsync();
                    if (message == null) throw new Exception("Соединение разорвано сервером");

                    string[] parts = message.Split('|');
                    OnCommandReceived?.Invoke(parts);
                }
            }
            catch
            {
                if (!_isIntentionalDisconnect)
                {
                    OnConnectionLost?.Invoke();
                    _ = ReconnectLoopAsync();
                }
            }
        }

        private async Task ReconnectLoopAsync()
        {
            while (!_isIntentionalDisconnect)
            {
                await Task.Delay(3000);

                try
                {
                    _client = new TcpClient(_ip, _port);
                    var stream = _client.GetStream();
                    _reader = new StreamReader(stream, Encoding.UTF8);
                    _writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

                    OnReconnected?.Invoke();
                    _ = Task.Run(() => ReceiveLoopAsync());
                    break;
                }
                catch { }
            }
        }

        public async Task SendAsync(string message)
        {
            try
            {
                if (_client != null && _client.Connected)
                {
                    await _writer.WriteLineAsync(message);
                }
            }
            catch { }
        }

        public void Disconnect(bool intentional = true)
        {
            _isIntentionalDisconnect = intentional;
            _reader?.Close();
            _writer?.Close();
            _client?.Close();
        }
    }
}