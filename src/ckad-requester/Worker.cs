using ckad_requester.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace ckad_requester
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IHttpClientFactory clientFactory;
        private readonly IOptionsMonitor<WorkerConfiguration> workerConfiguration;

        public Worker(ILogger<Worker> logger, IHttpClientFactory clientFactory, IOptionsMonitor<WorkerConfiguration> workerConfiguration)
        {
            _logger = logger;
            this.clientFactory = clientFactory;
            this.workerConfiguration = workerConfiguration;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            bool shouldRestart = true;
            CancellationTokenSource token = null;

            workerConfiguration.OnChange((configuration) =>
            {
                shouldRestart = true;
            });

            // OnChange does not work on Kubernetes ConfigMap :(
            var currentValues = workerConfiguration.CurrentValue;

            while (!stoppingToken.IsCancellationRequested)
            {
                if (shouldRestart)
                {
                    shouldRestart = false;
                    _logger.LogInformation("Restarting...");
                    token?.Cancel();
                    token = new CancellationTokenSource();
                    currentValues = workerConfiguration.CurrentValue;

                    await StartRequests(token.Token);
                }
                await Task.Delay(1000, stoppingToken);

                if (!currentValues.Websites.SequenceEqual(workerConfiguration.CurrentValue.Websites))
                {
                    shouldRestart = true;
                }
            }
        }

        private async Task StartRequests(CancellationToken token)
        {
            foreach (var configuration in workerConfiguration.CurrentValue.Websites)
            {
                await Task.Factory.StartNew(() => RunRequests(configuration, token), TaskCreationOptions.LongRunning);
            }
        }

        private async Task RunRequests(WebsiteConfiguration configuration, CancellationToken token)
        {
            var channel = Channel.CreateUnbounded<Response>();

            // Create request tasks
            for (int i = 0; i < configuration.NumberOfThreads; i++)
            {
                await Task.Factory.StartNew(() => RunRequest(configuration, channel, token), TaskCreationOptions.LongRunning);
            }

            var reader = channel.Reader;
            while (!token.IsCancellationRequested)
            {
                var numberOfRequests = await GetNumberOfRequestsWithin(TimeSpan.FromSeconds(1), reader);
                _logger.LogInformation($"Number of requests for {configuration.Url}: {numberOfRequests.success} OK, {numberOfRequests.failed} Failed");
            }
        }

        private async Task<(int success, int failed)> GetNumberOfRequestsWithin(TimeSpan timespan, ChannelReader<Response> reader)
        {
            int success = 0, failed = 0;

            try
            {
                CancellationTokenSource source = new CancellationTokenSource(timespan);
                while (await reader.WaitToReadAsync(source.Token))
                {
                    var request = await reader.ReadAsync(source.Token);
                    if (request.Success)
                    {
                        success++;
                    }
                    else
                    {
                        failed++;
                    }
                }
            }
            catch (OperationCanceledException) { }

            return (success, failed);
        }

        private async Task RunRequest(WebsiteConfiguration configuration, Channel<Response> channel, CancellationToken token)
        {
            var client = clientFactory.CreateClient(configuration.Url);
            var stopwatch = Stopwatch.StartNew();
            var writer = channel.Writer;

            while (!token.IsCancellationRequested)
            {
                stopwatch.Restart();
                try
                {
                    var request = new HttpRequestMessage(HttpMethod.Get, configuration.Url);
                    var response = await client.SendAsync(request, token);
                    await writer.WriteAsync(new Response
                    {
                        Success = response.IsSuccessStatusCode,
                        Time = stopwatch.Elapsed,
                    });
                }
                catch (Exception ex)
                {
                    await writer.WriteAsync(new Response
                    {
                        Success = false,
                        Time = stopwatch.Elapsed,
                        Exception = ex,
                    });
                }
            }
        }

        private class Response
        {
            public bool Success { get; set; }
            public TimeSpan Time { get; set; }
            public Exception Exception { get; set; }
        }
    }
}
