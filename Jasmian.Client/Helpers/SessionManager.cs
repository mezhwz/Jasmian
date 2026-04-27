using System.IO;
using System.Text.Json;
using Jasmian.Client.Models;

namespace Jasmian.Client.Helpers
{
    public static class SessionManager
    {
        private const string FilePath = "session.json";

        public static void SaveSession(string username, string password)
        {
            var session = new UserSession { Username = username, Password = password };
            string json = JsonSerializer.Serialize(session);
            File.WriteAllText(FilePath, json);
        }

        public static UserSession LoadSession()
        {
            if (!File.Exists(FilePath)) return null;
            string json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<UserSession>(json);
        }

        public static void ClearSession()
        {
            if (File.Exists(FilePath))
            {
                File.Delete(FilePath);
            }
        }
    }
}