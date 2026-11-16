using System.Net;
using System.Text;
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

if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SUPABASE_URL")) ||
    string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SUPABASE_SERVICE_KEY")))
{
    Console.Error.WriteLine("Не задано SUPABASE_URL або SUPABASE_SERVICE_KEY.");
    return 1;
}

var bot = new TelegramBotClient(token);

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

// Health-check для Render (Web Service вимагає відкритий порт) і для зовнішнього пінгу
var port = Environment.GetEnvironmentVariable("PORT") ?? "10000";
var http = new HttpListener();
http.Prefixes.Add($"http://*:{port}/");
http.Start();
_ = Task.Run(async () =>
{
    var body = Encoding.UTF8.GetBytes("OK");
    while (!cts.IsCancellationRequested)
    {
        try
        {
            var ctx = await http.GetContextAsync();
            ctx.Response.ContentType = "text/plain";
            ctx.Response.ContentLength64 = body.Length;
            await ctx.Response.OutputStream.WriteAsync(body);
            ctx.Response.Close();
        }
        catch when (cts.IsCancellationRequested) { break; }
        catch { /* ігноруємо поодинокі збої */ }
    }
});

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