using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SlavaGPT.Telegram.Bot;
using Telegram.Bot;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddHttpClient("telegram_bot_client")
            .AddTypedClient<ITelegramBotClient>((httpClient, sp) =>
            {
                var telegramToken = Environment.GetEnvironmentVariable("TELEGRAM_BOT_KEY") ?? throw new ArgumentException("TELEGRAM_BOT_KEY not specified"); 
                TelegramBotClientOptions options = new(telegramToken);
                return new TelegramBotClient(options, httpClient);
            });

        services.AddScoped<UpdateHandler>();
        services.AddScoped<ReceiverService>();
        services.AddHostedService<PollingService>();
    })
    .Build();

await host.RunAsync();