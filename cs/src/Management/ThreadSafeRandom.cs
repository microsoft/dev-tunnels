using System;
using System.Threading.Tasks;

namespace Microsoft.DevTunnels.Management;

internal static class ThreadSafeRandom
{
    [ThreadStatic]
    private static Random? _local;
    private static readonly Random Global = new();

    private static Random Instance
    {
        get
        {
            if (_local is null)
            {
                int seed;
                lock (Global)
                {
                    seed = Global.Next();
                }

                _local = new Random(seed);
            }

            return _local;
        }
    }

    public static int Next() => Instance.Next();
    public static int Next(int maxValue) => Instance.Next(maxValue);

}