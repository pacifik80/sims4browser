using System.Runtime.InteropServices;
using Sims4ResourceExplorer.Core;

namespace Sims4ResourceExplorer.App.Services;

public interface ISystemMemoryService
{
    SystemMemorySnapshot GetSnapshot();
}

public sealed class Win32SystemMemoryService : ISystemMemoryService
{
    public SystemMemorySnapshot GetSnapshot()
    {
        if (TryGetSnapshot(out var snapshot))
        {
            return snapshot;
        }

        var fallback = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
        if (fallback > 0)
        {
            var bytes = (ulong)Math.Min(fallback, long.MaxValue);
            return new SystemMemorySnapshot(bytes, bytes);
        }

        return new SystemMemorySnapshot(
            (ulong)IndexingRunOptions.DefaultPackageByteCacheBudgetBytes,
            (ulong)IndexingRunOptions.DefaultPackageByteCacheBudgetBytes);
    }

    private static bool TryGetSnapshot(out SystemMemorySnapshot snapshot)
    {
        var status = new MemoryStatusEx
        {
            Length = (uint)Marshal.SizeOf<MemoryStatusEx>()
        };

        if (GlobalMemoryStatusEx(ref status))
        {
            snapshot = new SystemMemorySnapshot(status.TotalPhys, status.AvailPhys);
            return true;
        }

        snapshot = default!;
        return false;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhys;
        public ulong AvailPhys;
        public ulong TotalPageFile;
        public ulong AvailPageFile;
        public ulong TotalVirtual;
        public ulong AvailVirtual;
        public ulong AvailExtendedVirtual;
    }
}
