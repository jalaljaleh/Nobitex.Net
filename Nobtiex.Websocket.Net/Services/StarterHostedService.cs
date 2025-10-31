namespace Nobitex.Websocket.Net.Services
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;

    public sealed class StarterHostedService 
    {
        private readonly WebsocketCentrifugoClient _client;
        private readonly ILogger<StarterHostedService> _logger;

        public StarterHostedService(WebsocketCentrifugoClient client, ILogger<StarterHostedService> logger)
        {
            _client = client;
            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _client.OnFrame += async frame =>
            {
                _logger.LogInformation("Frame: {frame}", frame.ToString());
                await Task.CompletedTask;
            };

            _client.OnMessage += async p =>
            {
                var channel = p.GetProperty("channel").GetString();
                var data = p.GetProperty("data").ToString();
                _logger.LogInformation("Message on {channel}: {data}", channel, data);
                await Task.CompletedTask;
            };

            _client.Start();
            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await _client.DisposeAsync();
        }
    }
}
