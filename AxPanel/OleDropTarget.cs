using AxPanel.Model;
using AxPanel.UI.UserControls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AxPanel
{
    public class OleDropTarget : Win32Api.IDropTarget
    {
        private readonly ButtonContainerView _parent;
        public OleDropTarget( ButtonContainerView parent ) => _parent = parent;

        public void DragEnter( object pDataObj, int grfKeyState, Point pt, ref int pdwEffect )
        {
            DataObject data = new( pDataObj );

            if ( data.GetDataPresent( typeof( LaunchButtonView ) ) )
                pdwEffect = 2; // Move (перетаскивание кнопки)
            else if ( data.GetDataPresent( DataFormats.FileDrop ) )
                pdwEffect = 1; // Copy (файлы/ярлыки)
            else if ( data.GetDataPresent( "UniformResourceLocator" ) || data.GetDataPresent( DataFormats.Text ) )
                pdwEffect = 4; // Link (ссылки)
            else
                pdwEffect = 0;

            _parent.BeginInvoke( () => _parent.StartDragHoverTimer() );
        }

        public void DragOver( int grfKeyState, Point pt, ref int pdwEffect ) => pdwEffect = 4;

        public void DragLeave()
        {
            _parent.BeginInvoke( () => _parent.StopDragHoverTimer() );
        }

        public void Drop( object pDataObj, int grfKeyState, Point pt, ref int pdwEffect )
        {
            DataObject data = new( pDataObj );

            if ( data.GetDataPresent( DataFormats.FileDrop ) )
            {
                string[] files = ( string[] )data.GetData( DataFormats.FileDrop );
                List<LaunchItem> items = [];

                foreach ( string file in files )
                {
                    string targetPath = file;
                    string name = Path.GetFileNameWithoutExtension( file );

                    // Если это ярлык — резолвим его цель
                    if ( file.EndsWith( ".lnk", StringComparison.OrdinalIgnoreCase ) )
                    {
                        targetPath = ShortcutHelper.Resolve( file ); // _parent.ResolveShortcut( file );
                    }

                    items.Add( new LaunchItem { Name = name, FilePath = targetPath } );
                }

                _parent.BeginInvoke( () => {
                    _parent.AddButtons( items );
                    _parent.SyncModelOrder();
                } );

                pdwEffect = 1; // Copy
                return;
            }

            if ( data.GetDataPresent( typeof( LaunchButtonView ) ) )
            {
                LaunchButtonView? droppedBtn = data.GetData( typeof( LaunchButtonView ) ) as LaunchButtonView;
                if ( droppedBtn != null && droppedBtn.Parent != _parent )
                {
                    _parent.BeginInvoke( () => {
                        _parent.MoveButtonToThisContainer( droppedBtn );
                    } );
                    pdwEffect = 2; // Move
                    return;
                }
            }

            string url = "";

            if ( data.GetDataPresent( "UniformResourceLocator" ) )
            {
                using MemoryStream? ms = data.GetData( "UniformResourceLocator" ) as MemoryStream;
                if ( ms != null ) url = Encoding.ASCII.GetString( ms.ToArray() ).Split( '\0' )[ 0 ];
            }

            if ( string.IsNullOrEmpty( url ) && data.GetDataPresent( DataFormats.Text ) )
                url = data.GetData( DataFormats.Text )?.ToString();

            if ( !string.IsNullOrEmpty( url ) && url.StartsWith( "http" ) )
            {
                _parent.BeginInvoke( () => {
                    _parent.AddButtons( [ new LaunchItem() { Name = new Uri( url ).Host, FilePath = url } ] );
                    _parent.SyncModelOrder();
                } );
            }
        }
    }
}
