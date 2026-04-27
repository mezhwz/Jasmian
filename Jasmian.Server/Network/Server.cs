using Jasmian.Server.Helpers;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Jasmian.Server.Network
{
    public class Server
    {
        private TcpListener _listener;
        private List<ClientHandler> _clients = new List<ClientHandler>();

        public async Task StartAsync(int port)
        {

            _listener = new TcpListener(IPAddress.Parse(GlobalConfig.BindIp), port);
            _listener.Start();
            Console.WriteLine($"[SERVER] Сервер Jasmian запущен на {GlobalConfig.BindIp}:{port}");

            while (true)
            {
                TcpClient tcpClient = await _listener.AcceptTcpClientAsync();
                ClientHandler clientHandler = new ClientHandler(tcpClient, this);

                lock (_clients) { _clients.Add(clientHandler); }

                _ = Task.Run(() => clientHandler.ProcessAsync());
            }
        }

        public void RemoveClient(ClientHandler client)
        {
            lock (_clients) { _clients.Remove(client); }
        }
        public ClientHandler GetClientByUsername(string username)
        {
            lock (_clients)
            {
                return _clients.FirstOrDefault(c => c.CurrentUser != null && c.CurrentUser.Username == username);
            }
        }

        public bool IsUserOnline(string username)
        {
            return GetClientByUsername(username) != null;
        }

        public async Task BroadcastAsync(string message, ClientHandler excludeClient = null)
        {
            List<ClientHandler> targets;
            lock (_clients)
            {
                targets = _clients.Where(c => c != excludeClient && c.CurrentUser != null).ToList();
            }

            foreach (var client in targets)
            {
                await client.SendMessageAsync(message);
            }
        }
    }
}