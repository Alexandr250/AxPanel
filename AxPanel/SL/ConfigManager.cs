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
        if ( !File.Exists( ConfigFile ) ) return new MainConfig();

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
        MainModel panelModelClone = new MainModel
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

        if ( File.Exists( ItemsConfigFile ) )
        {
            try
            {
                string json = File.ReadAllText( ItemsConfigFile );
                panelModel = JsonSerializer.Deserialize<MainModel>( json );
            }
            catch ( Exception ex ) { Debug.WriteLine( ex.Message ); }
        }

        // Если файла нет или он битый — создаем пустую модель
        panelModel ??= new MainModel { Containers = new List<ContainerItem>() };

        foreach ( ContainerItem container in panelModel.Containers )
        {
            for ( int index = 0; index < container.Items.Count; index++ )
            {
                LaunchItem? item = container.Items[ index ];
                if ( item != null ) item.Id = index;
            }
        }

        panelModel.Containers.Add( BuildStandatrContainer() );

        return panelModel;
    }

    private static ContainerItem BuildStandatrContainer()
    {
        ContainerItem container = new()
        {
            Name = "Системные инструменты",
            Type = ContainerType.System,
            Items = new List<LaunchItem>()
        };

        // Вспомогательный список для удобного наполнения
        var groups = new Dictionary<string, List<(string Name, string Path)>>
    {
        { "Основные", new() {
            ("Проводник", "explorer.exe"),
            ("Блокнот", "notepad.exe"),
            ("Калькулятор", "calc.exe"),
            ("Диспетчер задач", "taskmgr.exe")
        }},
        { "Администрирование", new() {
            ("Командная строка", "cmd.exe"),
            ("PowerShell", "powershell.exe"),
            ("Редактор реестра", "regedit.exe"),
            ("Службы", "services.msc"),
            ("Управление компьютером", "compmgmt.msc")
        }},
        { "Сеть и Настройка", new() {
            ("Сетевые подключения", "ncpa.cpl"),
            ("Панель управления", "control.exe"),
            ("Удаление программ", "appwiz.cpl"),
            ("Параметры звука", "mmsys.cpl")
        }},
        { "Диагностика", new() {
            ("Монитор ресурсов", "resmon.exe"),
            ("Управление дисками", "diskmgmt.msc"),
            ("Просмотр событий", "eventvwr.msc"),
            ("Очистка диска", "cleanmgr.exe")
        }}
    };

        foreach ( var group in groups )
        {
            // Добавляем заголовок группы (можно отрисовать иначе в ButtonDrawer)
            container.Items.Add( new LaunchItem
            {
                Name = $"--- {group.Key} ---",
                FilePath = string.Empty, // Помечаем как неактивный
                IsSeparator = true // Если добавите такое свойство в модель
            } );

            foreach ( var item in group.Value )
            {
                container.Items.Add( new LaunchItem
                {
                    Name = item.Name,
                    FilePath = item.Path
                } );
            }
        }

        // Проставляем ID
        for ( int i = 0; i < container.Items.Count; i++ ) container.Items[ i ].Id = i;

        return container;
    }

    //private static ContainerItem BuildStandatrContainer()
    //{
    //    ContainerItem container = new()
    //    {
    //        Name = "Стандартные Windows",
    //        Type = ContainerType.System,
    //        Items = new List<LaunchItem>()
    //    };

    //    // Используем системные переменные вместо хардкода "C:\\"
    //    string win = Environment.GetFolderPath( Environment.SpecialFolder.Windows );
    //    string sys32 = Environment.GetFolderPath( Environment.SpecialFolder.System );

    //    container.Items.AddRange( new List<LaunchItem>
    //    {
    //        new() { Name = "Regedit", FilePath = Path.Combine(win, "regedit.exe") },
    //        new() { Name = "Проводник", FilePath = Path.Combine(win, "explorer.exe") },
    //        new() { Name = "Блокнот", FilePath = Path.Combine(sys32, "notepad.exe") },
    //        new() { Name = "Командная строка", FilePath = Path.Combine(sys32, "cmd.exe") },
    //        new() { Name = "Paint", FilePath = Path.Combine(sys32, "mspaint.exe") },
    //        new() { Name = "Диспетчер задач", FilePath = Path.Combine(sys32, "Taskmgr.exe") },
    //        new() { Name = "Панель управления", FilePath = Path.Combine(sys32, "control.exe") },
    //        new() { Name = "Калькулятор", FilePath = Path.Combine(sys32, "calc.exe") },
    //        new() { Name = "Конфигурация системы", FilePath = Path.Combine(sys32, "msconfig.exe") },
    //        new() { Name = "Подключение к удаленому рабочему столу", FilePath = Path.Combine(sys32, "mstsc.exe") },
    //        new() { Name = "Управление компьютером", FilePath = Path.Combine(sys32, "compmgmt.msc") },
    //        new() { Name = "Удаление или изменение программ", FilePath = Path.Combine(sys32, "appwiz.cpl") },

    //        // Категория: Мониторинг и диагностика
    //        new() { Name = "Монитор ресурсов", FilePath = "resmon.exe" },
    //        new() { Name = "Просмотр событий", FilePath = "eventvwr.msc" },
    //        new() { Name = "Сведения о системе", FilePath = "msinfo32.exe" },
    //        //new() { Name = "Средство диагностики DirectX", FilePath = "dxdiag.exe" },

    //        // Категория: Управление железом и сетью
    //        new() { Name = "Диспетчер устройств", FilePath = "devmgmt.msc" },
    //        new() { Name = "Управление дисками", FilePath = "diskmgmt.msc" },
    //        new() { Name = "Сетевые подключения", FilePath = "ncpa.cpl" },
    //        new() { Name = "Службы", FilePath = "services.msc" },

    //        // Категория: Настройки системы
    //        new() { Name = "Редактор групповых политик", FilePath = "gpedit.msc" },
    //        new() { Name = "Параметры звука", FilePath = "mmsys.cpl" },
    //        new() { Name = "Свойства системы (Дополнительно)", FilePath = "SystemPropertiesAdvanced.exe" },
    //        new() { Name = "Очистка диска", FilePath = "cleanmgr.exe" }
    //    } );

    //    // Проставляем ID системным элементам
    //    for ( int i = 0; i < container.Items.Count; i++ ) container.Items[ i ].Id = i;

    //    return container;
    //}
}

//public class ConfigManager
//{
//    private const string ItemsConfigFile = "items-config.json";
//    private const string ConfigFile = "config.json";

//    public static void SaveMainConfig(MainConfig mainConfig)
//    {
//        string jsonString = JsonSerializer.Serialize(mainConfig, new JsonSerializerOptions { WriteIndented = true });
//        File.WriteAllText(ConfigFile, jsonString);
//    }

//    public static MainConfig? ReadMainConfig()
//    {
//        MainConfig? config = JsonSerializer.Deserialize<MainConfig>(File.ReadAllText(ConfigFile));

//        return config;
//    }

//    public static void SaveItemsConfig( /*List<ConfigItem> configItems*/ MainModel panelModel)
//    {
//        /*AxPanelModel panelModel = new AxPanelModel();
//        var container = new ContainerItem();
//        container.Items = new List<ConfigItem>();
//        container.Items.Add( new  ConfigItem(){ Name = "visual"} );

//        panelModel.Containers = [ container ];*/

//        MainModel panelModelClone = new MainModel
//        {
//            Containers = panelModel.Containers.Where(c => c.Type != ContainerType.System).ToList()
//        };


//        string jsonString = JsonSerializer.Serialize(panelModelClone, new JsonSerializerOptions { WriteIndented = true });
//        File.WriteAllText(ItemsConfigFile, jsonString);
//    }

//    [Obsolete]
//    public static MainModel ReadItemsConfig()
//    {
//        /*List<ConfigItem>? items = JsonSerializer.Deserialize<List<ConfigItem>>( File.ReadAllText( ItemsConfigFile ) );*/

//        MainModel panelModel = JsonSerializer.Deserialize<MainModel>(File.ReadAllText(ItemsConfigFile));

//        foreach (var container in panelModel.Containers)
//        {
//            for (int index = 0; index < container.Items.Count; index++)
//            {
//                LaunchItem? item = container.Items[index];
//                item.Id = index;
//            }
//        }


//        return panelModel;
//    }

//    public static MainModel ReadModel()
//    {
//        MainModel panelModel = JsonSerializer.Deserialize<MainModel>(File.ReadAllText(ItemsConfigFile));

//        foreach (ContainerItem container in panelModel.Containers)
//        {
//            for (int index = 0; index < container.Items.Count; index++)
//            {
//                LaunchItem? item = container.Items[index];
//                item.Id = index;
//            }
//        }

//        panelModel.Containers.Add(BuildStandatrContainer());

//        return panelModel;
//    }

//    private static ContainerItem BuildStandatrContainer()
//    {
//        ContainerItem container = new()
//        {
//            Name = "Стандартные Windows",
//            Type = ContainerType.System
//        };

//        int itemIndex = 0;

//        container.Items.AddRange([
//            new LaunchItem
//            {
//                Name = "Regedit",
//                FilePath = "C:\\Windows\\regedit.exe",
//                Id = itemIndex++
//            },
//            new LaunchItem {
//                Name = "Проводник",
//                FilePath = "C:\\Windows\\explorer.exe",
//                Id = itemIndex++
//            },
//            new LaunchItem
//            {
//                Name = "Блокнот",
//                FilePath = "C:\\Windows\\System32\\notepad.exe",
//                Id = itemIndex++
//            },
//            new LaunchItem
//            {
//                Name = "Командная строка",
//                FilePath = "C:\\Windows\\System32\\cmd.exe",
//                Id = itemIndex++
//            },
//            new LaunchItem
//            {
//                Name = "Paint",
//                FilePath = "C:\\Windows\\System32\\mspaint.exe",
//                Id = itemIndex++
//            },
//            new LaunchItem
//            {
//                Name = "Диспетчер задач",
//                FilePath = "C:\\Windows\\System32\\Taskmgr.exe",
//                Id = itemIndex++
//            },
//             new LaunchItem
//            {
//                Name = "Панель управления",
//                FilePath = "C:\\Windows\\System32\\control.exe",
//                Id = itemIndex++
//            },
//            new LaunchItem
//            {
//                Name = "Калькулятор",
//                FilePath = "C:\\Windows\\System32\\calc.exe",
//                Id = itemIndex++
//            },
//            new LaunchItem
//            {
//                Name = "Конфигурация системы",
//                FilePath = "C:\\Windows\\System32\\msconfig.exe",
//                Id = itemIndex++
//            },
//            new LaunchItem
//            {
//                Name = "Подключение к удаленому рабочему столу",
//                FilePath = "C:\\Windows\\System32\\mstsc.exe",
//                Id = itemIndex++
//            },
//            new LaunchItem
//            {
//                Name = "Управление компьютером",
//                FilePath = "C:\\Windows\\System32\\compmgmt.msc",
//                Id = itemIndex++
//            },
//            new LaunchItem
//            {
//                Name = "Удаление или изменение программ",
//                FilePath = "C:\\Windows\\System32\\appwiz.cpl",
//                Id = itemIndex++
//            } ]);

//        return container;
//    }
//}