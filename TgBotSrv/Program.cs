using System.Text;
using System.Text.Json;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TgBotSrv.Services;
using TgBotSrv.Models;

Console.WriteLine("Bot is starting...");

DotNetEnv.Env.Load();

const string DEEPSEEK_API_URL = "https://api.deepseek.com/v1/chat/completions";

string BOT_TOKEN = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN")!;
string DEEPSEEK_API_KEY = Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY")!;

TelegramBotClient botClient = new(BOT_TOKEN);
using CancellationTokenSource cts = new();

var userService = new UserService();
var commandService = new CommandService(userService, botClient);

ReceiverOptions receiverOptions = new()
{
    AllowedUpdates = []
};

User me = await botClient.GetMe();
Console.WriteLine($"Bot @{me.Username} is running!");

botClient.StartReceiving(
    updateHandler: HandleUpdateAsync,
    errorHandler: HandleErrorAsync,
    receiverOptions: receiverOptions,
    cancellationToken: cts.Token
);

while (true)
{
    Console.WriteLine("Type \"exit\" to exit...");
    if (Console.ReadLine() == "exit")
    {
        cts.Cancel();
        break;
    }
}

async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
{
    if (update.Message is not { } message || message.Text is not { } messageText)
    {
        return;
    }

    long chatId = message.Chat.Id;
    string userName = message.From?.FirstName ?? "User";
    long userId = message.From?.Id ?? 0;

    Console.WriteLine($"Received '{messageText}' from {userName} in chat {chatId}");

    // 处理命令
    if (messageText.StartsWith("/"))
    {
        await commandService.HandleCommand(message, cancellationToken);
        return;
    }

    // 处理普通消息
    await ResponseByDeepSeek(chatId, userId, messageText, cancellationToken);
}

async Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
{
    await Task.Run(() =>
    {
        Console.WriteLine($"Error occurred: {exception.Message}");
    }, cancellationToken);
}

async Task ResponseByDeepSeek(long chatId, long userId, string messageText, CancellationToken cancellationToken)
{
    try
    {
        Message thinkingMessage = await botClient.SendMessage(chatId: chatId, text: "🤔正在思考...", cancellationToken: cancellationToken);

        // 创建一个取消令牌源
        using var thinkingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var thinkingTask = UpdateThinkingMessage(chatId, thinkingMessage.MessageId, thinkingCts.Token);

        var settings = userService.GetUserSettings(userId);
        string deepSeekResponse = await CallDeepSeekApi(messageText, settings);

        Console.WriteLine($"ChatId: {chatId}, DeepSeek: {deepSeekResponse}");

        // 取消等待提示
        thinkingCts.Cancel();
        try
        {
            await thinkingTask;
        }
        catch (OperationCanceledException)
        {
            // 忽略取消异常
        }

        // 先删除等待消息
        try
        {
            await botClient.DeleteMessage(chatId: chatId, messageId: thinkingMessage.MessageId, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deleting thinking message: {ex.Message}");
        }

        // 然后发送实际回复
        await botClient.SendMessage(
            chatId: chatId,
            text: deepSeekResponse,
            parseMode: ParseMode.Html,
            cancellationToken: cancellationToken);

        // 保存对话历史
        userService.AddMessageToHistory(userId, "user", messageText);
        userService.AddMessageToHistory(userId, "assistant", deepSeekResponse);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error: {ex.Message}");
        await botClient.SendMessage(
            chatId: chatId,
            text: "抱歉，处理您的请求时出错了。",
            cancellationToken: cancellationToken);
    }
}

async Task UpdateThinkingMessage(long chatId, int messageId, CancellationToken cancellationToken)
{
    string[] thinkingFrames = [
        "🤔正在思考",
        "🤔正在思考.",
        "🤔正在思考..",
        "🤔正在思考...",
        "🤔正在思考....",
        "🤔正在思考.....",
    ];

    int frameIndex = 0;
    while (!cancellationToken.IsCancellationRequested)
    {
        try
        {
            await botClient.EditMessageText(
                chatId: chatId,
                messageId: messageId,
                text: thinkingFrames[frameIndex],
                cancellationToken: cancellationToken);

            frameIndex = (frameIndex + 1) % thinkingFrames.Length;
            await Task.Delay(500, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // 正常取消，直接退出
            break;
        }
        catch (Exception ex)
        {
            // 忽略编辑消息时的错误，因为消息可能已经被删除
            if (!ex.Message.Contains("message to edit not found"))
            {
                Console.WriteLine($"Error updating thinking message: {ex.Message}");
            }
            break;
        }
    }
}

async Task<string> CallDeepSeekApi(string userMessage, UserSettings settings)
{
    using HttpClient httpClient = new();

    var messages = new List<object>
    {
        new { role = "system", content = "请使用HTML格式回复，而不是Markdown格式。Telegram只支持以下HTML标签：\n1. 粗体：使用<b>标签\n2. 斜体：使用<i>标签\n3. 代码：使用<code>标签\n4. 预格式化文本：使用<pre>标签\n5. 链接：使用<a href='url'>text</a>格式\n\n请确保只使用上述标签，不要使用其他HTML标签。对于换行，直接使用换行符即可。" }
    };

    // 添加历史消息
    foreach (var msg in settings.ChatHistory)
    {
        messages.Add(new { role = msg.Role, content = msg.Content });
    }

    // 添加当前消息
    messages.Add(new { role = "user", content = userMessage });

    var requestData = new
    {
        model = "deepseek-chat",
        messages = messages.ToArray(),
        temperature = settings.Temperature,
        max_tokens = settings.MaxTokens
    };

    StringContent content = new(
        JsonSerializer.Serialize(requestData),
        Encoding.UTF8,
        "application/json");

    httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {DEEPSEEK_API_KEY}");

    HttpResponseMessage response = await httpClient.PostAsync(DEEPSEEK_API_URL, content);
    response.EnsureSuccessStatusCode();

    string responseContent = await response.Content.ReadAsStringAsync();
    using JsonDocument jsonDoc = JsonDocument.Parse(responseContent);

    return jsonDoc.RootElement
        .GetProperty("choices")[0]
        .GetProperty("message")
        .GetProperty("content")
        .GetString() ?? "未能获取有效回复";
}
