using System.Text.Json.Serialization;

namespace CpuUsage.Core.Models;

public struct MemorySize : IComparable<MemorySize>, IEquatable<MemorySize>
{
    private const double Tolerance = 0.00000001;

    [JsonIgnore] public double Bytes { get; }
    [JsonIgnore] public double Kilobytes => Bytes / 1024.0;
    [JsonIgnore] public double Megabytes => Bytes / 1024.0 / 1024.0;
    public double Gigabytes => Bytes / 1024.0 / 1024.0 / 1024.0;

    private MemorySize(double bytes)
    {
        Bytes = bytes;
    }

    public static MemorySize FromBytes(ulong bytes) => new (bytes);
    public static MemorySize FromBytes(double bytes) => new (bytes);

    public static MemorySize FromKilobytes(double kb) => new ((long) (kb * 1024));

    public static MemorySize FromMegabytes(double mb) => new ((long) (mb * 1024 * 1024));

    public static MemorySize FromGigabytes(double gb) => new ((long) (gb * 1024 * 1024 * 1024));

    public override string ToString()
    {
        if (Gigabytes >= 1)
        {
            return $"{Gigabytes:F2} GB";
        }
        if (Megabytes >= 1)
        {
            return $"{Megabytes:F2} MB";
        }
        if (Kilobytes >= 1)
        {
            return $"{Kilobytes:F2} KB";
        }
        return $"{Bytes} B";
    }

    public static MemorySize operator +(MemorySize a, MemorySize b) => new (a.Bytes + b.Bytes);

    public static MemorySize operator -(MemorySize a, MemorySize b) => new (a.Bytes - b.Bytes);

    public static MemorySize operator /(MemorySize a, MemorySize b) => new (a.Bytes / b.Bytes);

    public static MemorySize operator *(MemorySize a, MemorySize b) => new (a.Bytes * b.Bytes);

    public int CompareTo(MemorySize other) => Bytes.CompareTo(other.Bytes);

    public bool Equals(MemorySize other) => Math.Abs(Bytes - other.Bytes) < Tolerance;

    public override bool Equals(object? obj) => obj is MemorySize other && Equals(other);

    public override int GetHashCode() => Bytes.GetHashCode();

    public static bool operator <(MemorySize a, MemorySize b) => a.Bytes < b.Bytes;

    public static bool operator >(MemorySize a, MemorySize b) => a.Bytes > b.Bytes;

    public static implicit operator MemorySize(long bytes) => new (bytes);

    public static implicit operator MemorySize(double bytes) => new ((long) bytes);

    public static implicit operator double(MemorySize bytes) => bytes.Bytes;
}
