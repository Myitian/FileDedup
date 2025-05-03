using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace FileDedup;

[InlineArray(8)]
public struct FileHash : IEquatable<FileHash>
{
    private int _;

    public FileHash(FileInfo file)
    {
        using FileStream fs = file.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
        SHA256.HashData(fs, MemoryMarshal.AsBytes<int>(this));
    }

    public readonly bool Equals(FileHash other)
        => ((ReadOnlySpan<int>)this).SequenceEqual(other);
    public override readonly bool Equals(object? obj)
        => obj is FileHash fh && Equals(fh);
    public override readonly int GetHashCode()
        => this[0];
    public static bool operator ==(FileHash left, FileHash right)
        => left.Equals(right);
    public static bool operator !=(FileHash left, FileHash right)
        => !(left == right);
}
