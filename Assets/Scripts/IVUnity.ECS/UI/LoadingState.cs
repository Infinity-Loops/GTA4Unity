using System.Threading;

namespace IVUnity.ECS.UI
{
    /// <summary>
    /// Thread-safe state for loading progress. Writers (any thread — e.g. GTADatLoader's
    /// Task.Run workers) update fields atomically. LoadingBridgeSystem snapshots the state
    /// on the main thread each frame and publishes it as the LoadingProgress ECS singleton.
    ///
    /// No UI access, no dispatch, no Unity API dependencies — safe to call from anywhere.
    /// </summary>
    public static class LoadingState
    {
        private static int      _target;
        private static int      _current;
        private static string   _label;
        private static int      _finished; // 0/1 via Interlocked

        public static void SetTarget(int n)
        {
            Interlocked.Exchange(ref _current, 0);
            Interlocked.Exchange(ref _target, n);
        }

        public static void ResetProgress()
        {
            Interlocked.Exchange(ref _current, 0);
        }

        public static void Advance(string label, int count = 1)
        {
            Interlocked.Add(ref _current, count);
            if (label != null) _label = label; // reference assignment is atomic for refs on all runtimes we target
        }

        public static void Finish() => Interlocked.Exchange(ref _finished, 1);

        public static void Reopen()
        {
            Interlocked.Exchange(ref _finished, 0);
            Interlocked.Exchange(ref _current, 0);
            Interlocked.Exchange(ref _target, 0);
            _label = null;
        }

        public static int    Target   => Volatile.Read(ref _target);
        public static int    Current  => Volatile.Read(ref _current);
        public static string Label    => _label ?? string.Empty;
        public static bool   Finished => Volatile.Read(ref _finished) != 0;
    }
}
