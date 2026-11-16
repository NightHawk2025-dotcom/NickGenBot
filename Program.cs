using NickGenBot;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

var token = Environment.GetEnvironmentVariable("BOT_TOKEN");
if (string.IsNullOrWhiteSpace(token))
{
    Console.Error.WriteLine("Не задано BOT_TOKEN. Отримай токен у @BotFather і додай у Properties/launchSettings.json.");
    return 1;
}

var bot = new TelegramBotClient(token);

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

var me = await bot.GetMe(cts.Token);

await bot.SetMyCommands(
[
    new BotCommand { Command = "start", Description = "Меню" },
    new BotCommand { Command = "nick", Description = "Згенерувати ніки" },
    new BotCommand { Command = "username", Description = "Згенерувати юзернейми" },
    new BotCommand { Command = "shop", Description = "Унікальні юзернейми (платно)" },
    new BotCommand { Command = "balance", Description = "Мій баланс наборів" },
    new BotCommand { Command = "paysupport", Description = "Проблеми з оплатою" },
], cancellationToken: cts.Token);

bot.StartReceiving(
    new DefaultUpdateHandler(Handlers.HandleUpdateAsync, Handlers.HandleErrorAsync),
    new ReceiverOptions { AllowedUpdates = [UpdateType.Message, UpdateType.CallbackQuery, UpdateType.PreCheckoutQuery] },
    cts.Token);

Console.WriteLine($"Бот @{me.Username} запущено. Ctrl+C — зупинити.");

try { await Task.Delay(Timeout.Infinite, cts.Token); }
catch (OperationCanceledException) { }

return 0;
