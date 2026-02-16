namespace AxPanel
{
    public class ShortcutHelper
    {
        public static string Resolve( string lnkPath )
        {
            try
            {
                // Используем динамику, чтобы не тащить тяжелые COM-библиотеки в зависимости
                Type shellType = Type.GetTypeFromProgID( "WScript.Shell" );
                dynamic shell = Activator.CreateInstance( shellType );
                dynamic? shortcut = shell.CreateShortcut( lnkPath );
                return shortcut.TargetPath;
            }
            catch
            {
                return lnkPath;
            }
        }
    }
}
