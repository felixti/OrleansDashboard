﻿using System;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Runtime;
using System.Threading;

namespace OrleansDashboard
{
    public sealed class Dashboard : IStartupTask, IDisposable
    {
        private IWebHost host;
        private readonly ILogger<Dashboard> logger;
        private readonly ILocalSiloDetails localSiloDetails;
        private readonly IGrainFactory grainFactory;
        private readonly DashboardOptions dashboardOptions;

        public static int HistoryLength => 100;

        public Dashboard(
            ILogger<Dashboard> logger,
            ILocalSiloDetails localSiloDetails,
            IGrainFactory grainFactory,
            IOptions<DashboardOptions> dashboardOptions)
        {
            this.logger = logger;
            this.grainFactory = grainFactory;
            this.localSiloDetails = localSiloDetails;
            this.dashboardOptions = dashboardOptions.Value;
        }

        public Task Execute(CancellationToken cancellationToken)
        {
            if (dashboardOptions.HostSelf)
            {
                try
                {
                    host =
                        new WebHostBuilder()
                            .ConfigureServices(services =>
                            {
                                services.AddServicesForHostedDashboard(grainFactory, dashboardOptions);
                            })
                            .Configure(app =>
                            {
                                if (dashboardOptions.HasUsernameAndPassword())
                                {
                                    // only when usename and password are configured
                                    // do we inject basicauth middleware in the pipeline
                                    app.UseMiddleware<BasicAuthMiddleware>();
                                }

                                app.UseOrleansDashboard();
                            })
                            .UseKestrel()
                            .UseUrls($"http://{dashboardOptions.Host}:{dashboardOptions.Port}")
                            .Build();

                    host.Start();
                }
                catch (Exception ex)
                {
                    logger.Error(10001, ex.ToString());
                }

                logger.LogInformation($"Dashboard listening on {dashboardOptions.Port}");
            }

            // horrible hack to grab the scheduler
            // to allow the stats publisher to push
            // counters to grains
            SiloDispatcher.Setup();

            return Task.WhenAll(
                ActivateDashboardGrainAsync(),
                ActivateSiloGrainAsync());
        }

        private async Task ActivateSiloGrainAsync()
        {
            var siloGrain = grainFactory.GetGrain<ISiloGrain>(localSiloDetails.SiloAddress.ToParsableString());

            await siloGrain.SetVersion(GetOrleansVersion(), GetHostVersion());
        }

        private async Task ActivateDashboardGrainAsync()
        {
            var dashboardGrain = grainFactory.GetGrain<IDashboardGrain>(0);

            await dashboardGrain.Init();
        }

        public void Dispose()
        {
            try
            {
                host?.Dispose();
            }
            catch
            {
                /* NOOP */
            }

            try
            {
                // Teardown the silo dispatcher to prevent deadlocks.
                SiloDispatcher.Teardown();
            }
            catch
            {
                /* NOOP */
            }
        }

        private static string GetOrleansVersion()
        {
            return typeof(SiloAddress).GetTypeInfo().Assembly.GetName().Version.ToString();
        }

        private static string GetHostVersion()
        {
            try
            {
                var assembly = Assembly.GetEntryAssembly();

                if (assembly != null)
                {
                    return assembly.GetName().Version.ToString();
                }
            }
            catch
            {
                /* NOOP */
            }

            return "1.0.0.0";
        }
    }
}