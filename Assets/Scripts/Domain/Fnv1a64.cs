using System;

namespace ZipTrip.Domain
{
    /// <summary>
    /// Stable 64-bit FNV-1a over canonical bytes. Neutral shared primitive (no gameplay semantics):
    /// used by the ADR-0006 board model and by legacy StateHash. Retained across ZT-049.
    /// </summary>
    public static class Fnv1a64
    {
        private const ulong OffsetBasis = 14695981039346656037UL;
        private const ulong Prime = 1099511628211UL;

        public static ulong Compute(byte[] bytes)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));

            var hash = OffsetBasis;
            unchecked
            {
                foreach (var value in bytes)
                {
                    hash ^= value;
                    hash *= Prime;
                }
            }
            return hash;
        }
    }
}
