using AxPanel.Model;
using AxPanel.SL;
using AxPanel.UI.UserControls;

namespace AxPanel;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        MainConfig? config = ConfigManager.ReadMainConfig();
        MainModel panelModel = ConfigManager.ReadModel();

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
                uiContainer.ItemCollectionChanged += list =>
                {
                    if( list != null )
                        containerItem.Items.AddRange( list );
                    ConfigManager.SaveItemsConfig( panelModel );
                };
            }
        }
    }
}