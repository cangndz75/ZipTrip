using System;

namespace ZipTrip.Domain
{
    public sealed class ContainerDefinition
    {
        public string Id { get; }
        public ContainerMask Mask { get; }
        public ZipperEdge ZipperEdge { get; }

        public ContainerDefinition(
            string id,
            ContainerMask mask,
            ZipperEdge zipperEdge)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Container id is required.", nameof(id));

            Mask = mask ?? throw new ArgumentNullException(nameof(mask));

            Id = id;
            ZipperEdge = zipperEdge;
        }
    }
}
