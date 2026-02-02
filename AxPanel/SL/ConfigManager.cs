using System.Diagnostics;
using System.Text.Json;
using System.Xml.Linq;
using AxPanel.Model;

namespace AxPanel.SL;

public class ConfigManager
{
    private const string ItemsConfigFile = "items-config.json";
    private const string ConfigFile = "config.json";

    // Опции для красивого JSON
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static void SaveMainConfig( MainConfig mainConfig )
    {
        try
        {
            string jsonString = JsonSerializer.Serialize( mainConfig, JsonOptions );
            File.WriteAllText( ConfigFile, jsonString );
        }
        catch ( Exception ex ) { Debug.WriteLine( ex.Message ); }
    }

    public static MainConfig? ReadMainConfig()
    {
        if ( !File.Exists( ConfigFile ) )
        {
            var defaultConfig = new MainConfig
            {
                Width = 400,
                Height = 600,
                HeaderHeight = 30,
                BorderWidth = 5
            };
            SaveMainConfig( defaultConfig ); // Создаем файл сразу
            return defaultConfig;
        }

        try
        {
            string json = File.ReadAllText( ConfigFile );
            return JsonSerializer.Deserialize<MainConfig>( json );
        }
        catch { return new MainConfig(); }
    }

    public static void SaveItemsConfig( MainModel panelModel )
    {
        // Оставляем логику клонирования без системных контейнеров
        MainModel panelModelClone = new()
        {
            Containers = panelModel.Containers.Where( c => c.Type != ContainerType.System ).ToList()
        };

        try
        {
            string jsonString = JsonSerializer.Serialize( panelModelClone, JsonOptions );
            File.WriteAllText( ItemsConfigFile, jsonString );
        }
        catch ( Exception ex ) { Debug.WriteLine( ex.Message ); }
    }

    [Obsolete]
    public static MainModel ReadItemsConfig()
    {
        return ReadModel(); // Перенаправляем на актуальный метод для чистоты
    }

    public static MainModel ReadModel()
    {
        MainModel? panelModel = null;

        // 1. Попытка чтения из файла
        if ( File.Exists( ItemsConfigFile ) )
        {
            try
            {
                string json = File.ReadAllText( ItemsConfigFile );
                panelModel = JsonSerializer.Deserialize<MainModel>( json );
            }
            catch ( Exception ex )
            {
                Debug.WriteLine( $"Ошибка загрузки: {ex.Message}" );
            }
        }

        // 2. Если файла нет, он пуст или в нем нет контейнеров — создаем один пустой
        if ( panelModel == null || panelModel.Containers == null || panelModel.Containers.Count == 0 )
        {
            panelModel = new MainModel { Containers = new List<ContainerItem>() };

            // Создаем абсолютно пустой контейнер (без кнопок)
            var emptyContainer = new ContainerItem
            {
                Name = "Новая панель",
                Items = [],
                Type = ContainerType.Normal // Предполагаем, что это обычный тип
            };

            panelModel.Containers.Add( emptyContainer );

            // Сохраняем, чтобы файл физически появился на диске
            SaveItemsConfig( panelModel );
        }

        // 3. Индексация существующих элементов (если они есть)
        foreach ( var container in panelModel.Containers )
        {
            if ( container.Items == null ) continue;
            for ( int i = 0; i < container.Items.Count; i++ )
            {
                if ( container.Items[ i ] != null ) container.Items[ i ].Id = i;
            }
        }

        // Принудительное добавление системного контейнера (если ваша логика это требует)
        if ( !panelModel.Containers.Any( c => c.Type == ContainerType.System ) )
        {
            panelModel.Containers.Add( BuildStandatrContainer() );
        }

        return panelModel;
    }

    private static ContainerItem BuildStandatrContainer()
    {
        ContainerItem container = new()
        {
            Name = "Системные инструменты",
            Type = ContainerType.System,
            Items = []
        };

        var groups = new Dictionary<string, List<(string Name, string Path)>>
    {
        { "Основные", [
                ( "Проводник", Environment.GetEnvironmentVariable( "windir" ) + "\\explorer.exe" ),
                ( "Блокнот", Environment.GetFolderPath( Environment.SpecialFolder.System ) + "\\notepad.exe" ),
                ( "Калькулятор", Environment.GetFolderPath( Environment.SpecialFolder.System ) + "\\calc.exe" ),
                ( "Диспетчер задач", Environment.GetFolderPath( Environment.SpecialFolder.System ) + "\\taskmgr.exe" )
            ]
        },
        { "Администрирование", [
                ( "Командная строка", Environment.GetFolderPath( Environment.SpecialFolder.System ) + "\\cmd.exe" ),
                ( "PowerShell", Environment.GetFolderPath( Environment.SpecialFolder.System ) + "\\WindowsPowerShell\\v1.0\\powershell.exe" ),
                ( "Редактор реестра", Environment.GetEnvironmentVariable( "windir" ) + "\\regedit.exe" ),
                ( "Службы", Environment.GetFolderPath( Environment.SpecialFolder.System ) + "\\services.msc" ),
                ( "Управление компьютером", Environment.GetFolderPath( Environment.SpecialFolder.System ) + "\\compmgmt.msc" )
            ]
        },
        { "Сеть и Настройка", [
                ( "Сетевые подключения", Environment.GetFolderPath( Environment.SpecialFolder.System ) + "\\ncpa.cpl" ),
                ( "Панель управления", Environment.GetFolderPath( Environment.SpecialFolder.System ) + "\\control.exe" ),
                ( "Удаление программ", Environment.GetFolderPath( Environment.SpecialFolder.System ) + "\\appwiz.cpl" ),
                ( "Параметры звука", Environment.GetFolderPath( Environment.SpecialFolder.System ) + "\\mmsys.cpl" )
            ]
        },
        { "Диагностика", [
                ( "Монитор ресурсов", Environment.GetFolderPath( Environment.SpecialFolder.System ) + "\\resmon.exe" ),
                ( "Управление дисками", Environment.GetFolderPath( Environment.SpecialFolder.System ) + "\\diskmgmt.msc" ),
                ( "Просмотр событий", Environment.GetFolderPath( Environment.SpecialFolder.System ) + "\\eventvwr.msc" ),
                ( "Очистка диска", Environment.GetFolderPath( Environment.SpecialFolder.System ) + "\\cleanmgr.exe" )
            ]
        }
    };

        foreach ( var group in groups )
        {
            // Разделитель группы (FilePath пустой — отрисуется как разделитель)
            container.Items.Add( new LaunchItem
            {
                Name = $"--- {group.Key} ---",
                FilePath = string.Empty
            } );

            foreach ( var item in group.Value )
            {
                container.Items.Add( new LaunchItem
                {
                    Name = item.Name,
                    FilePath = item.Path // Теперь здесь ПОЛНЫЙ ПУТЬ
                } );
            }
        }

        for ( int i = 0; i < container.Items.Count; i++ ) 
            container.Items[ i ].Id = i;

        return container;
    }
}
