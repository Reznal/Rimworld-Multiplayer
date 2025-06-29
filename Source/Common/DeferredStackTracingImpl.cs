using System;
using System.Runtime.CompilerServices;
using Verse;

namespace Multiplayer.Client.Desyncs;

public static class DeferredStackTracingImpl
{
    struct AddrInfo
    {
        public long addr;
        public long stackUsage;
        public long nameHash;
        public long unused;
    }

    const int StartingN = 7;
    const int StartingShift = 64 - StartingN;
    const int StartingSize = 1 << StartingN;
    const float LoadFactor = 0.5f;

    static AddrInfo[] hashtable = new AddrInfo[StartingSize];
    public static int hashtableSize = StartingSize;
    public static int hashtableEntries;
    public static int hashtableShift = StartingShift;
    public static int collisions;

    const long NotJit = long.MaxValue;
    const long RbpBased = long.MaxValue - 1;

    const long UsesRbpAsGpr = 1L << 50;
    const long UsesRbx = 1L << 51;
    const long RbpInfoClearMask = ~(UsesRbpAsGpr | UsesRbx);

    public const int MaxDepth = 32;
    public const int HashInfluence = 6;

    public static unsafe int TraceImpl(long[] traceIn, ref int hash)
    {
        // Critical safety checks - prevent null reference exceptions
        if (traceIn == null)
        {
            // Log error and return safely
            if (MpVersion.IsDebug)
                Log.Error("DeferredStackTracing: traceIn array is null");
            return 0;
        }

        // Additional safety check for Native.LmfPtr
        if (Native.LmfPtr == 0)
        {
            if (MpVersion.IsDebug)
                Log.Warning("DeferredStackTracing: Native.LmfPtr is not initialized");
            return 0;
        }

        try
        {
            long[] trace = traceIn;
            long rbp = GetRbp();
            long stck = rbp;
            
            // Safety check for initial RBP
            if (rbp == 0)
            {
                if (MpVersion.IsDebug)
                    Log.Warning("DeferredStackTracing: Initial RBP is null");
                return 0;
            }

            rbp = *(long*)rbp;

            int indexmask = hashtableSize - 1;
            int shift = hashtableShift;

            long ret;
            long lmfPtr = *(long*)Native.LmfPtr;

            int depth = 0;

            while (true)
            {
                // Safety check for stack pointer
                if (stck == 0)
                {
                    if (MpVersion.IsDebug)
                        Log.Warning("DeferredStackTracing: Stack pointer became null");
                    break;
                }

                ret = *(long*)(stck + 8);

                int index = (int)(HashAddr((ulong)ret) >> shift);
                ref var info = ref hashtable[index];
                int colls = 0;

                // Open addressing
                while (info.addr != 0 && info.addr != ret)
                {
                    index = (index + 1) & indexmask;
                    info = ref hashtable[index];
                    colls++;
                }

                if (colls > collisions)
                    collisions = colls;

                long stackUsage = 0;

                if (info.addr != 0)
                    stackUsage = info.stackUsage;
                else
                {
                    try
                    {
                        stackUsage = UpdateNewElement(ref info, ret);
                    }
                    catch (Exception ex)
                    {
                        if (MpVersion.IsDebug)
                            Log.Error($"DeferredStackTracing: Failed to update new element for addr {ret:X}: {ex.Message}");
                        stackUsage = NotJit; // Treat as non-JIT to continue
                    }
                }

                if (stackUsage == NotJit)
                {
                    // LMF (Last Managed Frame) layout on x64:
                    // previous
                    // rbp
                    // rsp

                    // Safety check for LMF operations
                    if (lmfPtr == 0)
                    {
                        if (MpVersion.IsDebug)
                            Log.Warning("DeferredStackTracing: LMF pointer is null, stopping trace");
                        break;
                    }

                    try
                    {
                        lmfPtr = *(long*)lmfPtr;
                        if (lmfPtr == 0)
                            break;

                        var lmfRbp = *(long*)(lmfPtr + 8);
                        if (lmfRbp == 0)
                            break;

                        rbp = lmfRbp;
                        stck = *(long*)(lmfPtr + 16) - 16;
                    }
                    catch (Exception ex)
                    {
                        if (MpVersion.IsDebug)
                            Log.Error($"DeferredStackTracing: LMF access failed: {ex.Message}");
                        break;
                    }

                    continue;
                }

                // Safety check for trace array bounds
                if (depth >= trace.Length)
                {
                    if (MpVersion.IsDebug)
                        Log.Warning($"DeferredStackTracing: Trace depth {depth} exceeds array length {trace.Length}");
                    break;
                }

                trace[depth] = ret;

                // info.nameHash == 0 marks methods to skip
                if (depth < HashInfluence && info.nameHash != 0)
                    hash = HashCombineInt(hash, (int)info.nameHash);

                if (info.nameHash != 0 && ++depth == MaxDepth)
                    break;

                if (stackUsage == RbpBased)
                {
                    // Safety check for RBP operations
                    if (rbp == 0)
                    {
                        if (MpVersion.IsDebug)
                            Log.Warning("DeferredStackTracing: RBP became null during RBP-based tracing");
                        break;
                    }

                    try
                    {
                        stck = rbp;
                        rbp = *(long*)rbp;
                    }
                    catch (Exception ex)
                    {
                        if (MpVersion.IsDebug)
                            Log.Error($"DeferredStackTracing: RBP access failed: {ex.Message}");
                        break;
                    }
                    continue;
                }

                stck += 8;

                if ((stackUsage & UsesRbpAsGpr) != 0)
                {
                    try
                    {
                        if ((stackUsage & UsesRbx) != 0)
                            rbp = *(long*)(stck + 16);
                        else
                            rbp = *(long*)(stck + 8);

                        stackUsage &= RbpInfoClearMask;
                    }
                    catch (Exception ex)
                    {
                        if (MpVersion.IsDebug)
                            Log.Error($"DeferredStackTracing: GPR RBP access failed: {ex.Message}");
                        break;
                    }
                }

                stck += stackUsage;
            }

            return depth;
        }
        catch (Exception ex)
        {
            // Catch-all for any remaining exceptions
            if (MpVersion.IsDebug)
                Log.Error($"DeferredStackTracing: Unexpected error in TraceImpl: {ex}");
            return 0;
        }
    }

    static long UpdateNewElement(ref AddrInfo info, long ret)
    {
        try
        {
            long stackUsage = GetStackUsage(ret);

            info.addr = ret;
            info.stackUsage = stackUsage;

            // Safety check for Native method calls
            try
            {
                var rawName = Native.MethodNameFromAddr(ret, true); // Use the original instead of replacement for hashing
                info.nameHash = rawName != null ? Native.GetMethodAggressiveInlining(ret) ? 0 : StableStringHash(rawName) : 1;
            }
            catch (Exception ex)
            {
                if (MpVersion.IsDebug)
                    Log.Warning($"DeferredStackTracing: Failed to get method name for addr {ret:X}: {ex.Message}");
                info.nameHash = 1; // Default hash value
            }

            hashtableEntries++;
            if (hashtableEntries > hashtableSize * LoadFactor)
                ResizeHashtable();

            return stackUsage;
        }
        catch (Exception ex)
        {
            if (MpVersion.IsDebug)
                Log.Error($"DeferredStackTracing: Failed to update new element: {ex}");
            return NotJit; // Return safe value to continue
        }
    }

    static ulong HashAddr(ulong addr) => ((addr >> 4) | addr << 60) * 11400714819323198485;

    static int ResizeHashtable()
    {
        var oldTable = hashtable;

        hashtableSize *= 2;
        hashtableShift--;

        hashtable = new AddrInfo[hashtableSize];
        collisions = 0;

        int indexmask = hashtableSize - 1;
        int shift = hashtableShift;

        for (int i = 0; i < oldTable.Length; i++)
        {
            ref var oldInfo = ref oldTable[i];
            if (oldInfo.addr != 0)
            {
                int index = (int)(HashAddr((ulong)oldInfo.addr) >> shift);

                while (hashtable[index].addr != 0)
                    index = (index + 1) & indexmask;

                ref var newInfo = ref hashtable[index];
                newInfo.addr = oldInfo.addr;
                newInfo.stackUsage = oldInfo.stackUsage;
                newInfo.nameHash = oldInfo.nameHash;
            }
        }

        return indexmask;
    }

    static unsafe long GetStackUsage(long addr)
    {
        try
        {
            // Safety check for Native.DomainPtr
            if (Native.DomainPtr == IntPtr.Zero)
            {
                if (MpVersion.IsDebug)
                    Log.Warning("DeferredStackTracing: Native.DomainPtr is null");
                return NotJit;
            }

            var ji = Native.mono_jit_info_table_find(Native.DomainPtr, (IntPtr)addr);

            if (ji == IntPtr.Zero)
                return NotJit;

            var start = (uint*)Native.mono_jit_info_get_code_start(ji);
            
            // Safety check for code start pointer
            if (start == null)
            {
                if (MpVersion.IsDebug)
                    Log.Warning("DeferredStackTracing: Code start pointer is null");
                return NotJit;
            }

            long usage = 0;

            if ((*start & 0xFFFFFF) == 0xEC8348) // sub rsp,XX (4883EC XX)
            {
                usage = *start >> 24;
                start += 1;
            } else if ((*start & 0xFFFFFF) == 0xEC8148) // sub rsp,XXXXXXXX (4881EC XXXXXXXX)
            {
                usage = *(uint*)((long)start + 3);
                start = (uint*)((long)start + 7);
            }

            if (usage != 0)
            {
                CheckRbpUsage(start, ref usage);
                return usage;
            }

            // push rbp (55)
            if (*(byte*)start == 0x55)
                return RbpBased;

            // Instead of throwing exception, log warning and return safe value
            if (MpVersion.IsDebug)
            {
                try
                {
                    var methodName = Native.MethodNameFromAddr(addr, false);
                    Log.Warning($"DeferredStackTracing: Unknown function header {*start:X} for method {methodName ?? "unknown"}");
                }
                catch
                {
                    Log.Warning($"DeferredStackTracing: Unknown function header {*start:X} at addr {addr:X}");
                }
            }
            return NotJit; // Return safe value instead of throwing
        }
        catch (Exception ex)
        {
            if (MpVersion.IsDebug)
                Log.Error($"DeferredStackTracing: Exception in GetStackUsage for addr {addr:X}: {ex}");
            return NotJit; // Return safe value on any exception
        }
    }

    private static unsafe void CheckRbpUsage(uint* at, ref long stackUsage)
    {
        try
        {
            // Safety check for null pointer
            if (at == null)
            {
                if (MpVersion.IsDebug)
                    Log.Warning("DeferredStackTracing: CheckRbpUsage called with null pointer");
                return;
            }

            // If rbp is used as a gp reg then the prologue looks like (after frame alloc):
            // mov [rsp],rbp   (48892C24)
            // or:
            // mov [rsp],rbx   (48891C24)
            // mov [rsp+8],rbp (48896C2408)
            // (The calle saved registers are always in the same order
            // and are saved at the bottom of the frame)

            if (*at == 0x242C8948)
            {
                stackUsage |= UsesRbpAsGpr;
            }
            else if (*at == 0x241C8948 && *(at + 1) == 0x246C8948)
            {
                stackUsage |= UsesRbpAsGpr;
                stackUsage |= UsesRbx;
            }
        }
        catch (Exception ex)
        {
            if (MpVersion.IsDebug)
                Log.Error($"DeferredStackTracing: Exception in CheckRbpUsage: {ex.Message}");
            // Don't modify stackUsage on error
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static unsafe long GetRbp()
    {
        long rbp = 0;
        return *(&rbp + 1);
    }

    public static int HashCombineInt(int seed, int value)
    {
        return (int)(seed ^ (value + 2654435769u + (seed << 6) + (seed >> 2)));
    }

    public static int StableStringHash(string? str)
    {
        if (str == null)
        {
            return 0;
        }
        int num = 23;
        int length = str.Length;
        for (int i = 0; i < length; i++)
        {
            num = num * 31 + str[i];
        }
        return num;
    }
}
