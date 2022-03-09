using System.IO;
using System.Text;

namespace XmfExtractor {
	static class MidiExtensionMethods {

		public static ulong ReadVLQ(this BinaryReader reader) {

			ulong result = 0;

			byte b;
			do {
				result <<= 7;
				b = reader.ReadByte();
				result |= (byte)(b & 0x7F);
			} while ((b & 0x80) != 0);

			return result;
		}

		static uint SwapEndianness(uint value) {
			uint result = 0;
			for (int i = 0; i < 4; ++i) {
				result <<= 8;
				result |= (byte)value;
				value >>= 8;
			}
			return result;
		}

		static ushort SwapEndianness(ushort value) {
			ushort result = 0;
			for (int i = 0; i < 2; ++i) {
				result <<= 8;
				result |= (byte)value;
				value >>= 8;
			}
			return result;
		}

		public static uint ReadBigEndianUInt32(this BinaryReader reader) {
			return SwapEndianness(reader.ReadUInt32());
		}

		public static ushort ReadBigEndianUInt16(this BinaryReader reader) {
			return SwapEndianness(reader.ReadUInt16());
		}

		public static void WriteBigEndianValue(this BinaryWriter writer, uint value) {
			writer.Write(SwapEndianness(value));
		}

		public static void WriteBigEndianValue(this BinaryWriter writer, ushort value) {
			writer.Write(SwapEndianness(value));
		}

		public static string ReadXString(this BinaryReader reader) {
			var length = reader.ReadVLQ();
			return Encoding.ASCII.GetString(reader.ReadBytes(checked((int)length)));
		}

	}
}
