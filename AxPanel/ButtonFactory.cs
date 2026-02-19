using AxPanel.Model;
using AxPanel.UI.Themes;
using AxPanel.UI.UserControls;

namespace AxPanel;

public interface IButtonFactory
{
    List<LaunchButtonView> CreateAll( List<LaunchItem> items, ButtonContainerView parent );
    LaunchButtonView CreateSingle( LaunchItem item, ButtonContainerView parent );
    List<LaunchItem> SortByGroups( List<LaunchItem> items );
}

public class ButtonFactory : IButtonFactory
{
    private readonly ITheme _theme;

    public ButtonFactory( ITheme theme )
    {
        _theme = theme;
    }

    public List<LaunchButtonView> CreateAll( List<LaunchItem> items, ButtonContainerView parent )
    {
        List<LaunchItem> orderedItems = SortByGroups( items );
        return orderedItems.Select( item => CreateSingle( item, parent ) ).ToList();
    }

    public LaunchButtonView CreateSingle( LaunchItem item, ButtonContainerView parent )
    {

        LaunchButtonView btn = new( _theme )
        {
            Dock = DockStyle.None,
            Anchor = AnchorStyles.Top | AnchorStyles.Left,
            Width = parent.Width,
            Height = item.IsSeparator ? item.Height : _theme.ButtonStyle.DefaultHeight,
            Text = item.Name,
            BaseControlPath = item.FilePath,
            Arguments = item.Arguments,
            IconPath = item.Icon
            //IsSeparator = item.IsSeparator
        };

        if ( item is PortableItem portable )
        {
            btn.DownloadUrl = portable.DownloadUrl;
            btn.IsArchive = portable.IsArchive;
        }

        // Привязываем события через замыкания на методы родителя (Container)
        BindEvents( btn, parent );

        return btn;
    }

    private void BindEvents( LaunchButtonView btn, ButtonContainerView parent )
    {
        btn.ButtonLeftClick += parent.NotifyProcessStart;
        btn.ButtonRightClick += b => parent.NotifyExplorerOpen( b.BaseControlPath );
        btn.ButtonMiddleClick += parent.NotifyProcessStartAsAdmin;

        btn.DeleteButtonClick += parent.RemoveButton;

        btn.MouseMove += ( s, e ) =>
        {
            if ( e.Button == MouseButtons.Left ) 
                parent.HandleButtonDragMove();
        };

        btn.MouseUp += ( s, e ) => parent.HandleButtonDragEnd();

        btn.Dragging += parent.HandleButtonReorder;
    }

    //private void BindEvents( LaunchButtonView btn, ButtonContainerInteractionManager interactions )
    //{
    //    if ( btn.IsSeparator )
    //    {
    //        btn.ButtonLeftClick += ( b, args ) => interactions.RequestGroupStart( b );
    //    }
    //    else
    //    {
    //        // Обычная логика кнопок
    //        btn.ButtonLeftClick += ( b, args ) => interactions.RequestStart( b, args );
    //        btn.ButtonRightClick += b => interactions.RequestExplorer( b.BaseControlPath );
    //        btn.ButtonMiddleClick += b => interactions.RequestAdminStart( b );
    //    }

    //    // Общая логика (удаление и перетаскивание)
    //    btn.DeleteButtonClick += b => interactions.RequestDelete( b );

    //    btn.MouseMove += ( s, e ) => {
    //        if ( e.Button == MouseButtons.Left ) interactions.OnButtonDragMove();
    //    };

    //    btn.MouseUp += ( s, e ) => interactions.OnButtonDragEnd();
    //    btn.Dragging += ( b ) => interactions.HandleReorder( b );
    //}

    public List<LaunchItem> SortByGroups( List<LaunchItem> items )
    {
        List<LaunchItem> result = [];
        List<LaunchItem> currentGroup = [];

        foreach ( LaunchItem item in items )
        {
            if ( item.IsSeparator )
            {
                if ( currentGroup.Count > 0 )
                {
                    result.AddRange( currentGroup.OrderByDescending( i => i.ClicksCount ) );
                    currentGroup.Clear();
                }
                result.Add( item );
            }
            else
            {
                currentGroup.Add( item );
            }
        }

        result.AddRange( currentGroup.OrderByDescending( i => i.ClicksCount ) );

        return result;
    }
}
