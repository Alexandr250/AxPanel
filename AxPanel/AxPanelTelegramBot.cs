using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace AxPanel;

public class AxPanelTelegramBot : IDisposable
{
    [DllImport( "user32.dll" )]
    private static extern bool LockWorkStation();

    private readonly string _botToken;
    private TelegramBotClient? _botClient;
    private CancellationTokenSource? _cts;
    private bool _isRunning = false;

    // Системные счетчики для получения общей информации
    private PerformanceCounter? _cpuCounter;
    private PerformanceCounter? _ramCounter;

    public AxPanelTelegramBot()
    {
        // Читаем токен из файла
        _botToken = ReadTokenFromFile();

        if ( string.IsNullOrEmpty( _botToken ) )
        {
            throw new InvalidOperationException( "Не удалось прочитать токен Telegram бота. Проверьте файл C:\\ax-panel-telegram-bot-token-file.inf" );
        }

        // Инициализация счетчиков (как было раньше)
        try
        {
            _cpuCounter = new PerformanceCounter( "Processor", "% Processor Time", "_Total" );
            _cpuCounter.NextValue();
            _ramCounter = new PerformanceCounter( "Memory", "Available MBytes" );
        }
        catch
        {
            _cpuCounter = null;
            _ramCounter = null;
        }
    }

    private string ReadTokenFromFile()
    {
        string tokenFilePath = @"D:\ax-panel-telegram-bot-token-file.inf";

        try
        {
            if ( !File.Exists( tokenFilePath ) )
            {
                Debug.WriteLine( $"Файл с токеном не найден: {tokenFilePath}" );
                return string.Empty;
            }

            string token = File.ReadAllText( tokenFilePath ).Trim();
            return token;
        }
        catch ( Exception ex )
        {
            Debug.WriteLine( $"Ошибка чтения токена: {ex.Message}" );
            return string.Empty;
        }
    }

    public async Task StartAsync()
    {
        if ( _isRunning ) return;

        _botClient = new TelegramBotClient( _botToken );
        _cts = new CancellationTokenSource();

        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = new[] { UpdateType.Message } // Обрабатываем только сообщения
        };

        _botClient.StartReceiving(
            HandleUpdateAsync,
            HandleErrorAsync,
            receiverOptions,
            _cts.Token
        );

        _isRunning = true;
        Debug.WriteLine( "Telegram бот запущен." );
    }

    public async Task StopAsync()
    {
        if ( !_isRunning ) return;

        _cts?.Cancel();
        _isRunning = false;
        Debug.WriteLine( "Telegram бот остановлен." );
    }

    private async Task HandleUpdateAsync( ITelegramBotClient botClient, Update update, CancellationToken cancellationToken )
    {
        // Обрабатываем только текстовые сообщения
        if ( update.Message?.Text is not { } messageText )
            return;

        long chatId = update.Message.Chat.Id;
        string? command = messageText.Trim().ToLowerInvariant();

        switch ( command )
        {
            case "/status":
                string statusMessage = GetSystemStatus();
                await botClient.SendMessage( chatId, statusMessage, cancellationToken: cancellationToken );
                break;
            case "/lock":
                bool success = LockWorkStation();
                if ( success )
                    await botClient.SendMessage( chatId, "🔒 Экран заблокирован.", cancellationToken: cancellationToken );
                else
                    await botClient.SendMessage( chatId, "❌ Не удалось заблокировать экран.", cancellationToken: cancellationToken );
                break;
            case "/start":

                await botClient.SendMessage( chatId,
                    "Привет! Я бот для управления AxPanel.\nДоступные команды:\n/status - показать состояние системы",
                    cancellationToken: cancellationToken );
                break;
            case "/screenshot":
                await botClient.SendChatAction( chatId, ChatAction.UploadPhoto, cancellationToken: cancellationToken );

                string? screenshotPath = await CaptureFullScreen();
                if ( screenshotPath != null )
                {
                    await using FileStream fileStream = new FileStream( screenshotPath, FileMode.Open, FileAccess.Read );
                    var inputFile = new InputFileStream( fileStream, "screenshot.png" );
                    await botClient.SendPhoto( chatId, inputFile, cancellationToken: cancellationToken );

                    File.Delete( screenshotPath );
                }
                else
                {
                    await botClient.SendMessage( chatId, "Не удалось сделать скриншот.", cancellationToken: cancellationToken );
                }
                break;
            default:
                await botClient.SendMessage( chatId, "Неизвестная команда. Используйте /start или /status",
                        cancellationToken: cancellationToken );
                break;
        }
    }

    private async Task<string?> CaptureFullScreen()
    {
        // Получаем границы всех экранов системы
        int allScreenWidth = SystemInformation.VirtualScreen.Width;
        int allScreenHeight = SystemInformation.VirtualScreen.Height;
        Point screenTopLeft = SystemInformation.VirtualScreen.Location;

        // Создаём битмап нужного размера
        using ( var bitmap = new Bitmap( allScreenWidth, allScreenHeight ) )
        {
            using ( var graphics = Graphics.FromImage( bitmap ) )
            {
                // Копируем содержимое экрана в битмап
                graphics.CopyFromScreen( screenTopLeft.X, screenTopLeft.Y, 0, 0, bitmap.Size );
            }

            // Сохраняем изображение во временный файл
            string tempFilePath = Path.GetTempFileName() + ".png";
            bitmap.Save( tempFilePath, ImageFormat.Png );

            return tempFilePath; // Возвращаем путь к файлу
        }
    }

    private string GetSystemStatus()
    {
        // Получаем общую загрузку CPU
        string cpuUsage = "N/A";
        if ( _cpuCounter != null )
        {
            try
            {
                // Для точности берем два значения с задержкой, но для краткости можно одно
                float cpu = _cpuCounter.NextValue();
                cpuUsage = $"{cpu:F1}%";
            }
            catch { cpuUsage = "N/A"; }
        }

        // Получаем доступную память (MB)
        string ramAvailable = "N/A";
        if ( _ramCounter != null )
        {
            try
            {
                float available = _ramCounter.NextValue();
                ramAvailable = $"{available:F0} MB";
            }
            catch { ramAvailable = "N/A"; }
        }
        
        // Теперь можно добавить список отслеживаемых процессов (из ProcessMonitor)
        var runningProcesses = GetMonitoredProcesses();

        string result = $"📊 **Статус системы**\n" + $"Работает";

        return result;
    }

    private string GetMonitoredProcesses()
    {
        // Здесь мы используем существующий ProcessMonitor, который уже собирает статистику по целевым путям.
        // Если у вас есть доступ к экземпляру ProcessMonitor (например, через синглтон или сервис), то можно взять его.
        // Для простоты сделаем заглушку: покажем общий список процессов, чьи пути отслеживаются и активны.
        // Но лучше получить данные из вашего ProcessMonitor.

        // Если у вас нет доступа к ProcessMonitor из этого класса, можно вернуть просто "недоступно".
        // В реальном проекте я бы предложил сделать статический класс AppState с текущей статистикой.
        // Или передать ссылку на ProcessMonitor через конструктор.

        // Пока вернём пример:
        try
        {
            // Попробуем получить данные из глобального монитора (если он доступен через статическое свойство)
            // Допустим, у вас есть статический класс GlobalProcessMonitor со свойством LastStats
            // var stats = GlobalProcessMonitor.LastStats; 
            // затем форматируем...

            // Заглушка:
            return "- *Блокнот* (активен)\n- *Калькулятор* (активен)";
        }
        catch
        {
            return "Нет данных о процессах.";
        }
    }

    private Task HandleErrorAsync( ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken )
    {
        Debug.WriteLine( $"Ошибка в Telegram боте: {exception.Message}" );
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cpuCounter?.Dispose();
        _ramCounter?.Dispose();
    }
}
