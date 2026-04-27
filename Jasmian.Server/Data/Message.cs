using System;

namespace Jasmian.Server.Data
{
    public class Message
    {
        public int Id { get; set; }
        public int SenderId { get; set; }
        public User Sender { get; set; }
        public int ReceiverId { get; set; }
        public User Receiver { get; set; }

        public string Text { get; set; }
        public DateTime Timestamp { get; set; }
        public bool IsRead { get; set; }
        public string? AttachedImageBase64 { get; set; }

                public bool IsEdited { get; set; }
    }
}