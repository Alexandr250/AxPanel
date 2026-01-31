using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace AxPanel
{
    /// <summary>
    /// Содержит низкоуровневые функции Windows API для работы с системными процессами.
    /// </summary>
    public static class Win32Api
    {
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
    }
}
