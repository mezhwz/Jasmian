using System.Collections.Generic;

namespace Jasmian.Server.Data
{
    public class User
    {
        public int Id { get; set; }
        public string Username { get; set; }         public string PasswordHash { get; set; }
        public string? PublicKey { get; set; }

                public string? DisplayName { get; set; }         public string? Phone { get; set; }
        public string? Bio { get; set; }
        public string? Birthday { get; set; }

                public ICollection<UserPhoto> Photos { get; set; } = new List<UserPhoto>();

        public ICollection<Message> SentMessages { get; set; }
        public ICollection<Message> ReceivedMessages { get; set; }
    }
}