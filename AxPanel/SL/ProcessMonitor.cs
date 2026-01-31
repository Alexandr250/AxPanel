using AxPanel.Model;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;

namespace AxPanel.SL
{
    public class ProcessMonitor : IDisposable
    {
        private readonly System.Windows.Forms.Timer _timer;
        private readonly ConcurrentDictionary<string, (TimeSpan Time, DateTime Stamp)> _cpuStats = new();

        // Событие, на которое подпишется панель
        public event Action<Dictionary<string, ProcessStats>> StatisticsUpdated;

        public ProcessMonitor( int intervalMs = 2000 )
        {
            _timer = new System.Windows.Forms.Timer { Interval = intervalMs };
            _timer.Tick += async ( s, e ) => await RefreshStatsAsync();
        }

        public void Start() => _timer.Start();
        public void Stop() => _timer.Stop();

        // Передаем список путей, которые нужно мониторить
        public HashSet<string> TargetPaths { get; set; } = new( StringComparer.OrdinalIgnoreCase );

        private async Task RefreshStatsAsync()
        {
            if ( TargetPaths.Count == 0 ) return;

            // Копируем список путей для безопасной итерации в потоке
            var paths = TargetPaths.ToHashSet();

            var results = await Task.Run( () => PerformCrawl( paths ) );

            StatisticsUpdated?.Invoke( results );
        }

        private Dictionary<string, ProcessStats> PerformCrawl( HashSet<string> targetPaths )
        {
            var foundProcesses = new Dictionary<string, List<int>>( StringComparer.OrdinalIgnoreCase );
            var statsResult = new Dictionary<string, ProcessStats>( StringComparer.OrdinalIgnoreCase );

            // 1. Поиск PID по путям через Win32Api
            int[] pids = new int[ 2048 ];
            if ( !Win32Api.EnumProcesses( pids, pids.Length * sizeof( int ), out int bytesReturned ) ) return statsResult;

            int count = bytesReturned / sizeof( int );
            for ( int i = 0; i < count; i++ )
            {
                int pid = pids[ i ];
                if ( pid <= 0 ) continue;

                string path = GetPath( pid );
                if ( path != null && targetPaths.Contains( path ) )
                {
                    if ( !foundProcesses.ContainsKey( path ) ) foundProcesses[ path ] = new List<int>();
                    foundProcesses[ path ].Add( pid );
                }
            }

            // 2. Расчет метрик (CPU, RAM, StartTime)
            DateTime now = DateTime.Now;
            foreach ( var path in targetPaths )
            {
                if ( foundProcesses.TryGetValue( path, out var pidList ) )
                {
                    statsResult[ path ] = CalculateStats( path, pidList, now );
                }
                else
                {
                    statsResult[ path ] = new ProcessStats( false, 0, 0, null );
                }
            }

            return statsResult;
        }

        private ProcessStats CalculateStats( string path, List<int> pids, DateTime now )
        {
            TimeSpan totalCpu = TimeSpan.Zero;
            long totalRam = 0;
            DateTime? earliestStart = null;

            foreach ( var pid in pids )
            {
                try
                {
                    using var p = Process.GetProcessById( pid );
                    if ( earliestStart == null || p.StartTime < earliestStart ) earliestStart = p.StartTime;
                    totalCpu += p.TotalProcessorTime;
                    totalRam += p.WorkingSet64;
                }
                catch { /* Процесс закрылся */ }
            }

            float cpuUsage = 0;
            if ( _cpuStats.TryGetValue( path, out var last ) )
            {
                double msDiff = ( now - last.Stamp ).TotalMilliseconds;
                double cpuDiff = ( totalCpu - last.Time ).TotalMilliseconds;
                cpuUsage = ( float )( cpuDiff / ( msDiff * Environment.ProcessorCount ) * 100 );
            }
            _cpuStats[ path ] = (totalCpu, now);

            return new ProcessStats( true, Math.Clamp( cpuUsage, 0, 100 ), totalRam / 1024f / 1024f, earliestStart );
        }

        private string GetPath( int pid )
        {
            IntPtr h = Win32Api.OpenProcess( Win32Api.PROCESS_QUERY_LIMITED_INFORMATION, false, pid );
            if ( h == IntPtr.Zero ) return null;
            try
            {
                StringBuilder sb = new StringBuilder( 1024 );
                int cap = sb.Capacity;
                return Win32Api.QueryFullProcessImageName( h, 0, sb, ref cap ) ? sb.ToString() : null;
            }
            finally { Win32Api.CloseHandle( h ); }
        }

        public void Dispose() => _timer?.Dispose();
    }
}
