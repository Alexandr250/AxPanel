using AxPanel.Model;
using System.Diagnostics;

namespace AxPanel.SL;

public class ProcessMonitor : IDisposable
{
    private readonly Dictionary<string, PerformanceCounter> _counters = new();
    private readonly object _lock = new();
    private readonly CancellationTokenSource _cts = new();
    private HashSet<string> _targetPaths = [];
    private bool _disposed;

    public event Action<Dictionary<string, ProcessStats>> StatisticsUpdated;

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

    public void Start() => 
        Task.Run( () => MonitorLoop( _cts.Token ) );

    public void Stop() => 
        _cts.Cancel();

    private async Task MonitorLoop( CancellationToken token )
    {
        while ( !token.IsCancellationRequested )
        {
            Dictionary<string, ProcessStats> stats = new( StringComparer.OrdinalIgnoreCase );
            Process[] allProcesses = Process.GetProcesses();

            string[] paths;
            lock ( _lock ) paths = [ .. _targetPaths ];

            foreach ( string path in paths )
            {
                string fileName = Path.GetFileNameWithoutExtension( path );
                // Важно: берем только первый процесс, но фильтруем по пути, если это критично
                Process? process = allProcesses.FirstOrDefault( p => p.ProcessName.Equals( fileName, StringComparison.OrdinalIgnoreCase ) );

                if ( process != null )
                {
                    try
                    {
                        stats[ path ] = new ProcessStats
                        {
                            IsRunning = true,
                            CpuUsage = GetCpuUsage( path, process.ProcessName ),
                            RamMb = process.WorkingSet64 / 1024 / 1024,
                            WindowCount = allProcesses.Count( p =>
                                p.ProcessName.Equals( fileName, StringComparison.OrdinalIgnoreCase ) &&
                                p.MainWindowHandle != IntPtr.Zero ),
                            StartTime = process.StartTime
                        };
                    }
                    catch { stats[ path ] = new ProcessStats { IsRunning = false }; }
                }
                else
                {
                    stats[ path ] = new ProcessStats { IsRunning = false };
                }
            }

            // Освобождаем ресурсы процессов
            foreach ( Process p in allProcesses ) 
                p.Dispose();

            StatisticsUpdated?.Invoke( stats );

            try
            {
                await Task.Delay( 1000, token );
            }
            catch( OperationCanceledException )
            {
                break;
            }
        }
    }

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