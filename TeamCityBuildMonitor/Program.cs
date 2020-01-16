using System;
using System.IO;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NLog;

namespace TeamCityBuildMonitor
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var logger = LogManager.LoadConfiguration("NLog.config").GetCurrentClassLogger();
            try
            {
                logger.Debug("init main");
                CreateWebHostBuilder(args).Build().Run();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Stopped program because of exception");
                throw;
            }
            finally
            {
                LogManager.Shutdown();
            }
        }

        public static IWebHostBuilder CreateWebHostBuilder(string[] args)
        {
            var configuration = new ConfigurationBuilder()
            .AddCommandLine(args)
            .Build();

            var portEnv = Environment.GetEnvironmentVariable("port");
            var port = string.IsNullOrWhiteSpace(portEnv) ? "8081" : portEnv;

            return WebHost.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((hostingContext, config) =>
                {
                    config.SetBasePath(Directory.GetCurrentDirectory());
                    config.AddJsonFile("appsettings.json", false, true);
                })
                .UseUrls($"http://0.0.0.0:{port}/")
                .UseConfiguration(configuration)
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace);
                })
                .UseStartup<Startup>();
        }
    }
}