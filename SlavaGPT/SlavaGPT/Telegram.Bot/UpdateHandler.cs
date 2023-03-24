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
    private readonly long _chatId;

    public UpdateHandler(ILogger<UpdateHandler> logger)
    {
        _logger = logger;
        _openAiModel = new OpenAiModel();
        var chatIdStr = Environment.GetEnvironmentVariable("CHAT_ID") ?? throw new ArgumentException("CHAT_ID is not provided");
        _chatId = long.Parse(chatIdStr);
    }

    public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        var message = update.Message;
        if (message != null && message.Chat.Id == _chatId)
        {
            _logger.LogInformation($"Message from chatId {message.Chat.Id} - {message.Chat.Title}\n{message.From?.Username}: {message.Text}");
            var botId = botClient.BotId;

            var isReply = message.ReplyToMessage?.From?.Id == botId;
            var textMentioned = message.Text?.ToLowerInvariant().Contains(_openAiModel.BotName.ToLowerInvariant()) ?? false;
            var tagMentioned = message.Text?.Contains(BotUsername) ?? false;
            var botReplyOrMention = isReply || tagMentioned || textMentioned;

            var result = await ProcessTextMessage(message, botReplyOrMention);
            _logger.LogInformation(result.GetType().ToString());
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
        
        return await _openAiModel.Process(sender, text, replyOrMention);
    }
}