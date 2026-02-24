using AxPanel.Model;
using AxPanel.SL;
using AxPanel.UI.UserControls;

namespace AxPanel;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        AppDomain.CurrentDomain.UnhandledException += ( s, e ) => {
            MessageBox.Show( e.ExceptionObject.ToString(), "Критическая ошибка" );
        };

        ApplicationConfiguration.Initialize();

        MainConfig config = ConfigManager.GetMainConfig();
        MainModel panelModel = ConfigManager.GetModel();

        MainView view = new();
        view.MainModel = panelModel;
        
        ConfigureMainPanelView( view, config, panelModel );
        view.Move += ( sender, args ) =>
        {
            config.Left = view.Left;
            config.Top = view.Top;
            config.Height = view.Height;
            config.Width = view.Width;
            ConfigManager.SaveMainConfig( config );
        };

        Application.Run( view );
    }

    private static void ConfigureMainPanelView( MainView? mainView, MainConfig? config, MainModel panelModel )
    {
        if ( mainView != null && config != null )
        {
            mainView.Padding = new Padding( 
                config.BorderWidth, 
                config.HeaderHeight, 
                config.BorderWidth, 
                config.BorderWidth );
            
            mainView.Width = config.Width;
            mainView.Height = config.Height;
            mainView.Top = config.Top;
            mainView.Left = config.Left;

            foreach ( ContainerItem containerItem in panelModel.Containers )
            {
                ButtonContainerView uiContainer = mainView.MainContainer.AddContainer( containerItem.Name, containerItem.Items );
                
                uiContainer.ButtonContainerEvents.ItemCollectionChanged += list =>
                {
                    // 1. Если list == null, значит это промежуточное перемещение 
                    // (например, кнопка просто пролетает над другой). Ничего не сохраняем.
                    if ( list == null ) 
                        return;

                    // 2. Если пришел заполненный список — это ФИНАЛЬНЫЙ порядок (после MouseUp или DragDrop)
                    // Просто заменяем старый список в модели на новый
                    containerItem.Items = list;

                    // 3. Теперь сохраняем актуальную модель
                    ConfigManager.SaveItemsConfig( panelModel );
                };
            }
        }
    }
}