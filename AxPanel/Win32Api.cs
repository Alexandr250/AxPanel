using System.Runtime.InteropServices;
using System.Text;

namespace AxPanel
{

    /// <summary>
    /// Содержит низкоуровневые функции Windows API для работы с системными процессами.
    /// </summary>
    public static class Win32Api
    {
        [ComImport, Guid( "00000122-0000-0000-C000-000000000046" ), InterfaceType( ComInterfaceType.InterfaceIsIUnknown )]
        public interface IDropTarget
        {
            void DragEnter( [In, MarshalAs( UnmanagedType.Interface )] object pDataObj, [In] int grfKeyState, [In] Point pt, [In, Out] ref int pdwEffect );
            void DragOver( [In] int grfKeyState, [In] Point pt, [In, Out] ref int pdwEffect );
            void DragLeave();
            void Drop( [In, MarshalAs( UnmanagedType.Interface )] object pDataObj, [In] int grfKeyState, [In] Point pt, [In, Out] ref int pdwEffect );
        }

        /// <summary>
        /// Флаг указывает функции SHGetFileInfo, что необходимо извлечь дескриптор иконки (hIcon).
        /// Если этот флаг установлен, вы обязаны вызвать DestroyIcon для освобождения ресурсов.
        /// </summary>
        public const uint SHGFI_ICON = 0x100;

        /// <summary>
        /// Флаг указывает, что необходимо получить "большую" системную иконку (обычно 32x32 или 48x48 пикселей).
        /// Значение 0x0 является стандартным (Large Icon) и используется по умолчанию, если не указан SHGFI_SMALLICON.
        /// </summary>
        public const uint SHGFI_LARGEICON = 0x0;

        public const uint SHGFI_USEFILEATTRIBUTES = 0x10;

        /// <summary>
        /// WM_NCHITTEST (0x84) — "Запрос на проверку области". Windows отправляет его при каждом движении мыши,
        /// чтобы понять: курсор над фоном (HTCLIENT), над заголовком (HTCAPTION) или над рамкой ресайза.
        /// </summary>
        public const int WM_NCHITTEST = 0x84;

        /// <summary>
        /// HTBOTTOMRIGHT (17) — Код области "нижний правый угол". Возвращая его, мы заставляем Windows
        /// думать, что в этой точке находится край рамки, что включает системный ресайз окна по диагонали.
        /// </summary>
        public const int HTBOTTOMRIGHT = 17;

        /// <summary>
        /// HTCLIENT (1) — Код "рабочей области". Мы возвращаем его, когда мышь над нашими кнопками (свернуть/закрыть),
        /// чтобы Windows не перехватывала клики как перетаскивание, а передавала их в наш OnMouseDown.
        /// </summary>
        public const int HTCLIENT = 1;

        /// <summary>
        /// Содержит информацию об объекте файловой системы, полученную через функцию SHGetFileInfo.
        /// </summary>
        [StructLayout( LayoutKind.Sequential, CharSet = CharSet.Auto )]
        public struct SHFILEINFO
        {
            /// <summary>
            /// Дескриптор иконки, представляющей файл. 
            /// После использования необходимо обязательно освободить через DestroyIcon.
            /// </summary>
            public IntPtr hIcon;

            /// <summary>
            /// Индекс иконки в системном списке изображений (System Image List).
            /// </summary>
            public int iIcon;

            /// <summary>
            /// Набор флагов, описывающих атрибуты объекта (например, сжатый, скрытый, папка и т.д.).
            /// </summary>
            public uint dwAttributes;

            /// <summary>
            /// Имя файла в том виде, в котором оно отображается в Windows Explorer (может включать или не включать расширение).
            /// </summary>
            [MarshalAs( UnmanagedType.ByValTStr, SizeConst = 260 )]
            public string szDisplayName;

            /// <summary>
            /// Строка, описывающая тип файла (например, "Текстовый документ", "Приложение" или "Папка с файлами").
            /// </summary>
            [MarshalAs( UnmanagedType.ByValTStr, SizeConst = 80 )]
            public string szTypeName;
        }

        /// <summary>
        /// Получает информацию об объекте файловой системы (файл, папка, диск).
        /// </summary>
        /// <param name="pszPath">Полный путь к объекту или указатель на список идентификаторов (PIDL).</param>
        /// <param name="dwFileAttributes">Атрибуты файла (используется только вместе с флагом SHGFI_USEFILEATTRIBUTES, в остальных случаях — 0).</param>
        /// <param name="psfi">Ссылка на структуру <see cref="SHFILEINFO"/>, которая будет заполнена данными.</param>
        /// <param name="cbSizeFileInfo">Размер структуры <see cref="SHFILEINFO"/> в байтах.</param>
        /// <param name="uFlags">Комбинация флагов (например, <see cref="SHGFI_ICON"/>), определяющая, какую именно информацию нужно извлечь.</param>
        /// <returns>Значение, зависящее от флагов <paramref name="uFlags"/>. Если извлекается иконка, возвращает ненулевое значение при успехе.</returns>
        [DllImport( "shell32.dll", CharSet = CharSet.Auto )]
        public static extern IntPtr SHGetFileInfo( string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbSizeFileInfo, uint uFlags );

        [DllImport( "shell32.dll", CharSet = CharSet.Auto )]
        public static extern uint ExtractIconEx( string szFileName, int nIconIndex, IntPtr[] phiconLarge, IntPtr[] phiconSmall, uint nIcons );

        [DllImport( "user32.dll", SetLastError = true )]
        public static extern IntPtr CopyIcon( IntPtr hIcon );

        /// <summary>
        /// Уничтожает дескриптор иконки и освобождает память, которую она занимала.
        /// </summary>
        /// <param name="hIcon">Дескриптор (Handle) иконки, которую необходимо уничтожить.</param>
        /// <returns>Возвращает <see langword="true"/>, если иконка была успешно уничтожена.</returns>
        /// <remarks>
        /// ВАЖНО: Вызов этого метода обязателен для каждого дескриптора hIcon, 
        /// полученного через <see cref="SHGetFileInfo"/> с флагом SHGFI_ICON, чтобы избежать утечек памяти GDI.
        /// </remarks>
        [DllImport( "user32.dll" )]
        public static extern bool DestroyIcon( IntPtr hIcon );


        /// <summary>
        /// Флаг доступа к процессу: позволяет опрашивать ограниченную информацию (путь к файлу, статус).
        /// В отличие от PROCESS_QUERY_INFORMATION, работает даже для процессов с более высокими правами доступа.
        /// </summary>
        public const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

        /// <summary>
        /// Перечисляет идентификаторы (PID) всех процессов в системе.
        /// </summary>
        /// <param name="lpidProcess">Массив, в который будут записаны ID процессов.</param>
        /// <param name="cb">Размер массива lpidProcess в байтах (количество элементов * sizeof(int)).</param>
        /// <param name="lpcbNeeded">Количество байт, фактически записанных в массив.</param>
        /// <returns>True, если вызов успешен.</returns>
        [DllImport( "psapi.dll", SetLastError = true )]
        public static extern bool EnumProcesses( [Out] int[] lpidProcess, int cb, out int lpcbNeeded );

        /// <summary>
        /// Получает полный путь к исполняемому файлу указанного процесса.
        /// </summary>
        /// <param name="hProcess">Дескриптор (Handle) процесса, полученный через OpenProcess.</param>
        /// <param name="dwFlags">0 — путь в формате Win32, 1 — в формате устройства (Device).</param>
        /// <param name="lpExeName">Буфер (StringBuilder) для записи пути.</param>
        /// <param name="lpdwSize">Вход: размер буфера; Выход: количество записанных символов.</param>
        /// <returns>True, если путь успешно получен.</returns>
        [DllImport( "kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode )]
        public static extern bool QueryFullProcessImageName( IntPtr hProcess, int dwFlags, StringBuilder lpExeName, ref int lpdwSize );

        /// <summary>
        /// Открывает существующий локальный процесс и возвращает его дескриптор.
        /// </summary>
        /// <param name="processAccess">Уровень доступа (например, PROCESS_QUERY_LIMITED_INFORMATION).</param>
        /// <param name="bInheritHandle">Если true, дочерние процессы наследуют этот дескриптор.</param>
        /// <param name="processId">Идентификатор процесса (PID).</param>
        /// <returns>Указатель на дескриптор процесса. Если неудача — IntPtr.Zero.</returns>
        [DllImport( "kernel32.dll", SetLastError = true )]
        public static extern IntPtr OpenProcess( uint processAccess, bool bInheritHandle, int processId );

        /// <summary>
        /// Закрывает открытый дескриптор объекта (процесса), освобождая ресурсы ОС.
        /// </summary>
        /// <param name="hObject">Дескриптор, который нужно закрыть.</param>
        /// <returns>True, если дескриптор успешно освобожден.</returns>
        [DllImport( "kernel32.dll", SetLastError = true )]
        public static extern bool CloseHandle( IntPtr hObject );

        [DllImport( "dwmapi.dll" )]
        public static extern int DwmSetWindowAttribute( IntPtr hwnd, int attr, ref int value, int attrLen );

        // Константы для Windows 11 (Mica, Acrylic)
        public const int DWMWA_SYSTEMBACKDROP_TYPE = 38;
        public const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        public enum BackdropType
        {
            None = 1,
            Mica = 2,
            Acrylic = 3,
            Tabbed = 4
        }

        [StructLayout( LayoutKind.Sequential )]
        public struct WindowCompositionAttributeData
        {
            public WindowCompositionAttribute Attribute;
            public IntPtr Data;
            public int SizeOfData;
        }

        public enum WindowCompositionAttribute
        {
            WCA_ACCENT_POLICY = 19 // Этот атрибут отвечает за эффекты размытия
        }

        [StructLayout( LayoutKind.Sequential )]
        public struct AccentPolicy
        {
            public AccentState AccentState;
            public int AccentFlags;
            public int GradientColor;
            public int AnimationId;
        }

        public enum AccentState
        {
            ACCENT_DISABLED = 0,
            ACCENT_ENABLE_BLURBEHIND = 3,      // Обычное размытие (Win10)
            ACCENT_ENABLE_ACRYLICBLURBEHIND = 4 // Acrylic (Win10 1803+)
        }

        [DllImport( "user32.dll" )]
        public static extern int SetWindowCompositionAttribute( IntPtr hwnd, ref WindowCompositionAttributeData data );

        public delegate bool EnumWindowsProc( IntPtr hWnd, int lParam );

        [DllImport( "user32.dll" )]
        public static extern bool EnumWindows( EnumWindowsProc lpEnumFunc, int lParam );

        [DllImport( "user32.dll" )]
        public static extern uint GetWindowThreadProcessId( IntPtr hWnd, out uint lpdwProcessId );

        [DllImport( "user32.dll" )]
        public static extern bool IsWindowVisible( IntPtr hWnd );

        public static int GetWindowCount( int processId )
        {
            int count = 0;
            EnumWindows( ( hWnd, lParam ) =>
            {
                GetWindowThreadProcessId( hWnd, out uint windowPid );
                if ( windowPid == processId && IsWindowVisible( hWnd ) )
                {
                    count++;
                }
                return true;
            }, 0 );
            return count;
        }

        [DllImport( "user32.dll" )]
        public static extern bool ChangeWindowMessageFilterEx( IntPtr hWnd, uint msg, uint action, ref CHANGEFILTERSTRUCT pChangeFilterStruct );

        public struct CHANGEFILTERSTRUCT
        {
            public uint cbSize;
            public uint ExtStatus;
        }

        public const uint WM_DROPFILES = 0x0233;
        public const uint WM_COPYDATA = 0x004A;
        public const uint WM_COPYGLOBALDATA = 0x0049;
        public const uint MSGFLT_ALLOW = 1;

        [DllImport( "shell32.dll" )]
        public static extern void DragAcceptFiles( IntPtr hWnd, bool fAccept );

        [DllImport( "shell32.dll" )]
        public static extern uint DragQueryFile( IntPtr hDrop, uint iFile, [Out] StringBuilder lpszFile, uint cch );

        [DllImport( "ole32.dll" )]
        public static extern int RevokeDragDrop( IntPtr hwnd );

        [DllImport( "ole32.dll" )]
        public static extern int RegisterDragDrop( IntPtr hwnd, IDropTarget pDropTarget );

        [DllImport( "shell32.dll" )]
        public static extern void DragFinish( IntPtr hDrop );
    }
}
