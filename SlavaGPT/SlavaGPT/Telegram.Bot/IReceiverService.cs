namespace SlavaGPT.Telegram.Bot;

public interface IReceiverService
{
    Task ReceiveAsync(CancellationToken stoppingToken);
}