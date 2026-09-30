
namespace ZipTrip.Domain
{
    public readonly struct DomainSmokeValue
    {
        public int Value { get; }

        public DomainSmokeValue(int value)
        {
            Value = value;
        }
    }
}
