using System;
using System.IO;
using System.Text;

namespace ZipTrip.Domain
{
    public static class CanonicalStateSerializer
    {
        public static byte[] Serialize(GameState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            using (var stream = new MemoryStream())
            {
                WriteInt32(stream, 1); // Format version.
                WriteInt32(stream, GridSize.Width);
                WriteInt32(stream, GridSize.Height);
                WriteString(stream, state.Container.Id);
                WriteInt32(stream, (int)state.Container.ZipperEdge);
                var mask = state.Container.Mask.ToStableBytes();
                WriteInt32(stream, mask.Length);
                stream.Write(mask, 0, mask.Length);

                WriteInt32(stream, state.Placements.Count);
                foreach (var placement in state.Placements) // Ordinal item id order.
                {
                    WriteString(stream, placement.ItemId);
                    WriteInt32(stream, placement.Anchor.X);
                    WriteInt32(stream, placement.Anchor.Y);
                    WriteInt32(stream, (int)placement.Rotation);
                    WriteString(stream, placement.ShapeState);
                    WriteInt32(stream, placement.OccupiedCells.Count);
                    foreach (var cell in placement.OccupiedCells) // Canonical row-major order.
                    {
                        WriteInt32(stream, cell.X);
                        WriteInt32(stream, cell.Y);
                    }
                }

                WriteInt32(stream, state.Tray.Count);
                foreach (var itemId in state.Tray) // Slot order is gameplay state.
                    WriteString(stream, itemId);

                WriteInt32(stream, state.Targets.Count);
                foreach (var itemId in state.Targets) // Ordinal id order.
                    WriteString(stream, itemId);

                return stream.ToArray();
            }
        }

        private static void WriteString(Stream stream, string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            WriteInt32(stream, bytes.Length);
            stream.Write(bytes, 0, bytes.Length);
        }

        private static void WriteInt32(Stream stream, int value)
        {
            unchecked
            {
                stream.WriteByte((byte)value);
                stream.WriteByte((byte)(value >> 8));
                stream.WriteByte((byte)(value >> 16));
                stream.WriteByte((byte)(value >> 24));
            }
        }
    }
}
