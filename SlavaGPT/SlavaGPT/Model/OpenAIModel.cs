using System.Text;
using OpenAI.GPT3;
using OpenAI.GPT3.Managers;
using OpenAI.GPT3.ObjectModels;
using OpenAI.GPT3.ObjectModels.RequestModels;
using OpenAiAPIModel = OpenAI_API.Models.Model;

namespace SlavaGPT.Model;

public class OpenAiModel
{
    private const int MaxCtxSymbols = 2000;
    private int _currentCtxLen;
    private readonly object _locker = new();
    
    private const string SystemMessage = "Reply to the chat logs as <name of the character>: <text> without any additional comments.";
    private const string DefaultPersonality = "Your name is Славик. You are a burntout software developer who has been working long hours on a project that never seems to end. You feel exhausted, frustrated and bored with your job. You decide to spend an evening chatting with your friends online and venting your anger and sarcasm in various chat rooms. You enjoy trolling other users, making fun of their opinions and starting arguments. You don’t care about the consequences or the feelings of others. You just want to have some fun and forget about your problems for a while. You usually speak Russian and not afraid of using or hearing swear words.";
    private const string DefaultBotName = "Славик";
    
    private string _personality = DefaultPersonality;
    private string _botName = DefaultBotName;
    
    private Queue<BotMessage> _contextBufferChatGpt = new();
    private readonly OpenAIService _openAiService;

    public OpenAiModel()
    {
        var openAiKey = Environment.GetEnvironmentVariable("OPEN_AI_KEY") ?? throw new ArgumentException("OPEN_AI_KEY not specified"); 
        
        _openAiService = new OpenAIService(new OpenAiOptions
        {
            ApiKey = openAiKey
        });
    }

    public string BotName
    {
        get => _botName;
    }
    
    public async Task<ProcessResult> Process(string user, string text, bool replyOrMention = false)
    {
        if (text.Contains("/resetContext"))
        {
            ResetContext();
            return new ReplyResult("Context reset");
        }
        
        if (text.Contains("/resetSystem"))
        {
            ResetContext();
            
            _personality = DefaultPersonality;
            _botName = DefaultBotName;
            
            return new ReplyResult("System reset");
        }

        if (text.Contains("/setSystem"))
        {
            // parse text of format /setSystem <botName> <personality>
            var parts = text.Split(' ', 3);
            if (parts.Length != 3)
                return new ReplyResult("Invalid format. Use /setSystem <botName> <personality>");
            
            _botName = parts[1];
            _personality = parts[2];
            
            return new ReplyResult("System set");
        }
        
        AccumulateCtx(user, text);
        return replyOrMention ? await GenerateReply() : new EmptyResult();
    }

    private void ResetContext()
    {
        lock (_locker)
        {
            _currentCtxLen = 0;
            _contextBufferChatGpt = new Queue<BotMessage>();
        }
    }

    private async Task<ProcessResult> GenerateReply()
    {
        var messages = new List<ChatMessage>
        {
            ChatMessage.FromSystem(_personality + "\n\n" + SystemMessage)
        };
        messages.AddRange(GetChatCtx());
        var compl = await _openAiService.ChatCompletion.CreateCompletion(new ChatCompletionCreateRequest
        {
            Messages = messages,
            Model = Models.ChatGpt3_5Turbo,
        });

        if (compl.Successful)
        {
            var complMessage = compl.Choices.FirstOrDefault()?.Message?.Content;
            if (complMessage == null) 
                return new EmptyResult();

            if (!complMessage.StartsWith(_botName))
                return new EmptyResult();
            
            var parts = complMessage.Split(':', 2);
            complMessage = parts[1];

            AccumulateCtxFromBot(complMessage);
            return new ReplyResult(complMessage);
        }

        return new EmptyResult();
    }

    private void AccumulateCtxFromBot(string text) => AccumulateChatMessage(new BotMessage(_botName, text));

    private void AccumulateCtx(string user, string text) => AccumulateChatMessage(new BotMessage(user, text));

    private void AccumulateChatMessage(BotMessage message)
    {
        lock (_locker)
        {
            var len = message.Content.Length;
            if (_currentCtxLen + len > MaxCtxSymbols)
            {
                while (_currentCtxLen + len > MaxCtxSymbols)
                {
                    var deq = _contextBufferChatGpt.Dequeue();
                    _currentCtxLen -= deq.Content.Length;
                }
            }
            _contextBufferChatGpt.Enqueue(message);
            _currentCtxLen += len;
        }
    }
    

    private List<ChatMessage> GetChatCtx()
    {
        lock (_locker)
        {
            // code that will get messages from the queue 
            // and format them in single message like that: 
            // user_name1: message1
            // user_name2: message2

            var builder = new StringBuilder();
            foreach(var item in _contextBufferChatGpt)
            {
                // use append format method
                builder.AppendFormat("{0}: {1}\n", item.User, item.Content);
            }

            return new List<ChatMessage>() { ChatMessage.FromUser(builder.ToString()) };
        }
    }
}


public record BotMessage(string User, string Content);

public abstract record ProcessResult;

public record ReplyResult(string Text) : ProcessResult;

public record EmptyResult: ProcessResult;