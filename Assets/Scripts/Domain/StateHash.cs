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
            return Fnv1a64.Compute(bytes);
        }
    }
}
