using AxPanel.Model;
using System.Diagnostics;

namespace AxPanel.SL;

/// <summary>
/// Обеспечивает мониторинг системных процессов, отслеживая использование CPU, RAM и количество окон.
/// </summary>
public class ProcessMonitor : IDisposable
{
    private readonly Dictionary<string, PerformanceCounter> _counters = new();
    private readonly object _lock = new();
    private readonly CancellationTokenSource _cts = new();
    private HashSet<string> _targetPaths = [];
    private bool _disposed;

    /// <summary>
    /// Событие, возникающее при обновлении статистики процессов.
    /// Передает словарь, где ключ — путь к файлу, а значение — объект со статистикой <see cref="ProcessStats"/>.
    /// </summary>
    public event Action<Dictionary<string, ProcessStats>> StatisticsUpdated;

    /// <summary>
    /// Список полных путей к исполняемым файлам процессов, за которыми необходимо вести наблюдение.
    /// </summary>
    public HashSet<string> TargetPaths
    {
        get
        {
            lock ( _lock )
                return [ .. _targetPaths ];
        }
        set
        {
            lock ( _lock )
                _targetPaths = new HashSet<string>( value, StringComparer.OrdinalIgnoreCase );
        }
    }

    /// <summary>
    /// Запускает фоновый цикл мониторинга процессов.
    /// </summary>
    public void Start() =>
        Task.Run( () => MonitorLoop( _cts.Token ) );

    /// <summary>
    /// Останавливает цикл мониторинга и отменяет текущие задачи.
    /// </summary>
    public void Stop() =>
        _cts.Cancel();

    /// <summary>
    /// Основной цикл мониторинга, выполняющий сбор данных о процессах один раз в секунду.
    /// </summary>
    /// <param name="token">Токен отмены операции.</param>
    private async Task MonitorLoop( CancellationToken token )
    {
        var lastCpuTimes = new Dictionary<int, (TimeSpan cpuTime, DateTime timeStamp)>();

        while ( !token.IsCancellationRequested )
        {
            Dictionary<string, ProcessStats> stats = new( StringComparer.OrdinalIgnoreCase );
            string[] paths;
            lock ( _lock ) paths = [ .. _targetPaths ];

            // 1. Просто получаем массив процессов
            Process[] allProcesses = Process.GetProcesses();

            try
            {
                foreach ( string path in paths )
                {
                    string fileName = Path.GetFileNameWithoutExtension( path );

                    // Ищем процесс в массиве
                    Process? process = allProcesses.FirstOrDefault( p =>
                        p.ProcessName.Equals( fileName, StringComparison.OrdinalIgnoreCase ) );

                    //var tproc = allProcesses.Where( p=> p.ProcessName.StartsWith( "v" ) ).Where( p =>
                    //{
                    //    try
                    //    {
                    //        var procName = p.MainModule != null ? p.MainModule.ModuleName : string.Empty;
                    //        var result = procName.Equals( path, StringComparison.OrdinalIgnoreCase );
                    //        return result;
                    //    }
                    //    catch
                    //    {
                    //        return false;
                    //    }
                    //} ).ToArray();

                    if ( process != null )
                    {
                        try
                        {
                            int pid = process.Id;
                            var currentTime = DateTime.UtcNow;
                            var currentCpuTime = process.TotalProcessorTime;

                            float cpuUsage = 0;

                            if ( lastCpuTimes.TryGetValue( pid, out var last ) )
                            {
                                double cpuUsedMs = ( currentCpuTime - last.cpuTime ).TotalMilliseconds;
                                double totalMsPassed = ( currentTime - last.timeStamp ).TotalMilliseconds;
                                cpuUsage = ( float )( cpuUsedMs / ( Environment.ProcessorCount * totalMsPassed ) * 100 );
                            }

                            lastCpuTimes[ pid ] = (currentCpuTime, currentTime);

                            

                            stats[ path ] = new ProcessStats
                            {
                                IsRunning = true,
                                CpuUsage = Math.Clamp( cpuUsage, 0, 100 ), // GetCpuUsage( path, process.ProcessName ), //Math.Clamp( cpuUsage, 0, 100 ),
                                RamMb = process.WorkingSet64 / 1024 / 1024,
                                WindowCount = allProcesses.Count( p =>
                                    p.ProcessName.Equals( fileName, StringComparison.OrdinalIgnoreCase ) &&
                                    p.MainWindowHandle != IntPtr.Zero ),
                                //WindowCount = tproc.Count( p =>
                                //    p.MainModule.ModuleName.Equals( path, StringComparison.OrdinalIgnoreCase ) ),
                                StartTime = process.StartTime
                            };
                        }
                        catch( Exception ex )
                        {
                            Debug.WriteLine( ex );
                            stats[ path ] = new ProcessStats { IsRunning = true, CpuUsage = 0 };
                        }
                    }
                    else
                    {
                        stats[ path ] = new ProcessStats { IsRunning = false };
                    }
                }

                // Очистка кэша PID
                var currentPids = allProcesses.Select( p => p.Id ).ToHashSet();
                var keysToRemove = lastCpuTimes.Keys.Where( k => !currentPids.Contains( k ) ).ToList();

                foreach ( var key in keysToRemove )
                    lastCpuTimes.Remove( key );

                StatisticsUpdated?.Invoke( stats );
            }
            finally
            {
                foreach ( Process p in allProcesses )
                {
                    p.Dispose();
                }
            }

            await Task.Delay( 1000, token );
        }
    }

    /// <summary>
    /// Вычисляет процент использования процессора с помощью PerformanceCounter.
    /// </summary>
    /// <param name="path">Путь к файлу (используется как ключ кэша счетчиков).</param>
    /// <param name="processName">Имя процесса в системе.</param>
    /// <returns>Процент загрузки CPU, деленный на количество ядер.</returns>
    private float GetCpuUsage( string path, string processName )
    {
        lock ( _lock ) // Защищаем словарь _counters
        {
            if ( !_counters.TryGetValue( path, out PerformanceCounter? counter ) )
            {
                try
                {
                    counter = new PerformanceCounter( "Process", "% Processor Time", processName, true );
                    counter.NextValue();
                    _counters[ path ] = counter;
                }
                catch
                {
                    return 0;
                }
            }

            try
            {
                return counter.NextValue() / Environment.ProcessorCount;
            }
            catch
            {
                counter.Dispose();
                _counters.Remove( path );
                return 0;
            }
        }
    }

    /// <summary>
    /// Освобождает все ресурсы, используемые текущим экземпляром монитора.
    /// </summary>
    public void Dispose()
    {
        if ( _disposed )
            return;

        _disposed = true;
        _cts.Cancel();
        _cts.Dispose();

        lock ( _lock )
        {
            foreach ( PerformanceCounter counter in _counters.Values )
                counter.Dispose();

            _counters.Clear();
        }
    }
}