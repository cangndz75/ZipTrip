using System;

namespace ZipTrip.Domain
{
    public static class StateHash
    {
        public static ulong Compute(GameState state)
        {
            return Compute(CanonicalStateSerializer.Serialize(state));
        }

        public static ulong Compute(byte[] bytes)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));

            const ulong offsetBasis = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            var hash = offsetBasis;
            unchecked
            {
                foreach (var value in bytes)
                {
                    hash ^= value;
                    hash *= prime;
                }
            }
            return hash;
        }
    }
}
