using Microsoft.Extensions.Logging;
using SlavaGPT.Model;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;

namespace SlavaGPT.Telegram.Bot;

public class UpdateHandler: IUpdateHandler
{
    private readonly ILogger _logger;
    private readonly OpenAiModel _openAiModel;
    private const string BotUsername = "@slava_gpt_bot";

    public UpdateHandler(ILogger<UpdateHandler> logger)
    {
        _logger = logger;
        _openAiModel = new OpenAiModel();
    }

    public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        var message = update.Message;
        if (message != null)
        {
            _logger.LogInformation($"Message from chatId {message.Chat.Id} - {message.Chat.Title}\n{message.From?.Username}: {message.Text}");
            var botId = botClient.BotId;

            var isReply = message.ReplyToMessage?.From?.Id == botId;
            var textMentioned = message.Text?.ToLowerInvariant().Contains("славик") ?? false;
            var tagMentioned = message.Text?.Contains(BotUsername) ?? false;
            var botReplyOrMention = isReply || tagMentioned || textMentioned;

            var result = await ProcessTextMessage(message, botReplyOrMention);
            if (result is ReplyResult rr)
            {
                await botClient.SendTextMessageAsync(message.Chat.Id, rr.Text, replyToMessageId: message.MessageId, cancellationToken: cancellationToken);
            }
        }
    }

    public Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        _logger.LogError(exception.Message);
        return Task.CompletedTask;
    }

    private async Task<ProcessResult> ProcessTextMessage(Message message, bool replyOrMention)
    {
        var sender = message.From?.Username;
        var text = message.Text;
        if (sender == null || text == null) return new EmptyResult();
        
        return await _openAiModel.Process(text, replyOrMention);
    }
}