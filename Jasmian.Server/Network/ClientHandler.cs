using Jasmian.Server.Data;
using Jasmian.Server.Helpers;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Jasmian.Server.Network
{
    public class ClientHandler
    {
        private TcpClient _client;
        private Server _server;
        private NetworkStream _stream;
        private StreamReader _reader;
        private StreamWriter _writer;

        public User CurrentUser { get; private set; }

        public ClientHandler(TcpClient client, Server server)
        {
            _client = client;
            _server = server;
            _stream = client.GetStream();
            _reader = new StreamReader(_stream, Encoding.UTF8);
            _writer = new StreamWriter(_stream, Encoding.UTF8) { AutoFlush = true };
        }

        public async Task ProcessAsync()
        {
            try
            {
                Console.WriteLine("Новый клиент подключился!");

                while (true)
                {
                    string message = await _reader.ReadLineAsync();
                    if (message == null) break;

                    Console.WriteLine($"[Получено сырое сообщение]: {message}");

                                        await HandleCommandAsync(message);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка соединения: {ex.Message}");
            }
            finally
            {
                Close();
            }
        }

        private async Task HandleCommandAsync(string commandLine)
        {
            string[] parts = commandLine.Split('|');
            string command = parts[0];

            using (var db = new JasmianDbContext())
            {
                if (command == "REGISTER" && parts.Length >= 4)
                {
                    string username = parts[1];
                    string password = parts[2];
                    string publicKey = parts[3]; 
                    if (db.Users.Any(u => u.Username == username))
                    {
                        await SendMessageAsync("ERROR|Пользователь с таким именем уже существует.");
                    }
                    else
                    {
                                                var newUser = new User { Username = username, PasswordHash = SecurityHelper.HashPassword(password), PublicKey = publicKey };
                        db.Users.Add(newUser);
                        await db.SaveChangesAsync();
                        await SendMessageAsync("REGISTER_SUCCESS");
                        Console.WriteLine($"Зарегистрирован новый пользователь: {username}");
                    }
                }
                else if (command == "LOGIN" && parts.Length >= 4)
                {
                    string username = parts[1];
                    string password = parts[2];
                    string publicKey = parts[3];                     string hash = SecurityHelper.HashPassword(password);

                    var user = db.Users.FirstOrDefault(u => u.Username == username && u.PasswordHash == hash);

                    if (user != null)
                    {
                        CurrentUser = user;

                                                user.PublicKey = publicKey;
                        await db.SaveChangesAsync();

                        await SendMessageAsync($"LOGIN_SUCCESS|{user.Id}");
                        Console.WriteLine($"Пользователь {username} успешно авторизовался.");
                        await _server.BroadcastAsync($"STATUS|{CurrentUser.Username}|True", this);
                    }
                    else
                    {
                        await SendMessageAsync("ERROR|Неверный логин или пароль.");
                    }
                }
                else if (command == "SEND" && parts.Length >= 3)
                {
                    if (CurrentUser == null) return;

                    string receiverUsername = parts[1];
                    string messageText = parts[2];

                                        string attachedImage = parts.Length > 3 ? parts[3] : "NONE";

                    var receiver = db.Users.FirstOrDefault(u => u.Username == receiverUsername);
                    if (receiver != null)
                    {
                        var message = new Message
                        {
                            SenderId = CurrentUser.Id,
                            ReceiverId = receiver.Id,
                            Text = messageText,
                            AttachedImageBase64 = attachedImage == "NONE" ? null : attachedImage,
                            Timestamp = DateTime.Now,
                            IsEdited = false                         };
                        db.Messages.Add(message);
                        await db.SaveChangesAsync();

                        var conversation = await db.Conversations.FirstOrDefaultAsync(c =>
                            (c.User1Id == CurrentUser.Id && c.User2Id == receiver.Id) ||
                            (c.User1Id == receiver.Id && c.User2Id == CurrentUser.Id));

                        if (conversation == null)
                        {
                            conversation = new Conversation
                            {
                                User1Id = CurrentUser.Id,
                                User2Id = receiver.Id,
                                LastInteraction = DateTime.Now
                            };
                            db.Conversations.Add(conversation);
                        }
                        else
                        {
                            conversation.LastInteraction = DateTime.Now;
                        }
                        await db.SaveChangesAsync();

                        var onlineReceiver = _server.GetClientByUsername(receiverUsername);
                        if (onlineReceiver != null)
                        {
                            string senderDName = string.IsNullOrWhiteSpace(CurrentUser.DisplayName) ? CurrentUser.Username : CurrentUser.DisplayName;

                                                        await onlineReceiver.SendMessageAsync($"NEW_MSG|{CurrentUser.Username}|{senderDName}|{messageText}|{message.Timestamp:HH:mm}|{attachedImage}|{message.Id}|False");
                        }
                        await SendMessageAsync($"SEND_SUCCESS|{message.Id}");
                    }
                }
                else if (command == "HISTORY" && parts.Length >= 2)
                {
                    if (CurrentUser == null) return;
                    string targetUsername = parts[1];

                    var targetUser = db.Users.Include(u => u.Photos).FirstOrDefault(u => u.Username == targetUsername);

                    if (targetUser != null)
                    {
                        var history = db.Messages
                            .Where(m => (m.SenderId == CurrentUser.Id && m.ReceiverId == targetUser.Id) || (m.SenderId == targetUser.Id && m.ReceiverId == CurrentUser.Id))
                            .OrderBy(m => m.Timestamp).ToList();

                        var unreadMsgs = history.Where(m => m.ReceiverId == CurrentUser.Id && !m.IsRead).ToList();
                        if (unreadMsgs.Any())
                        {
                            unreadMsgs.ForEach(m => m.IsRead = true);
                            await db.SaveChangesAsync();
                        }

                        var latestPhoto = targetUser.Photos.OrderByDescending(p => p.Id).FirstOrDefault();
                        string avatar = latestPhoto != null ? latestPhoto.Base64Data : "NONE";

                        string dName = string.IsNullOrWhiteSpace(targetUser.DisplayName) ? targetUser.Username : targetUser.DisplayName;
                        await SendMessageAsync($"HISTORY_START|{targetUsername}|{dName}|{avatar}");

                        foreach (var msg in history)
                        {
                            string senderUser = msg.SenderId == CurrentUser.Id ? CurrentUser.Username : targetUsername;
                            string senderDName = msg.SenderId == CurrentUser.Id
                                ? (string.IsNullOrWhiteSpace(CurrentUser.DisplayName) ? CurrentUser.Username : CurrentUser.DisplayName)
                                : (string.IsNullOrWhiteSpace(targetUser.DisplayName) ? targetUser.Username : targetUser.DisplayName);

                            string attachedImage = string.IsNullOrWhiteSpace(msg.AttachedImageBase64) ? "NONE" : msg.AttachedImageBase64;

                            await SendMessageAsync($"MSG|{senderUser}|{senderDName}|{msg.Text}|{msg.Timestamp:HH:mm}|{attachedImage}|{msg.Id}|{msg.IsEdited}");
                        }

                        bool isOnline = _server.IsUserOnline(targetUsername);
                        await SendMessageAsync($"CHAT_STATUS|{targetUsername}|{isOnline}");

                        var onlineTarget = _server.GetClientByUsername(targetUsername);
                        if (onlineTarget != null) await onlineTarget.SendMessageAsync($"READ_RECEIPT|{CurrentUser.Username}");
                    }
                }
                else if (command == "GET_CHATS")
                {
                    if (CurrentUser == null) return;

                                        var userConversations = db.Conversations
                        .Where(c => c.User1Id == CurrentUser.Id || c.User2Id == CurrentUser.Id)
                        .OrderByDescending(c => c.LastInteraction)
                        .ToList();

                    await SendMessageAsync("CHAT_LIST_START");

                    foreach (var conv in userConversations)
                    {
                                                int targetId = (conv.User1Id == CurrentUser.Id) ? conv.User2Id : conv.User1Id;
                        var targetUser = db.Users.Include(u => u.Photos).FirstOrDefault(u => u.Id == targetId);

                        if (targetUser == null) continue;

                                                var lastMsg = db.Messages
                            .Where(m => (m.SenderId == CurrentUser.Id && m.ReceiverId == targetId) ||
                                        (m.SenderId == targetId && m.ReceiverId == CurrentUser.Id))
                            .OrderByDescending(m => m.Timestamp)
                            .FirstOrDefault();

                        string shortText = "Нет сообщений";                         string time = "";

                                                if (lastMsg != null)
                        {
                            shortText = lastMsg.Text;
                            if (!string.IsNullOrWhiteSpace(lastMsg.AttachedImageBase64))
                            {
                                shortText = string.IsNullOrWhiteSpace(shortText) ? "[Фото]" : $"[Фото] {shortText}";
                            }
                            time = lastMsg.Timestamp.ToString("HH:mm");
                        }

                                                bool isOnline = _server.IsUserOnline(targetUser.Username);

                        var latestPhoto = targetUser.Photos.OrderByDescending(p => p.Id).FirstOrDefault();
                        string avatar = latestPhoto != null ? latestPhoto.Base64Data : "NONE";
                        string dName = string.IsNullOrWhiteSpace(targetUser.DisplayName) ? targetUser.Username : targetUser.DisplayName;

                        int unreadCount = db.Messages.Count(m => m.SenderId == targetUser.Id && m.ReceiverId == CurrentUser.Id && !m.IsRead);

                                                await SendMessageAsync($"CHAT_ITEM|{targetUser.Username}|{dName}|{shortText}|{time}|{isOnline}|{avatar}|{unreadCount}");
                    }

                    await SendMessageAsync("CHAT_LIST_END");
                }
                else if (command == "TYPING" && parts.Length >= 2)
                {
                    if (CurrentUser == null) return;

                    string receiverUsername = parts[1];

                    var onlineReceiver = _server.GetClientByUsername(receiverUsername);
                    if (onlineReceiver != null)
                    {
                        await onlineReceiver.SendMessageAsync($"TYPING_EVENT|{CurrentUser.Username}");
                    }
                }
                else if (command == "SEARCH" && parts.Length >= 2)
                {
                    if (CurrentUser == null) return;
                    string query = parts[1].ToLower();

                    var results = db.Users.Include(u => u.Photos)
                        .Where(u => u.Username.ToLower().Contains(query) && u.Username != CurrentUser.Username).Take(10).ToList()
                        .Select(u => {
                            var latestPhoto = u.Photos.OrderByDescending(p => p.Id).FirstOrDefault();                             string avatar = latestPhoto != null ? latestPhoto.Base64Data : "NONE";
                            string dName = string.IsNullOrWhiteSpace(u.DisplayName) ? u.Username : u.DisplayName;
                                                        return u.Username + "~" + dName + "~" + avatar;
                        }).ToList();

                    string response = "SEARCH_RESULTS|" + string.Join("|", results);
                    await SendMessageAsync(response);
                }
                else if (command == "MARK_READ" && parts.Length >= 2)
                {
                    if (CurrentUser == null) return;
                    string targetUsername = parts[1];

                    var targetUser = db.Users.FirstOrDefault(u => u.Username == targetUsername);
                    if (targetUser != null)
                    {
                        var unread = db.Messages.Where(m => m.SenderId == targetUser.Id && m.ReceiverId == CurrentUser.Id && !m.IsRead).ToList();
                        if (unread.Any())
                        {
                            unread.ForEach(m => m.IsRead = true);
                            await db.SaveChangesAsync();
                        }

                        var onlineTarget = _server.GetClientByUsername(targetUsername);
                        if (onlineTarget != null)
                        {
                            await onlineTarget.SendMessageAsync($"READ_RECEIPT|{CurrentUser.Username}");
                        }
                    }
                }
                else if (command == "GET_FULL_PROFILE" && parts.Length >= 2)
                {
                    if (CurrentUser == null) return;
                    string targetUsername = parts[1];

                                        var targetUser = db.Users.Include(u => u.Photos)
                                             .FirstOrDefault(u => u.Username == targetUsername);

                    if (targetUser != null)
                    {
                        string dName = string.IsNullOrWhiteSpace(targetUser.DisplayName) ? targetUser.Username : targetUser.DisplayName;
                        string phone = string.IsNullOrWhiteSpace(targetUser.Phone) ? "Не указан" : targetUser.Phone;
                        string bio = string.IsNullOrWhiteSpace(targetUser.Bio) ? "О себе" : targetUser.Bio;
                        string birthday = string.IsNullOrWhiteSpace(targetUser.Birthday) ? "Не указана" : targetUser.Birthday;

                                                await SendMessageAsync($"PROFILE_INFO_FULL|{targetUser.Username}|{dName}|{phone}|{bio}|{birthday}|{targetUser.Photos.Count}");

                                                foreach (var photo in targetUser.Photos.OrderByDescending(p => p.Id))
                        {
                            await SendMessageAsync($"PROFILE_PHOTO|{targetUser.Username}|{photo.Id}|{photo.Base64Data}");
                        }
                    }
                }
                else if (command == "UPDATE_PROFILE_BASIC" && parts.Length >= 5)
                {
                    if (CurrentUser == null) return;

                    var userToUpdate = db.Users.FirstOrDefault(u => u.Id == CurrentUser.Id);
                    if (userToUpdate != null)
                    {
                        userToUpdate.DisplayName = parts[1];
                        userToUpdate.Phone = parts[2];
                        userToUpdate.Bio = parts[3];
                        userToUpdate.Birthday = parts[4];
                        await db.SaveChangesAsync();

                                                CurrentUser.DisplayName = parts[1];
                        await SendMessageAsync("UPDATE_PROFILE_SUCCESS");
                        await _server.BroadcastAsync($"PROFILE_UPDATED|{CurrentUser.Username}", this);
                    }
                }
                else if (command == "ADD_PHOTO" && parts.Length >= 2)
                {
                    if (CurrentUser == null) return;
                    string base64 = parts[1];

                    var newPhoto = new UserPhoto { UserId = CurrentUser.Id, Base64Data = base64 };
                    db.UserPhotos.Add(newPhoto);
                    await db.SaveChangesAsync();

                    await SendMessageAsync($"ADD_PHOTO_SUCCESS|{newPhoto.Id}");
                    await _server.BroadcastAsync($"PROFILE_UPDATED|{CurrentUser.Username}", null);
                }
                else if (command == "REMOVE_PHOTO" && parts.Length >= 2)
                {
                    if (CurrentUser == null) return;
                    if (int.TryParse(parts[1], out int photoId))
                    {
                        var photo = db.UserPhotos.FirstOrDefault(p => p.Id == photoId && p.UserId == CurrentUser.Id);
                        if (photo != null)
                        {
                            db.UserPhotos.Remove(photo);
                            await db.SaveChangesAsync();
                            await SendMessageAsync("REMOVE_PHOTO_SUCCESS");
                            await _server.BroadcastAsync($"PROFILE_UPDATED|{CurrentUser.Username}", null);
                        }
                    }
                }
                else if (command == "EDIT_MSG" && parts.Length >= 3)
                {
                    if (CurrentUser == null) return;
                    int msgId = int.Parse(parts[1]);
                    string newText = parts[2]; 
                                        var msg = db.Messages.Include(m => m.Receiver).FirstOrDefault(m => m.Id == msgId && m.SenderId == CurrentUser.Id);
                    if (msg != null)
                    {
                        msg.Text = newText;
                        msg.IsEdited = true;
                        await db.SaveChangesAsync();

                                                var onlineReceiver = _server.GetClientByUsername(msg.Receiver.Username);
                        if (onlineReceiver != null)
                        {
                            await onlineReceiver.SendMessageAsync($"MSG_EDITED|{msgId}|{newText}");
                        }
                                                await SendMessageAsync($"MSG_EDITED|{msgId}|{newText}");
                    }
                }

                else if (command == "DELETE_MSG" && parts.Length >= 2)
                {
                    if (CurrentUser == null) return;
                    int msgId = int.Parse(parts[1]);

                                        var msg = db.Messages.Include(m => m.Receiver).FirstOrDefault(m => m.Id == msgId && m.SenderId == CurrentUser.Id);
                    if (msg != null)
                    {
                        string receiverUsername = msg.Receiver.Username;

                        db.Messages.Remove(msg);
                        await db.SaveChangesAsync();

                                                var onlineReceiver = _server.GetClientByUsername(receiverUsername);
                        if (onlineReceiver != null)
                        {
                            await onlineReceiver.SendMessageAsync($"MSG_DELETED|{msgId}");
                        }
                                                await SendMessageAsync($"MSG_DELETED|{msgId}");
                    }
                }
                else if (command == "GET_PUBLIC_KEY" && parts.Length >= 2)
                {
                    if (CurrentUser == null) return;
                    string targetUsername = parts[1];

                    var targetUser = db.Users.FirstOrDefault(u => u.Username == targetUsername);

                    if (targetUser != null && !string.IsNullOrEmpty(targetUser.PublicKey))
                    {
                                                await SendMessageAsync($"PUBLIC_KEY|{targetUsername}|{targetUser.PublicKey}");
                    }
                    else
                    {
                                                await SendMessageAsync($"ERROR|Не удалось найти публичный ключ для {targetUsername}");
                    }
                }
            }
        }

        public async Task SendMessageAsync(string message)
        {
            if (_client.Connected)
            {
                await _writer.WriteLineAsync(message);
            }
        }

        public void Close()
        {
            _server.RemoveClient(this);

                        if (CurrentUser != null)
            {
                Console.WriteLine($"Пользователь {CurrentUser.Username} отключился.");
                _ = _server.BroadcastAsync($"STATUS|{CurrentUser.Username}|False");
            }

            _stream?.Close();
            _client?.Close();
        }
    }
}