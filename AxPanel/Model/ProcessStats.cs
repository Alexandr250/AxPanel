namespace AxPanel.Model;

public struct ProcessStats : IComparable<ProcessStats>, IEquatable<ProcessStats>
{
    public bool IsRunning;
    public float CpuUsage;
    public float RamMb;
    public int WindowCount;
    public DateTime? StartTime;

    // Перегрузка всех операторов сравнения
    public static bool operator == ( ProcessStats left, ProcessStats right ) => left.Equals( right );
    public static bool operator != ( ProcessStats left, ProcessStats right ) => !left.Equals( right );
    public static bool operator < ( ProcessStats left, ProcessStats right ) => left.CompareTo( right ) < 0;
    public static bool operator > ( ProcessStats left, ProcessStats right ) => left.CompareTo( right ) > 0;
    public static bool operator <= ( ProcessStats left, ProcessStats right ) => left.CompareTo( right ) <= 0;
    public static bool operator >= ( ProcessStats left, ProcessStats right ) => left.CompareTo( right ) >= 0;

    public bool Equals( ProcessStats other )
    {
        return IsRunning == other.IsRunning &&
               Math.Abs( CpuUsage - other.CpuUsage ) < 0.001f && // Сравнение float с допуском
               Math.Abs( RamMb - other.RamMb ) < 0.001f &&
               WindowCount == other.WindowCount &&
               StartTime == other.StartTime;
    }

    public override bool Equals( object? obj ) => 
        obj is ProcessStats other && Equals( other );

    public override int GetHashCode() => 
        HashCode.Combine( IsRunning, CpuUsage, RamMb, WindowCount, StartTime );

    public int CompareTo( ProcessStats other )
    {
        // Кастомная логика сравнения
        int runningComparison = other.IsRunning.CompareTo( IsRunning );
        if ( runningComparison != 0 ) 
            return runningComparison;

        int cpuComparison = other.CpuUsage.CompareTo( CpuUsage );
        if ( cpuComparison != 0 ) 
            return cpuComparison;

        int ramComparison = other.RamMb.CompareTo( RamMb );
        if ( ramComparison != 0 ) 
            return ramComparison;

        int windowComparison = WindowCount.CompareTo( other.WindowCount );
        if ( windowComparison != 0 ) 
            return windowComparison;

        return Nullable.Compare( StartTime, other.StartTime );
    }

    public bool HasSignificantChangeFrom( ProcessStats other, float cpuThreshold = 0.5f, float ramThreshold = 0.5f )
    {
        return IsRunning != other.IsRunning ||
               WindowCount != other.WindowCount ||
               Math.Abs( CpuUsage - other.CpuUsage ) > cpuThreshold ||
               Math.Abs( RamMb - other.RamMb ) > ramThreshold ||
               StartTime != other.StartTime;
    }
}