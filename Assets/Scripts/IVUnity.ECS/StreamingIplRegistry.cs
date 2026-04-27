using System.Collections.Generic;

namespace IVUnity.ECS
{
    /// <summary>
    /// Tracks which WPLs are base (gta.dat) vs streaming.
    /// Streaming WPL entities use inverted proximity: only visible beyond
    /// a radius from the focus point. Close = hidden (gta.dat HD covers it).
    /// Engine: FUN_00c77c70 — loaded when far, unloaded when close.
    /// </summary>
    public static class StreamingIplRegistry
    {
        public static readonly HashSet<string> BaseWplNames = new(System.StringComparer.OrdinalIgnoreCase);
        public static int StreamingWplCount;

        public static void Clear()
        {
            BaseWplNames.Clear();
            StreamingWplCount = 0;
        }
    }
}
