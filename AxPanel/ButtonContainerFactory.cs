using AxPanel.Model;
using AxPanel.SL;
using AxPanel.UI.Themes;
using AxPanel.UI.UserControls;

namespace AxPanel;

public class ButtonContainerFactory
{
    private readonly ITheme _theme;
    private readonly MainConfig _mainConfig;
    private readonly RootContainerView _rootContainerView;
    private readonly ContainerService _containerService;
    private readonly GlobalAnimator _animator;

    public event Action<ButtonContainerView> SelectedChangedRequested;
    public event Action LayoutUpdateRequested;
    public event Action MonitorPathsUpdateRequested;
    public event Action SaveConfigRequested;

    public ButtonContainerFactory( ITheme theme, MainConfig mainConfig, RootContainerView rootContainerView, ContainerService containerService, GlobalAnimator animator )
    {
        _theme = theme;
        _mainConfig = mainConfig;
        _rootContainerView = rootContainerView;
        _containerService = containerService;
        _animator = animator;
    }

    public ButtonContainerView AddContainer( string name, List<LaunchItem>? items )
    {
        ButtonContainerView container = new( _theme, _mainConfig )
        {
            PanelName = name,
            BaseControlPath = name,
            Width = _rootContainerView.Width,
            Height = _theme.ContainerStyle.HeaderHeight
        };

        container.ContainerSelected += panel => SelectedChangedRequested?.Invoke( panel ); // Selected = panel;
        container.ContainerDeleteRequested += DeleteContainer;
        container.ContainerDeleteRequested += c => _animator.Unregister( c );

        container.AddButtons( items );

        _rootContainerView.Controls.Add( container );

        if ( _rootContainerView.Selected == null )
            SelectedChangedRequested?.Invoke( container );  //Selected = container;
        else
            LayoutUpdateRequested?.Invoke(); // ArrangeContainers();

        container.ProcessStartRequested += ( btn, args ) => _containerService.RunProcess( btn, false, args );
        container.ProcessStartAsAdminRequested += btn => _containerService.RunProcess( btn, true );
        container.ExplorerOpenRequested += path => _containerService.OpenLocation( path );

        container.GroupStartRequested += separator => {
            List<LaunchButtonView> allButtons = container.Controls.OfType<LaunchButtonView>().ToList();
            int startIndex = allButtons.IndexOf( separator );
            if ( startIndex != -1 )
            {
                IEnumerable<LaunchButtonView> group = allButtons.Skip( startIndex + 1 ).TakeWhile( b => !string.IsNullOrEmpty( b.BaseControlPath ) );
                _containerService.RunProcessGroup( group );
            }
        };

        container.ItemCollectionChanged += newItems => {
            if ( newItems == null || newItems.Count == 0 )
                return;

            MainModel model = ConfigManager.GetModel();
            ContainerItem? target = model.Containers.FirstOrDefault( c => c.Name == container.PanelName );
            if ( target != null )
            {
                target.Items = newItems;
                SaveConfigRequested?.Invoke(); // OnSaveConfigRequered?.Invoke();
            }
            
            MonitorPathsUpdateRequested?.Invoke(); // UpdateGlobalMonitorPaths();
        };

        _animator.Register( container );

        container.DragHoverActivated += ( panel ) => {
            if ( _rootContainerView.Selected != panel )
                SelectedChangedRequested?.Invoke( panel );  //Selected = panel;
        };

        return container;
    }

    private void DeleteContainer( ButtonContainerView container )
    {
        if ( _rootContainerView.Controls.OfType<ButtonContainerView>().Count() <= 1 )
            return;

        if ( _rootContainerView.Selected == container )
        {
            ButtonContainerView? firstContainer = _rootContainerView.Controls.OfType<ButtonContainerView>().FirstOrDefault( c => c != container );
            SelectedChangedRequested?.Invoke( firstContainer ); //Selected = firstContainer;
        }

        _rootContainerView.Controls.Remove( container );

        container.ContainerDeleteRequested -= DeleteContainer;

        container.Dispose();
        LayoutUpdateRequested?.Invoke();  //ArrangeContainers();
        MonitorPathsUpdateRequested?.Invoke(); // UpdateGlobalMonitorPaths();
    }
}
