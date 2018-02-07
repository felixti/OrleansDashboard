using System;
using System.Threading;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Hosting;
using Orleans.Runtime.Configuration;
using TestGrains;

// ReSharper disable MethodSupportsCancellation

namespace TestHost
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            var configuration =
                ClusterConfiguration.LocalhostPrimarySilo(33333)
                    .AddPerfCountersTelemetryConsumer("")
                    .RegisterDashboard();

            configuration.Defaults.StatisticsMetricsTableWriteInterval = TimeSpan.FromSeconds(15);
            configuration.Defaults.StatisticsPerfCountersWriteInterval = TimeSpan.FromSeconds(15);

            var silo =
                new SiloHostBuilder()
                    .UseConfiguration(configuration)
                    .UseDashboard(options =>
                    {
                        options.HostSelf = true;
                        options.HideTrace = false;
                    })
                    .ConfigureApplicationParts(appParts => appParts.AddApplicationPart(typeof(TestCalls).Assembly))
                    .ConfigureLogging(builder =>
                    {
                        builder.AddFilter("Orleans.Runtime.SiloControl", LogLevel.Warning);
                        builder.AddConsole();
                    })
                    .Build();

            silo.StartAsync().Wait();

            var client =
                new ClientBuilder()
                    .UseConfiguration(ClientConfiguration.LocalhostSilo())
                    .ConfigureApplicationParts(appParts => appParts.AddApplicationPart(typeof(TestCalls).Assembly))
                    .ConfigureLogging(builder =>
                    {
                        builder.AddConsole();
                    })
                    .Build();

            client.Connect().Wait();

            var cts = new CancellationTokenSource();

            TestCalls.Make(client, cts);

            Console.WriteLine("Press key to exit...");
            Console.ReadLine();

            cts.Cancel();

            silo.StopAsync().Wait();
        }
    }
}