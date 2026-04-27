namespace Jasmian.Server.Helpers
{
    public static class GlobalConfig
    {
        public static string DbConnectionString { get; set; } = @"Server=localhost\SQLEXPRESS;Database=JasmianDb;Trusted_Connection=True;TrustServerCertificate=True;";
        public static string BindIp { get; set; } = "127.0.0.1";
        public static int Port { get; set; } = 8888;
    }
}