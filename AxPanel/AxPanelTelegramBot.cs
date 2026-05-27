using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using AxPanel.Model;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace AxPanel;

public class AxPanelTelegramBot : IDisposable
{
    [DllImport( "user32.dll" )]
    private static extern bool LockWorkStation();

    private readonly string _botToken;
    private readonly MainConfig _config;
    private TelegramBotClient? _botClient;
    private CancellationTokenSource? _cts;
    private bool _isRunning = false;

    private readonly ConcurrentDictionary<long, FileBrowserSession> _sessions = new();

    private static readonly HashSet<string> TextExtensions = new( StringComparer.OrdinalIgnoreCase )
    {
        ".txt", ".log", ".cs", ".json", ".xml", ".md", ".ini", ".cfg",
        ".ps1", ".bat", ".cmd", ".yaml", ".yml", ".toml", ".sql",
        ".py", ".js", ".ts", ".jsx", ".tsx", ".css", ".html", ".htm",
        ".h", ".c", ".cpp", ".hpp", ".csproj", ".sln", ".env", ".gitignore",
        ".config", ".props", ".targets", ".editorconfig", ".ruleset",
        ".nuspec", ".asmdef", ".rsp", ".resx", ".scss", ".less",
        ".vue", ".svelte", ".php", ".rb", ".go", ".rs", ".swift", ".kt",
        ".gradle", ".pl", ".sh", ".bash", ".zshrc", ".bashrc"
    };

    private const int MaxMessageLength = 4000;
    private static readonly TimeSpan DeleteConfirmTimeout = TimeSpan.FromMinutes( 5 );

    public AxPanelTelegramBot( MainConfig config )
    {
        _config = config;
        _botToken = ReadTokenFromFile();

        if ( string.IsNullOrEmpty( _botToken ) )
        {
            throw new InvalidOperationException(
                "Не удалось прочитать токен Telegram бота. " +
                "Укажите путь к файлу с токеном в config.json (TelegramBotTokenFile) " +
                $"или создайте файл по умолчанию: {config.TelegramBotTokenFile ?? @"D:\ax-panel-telegram-bot-token-file.inf"}" );
        }
    }

    private string ReadTokenFromFile()
    {
        string tokenFilePath = _config.TelegramBotTokenFile ?? @"D:\ax-panel-telegram-bot-token-file.inf";

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
            AllowedUpdates = new[] { UpdateType.Message }
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
        if ( update.Message?.Text is not { } messageText )
            return;

        long chatId = update.Message.Chat.Id;

        if ( _config.AllowedChatIds == null || !_config.AllowedChatIds.Contains( chatId ) )
        {
            Debug.WriteLine( $"⛔ Доступ запрещён для chatId={chatId}" );
            await botClient.SendMessage( chatId, "⛔ У вас нет доступа к этому боту.", cancellationToken: cancellationToken );
            return;
        }

        string fullCommand = messageText.Trim();
        int slashIdx = fullCommand.IndexOf( '/' );
        string effectiveCmd = slashIdx >= 0 ? fullCommand[slashIdx..] : fullCommand;
        string lowerCommand = effectiveCmd.ToLowerInvariant();

        if ( lowerCommand.StartsWith( "/tree" ) )
        {
            await HandleTreeCommand( botClient, chatId, effectiveCmd, cancellationToken );
            return;
        }

        string[] parts = effectiveCmd.Split( ' ', 2, StringSplitOptions.RemoveEmptyEntries );
        string cmd = parts[0].ToLowerInvariant();
        string args = parts.Length > 1 ? parts[1].Trim() : "";

        switch ( cmd )
        {
            case "/start":
                await botClient.SendMessage( chatId,
                    "Привет! Я бот для управления AxPanel.\n" +
                    "Используйте /help для списка команд.",
                    replyMarkup: GetMainMenuKeyboard(),
                    cancellationToken: cancellationToken );
                break;

            case "/help":
                await botClient.SendMessage( chatId, GetHelpMessage(), cancellationToken: cancellationToken );
                break;

            case "/status":
                await botClient.SendMessage( chatId, GetSystemStatus(), cancellationToken: cancellationToken );
                break;

            case "/lock":
                bool success = LockWorkStation();
                if ( success )
                    await botClient.SendMessage( chatId, "🔒 Экран заблокирован.", cancellationToken: cancellationToken );
                else
                    await botClient.SendMessage( chatId, "❌ Не удалось заблокировать экран.", cancellationToken: cancellationToken );
                break;

            case "/screenshot":
                await HandleScreenshot( botClient, chatId, cancellationToken );
                break;

            case "/drives":
                await HandleListDrives( botClient, chatId, cancellationToken );
                break;

            case "/cd":
                await HandleChangeDirectory( botClient, chatId, args, cancellationToken );
                break;

            case "/ls":
                await HandleListDirectory( botClient, chatId, args, cancellationToken );
                break;

            case "/pwd":
                await HandlePrintWorkingDirectory( botClient, chatId, cancellationToken );
                break;

            case "/cat":
                await HandleCatFile( botClient, chatId, args, cancellationToken );
                break;

            case "/mkdir":
                await HandleCreateDirectory( botClient, chatId, args, cancellationToken );
                break;

            case "/touch":
                await HandleCreateFile( botClient, chatId, args, cancellationToken );
                break;

            case "/rm":
                await HandleDeleteRequest( botClient, chatId, args, cancellationToken );
                break;

            case "/confirm_rm":
                await HandleDeleteConfirm( botClient, chatId, args, cancellationToken );
                break;

            case "/rename":
                await HandleRename( botClient, chatId, args, cancellationToken );
                break;

            case "/info":
                await HandleFileInfo( botClient, chatId, args, cancellationToken );
                break;

            case "/find":
                await HandleFindFiles( botClient, chatId, args, cancellationToken );
                break;

            case "/download":
                await HandleDownloadFile( botClient, chatId, args, cancellationToken );
                break;

            default:
                await botClient.SendMessage( chatId,
                    "Неизвестная команда. Используйте /help для списка команд.",
                    cancellationToken: cancellationToken );
                break;
        }
    }

    private async Task HandleScreenshot( ITelegramBotClient botClient, long chatId, CancellationToken ct )
    {
        await botClient.SendChatAction( chatId, ChatAction.UploadPhoto, cancellationToken: ct );

        string? screenshotPath = await CaptureFullScreen();
        if ( screenshotPath != null )
        {
            await using FileStream fileStream = new( screenshotPath, FileMode.Open, FileAccess.Read );
            var inputFile = new InputFileStream( fileStream, "screenshot.png" );
            await botClient.SendPhoto( chatId, inputFile, cancellationToken: ct );
            File.Delete( screenshotPath );
        }
        else
        {
            await botClient.SendMessage( chatId, "Не удалось сделать скриншот.", cancellationToken: ct );
        }
    }

    private async Task<string?> CaptureFullScreen()
    {
        try
        {
            int allScreenWidth = SystemInformation.VirtualScreen.Width;
            int allScreenHeight = SystemInformation.VirtualScreen.Height;
            Point screenTopLeft = SystemInformation.VirtualScreen.Location;

            using var bitmap = new Bitmap( allScreenWidth, allScreenHeight );
            using var graphics = Graphics.FromImage( bitmap );
            graphics.CopyFromScreen( screenTopLeft.X, screenTopLeft.Y, 0, 0, bitmap.Size );

            string tempFilePath = Path.GetTempFileName() + ".png";
            bitmap.Save( tempFilePath, ImageFormat.Png );
            return tempFilePath;
        }
        catch
        {
            return null;
        }
    }

    private string GetSystemStatus()
    {
        return "📊 **Статус системы**\n" + "Работает\n" + "Подробный статус будет реализован через ProcessMonitor.";
    }

    private string GetHelpMessage()
    {
        return "🤖 **AxPanel Bot — доступные команды**\n\n" +
               "**Система:**\n" +
               "/status — состояние системы\n" +
               "/lock — заблокировать экран\n" +
               "/screenshot — скриншот\n\n" +
               "**Навигация:**\n" +
               "/drives — список дисков\n" +
               "/cd <путь> — сменить папку\n" +
               "/ls [путь] — содержимое папки\n" +
               "/pwd — текущая папка\n\n" +
               "**Файлы:**\n" +
               "/cat <путь> — чтение текстового файла\n" +
               "/download <имя> — скачать файл\n" +
               "/mkdir <путь> — создать папку\n" +
               "/touch <путь> — создать файл\n" +
               "/rm <путь> — удалить (с подтверждением)\n" +
               "/rename <путь> <новое_имя> — переименовать\n" +
               "/info <путь> — свойства файла/папки\n" +
               "/find <маска> — поиск файлов\n" +
               "/tree[N] [путь] — дерево папок\n\n" +
               "/help — это сообщение";
    }

    private ReplyKeyboardMarkup GetMainMenuKeyboard()
    {
        return new ReplyKeyboardMarkup( new[]
        {
            new[] { new KeyboardButton( "🔍 /help" ), new KeyboardButton( "📊 /status" ) },
            new[] { new KeyboardButton( "🔒 /lock" ), new KeyboardButton( "📸 /screenshot" ) }
        } )
        {
            ResizeKeyboard = true
        };
    }

    private FileBrowserSession GetOrCreateSession( long chatId )
    {
        return _sessions.GetOrAdd( chatId, _ => new FileBrowserSession() );
    }

    private string ResolvePath( long chatId, string? path )
    {
        var session = GetOrCreateSession( chatId );

        if ( string.IsNullOrWhiteSpace( path ) )
            return Path.GetFullPath( session.CurrentDirectory );

        if ( Path.IsPathRooted( path ) )
            return Path.GetFullPath( path );

        return Path.GetFullPath( Path.Combine( session.CurrentDirectory, path ) );
    }

    private async Task HandleListDrives( ITelegramBotClient botClient, long chatId, CancellationToken ct )
    {
        try
        {
            var drives = DriveInfo.GetDrives()
                .Where( d => d.IsReady )
                .Select( d =>
                {
                    string type = d.DriveType switch
                    {
                        DriveType.Fixed => "💽 Локальный диск",
                        DriveType.Removable => "💾 Съёмный диск",
                        DriveType.Network => "🌐 Сетевой диск",
                        DriveType.CDRom => "💿 CD/DVD",
                        _ => "📁 Диск"
                    };
                    string size = d.TotalSize > 0
                        ? $" ({d.AvailableFreeSpace / 1073741824.0:F1} ГБ свободно из {d.TotalSize / 1073741824.0:F1} ГБ)"
                        : "";
                    return $"{type} {d.Name} {d.VolumeLabel ?? ""}{size}";
                } );

            string result = "📂 **Доступные диски:**\n\n" + string.Join( "\n", drives );
            await botClient.SendMessage( chatId, result, cancellationToken: ct );
        }
        catch ( Exception ex )
        {
            await botClient.SendMessage( chatId, $"❌ Ошибка: {ex.Message}", cancellationToken: ct );
        }
    }

    private async Task HandleChangeDirectory( ITelegramBotClient botClient, long chatId, string args, CancellationToken ct )
    {
        if ( string.IsNullOrWhiteSpace( args ) )
        {
            await botClient.SendMessage( chatId, "Укажите путь. Пример: /cd C:\\Windows", cancellationToken: ct );
            return;
        }

        try
        {
            string resolvedPath = ResolvePath( chatId, args );

            if ( !Directory.Exists( resolvedPath ) )
            {
                await botClient.SendMessage( chatId, $"❌ Директория не найдена: {resolvedPath}", cancellationToken: ct );
                return;
            }

            var session = GetOrCreateSession( chatId );
            session.CurrentDirectory = resolvedPath;

            await botClient.SendMessage( chatId, $"📂 Текущая директория: {resolvedPath}", cancellationToken: ct );
        }
        catch ( UnauthorizedAccessException )
        {
            await botClient.SendMessage( chatId, "❌ Нет доступа к этой директории.", cancellationToken: ct );
        }
        catch ( Exception ex )
        {
            await botClient.SendMessage( chatId, $"❌ Ошибка: {ex.Message}", cancellationToken: ct );
        }
    }

    private async Task HandleListDirectory( ITelegramBotClient botClient, long chatId, string args, CancellationToken ct )
    {
        string resolvedPath;
        try
        {
            resolvedPath = ResolvePath( chatId, args );
        }
        catch ( Exception ex )
        {
            await botClient.SendMessage( chatId, $"❌ Некорректный путь: {ex.Message}", cancellationToken: ct );
            return;
        }

        if ( !Directory.Exists( resolvedPath ) )
        {
            await botClient.SendMessage( chatId, $"❌ Директория не найдена: {resolvedPath}", cancellationToken: ct );
            return;
        }

        try
        {
            var dirs = new List<string>();
            var files = new List<(string Name, string Size)>();

            try
            {
                foreach ( string dir in Directory.EnumerateDirectories( resolvedPath ) )
                {
                    string name = Path.GetFileName( dir );
                    if ( !name.StartsWith( "." ) )
                        dirs.Add( name );
                }
            }
            catch ( UnauthorizedAccessException ) { }

            try
            {
                foreach ( string file in Directory.EnumerateFiles( resolvedPath ) )
                {
                    string name = Path.GetFileName( file );
                    if ( !name.StartsWith( "." ) )
                    {
                        try
                        {
                            var fi = new FileInfo( file );
                            files.Add( (name, FormatSize( fi.Length )) );
                        }
                        catch
                        {
                            files.Add( (name, "?") );
                        }
                    }
                }
            }
            catch ( UnauthorizedAccessException ) { }

            dirs.Sort();
            files.Sort( ( a, b ) => string.Compare( a.Name, b.Name, StringComparison.OrdinalIgnoreCase ) );

            var sb = new StringBuilder();
            sb.AppendLine( $"📁 **{resolvedPath}**" );
            sb.AppendLine();

            int totalDirs = dirs.Count;
            int totalFiles = files.Count;
            const int maxItems = 50;
            int shown = 0;

            sb.AppendLine( "📁 **Папки:**" );
            foreach ( string dir in dirs )
            {
                if ( shown >= maxItems ) break;
                sb.AppendLine( $"  📁 {dir}" );
                shown++;
            }

            shown = 0;
            sb.AppendLine();
            sb.AppendLine( "📄 **Файлы:**" );

            int maxNameLen = files.Count > 0 ? files.Max( f => f.Name.Length ) : 0;
            int padWidth = Math.Min( maxNameLen, 40 );

            foreach ( var (name, size) in files )
            {
                if ( shown >= maxItems ) break;
                sb.AppendLine( $"  📄 {name.PadRight( padWidth )} [{size}]" );
                shown++;
            }

            int totalShown = Math.Min( totalDirs, maxItems ) + Math.Min( totalFiles, maxItems );
            int total = totalDirs + totalFiles;
            if ( total > totalShown )
            {
                sb.AppendLine();
                sb.AppendLine( $"... и ещё {total - totalShown} элементов" );
            }

            sb.AppendLine();
            sb.AppendLine( $"Всего: {totalDirs} папок, {totalFiles} файлов" );

            string result = sb.ToString();
            if ( result.Length > MaxMessageLength )
                result = result[..MaxMessageLength] + $"\n\n... сообщение обрезано ({result.Length - MaxMessageLength} символов не поместилось)";

            await botClient.SendMessage( chatId, result, cancellationToken: ct );
        }
        catch ( UnauthorizedAccessException )
        {
            await botClient.SendMessage( chatId, "❌ Нет доступа к этой директории.", cancellationToken: ct );
        }
        catch ( Exception ex )
        {
            await botClient.SendMessage( chatId, $"❌ Ошибка: {ex.Message}", cancellationToken: ct );
        }
    }

    private async Task HandlePrintWorkingDirectory( ITelegramBotClient botClient, long chatId, CancellationToken ct )
    {
        var session = GetOrCreateSession( chatId );
        await botClient.SendMessage( chatId, $"📂 Текущая директория: {session.CurrentDirectory}", cancellationToken: ct );
    }

    private async Task HandleCatFile( ITelegramBotClient botClient, long chatId, string args, CancellationToken ct )
    {
        if ( string.IsNullOrWhiteSpace( args ) )
        {
            await botClient.SendMessage( chatId, "Укажите путь к файлу. Пример: /cat C:\\file.txt", cancellationToken: ct );
            return;
        }

        string resolvedPath;
        try
        {
            resolvedPath = ResolvePath( chatId, args );
        }
        catch ( Exception ex )
        {
            await botClient.SendMessage( chatId, $"❌ Некорректный путь: {ex.Message}", cancellationToken: ct );
            return;
        }

        if ( !File.Exists( resolvedPath ) )
        {
            await botClient.SendMessage( chatId, $"❌ Файл не найден: {resolvedPath}", cancellationToken: ct );
            return;
        }

        string ext = Path.GetExtension( resolvedPath );
        if ( !TextExtensions.Contains( ext ) )
        {
            await botClient.SendMessage( chatId, $"❌ Неподдерживаемый тип файла ({ext}). Разрешены только текстовые форматы.", cancellationToken: ct );
            return;
        }

        try
        {
            string content = await File.ReadAllTextAsync( resolvedPath, ct );
            string fileName = Path.GetFileName( resolvedPath );

            if ( content.Length > MaxMessageLength )
                content = content[..MaxMessageLength] + $"\n\n... файл обрезан (всего {content.Length} символов)";

            await botClient.SendMessage( chatId, $"📄 **{fileName}**\n\n{content}", cancellationToken: ct );
        }
        catch ( UnauthorizedAccessException )
        {
            await botClient.SendMessage( chatId, "❌ Нет доступа к файлу.", cancellationToken: ct );
        }
        catch ( Exception ex )
        {
            await botClient.SendMessage( chatId, $"❌ Ошибка чтения: {ex.Message}", cancellationToken: ct );
        }
    }

    private async Task HandleCreateDirectory( ITelegramBotClient botClient, long chatId, string args, CancellationToken ct )
    {
        if ( string.IsNullOrWhiteSpace( args ) )
        {
            await botClient.SendMessage( chatId, "Укажите путь. Пример: /mkdir C:\\NewFolder", cancellationToken: ct );
            return;
        }

        string resolvedPath;
        try
        {
            resolvedPath = ResolvePath( chatId, args );
        }
        catch ( Exception ex )
        {
            await botClient.SendMessage( chatId, $"❌ Некорректный путь: {ex.Message}", cancellationToken: ct );
            return;
        }

        try
        {
            Directory.CreateDirectory( resolvedPath );
            await botClient.SendMessage( chatId, $"✅ Папка создана: {resolvedPath}", cancellationToken: ct );
        }
        catch ( UnauthorizedAccessException )
        {
            await botClient.SendMessage( chatId, "❌ Нет доступа для создания папки.", cancellationToken: ct );
        }
        catch ( Exception ex )
        {
            await botClient.SendMessage( chatId, $"❌ Ошибка: {ex.Message}", cancellationToken: ct );
        }
    }

    private async Task HandleCreateFile( ITelegramBotClient botClient, long chatId, string args, CancellationToken ct )
    {
        if ( string.IsNullOrWhiteSpace( args ) )
        {
            await botClient.SendMessage( chatId, "Укажите путь. Пример: /touch C:\\test.txt", cancellationToken: ct );
            return;
        }

        string resolvedPath;
        try
        {
            resolvedPath = ResolvePath( chatId, args );
        }
        catch ( Exception ex )
        {
            await botClient.SendMessage( chatId, $"❌ Некорректный путь: {ex.Message}", cancellationToken: ct );
            return;
        }

        try
        {
            if ( File.Exists( resolvedPath ) )
            {
                File.SetLastWriteTime( resolvedPath, DateTime.Now );
                await botClient.SendMessage( chatId, $"✅ Время файла обновлено: {resolvedPath}", cancellationToken: ct );
            }
            else
            {
                await File.WriteAllTextAsync( resolvedPath, "", ct );
                await botClient.SendMessage( chatId, $"✅ Файл создан: {resolvedPath}", cancellationToken: ct );
            }
        }
        catch ( UnauthorizedAccessException )
        {
            await botClient.SendMessage( chatId, "❌ Нет доступа для создания файла.", cancellationToken: ct );
        }
        catch ( Exception ex )
        {
            await botClient.SendMessage( chatId, $"❌ Ошибка: {ex.Message}", cancellationToken: ct );
        }
    }

    private async Task HandleDeleteRequest( ITelegramBotClient botClient, long chatId, string args, CancellationToken ct )
    {
        if ( string.IsNullOrWhiteSpace( args ) )
        {
            await botClient.SendMessage( chatId, "Укажите путь. Пример: /rm C:\\folder", cancellationToken: ct );
            return;
        }

        string resolvedPath;
        try
        {
            resolvedPath = ResolvePath( chatId, args );
        }
        catch ( Exception ex )
        {
            await botClient.SendMessage( chatId, $"❌ Некорректный путь: {ex.Message}", cancellationToken: ct );
            return;
        }

        bool exists = Directory.Exists( resolvedPath ) || File.Exists( resolvedPath );
        if ( !exists )
        {
            await botClient.SendMessage( chatId, $"❌ Путь не найден: {resolvedPath}", cancellationToken: ct );
            return;
        }

        string type = Directory.Exists( resolvedPath ) ? "папку" : "файл";
        var session = GetOrCreateSession( chatId );
        session.LastDeletePath = resolvedPath;
        session.LastDeleteTime = DateTime.UtcNow;

        await botClient.SendMessage( chatId,
            $"⚠️ **Подтвердите удаление**\n\n" +
            $"Вы уверены, что хотите удалить {type}?\n" +
            $"`{resolvedPath}`\n\n" +
            $"Для подтверждения отправьте:\n" +
            $"/confirm_rm {resolvedPath}\n\n" +
            $"⏱ Подтверждение действует 5 минут.",
            parseMode: ParseMode.Markdown,
            cancellationToken: ct );
    }

    private async Task HandleDeleteConfirm( ITelegramBotClient botClient, long chatId, string args, CancellationToken ct )
    {
        string resolvedPath;
        try
        {
            resolvedPath = ResolvePath( chatId, args );
        }
        catch ( Exception ex )
        {
            await botClient.SendMessage( chatId, $"❌ Некорректный путь: {ex.Message}", cancellationToken: ct );
            return;
        }

        var session = GetOrCreateSession( chatId );

        if ( session.LastDeletePath != resolvedPath )
        {
            await botClient.SendMessage( chatId,
                "❌ Путь не совпадает с запрошенным для удаления. Сначала выполните /rm <путь>.",
                cancellationToken: ct );
            return;
        }

        if ( session.LastDeleteTime == null || DateTime.UtcNow - session.LastDeleteTime > DeleteConfirmTimeout )
        {
            session.LastDeletePath = "";
            await botClient.SendMessage( chatId, "⏱ Время подтверждения истекло. Выполните /rm заново.", cancellationToken: ct );
            return;
        }

        try
        {
            if ( Directory.Exists( resolvedPath ) )
            {
                Directory.Delete( resolvedPath, recursive: true );
                await botClient.SendMessage( chatId, $"✅ Папка удалена: {resolvedPath}", cancellationToken: ct );
            }
            else if ( File.Exists( resolvedPath ) )
            {
                File.Delete( resolvedPath );
                await botClient.SendMessage( chatId, $"✅ Файл удалён: {resolvedPath}", cancellationToken: ct );
            }
            else
            {
                await botClient.SendMessage( chatId, $"❌ Путь не найден: {resolvedPath}", cancellationToken: ct );
            }

            session.LastDeletePath = "";
            session.LastDeleteTime = null;
        }
        catch ( UnauthorizedAccessException )
        {
            await botClient.SendMessage( chatId, "❌ Нет доступа для удаления.", cancellationToken: ct );
        }
        catch ( IOException ex )
        {
            await botClient.SendMessage( chatId, $"❌ Файл занят: {ex.Message}", cancellationToken: ct );
        }
        catch ( Exception ex )
        {
            await botClient.SendMessage( chatId, $"❌ Ошибка удаления: {ex.Message}", cancellationToken: ct );
        }
    }

    private async Task HandleRename( ITelegramBotClient botClient, long chatId, string args, CancellationToken ct )
    {
        if ( string.IsNullOrWhiteSpace( args ) )
        {
            await botClient.SendMessage( chatId, "Укажите путь и новое имя. Пример: /rename C:\\old.txt new.txt", cancellationToken: ct );
            return;
        }

        int lastSpace = args.LastIndexOf( ' ' );
        if ( lastSpace < 0 )
        {
            await botClient.SendMessage( chatId, "Укажите новое имя. Пример: /rename C:\\file.txt newname.txt", cancellationToken: ct );
            return;
        }

        string sourcePathPart = args[..lastSpace].Trim();
        string newName = args[(lastSpace + 1)..].Trim();

        if ( string.IsNullOrWhiteSpace( sourcePathPart ) || string.IsNullOrWhiteSpace( newName ) )
        {
            await botClient.SendMessage( chatId, "Укажите путь и новое имя. Пример: /rename C:\\file.txt newname.txt", cancellationToken: ct );
            return;
        }

        string resolvedPath;
        try
        {
            resolvedPath = ResolvePath( chatId, sourcePathPart );
        }
        catch ( Exception ex )
        {
            await botClient.SendMessage( chatId, $"❌ Некорректный путь: {ex.Message}", cancellationToken: ct );
            return;
        }

        bool isDir = Directory.Exists( resolvedPath );
        bool isFile = File.Exists( resolvedPath );

        if ( !isDir && !isFile )
        {
            await botClient.SendMessage( chatId, $"❌ Путь не найден: {resolvedPath}", cancellationToken: ct );
            return;
        }

        string? parentDir = Path.GetDirectoryName( resolvedPath );
        if ( parentDir == null )
        {
            await botClient.SendMessage( chatId, "❌ Невозможно определить родительскую директорию.", cancellationToken: ct );
            return;
        }

        string destPath = Path.Combine( parentDir, newName );

        try
        {
            if ( isDir )
                Directory.Move( resolvedPath, destPath );
            else
                File.Move( resolvedPath, destPath );

            string type = isDir ? "Папка" : "Файл";
            await botClient.SendMessage( chatId, $"✅ {type} переименована:\n`{resolvedPath}` → `{destPath}`",
                parseMode: ParseMode.Markdown, cancellationToken: ct );
        }
        catch ( UnauthorizedAccessException )
        {
            await botClient.SendMessage( chatId, "❌ Нет доступа для переименования.", cancellationToken: ct );
        }
        catch ( IOException ex )
        {
            await botClient.SendMessage( chatId, $"❌ Ошибка: {ex.Message}", cancellationToken: ct );
        }
        catch ( Exception ex )
        {
            await botClient.SendMessage( chatId, $"❌ Ошибка: {ex.Message}", cancellationToken: ct );
        }
    }

    private async Task HandleFileInfo( ITelegramBotClient botClient, long chatId, string args, CancellationToken ct )
    {
        if ( string.IsNullOrWhiteSpace( args ) )
        {
            await botClient.SendMessage( chatId, "Укажите путь. Пример: /info C:\\file.txt", cancellationToken: ct );
            return;
        }

        string resolvedPath;
        try
        {
            resolvedPath = ResolvePath( chatId, args );
        }
        catch ( Exception ex )
        {
            await botClient.SendMessage( chatId, $"❌ Некорректный путь: {ex.Message}", cancellationToken: ct );
            return;
        }

        try
        {
            if ( Directory.Exists( resolvedPath ) )
            {
                var di = new DirectoryInfo( resolvedPath );
                var sb = new StringBuilder();
                sb.AppendLine( $"📁 **Информация о папке**" );
                sb.AppendLine( $"`{resolvedPath}`" );
                sb.AppendLine();
                sb.AppendLine( $"📅 Создана: {di.CreationTime:yyyy-MM-dd HH:mm:ss}" );
                sb.AppendLine( $"📅 Изменена: {di.LastWriteTime:yyyy-MM-dd HH:mm:ss}" );
                sb.AppendLine( $"📅 Открыта: {di.LastAccessTime:yyyy-MM-dd HH:mm:ss}" );
                sb.AppendLine( $"🔰 Атрибуты: {di.Attributes}" );

                try
                {
                    int subDirs = Directory.EnumerateDirectories( resolvedPath ).Count();
                    int subFiles = Directory.EnumerateFiles( resolvedPath ).Count();
                    long totalSize = Directory.EnumerateFiles( resolvedPath, "*", SearchOption.AllDirectories )
                        .Sum( f => { try { return new FileInfo( f ).Length; } catch { return 0L; } } );
                    sb.AppendLine();
                    sb.AppendLine( $"📊 Содержит: {subDirs} папок, {subFiles} файлов" );
                    sb.AppendLine( $"💾 Общий размер: {FormatSize( totalSize )}" );
                }
                catch { }

                await botClient.SendMessage( chatId, sb.ToString(), cancellationToken: ct );
            }
            else if ( File.Exists( resolvedPath ) )
            {
                var fi = new FileInfo( resolvedPath );
                var sb = new StringBuilder();
                sb.AppendLine( $"📄 **Информация о файле**" );
                sb.AppendLine( $"`{resolvedPath}`" );
                sb.AppendLine();
                sb.AppendLine( $"📏 Размер: {FormatSize( fi.Length )}" );
                sb.AppendLine( $"📅 Создан: {fi.CreationTime:yyyy-MM-dd HH:mm:ss}" );
                sb.AppendLine( $"📅 Изменён: {fi.LastWriteTime:yyyy-MM-dd HH:mm:ss}" );
                sb.AppendLine( $"📅 Открыт: {fi.LastAccessTime:yyyy-MM-dd HH:mm:ss}" );
                sb.AppendLine( $"🔰 Атрибуты: {fi.Attributes}" );
                sb.AppendLine( $"🔤 Расширение: {fi.Extension}" );

                await botClient.SendMessage( chatId, sb.ToString(), cancellationToken: ct );
            }
            else
            {
                await botClient.SendMessage( chatId, $"❌ Путь не найден: {resolvedPath}", cancellationToken: ct );
            }
        }
        catch ( UnauthorizedAccessException )
        {
            await botClient.SendMessage( chatId, "❌ Нет доступа.", cancellationToken: ct );
        }
        catch ( Exception ex )
        {
            await botClient.SendMessage( chatId, $"❌ Ошибка: {ex.Message}", cancellationToken: ct );
        }
    }

    private async Task HandleFindFiles( ITelegramBotClient botClient, long chatId, string args, CancellationToken ct )
    {
        if ( string.IsNullOrWhiteSpace( args ) )
        {
            await botClient.SendMessage( chatId, "Укажите маску поиска. Пример: /find *.cs", cancellationToken: ct );
            return;
        }

        var session = GetOrCreateSession( chatId );
        string searchDir = session.CurrentDirectory;

        await botClient.SendChatAction( chatId, ChatAction.Typing, cancellationToken: ct );

        try
        {
            var result = new List<string>();

            try
            {
                foreach ( string file in Directory.EnumerateFiles( searchDir, args, SearchOption.AllDirectories ) )
                {
                    if ( result.Count >= 50 ) break;
                    string relativePath = Path.GetRelativePath( searchDir, file );
                    result.Add( $"📄 {relativePath}" );
                }
            }
            catch ( UnauthorizedAccessException ) { }
            catch ( DirectoryNotFoundException ) { }

            if ( result.Count == 0 )
            {
                await botClient.SendMessage( chatId, $"🔍 Поиск `{args}` в `{searchDir}` — ничего не найдено.",
                    parseMode: ParseMode.Markdown, cancellationToken: ct );
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine( $"🔍 **Результаты поиска:** `{args}`" );
            sb.AppendLine( $"📂 `{searchDir}`" );
            sb.AppendLine();
            foreach ( string r in result.Take( 50 ) )
                sb.AppendLine( r );
            sb.AppendLine();
            sb.AppendLine( $"Найдено: {result.Count} файлов" );

            string output = sb.ToString();
            if ( output.Length > MaxMessageLength )
                output = output[..MaxMessageLength] + "\n\n... обрезано";

            await botClient.SendMessage( chatId, output, cancellationToken: ct );
        }
        catch ( Exception ex )
        {
            await botClient.SendMessage( chatId, $"❌ Ошибка: {ex.Message}", cancellationToken: ct );
        }
    }

    private async Task HandleDownloadFile( ITelegramBotClient botClient, long chatId, string args, CancellationToken ct )
    {
        if ( string.IsNullOrWhiteSpace( args ) )
        {
            await botClient.SendMessage( chatId, "Укажите имя файла. Пример: /download report.pdf", cancellationToken: ct );
            return;
        }

        var session = GetOrCreateSession( chatId );
        string searchDir = session.CurrentDirectory;

        await botClient.SendChatAction( chatId, ChatAction.UploadDocument, cancellationToken: ct );

        try
        {
            List<string> matches;

            try
            {
                matches = Directory.EnumerateFiles( searchDir, args, SearchOption.AllDirectories ).ToList();
            }
            catch ( UnauthorizedAccessException )
            {
                await botClient.SendMessage( chatId, "❌ Нет доступа для поиска файлов.", cancellationToken: ct );
                return;
            }
            catch ( DirectoryNotFoundException )
            {
                await botClient.SendMessage( chatId, "❌ Директория не найдена.", cancellationToken: ct );
                return;
            }

            if ( matches.Count == 0 )
            {
                await botClient.SendMessage( chatId,
                    $"❌ Файл `{args}` не найден в `{searchDir}`.",
                    parseMode: ParseMode.Markdown, cancellationToken: ct );
                return;
            }

            if ( matches.Count > 1 )
            {
                var sb = new StringBuilder();
                sb.AppendLine( $"⚠️ Найдено несколько файлов `{args}`:" );
                sb.AppendLine();
                int shown = 0;
                foreach ( string match in matches )
                {
                    if ( shown >= 20 ) break;
                    string rel = Path.GetRelativePath( searchDir, match );
                    sb.AppendLine( $"{shown + 1}. `{rel}`" );
                    shown++;
                }
                if ( matches.Count > 20 )
                    sb.AppendLine( $"... и ещё {matches.Count - 20}" );

                await botClient.SendMessage( chatId, sb.ToString(), parseMode: ParseMode.Markdown, cancellationToken: ct );
                return;
            }

            string filePath = matches[0];
            var fi = new FileInfo( filePath );

            if ( fi.Length > 50 * 1024 * 1024 )
            {
                await botClient.SendMessage( chatId, $"❌ Файл слишком большой (больше 50 MB): {FormatSize( fi.Length )}", cancellationToken: ct );
                return;
            }

            await using FileStream stream = new( filePath, FileMode.Open, FileAccess.Read );
            var inputFile = new InputFileStream( stream, fi.Name );
            await botClient.SendDocument( chatId, inputFile, cancellationToken: ct );
        }
        catch ( Exception ex )
        {
            await botClient.SendMessage( chatId, $"❌ Ошибка: {ex.Message}", cancellationToken: ct );
        }
    }

    private async Task HandleTreeCommand( ITelegramBotClient botClient, long chatId, string fullCommand, CancellationToken ct )
    {
        string rest = fullCommand["/tree".Length..].TrimStart();
        int depth = 2;

        if ( rest.Length > 0 && (char.IsDigit( rest[0] ) || rest[0] == '-') )
        {
            int endIdx = 0;
            while ( endIdx < rest.Length && (char.IsDigit( rest[endIdx] ) || rest[endIdx] == '-') )
                endIdx++;

            if ( int.TryParse( rest[..endIdx], out int parsedDepth ) && parsedDepth > 0 )
            {
                depth = parsedDepth;
                rest = rest[endIdx..].TrimStart();
            }
        }

        string path = rest;
        string resolvedPath;
        try
        {
            resolvedPath = ResolvePath( chatId, path );
        }
        catch ( Exception ex )
        {
            await botClient.SendMessage( chatId, $"❌ Некорректный путь: {ex.Message}", cancellationToken: ct );
            return;
        }

        if ( !Directory.Exists( resolvedPath ) )
        {
            await botClient.SendMessage( chatId, $"❌ Директория не найдена: {resolvedPath}", cancellationToken: ct );
            return;
        }

        await botClient.SendChatAction( chatId, ChatAction.Typing, cancellationToken: ct );

        try
        {
            var sb = new StringBuilder();
            sb.AppendLine( $"🌳 **Дерево папок** (глубина: {depth})" );
            sb.AppendLine( $"📂 `{resolvedPath}`" );
            sb.AppendLine();

            int totalCount = BuildTree( sb, resolvedPath, "", depth, 0, MaxMessageLength - 200 );

            if ( sb.Length > MaxMessageLength )
            {
                sb.Length = MaxMessageLength;
                sb.AppendLine();
                sb.Append( "... обрезано" );
            }

            sb.AppendLine();
            sb.AppendLine( $"Всего папок показано: {totalCount}" );

            await botClient.SendMessage( chatId, sb.ToString(), cancellationToken: ct );
        }
        catch ( UnauthorizedAccessException )
        {
            await botClient.SendMessage( chatId, "❌ Нет доступа для построения дерева.", cancellationToken: ct );
        }
        catch ( Exception ex )
        {
            await botClient.SendMessage( chatId, $"❌ Ошибка: {ex.Message}", cancellationToken: ct );
        }
    }

    private static int BuildTree( StringBuilder sb, string dirPath, string indent, int maxDepth, int currentDepth, int maxLen )
    {
        int count = 0;
        if ( currentDepth >= maxDepth || sb.Length >= maxLen )
            return count;

        try
        {
            var dirs = Directory.EnumerateDirectories( dirPath )
                .Where( d => !Path.GetFileName( d ).StartsWith( "." ) )
                .OrderBy( d => Path.GetFileName( d ) )
                .ToList();

            for ( int i = 0; i < dirs.Count; i++ )
            {
                if ( sb.Length >= maxLen ) break;

                string dirName = Path.GetFileName( dirs[i] );
                bool isLast = i == dirs.Count - 1;
                string connector = isLast ? "└── " : "├── ";
                string childIndent = isLast ? "    " : "│   ";

                sb.AppendLine( $"{indent}{connector}📁 {dirName}" );
                count++;

                count += BuildTree( sb, dirs[i], indent + childIndent, maxDepth, currentDepth + 1, maxLen );
            }
        }
        catch ( UnauthorizedAccessException ) { }

        return count;
    }

    private static string FormatSize( long bytes )
    {
        return bytes switch
        {
            < 1024 => $"{bytes} B",
            < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
            < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024.0):F1} MB",
            _ => $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB"
        };
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
    }

    private class FileBrowserSession
    {
        public string CurrentDirectory { get; set; } = Environment.GetFolderPath( Environment.SpecialFolder.Desktop );
        public string LastDeletePath { get; set; } = "";
        public DateTime? LastDeleteTime { get; set; }
    }
}
