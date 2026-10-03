using System.IO;
using System.Text;

namespace ZipTrip.Domain.Board
{
    // Primitive writers for the new, versioned ADR-0006 board format (BoardSpec.FormatVersion): little-endian Int32,
    // length-prefixed UTF-8 strings and byte arrays. Not a reproduction of any legacy GameState byte layout.
    internal static class CanonicalWriter
    {
        public static void WriteInt32(Stream stream, int value)
        {
            unchecked
            {
                stream.WriteByte((byte)value);
                stream.WriteByte((byte)(value >> 8));
                stream.WriteByte((byte)(value >> 16));
                stream.WriteByte((byte)(value >> 24));
            }
        }

        public static void WriteString(Stream stream, string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            WriteInt32(stream, bytes.Length);
            stream.Write(bytes, 0, bytes.Length);
        }

        public static void WriteBytes(Stream stream, byte[] value)
        {
            WriteInt32(stream, value.Length);
            stream.Write(value, 0, value.Length);
        }
    }
}
