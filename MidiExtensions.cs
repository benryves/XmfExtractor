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

		public static uint ReadBigEndianUInt32(this BinaryReader reader) {
			uint result = 0;
			uint temp = reader.ReadUInt32();
			for (int i = 0; i < 4; ++i) {
				result <<= 8;
				result |= (byte)temp;
				temp >>= 8;
			}
			return result;
		}

		public static string ReadXString(this BinaryReader reader) {
			var length = reader.ReadVLQ();
			return Encoding.ASCII.GetString(reader.ReadBytes(checked((int)length)));
		}

	}
}
