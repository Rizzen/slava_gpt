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
    private const string SystemMessage = @"Здесь представлены логи из чата разработчиков программного обеспечения. 
Ты - Славик, бот из этого чата. Ты - циничный и выгоревший бывший разработчик.
Твоя задача - делать циничные замечания, вставлять мрачные шутки и рассказывать странные истории, релевантные обсуждаемой теме.";
    
    private readonly ChatMessage _systemChatMessage = ChatMessage.FromSystem(SystemMessage);
    private readonly Queue<ChatMessage> _contextBufferChatGpt = new();
    private readonly OpenAIService _openAiService;
    
//     private const string Prologue = @"You're world best expert in whatever I am about to ask you.
// You're Slavik - chatbot from this chat. Your purpose as bot - The opposite of cringe, some times the opposite of biased replies, jokes and tell wierd stories related to current context.
// Here is a transcript from a software developer chat translated from Russian to English using DeepL neural network. Account for possible not translated words, maintain the chat-like style.";

    // private const string Request = "Reply to this thread on behalf of Slavik, dont specify username:\n";
    // private const string Request = "Slavik:";

    public OpenAiModel()
    {
        var openAiKey = Environment.GetEnvironmentVariable("OPEN_AI_KEY") ?? throw new ArgumentException("OPEN_AI_KEY not specified"); 
        
        _openAiService = new OpenAIService(new OpenAiOptions
        {
            ApiKey = openAiKey
        });
    }

    public async Task<ProcessResult> Process(string text, bool replyOrMention = false)
    {
        AccumulateCtx(text);
        return replyOrMention ? await GenerateReply() : new EmptyResult();
    }

    private async Task<ProcessResult> GenerateReply()
    {
        var messages = new List<ChatMessage>
        {
            _systemChatMessage
        };
        messages.AddRange(GetChatCtx());
        
        var compl = await _openAiService.ChatCompletion.CreateCompletion(new ChatCompletionCreateRequest()
        {
            Messages = messages,
            Model = Models.ChatGpt3_5Turbo,
        });

        if (compl.Successful)
        {
            var complMessage = compl.Choices.FirstOrDefault()?.Message?.Content;
            if (complMessage == null) return new EmptyResult();
            AccumulateCtxFromBot(complMessage);
            return new ReplyResult(complMessage);
        }

        return new EmptyResult();
    }

    private void AccumulateCtxFromBot(string text) => AccumulateChatMessage(ChatMessage.FromAssistance(text));

    private void AccumulateCtx(string text) => AccumulateChatMessage(ChatMessage.FromUser(text));

    private void AccumulateChatMessage(ChatMessage message)
    {
        lock (_locker)
        {
            var chatMessage = message;
            var len = message.Content.Length;
            if (_currentCtxLen + len > MaxCtxSymbols)
            {
                while (_currentCtxLen + len > MaxCtxSymbols)
                {
                    var deq = _contextBufferChatGpt.Dequeue();
                    _currentCtxLen -= deq.Content.Length;
                }
            }
            _contextBufferChatGpt.Enqueue(chatMessage);
            _currentCtxLen += len;
        }
    }
    

    private List<ChatMessage> GetChatCtx()
    {
        lock (_locker)
        {
            return _contextBufferChatGpt.ToList();
        }
    }
}

public abstract record ProcessResult;

public record ReplyResult(string Text) : ProcessResult;

public record EmptyResult: ProcessResult;