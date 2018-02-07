using Orleans;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Orleans.Runtime;
using System.Collections.Generic;

namespace TestGrains
{


    public interface ITestGrain : IGrainWithIntegerKey
    {
        Task ExampleMethod1();

        Task ExampleMethod2();
    }

    public class TestGrain : Grain, ITestGrain, IRemindable
    {
        private Queue<SiloRuntimeStatistics> stats;
        private ILogger<TestGrain> logger;
        private readonly Random random = new Random();
        private TelemetryManager telemetryManager;

        public TestGrain(ILogger<TestGrain> logger, TelemetryManager telemetryManager)
        {
            this.logger = logger;
            this.telemetryManager = telemetryManager;
            telemetryManager.TrackMetric("TestGrains.TestGrain.Constructor", 1);
        }

        public async Task ExampleMethod1()
        {
            await Task.Delay(random.Next(10000));
        }

        public Task ExampleMethod2()
        {
            if (random.Next(100) < 5)
            {
                throw new Exception();
            }
            return Task.CompletedTask;
        }

        public override async Task OnActivateAsync()
        {
            stats = new Queue<SiloRuntimeStatistics>();
            telemetryManager.TrackMetric("TestGrains.TestGrain.Activation", 1);

            await RegisterOrUpdateReminder("Frequent", TimeSpan.Zero, TimeSpan.FromMinutes(1));
            var timer = RegisterTimer(Callback, true, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(random.Next(10)+50));
        }

        public Task ReceiveReminder(string reminderName, TickStatus status)
        {
            telemetryManager.TrackMetric("TestGrains.TestGrain.Reminder." + reminderName, 1);
            //telemetryManager.IncrementMetric("TestGrains.TestGrain.Reminder." + reminderName, 1);
            logger.Info($"Reminder received: {reminderName} {status}");
            return Task.CompletedTask;
        }

        async Task Callback(object canDeactivate)
        {
            telemetryManager.IncrementMetric("TestGrains.TestGrain.Timer", 1);
            logger.Info($"Timer event - GrainId: {this.GetPrimaryKeyLong()}");
        }
    }
}