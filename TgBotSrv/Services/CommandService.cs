using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TgBotSrv.Models;

namespace TgBotSrv.Services;

public class CommandService
{
    private readonly UserService _userService;
    private readonly ITelegramBotClient _botClient;

    public CommandService(UserService userService, ITelegramBotClient botClient)
    {
        _userService = userService;
        _botClient = botClient;
    }

    public async Task HandleCommand(Message message, CancellationToken cancellationToken)
    {
        var command = message.Text?.Split(' ')[0].ToLower();
        var userId = message.From?.Id ?? 0;

        switch (command)
        {
            case "/start":
                await HandleStartCommand(message, cancellationToken);
                break;
            case "/help":
                await HandleHelpCommand(message, cancellationToken);
                break;
            case "/clear":
                await HandleClearCommand(message, cancellationToken);
                break;
            case "/settings":
                await HandleSettingsCommand(message, cancellationToken);
                break;
            case "/language":
                await HandleLanguageCommand(message, cancellationToken);
                break;
            default:
                await _botClient.SendMessage(
                    chatId: message.Chat.Id,
                    text: "未知命令。使用 /help 查看可用命令。",
                    cancellationToken: cancellationToken);
                break;
        }
    }

    private async Task HandleStartCommand(Message message, CancellationToken cancellationToken)
    {
        var welcomeMessage = @"👋 欢迎使用AI助手！

使用以下命令：
/help - 显示帮助信息
/clear - 清除对话历史
/settings - 查看当前设置
/language - 设置语言

直接发送消息即可开始对话！";

        await _botClient.SendMessage(
            chatId: message.Chat.Id,
            text: welcomeMessage,
            parseMode: ParseMode.Html,
            cancellationToken: cancellationToken);
    }

    private async Task HandleHelpCommand(Message message, CancellationToken cancellationToken)
    {
        var helpMessage = @"📚 命令列表：

/start - 显示欢迎信息
/help - 显示此帮助信息
/clear - 清除对话历史
/settings - 查看当前设置
/language - 设置语言

💡 提示：直接发送消息即可与AI对话！";

        await _botClient.SendMessage(
            chatId: message.Chat.Id,
            text: helpMessage,
            parseMode: ParseMode.Html,
            cancellationToken: cancellationToken);
    }

    private async Task HandleClearCommand(Message message, CancellationToken cancellationToken)
    {
        var userId = message.From?.Id ?? 0;
        _userService.ClearHistory(userId);

        await _botClient.SendMessage(
            chatId: message.Chat.Id,
            text: "✅ 对话历史已清除！",
            cancellationToken: cancellationToken);
    }

    private async Task HandleSettingsCommand(Message message, CancellationToken cancellationToken)
    {
        var userId = message.From?.Id ?? 0;
        var settings = _userService.GetUserSettings(userId);

        var settingsMessage = $@"⚙️ 当前设置：

语言：{settings.Language}
温度：{settings.Temperature}
最大回复长度：{settings.MaxTokens}
回复风格：{settings.ResponseStyle}

使用 /language 命令可以更改语言设置。";

        await _botClient.SendMessage(
            chatId: message.Chat.Id,
            text: settingsMessage,
            parseMode: ParseMode.Html,
            cancellationToken: cancellationToken);
    }

    private async Task HandleLanguageCommand(Message message, CancellationToken cancellationToken)
    {
        var args = message.Text?.Split(' ');
        if (args?.Length != 2)
        {
            await _botClient.SendMessage(
                chatId: message.Chat.Id,
                text: "请指定语言代码，例如：/language zh-CN",
                cancellationToken: cancellationToken);
            return;
        }

        var userId = message.From?.Id ?? 0;
        var settings = _userService.GetUserSettings(userId);
        settings.Language = args[1];
        _userService.UpdateUserSettings(userId, settings);

        await _botClient.SendMessage(
            chatId: message.Chat.Id,
            text: $"✅ 语言已设置为：{settings.Language}",
            cancellationToken: cancellationToken);
    }
} 