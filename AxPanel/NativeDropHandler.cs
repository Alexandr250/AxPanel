using AxPanel.Model;
using AxPanel.UI.UserControls;
using System.Runtime.InteropServices;
using System.Text;

namespace AxPanel;

public class NativeDropHandler
{
    private readonly Action<List<string>> _onFilesDropped;
    private readonly IntPtr _handle;

    public NativeDropHandler( IntPtr handle, Action<List<string>> onFilesDropped )
    {
        _handle = handle;
        _onFilesDropped = onFilesDropped;

        // Настройка фильтров (чтобы работал Drop от админа к обычному юзеру)
        ConfigureMessageFilters();

        // Разрешаем системный DragAcceptFiles
        Win32Api.DragAcceptFiles( _handle, true );
    }

    public DragDropEffects GetDragEffect( DragEventArgs e )
    {
        if ( e.Data.GetDataPresent( DataFormats.FileDrop ) )
        {
            return DragDropEffects.Copy;
        }

        if ( e.Data.GetDataPresent( "LaunchButtonView" ) ||
             e.Data.GetDataPresent( typeof( LaunchButtonView ) ) )
        {
            return DragDropEffects.Move;
        }

        if ( e.Data.GetDataPresent( "UniformResourceLocator" ) ||
             e.Data.GetDataPresent( DataFormats.Text ) ||
             e.Data.GetDataPresent( DataFormats.UnicodeText ) )
        {
            return DragDropEffects.Link;
        }

        return DragDropEffects.None;
    }

    public List<LaunchItem> ParseDroppedData( DragEventArgs e )
    {
        List<LaunchItem> result = new();

        // 1. Обработка URL (из браузера)
        if ( e.Data.GetDataPresent( "UniformResourceLocator" ) || e.Data.GetDataPresent( DataFormats.Text ) )
        {
            string url = string.Empty;
            if ( e.Data.GetDataPresent( "UniformResourceLocator" ) )
            {
                if ( e.Data.GetData( "UniformResourceLocator" ) is Stream stream )
                {
                    using StreamReader reader = new( stream );
                    url = reader.ReadToEnd().Split( '\0' ).FirstOrDefault() ?? "";
                }
            }

            if ( string.IsNullOrEmpty( url ) && e.Data.GetDataPresent( DataFormats.Text ) )
                url = e.Data.GetData( DataFormats.Text )?.ToString() ?? "";

            if ( !string.IsNullOrEmpty( url ) && Uri.TryCreate( url, UriKind.Absolute, out Uri? uri ) )
            {
                result.Add( new LaunchItem { Name = uri.Host, FilePath = url } );
                return result; // Нашли URL — выходим
            }
        }

        // 2. Обработка файлов (из проводника)
        if ( e.Data.GetDataPresent( DataFormats.FileDrop ) )
        {
            string[] files = ( string[] )e.Data.GetData( DataFormats.FileDrop );
            if ( files?.Length > 0 )
            {
                result.AddRange( files.Select( f => new LaunchItem
                {
                    FilePath = f,
                    Name = Path.GetFileName( f )
                } ) );
            }
        }

        return result;
    }

    private void ConfigureMessageFilters()
    {
        Win32Api.CHANGEFILTERSTRUCT cfs = new() { cbSize = ( uint )Marshal.SizeOf( typeof( Win32Api.CHANGEFILTERSTRUCT ) ) };
        Win32Api.ChangeWindowMessageFilterEx( _handle, Win32Api.WM_DROPFILES, 1, ref cfs );
        Win32Api.ChangeWindowMessageFilterEx( _handle, Win32Api.WM_COPYGLOBALDATA, 1, ref cfs );
        Win32Api.ChangeWindowMessageFilterEx( _handle, Win32Api.WM_COPYDATA, 1, ref cfs );
    }

    public bool HandleWndProc( ref Message m )
    {
        if ( m.Msg == Win32Api.WM_DROPFILES )
        {
            var files = ParseDropFiles( m.WParam );
            if ( files.Count > 0 ) _onFilesDropped?.Invoke( files );
            return true;
        }
        return false;
    }

    private List<string> ParseDropFiles( IntPtr hDrop )
    {
        uint count = Win32Api.DragQueryFile( hDrop, 0xFFFFFFFF, null, 0 );
        List<string> files = new();
        for ( uint i = 0; i < count; i++ )
        {
            StringBuilder sb = new( 260 );
            Win32Api.DragQueryFile( hDrop, i, sb, 260 );
            files.Add( sb.ToString() );
        }
        Win32Api.DragFinish( hDrop ); // Важно освободить память!
        return files;
    }
}
