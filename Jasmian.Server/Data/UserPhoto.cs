using System;

namespace Jasmian.Server.Data
{
    public class UserPhoto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public User User { get; set; }

        public string Base64Data { get; set; }
        public DateTime UploadedAt { get; set; } = DateTime.Now;
    }
}