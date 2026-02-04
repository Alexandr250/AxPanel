using AxPanel.Model;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;

namespace AxPanel.SL
{
    public class ProcessMonitor : IDisposable
    {
        private readonly Dictionary<string, PerformanceCounter> _counters = new();
        private bool _disposed;
        private HashSet<string> _targetPaths = new();
        private readonly object _lock = new();

        public event Action<Dictionary<string, ProcessStats>> StatisticsUpdated;

        public HashSet<string> TargetPaths
        {
            get { lock ( _lock ) return _targetPaths; }
            set { lock ( _lock ) _targetPaths = value; }
        }

        public void Start()
        {
            var thread = new Thread( MonitorLoop ) { IsBackground = true };
            thread.Start();
        }

        public void Stop()
        {
            // Устанавливаем флаг, чтобы цикл while (!_disposed) или while (_isRunning) завершился
            _disposed = true;
        }

        private void MonitorLoop()
        {
            while ( !_disposed )
            {
                var stats = new Dictionary<string, ProcessStats>( StringComparer.OrdinalIgnoreCase );
                var allProcesses = Process.GetProcesses();

                HashSet<string> currentPaths;
                lock ( _lock )
                {
                    currentPaths = new HashSet<string>( _targetPaths, StringComparer.OrdinalIgnoreCase );
                }

                foreach ( var path in currentPaths )
                {
                    var fileName = Path.GetFileNameWithoutExtension( path );

                    // Ищем процесс по имени файла (как в твоем оригинальном коде)
                    var process = allProcesses.FirstOrDefault( p =>
                        p.ProcessName.Equals( fileName, StringComparison.OrdinalIgnoreCase ) );

                    if ( process != null )
                    {
                        try
                        {
                            stats[ path ] = new ProcessStats
                            {
                                IsRunning = true,
                                CpuUsage = GetCpuUsage( path, process.ProcessName ),
                                RamMb = process.WorkingSet64 / 1024 / 1024,
                                // Используем новый метод из Win32Api
                                WindowCount = Win32Api.GetWindowCount( process.Id ),
                                StartTime = process.StartTime
                            };
                        }
                        catch
                        {
                            // На случай, если процесс закрылся прямо во время чтения данных
                            stats[ path ] = new ProcessStats { IsRunning = false };
                        }
                    }
                    else
                    {
                        stats[ path ] = new ProcessStats { IsRunning = false };
                    }
                }

                StatisticsUpdated?.Invoke( stats );
                Thread.Sleep( 1000 );
            }
        }

        private float GetCpuUsage( string path, string processName )
        {
            if ( !_counters.TryGetValue( path, out var counter ) )
            {
                try
                {
                    // Создаем счетчик. Параметр "true" позволяет читать данные только для чтения
                    counter = new PerformanceCounter( "Process", "% Processor Time", processName, true );
                    counter.NextValue(); // Первый вызов всегда возвращает 0
                    _counters[ path ] = counter;
                }
                catch
                {
                    return 0;
                }
            }

            try
            {
                // Делим на количество ядер, так как PerformanceCounter возвращает общую сумму нагрузки
                return counter.NextValue() / Environment.ProcessorCount;
            }
            catch
            {
                if ( _counters.ContainsKey( path ) )
                {
                    _counters[ path ].Dispose();
                    _counters.Remove( path );
                }
                return 0;
            }
        }

        public void Dispose()
        {
            if ( _disposed ) return;
            _disposed = true;

            lock ( _lock )
            {
                foreach ( var counter in _counters.Values )
                {
                    counter.Dispose();
                }
                _counters.Clear();
            }
        }
    }
    //public class ProcessMonitor : IDisposable
    //{
    //    private readonly System.Windows.Forms.Timer _timer;
    //    private readonly ConcurrentDictionary<string, (TimeSpan Time, DateTime Stamp)> _cpuStats = new();

    //    // Событие, на которое подпишется панель
    //    public event Action<Dictionary<string, ProcessStats>> StatisticsUpdated;

    //    public ProcessMonitor( int intervalMs = 2000 )
    //    {
    //        _timer = new System.Windows.Forms.Timer { Interval = intervalMs };
    //        _timer.Tick += async ( s, e ) => await RefreshStatsAsync();
    //    }

    //    public void Start() => _timer.Start();
    //    public void Stop() => _timer.Stop();

    //    // Передаем список путей, которые нужно мониторить
    //    public HashSet<string> TargetPaths { get; set; } = new( StringComparer.OrdinalIgnoreCase );

    //    private async Task RefreshStatsAsync()
    //    {
    //        if ( TargetPaths.Count == 0 ) return;

    //        // Копируем список путей для безопасной итерации в потоке
    //        var paths = TargetPaths.ToHashSet();

    //        var results = await Task.Run( () => PerformCrawl( paths ) );

    //        StatisticsUpdated?.Invoke( results );
    //    }

    //    private Dictionary<string, ProcessStats> PerformCrawl( HashSet<string> targetPaths )
    //    {
    //        var foundProcesses = new Dictionary<string, List<int>>( StringComparer.OrdinalIgnoreCase );
    //        var statsResult = new Dictionary<string, ProcessStats>( StringComparer.OrdinalIgnoreCase );

    //        // 1. Поиск PID по путям через Win32Api
    //        int[] pids = new int[ 2048 ];
    //        if ( !Win32Api.EnumProcesses( pids, pids.Length * sizeof( int ), out int bytesReturned ) ) return statsResult;

    //        int count = bytesReturned / sizeof( int );
    //        for ( int i = 0; i < count; i++ )
    //        {
    //            int pid = pids[ i ];
    //            if ( pid <= 0 ) continue;

    //            string path = GetPath( pid );
    //            if ( path != null && targetPaths.Contains( path ) )
    //            {
    //                if ( !foundProcesses.ContainsKey( path ) ) foundProcesses[ path ] = new List<int>();
    //                foundProcesses[ path ].Add( pid );
    //            }
    //        }

    //        // 2. Расчет метрик (CPU, RAM, StartTime)
    //        DateTime now = DateTime.Now;
    //        foreach ( var path in targetPaths )
    //        {
    //            if ( foundProcesses.TryGetValue( path, out var pidList ) )
    //            {
    //                statsResult[ path ] = CalculateStats( path, pidList, now );
    //            }
    //            else
    //            {
    //                statsResult[ path ] = new ProcessStats( false, 0, 0, null );
    //            }
    //        }

    //        return statsResult;
    //    }

    //    private ProcessStats CalculateStats( string path, List<int> pids, DateTime now )
    //    {
    //        TimeSpan totalCpu = TimeSpan.Zero;
    //        long totalRam = 0;
    //        DateTime? earliestStart = null;

    //        foreach ( var pid in pids )
    //        {
    //            try
    //            {
    //                using var p = Process.GetProcessById( pid );
    //                if ( earliestStart == null || p.StartTime < earliestStart ) earliestStart = p.StartTime;
    //                totalCpu += p.TotalProcessorTime;
    //                totalRam += p.WorkingSet64;
    //            }
    //            catch { /* Процесс закрылся */ }
    //        }

    //        float cpuUsage = 0;
    //        if ( _cpuStats.TryGetValue( path, out var last ) )
    //        {
    //            double msDiff = ( now - last.Stamp ).TotalMilliseconds;
    //            double cpuDiff = ( totalCpu - last.Time ).TotalMilliseconds;
    //            cpuUsage = ( float )( cpuDiff / ( msDiff * Environment.ProcessorCount ) * 100 );
    //        }
    //        _cpuStats[ path ] = (totalCpu, now);

    //        return new ProcessStats( true, Math.Clamp( cpuUsage, 0, 100 ), totalRam / 1024f / 1024f, earliestStart );
    //    }

    //    private string GetPath( int pid )
    //    {
    //        IntPtr h = Win32Api.OpenProcess( Win32Api.PROCESS_QUERY_LIMITED_INFORMATION, false, pid );
    //        if ( h == IntPtr.Zero ) return null;
    //        try
    //        {
    //            StringBuilder sb = new StringBuilder( 1024 );
    //            int cap = sb.Capacity;
    //            return Win32Api.QueryFullProcessImageName( h, 0, sb, ref cap ) ? sb.ToString() : null;
    //        }
    //        finally { Win32Api.CloseHandle( h ); }
    //    }

    //    public void Dispose() => _timer?.Dispose();
    //}
}
