using AxPanel.Model;
using AxPanel.UI.Themes;
using AxPanel.UI.UserControls;

namespace AxPanel.SL;

public class ContainerManager
{
    private readonly Control.ControlCollection _controls;
    private readonly ContainerService _service;
    private readonly ITheme _theme;
    private readonly Action _onConfigChanged;

    public ButtonContainerView Selected { get; private set; }

    public IEnumerable<ButtonContainerView> Containers => _controls.OfType<ButtonContainerView>();

    public event Action<ButtonContainerView> SelectionChanged;

    public ContainerManager( Control.ControlCollection controls, ContainerService service, ITheme theme, Action onConfigChanged )
    {
        _controls = controls;
        _service = service;
        _theme = theme;
        _onConfigChanged = onConfigChanged;
    }

    public void Register( ButtonContainerView container )
    {
        container.ContainerSelected += SetSelected;
        container.DragHoverActivated += SetSelected;

        container.ProcessStartRequested += ( btn, args ) => _service.RunProcess( btn, false, args );
        container.ProcessStartAsAdminRequested += btn => _service.RunProcess( btn, true );
        container.ExplorerOpenRequested += path => _service.OpenLocation( path );

        // ИСПОЛЬЗУЕМ СВОЙСТВО НАПРЯМУЮ, а не замыкание currentName
        container.ItemCollectionChanged += ( items ) => {
            if ( items == null || items.Count == 0 ) return; // Защита от затирания пустотой

            var model = ConfigManager.GetModel();
            
            var target = model.Containers.FirstOrDefault( c => c.Name == container.PanelName );

            if ( target != null )
            {
                target.Items = items;
                _onConfigChanged?.Invoke();
            }
        };

        _controls.Add( container );
        if ( Selected == null ) SetSelected( container );
    }

    public void SetSelected( ButtonContainerView container )
    {
        if ( Selected == container ) return;
        Selected = container;
        SelectionChanged?.Invoke( container );
    }

    public void Remove( ButtonContainerView container )
    {
        if ( Containers.Count() <= 1 ) return;

        if ( Selected == container )
            SetSelected( Containers.FirstOrDefault( c => c != container ) );

        _controls.Remove( container );
        container.Dispose();
    }

    private void UpdateModelData( string name, List<LaunchItem> items )
    {
        var target = ConfigManager.GetModel().Containers.FirstOrDefault( c => c.Name == name );
        if ( target != null ) target.Items = items;
    }
}
