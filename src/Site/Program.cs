using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrchardCore.Logging;

namespace Site
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureLogging(logging => logging.ClearProviders())
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseNLogWeb();
                    webBuilder.UseStartup<Startup>();
                });
    }
}
