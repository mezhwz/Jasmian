using Jasmian.Server.Data;
using Jasmian.Server.Helpers;
using Jasmian.Server.Network;
using System;
using System.Net.NetworkInformation;
using System.Net.WebSockets;
using System.Threading.Tasks;

namespace Jasmian.Server
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.Title = "Jasmian Server";

            string configFile = "server_config.txt";
            if (!File.Exists(configFile))
            {
                                string defaultConfig = $"IP={GlobalConfig.BindIp}\n" +
                                       $"PORT={GlobalConfig.Port}\n" +
                                       $"DB_CONNECTION={GlobalConfig.DbConnectionString}";
                File.WriteAllText(configFile, defaultConfig);
                Console.WriteLine("[CONFIG] Создан файл server_config.txt с настройками по умолчанию.");
            }
            else
            {
                                var lines = File.ReadAllLines(configFile);
                foreach (var line in lines)
                {
                    if (line.StartsWith("IP=")) GlobalConfig.BindIp = line.Substring(3);
                    if (line.StartsWith("PORT=")) GlobalConfig.Port = int.Parse(line.Substring(5));
                    if (line.StartsWith("DB_CONNECTION=")) GlobalConfig.DbConnectionString = line.Substring(14);
                }
                Console.WriteLine("[CONFIG] Настройки загружены из файла.");
            }

            Console.WriteLine("--------------------------------------------------");
            Console.WriteLine("ДОСТУПНЫЕ АДРЕСА ДЛЯ ПОДКЛЮЧЕНИЯ КЛИЕНТОВ:");
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus == OperationalStatus.Up)
                {
                    foreach (var ip in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                        {
                            Console.WriteLine($" -> IP: {ip.Address} (Интерфейс: {ni.Name})");
                        }
                    }
                }
            }
            Console.WriteLine("--------------------------------------------------\n");

            using (var db = new JasmianDbContext())
            {

                Console.WriteLine("Отчистить бд? Y - да");
                var key = Console.ReadKey().Key;
                Console.WriteLine();
                if (key == ConsoleKey.Y)
                {
                    db.Database.EnsureDeleted();
                    Console.WriteLine("База данных отчищена.");
                }

                Console.WriteLine("\nПроверка базы данных...");
                db.Database.EnsureCreated();
                Console.WriteLine("База данных готова.");
            }

            Network.Server server = new Network.Server();
            await server.StartAsync(8888);
        }
    }
}