using Ionic.Zlib;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace XmfExtractor {

	enum StringFormatTypeID {
		ExtendedAsciiVisible = 0,
		ExtendedAsciiHidden = 1,
		UnicodeVisible = 2,
		UnicodeHidden = 3,
		CompressedUnicodeVisible = 4,
		CompressedUnicodeHidden = 5,
		BinaryDataVisible = 6,
		BinaryDataHidden = 7,
	}

	enum FieldSpecifier {
		XmfFileType = 0,
		NodeName = 1,
		NodeIDNumber = 2,
		ResourceFormat = 3,
		FilenameOnDisk = 4,
		FilenameExtensionOnDisk = 5,
		MacOSFileTypeAndCreator = 6,
		MimeType = 7,
		Title = 8,
		CopyrightNotice = 9,
		Comment = 10,
		Custom = -1,
	}

	enum ResourceFormatID {
		StandardMidiFileType0 = 0,
		StandardMidiFileType1 = 1,
		DownloadableSoundsLevel1 = 2,
		DownloadableSoundsLevel2 = 3,
		DownloadableSoundsLevel2_1 = 4,
		MobileDownloadableSoundsInstrumentFile = 5,
	}

	struct MetaDataType {
		public ulong MetaDataTypeID;
		public StringFormatTypeID StringFormatTypeID;
		public string LangCountrySpec;
	}

	struct InternationalContent {
		public ulong MetaDataTypeID;
		public byte[] Data;
	}

	struct NodeMetaDataItem {

		public FieldSpecifier FieldSpecifier;
		public string CustomFieldSpecifier;

		public StringFormatTypeID UniversalContentsFormat;
		public byte[] UniversalContentsData;

		public InternationalContent[] InternationalContents;

		public override string ToString() {

			string name = null;
			if (FieldSpecifier == FieldSpecifier.Custom) {
				name = CustomFieldSpecifier;
			} else {
				name = FieldSpecifier.ToString();
			}
			name += ": ";

			if (UniversalContentsData != null) {
				if (((int)UniversalContentsFormat & 1) != 0) name += "(";
				name += this.GetStringValue();
				if (((int)UniversalContentsFormat & 1) != 0) name += ")";
			}

			return name;
		}

		public string GetStringValue() {
			if (UniversalContentsData != null) {
				switch ((StringFormatTypeID)((int)UniversalContentsFormat & ~1)) {
					case StringFormatTypeID.ExtendedAsciiVisible:
						return Encoding.Latin1.GetString(UniversalContentsData);
					case StringFormatTypeID.CompressedUnicodeVisible:
						return Encoding.UTF8.GetString(UniversalContentsData);
					case StringFormatTypeID.UnicodeVisible:
						return Encoding.BigEndianUnicode.GetString(UniversalContentsData);
					case StringFormatTypeID.BinaryDataVisible:
						return "<binary>";
					default:
						return "<???>";
				}
			} else {
				return "<international>";
			}
		}

	}


	enum UnpackerIDType {
		Standard = 0,
		MMAManufacturer = 1,
		Registered = 2,
		NonRegistered = 3,
	}

	enum StandardUnpackerID {
		NoUnpacker = 0,
		Zlib = 1,
	}

	struct UnpackerID {

		public UnpackerIDType Type;

		public StandardUnpackerID StandardUnpackerID;

		public byte[] MMAManufacturerID;
		public ulong MMAManufacturerUnpackerType;

		public ulong RegisteredUnpackerID;

	}

	struct NodeUnpackerEntry {
		public UnpackerID UnpackerID;
		public ulong DecodedSize;
	}

	enum ReferenceTypeID {
		InLineResource = 1,
		InFileResource = 2,
		InFileNode = 3,
		ExternalResourceFile = 4,
		ExternalXmfResource = 5,
	}

	struct ReferenceType {
		public ReferenceTypeID Type;
		public ulong ReferenceOffset;
		public Uri ReferenceUri;

	}

	struct Node {

		public ulong HeaderStart;
		public ulong Length;
		public ulong ContainedItems;
		public ulong HeaderLength;

		public ulong MetaDataTableStart;
		public ulong MetaDataLengthInBytes;

		public NodeMetaDataItem[] MetaData;

		public ulong UnpackersStart;
		public ulong UnpackersLengthInBytes;
		public NodeUnpackerEntry[] Unpackers;

		public ReferenceType Reference;

		public Node[] Children;

		public static Node FromStream(Stream stream) {

			var node = new Node();

			var xmfReader = new BinaryReader(stream);

			node.HeaderStart = checked((ulong)xmfReader.BaseStream.Position);

			node.Length = xmfReader.ReadVLQ();
			node.ContainedItems = xmfReader.ReadVLQ();
			node.HeaderLength = xmfReader.ReadVLQ();

			node.MetaDataTableStart = checked((ulong)xmfReader.BaseStream.Position);
			node.MetaDataLengthInBytes = xmfReader.ReadVLQ();

			var nodeMetaData = new List<NodeMetaDataItem>();

			while (xmfReader.BaseStream.Position < (checked((long)(node.MetaDataTableStart + node.MetaDataLengthInBytes)))) {

				var nodeMetaDataItem = new NodeMetaDataItem();

				// how long is the custom field specifier?
				ulong fieldSpecifierLength = xmfReader.ReadVLQ();

				if (fieldSpecifierLength == 0) {
					// if 0, it's a standard field type.
					nodeMetaDataItem.FieldSpecifier = checked((FieldSpecifier)xmfReader.ReadVLQ());
				} else {
					// if non-zero, it's a custom field type.
					nodeMetaDataItem.FieldSpecifier = FieldSpecifier.Custom;
					nodeMetaDataItem.CustomFieldSpecifier = Encoding.ASCII.GetString(xmfReader.ReadBytes(checked((int)fieldSpecifierLength)));
				}

				// how many international contents?
				ulong internationalContentsCount = xmfReader.ReadVLQ();

				if (internationalContentsCount == 0) {
					// universal contents only
					ulong lengthOfData = xmfReader.ReadVLQ();
					long startOfData = xmfReader.BaseStream.Position;

					if (lengthOfData > 0) {
						nodeMetaDataItem.UniversalContentsFormat = checked((StringFormatTypeID)xmfReader.ReadVLQ());
						nodeMetaDataItem.UniversalContentsData = xmfReader.ReadBytes(checked((int)(startOfData - xmfReader.BaseStream.Position + (long)lengthOfData)));
					}
				} else {
					// international contents
					nodeMetaDataItem.InternationalContents = new InternationalContent[internationalContentsCount];
					for (int i = 0; i < nodeMetaDataItem.InternationalContents.Length; ++i) {
						nodeMetaDataItem.InternationalContents[i] = new InternationalContent {
							MetaDataTypeID = xmfReader.ReadVLQ(),
							Data = xmfReader.ReadBytes(checked((int)xmfReader.ReadVLQ())),
						};
					}
				}

				// append the meta data item
				nodeMetaData.Add(nodeMetaDataItem);

			}
			node.MetaData = nodeMetaData.ToArray();


			// shouldn't be necessary..?
			//xmfReader.BaseStream.Seek(checked((long)(node.MetaDataTableStart + node.MetaDataLengthInBytes)), SeekOrigin.Begin);

			// node unpackers

			node.UnpackersStart = checked((ulong)xmfReader.BaseStream.Position);
			node.UnpackersLengthInBytes = xmfReader.ReadVLQ();

			var nodeUnpackers = new List<NodeUnpackerEntry>();

			while (xmfReader.BaseStream.Position < checked((long)(node.UnpackersStart + node.UnpackersLengthInBytes))) {

				var nodeUnpacker = new NodeUnpackerEntry();

				nodeUnpacker.UnpackerID.Type = checked((UnpackerIDType)xmfReader.ReadVLQ());
				switch (nodeUnpacker.UnpackerID.Type) {
					case UnpackerIDType.Standard:
						nodeUnpacker.UnpackerID.StandardUnpackerID = checked((StandardUnpackerID)xmfReader.ReadVLQ());
						break;
					case UnpackerIDType.MMAManufacturer:
						var firstByte = xmfReader.ReadByte();
						xmfReader.BaseStream.Seek(-1, SeekOrigin.Current);
						nodeUnpacker.UnpackerID.MMAManufacturerID = xmfReader.ReadBytes(firstByte == 0 ? 3 : 1);
						nodeUnpacker.UnpackerID.MMAManufacturerUnpackerType = xmfReader.ReadVLQ();
						break;
					case UnpackerIDType.Registered:
						nodeUnpacker.UnpackerID.RegisteredUnpackerID = xmfReader.ReadVLQ();
						break;
					case UnpackerIDType.NonRegistered:
						xmfReader.ReadVLQ(); // 16-byte GUID
						break;
					default:
						throw new InvalidDataException("Unsupported node unpacker ID type " + nodeUnpacker.UnpackerID.Type);
				}

				nodeUnpacker.DecodedSize = xmfReader.ReadVLQ();

				nodeUnpackers.Add(nodeUnpacker);
			}
			node.Unpackers = nodeUnpackers.ToArray();

			// node contents (at long last!)

			xmfReader.BaseStream.Seek(checked((long)(node.HeaderStart + node.HeaderLength)), SeekOrigin.Begin);

			node.Reference = new ReferenceType {
				Type = checked((ReferenceTypeID)xmfReader.ReadVLQ()),
			};

			switch (node.Reference.Type) {
				case ReferenceTypeID.InLineResource:
					node.Reference.ReferenceOffset = checked((ulong)xmfReader.BaseStream.Position);
					break;
				case ReferenceTypeID.InFileResource:
				case ReferenceTypeID.InFileNode:
					node.Reference.ReferenceOffset = xmfReader.ReadVLQ();
					break;
				case ReferenceTypeID.ExternalResourceFile:
				case ReferenceTypeID.ExternalXmfResource:
					node.Reference.ReferenceUri = new Uri(xmfReader.ReadXString());
					break;
				default:
					throw new InvalidDataException("Unsupported reference type ID " + node.Reference.Type);
			}

			if (node.ContainedItems == 0) {
				// file node
			} else {
				// folder node
				switch (node.Reference.Type) {
					case ReferenceTypeID.InLineResource:
					case ReferenceTypeID.InFileResource:
						ulong nodeOffset = node.Reference.ReferenceOffset;
						node.Children = new Node[node.ContainedItems];
						for (int i = 0; i < node.Children.Length; ++i) {
							stream.Seek(checked((long)nodeOffset), SeekOrigin.Begin);
							node.Children[i] = Node.FromStream(stream);
							nodeOffset += node.Children[i].Length;
						}
						break;
					default:
						throw new InvalidDataException("Unsupported node reference type for folder nodes: " + node.Reference.Type);
				}
			}

			return node;

		}

		public byte[] GetFileData(Stream stream) {

			if (this.Children != null) {
				throw new InvalidOperationException("This is a folder node, not a file node.");
			}


			byte[] data = null;

			switch (Reference.Type) {
				case ReferenceTypeID.InLineResource:
				case ReferenceTypeID.InFileResource:

					stream.Seek(checked((long)Reference.ReferenceOffset), SeekOrigin.Begin);

					if (Unpackers.Length == 0 && Reference.Type == ReferenceTypeID.InLineResource) {
						data = new BinaryReader(stream).ReadBytes(checked((int)(this.Length - ((ulong)stream.Position - this.HeaderStart))));
					} else if (Unpackers.Length == 0 && Reference.Type == ReferenceTypeID.InFileResource) {

						ResourceFormatID? resourceFormat = null;
						try {
							foreach (var item in this.MetaData) {
								if (item.FieldSpecifier == FieldSpecifier.ResourceFormat) {
									var resourceFormatReader = new BinaryReader(new MemoryStream(item.UniversalContentsData));
									if (resourceFormatReader.ReadByte() == 0) {
										resourceFormat = checked((ResourceFormatID)resourceFormatReader.ReadVLQ());
									}
								}
							}
						} catch {
							throw new InvalidDataException("Could not retrieve the resource format for the node.");
						}

						if (!resourceFormat.HasValue) {
							throw new InvalidDataException("Could not retrieve the resource format for the node.");
						}

						switch (resourceFormat.Value) {
							case ResourceFormatID.StandardMidiFileType0:
							case ResourceFormatID.StandardMidiFileType1:
								using (var smfData = new MemoryStream()) {
									
									var smfWriter = new BinaryWriter(smfData);
									var smfReader = new BinaryReader(stream);
									
									var chunkType = Encoding.ASCII.GetString(smfReader.ReadBytes(4));
									if (chunkType != "MThd") throw new InvalidDataException("Standard MIDI file does not start with correct header chunk type ('" + chunkType + "')");
									
									var chunkLength = smfReader.ReadBigEndianUInt32();
									var smfFormat = smfReader.ReadBigEndianUInt16();
									var trackCount = smfReader.ReadBigEndianUInt16();
									var divisions = smfReader.ReadBigEndianUInt16();

									smfWriter.Write(Encoding.ASCII.GetBytes(chunkType));
									smfWriter.WriteBigEndianValue(chunkLength);
									smfWriter.WriteBigEndianValue(smfFormat);
									smfWriter.WriteBigEndianValue(trackCount);
									smfWriter.WriteBigEndianValue(divisions);

									// in case there are any extra bytes...
									smfWriter.Write(smfReader.ReadBytes(checked((int)(chunkLength - 6))));

									for (int trackNumber = 0; trackNumber < trackCount; ++trackNumber) {
										chunkType = Encoding.ASCII.GetString(smfReader.ReadBytes(4));
										if (chunkType != "MTrk") throw new InvalidDataException("Standard MIDI file track does not start with correct track chunk type ('" + chunkType + "')");
										chunkLength = smfReader.ReadBigEndianUInt32();

										smfWriter.Write(Encoding.ASCII.GetBytes(chunkType));
										smfWriter.WriteBigEndianValue(chunkLength);
										
										smfWriter.Write(smfReader.ReadBytes(checked((int)chunkLength)));
									}

									data = smfData.ToArray();
								}
								break;
							case ResourceFormatID.DownloadableSoundsLevel1:
							case ResourceFormatID.DownloadableSoundsLevel2:
							case ResourceFormatID.DownloadableSoundsLevel2_1:
							case ResourceFormatID.MobileDownloadableSoundsInstrumentFile:
								using (var riffData = new MemoryStream()) {

									var riffWriter = new BinaryWriter(riffData);
									var riffReader = new BinaryReader(stream);

									var riffHeader = Encoding.ASCII.GetString(riffReader.ReadBytes(4));
									if (riffHeader != "RIFF") throw new InvalidDataException("Downloadable Sounds file does not start with correct RIFF header ('" + riffHeader + "')");

									var riffLength = riffReader.ReadUInt32();

									riffWriter.Write(Encoding.ASCII.GetBytes(riffHeader));
									riffWriter.Write(riffLength);
									riffWriter.Write(riffReader.ReadBytes((checked((int)riffLength) + 1) & ~1));

									data = riffData.ToArray();
								}
								break;
							default:
								throw new InvalidDataException("Unsupported in-file resource format '" + resourceFormat.Value.ToString() + "'.");
						}
					} else if (Unpackers.Length != 1) {
						throw new InvalidDataException("There are " + Unpackers.Length + " unpackers for this file node.");
					} else {
						switch (Unpackers[0].UnpackerID.Type) {
							case UnpackerIDType.Standard:
								Stream decodeStream = null;
								switch (Unpackers[0].UnpackerID.StandardUnpackerID) {
									case StandardUnpackerID.NoUnpacker:
										decodeStream = stream;
										break;
									case StandardUnpackerID.Zlib:
										decodeStream = new ZlibStream(stream, CompressionMode.Decompress, true);
										break;
									default:
										throw new InvalidDataException("Unsupported standard unpacker " + Unpackers[0].UnpackerID.StandardUnpackerID);
								}
								data = new BinaryReader(decodeStream).ReadBytes(checked((int)Unpackers[0].DecodedSize));
								break;
							default:
								throw new InvalidDataException("Unsupported unpacker type " + Unpackers[0].UnpackerID.Type);
						}
					}
					break;
				default:
					throw new InvalidDataException("Unsupported node reference type for file nodes: " + Reference.Type);
			}

			return data;

		}

	}

	struct Xmf {

		public string FileID;
		public string Version;

		uint FileTypeID;
		uint FileTypeRevisionID;

		public Node RootNode;

		public static Xmf FromStream(Stream stream) {

			var xmf = new Xmf();

			var xmfReader = new BinaryReader(stream);

			// check it's XMF
			xmf.FileID = Encoding.ASCII.GetString(xmfReader.ReadBytes(4));
			if (xmf.FileID != "XMF_") throw new InvalidDataException("Not an XMF file");

			// Get the version
			xmf.Version = Encoding.ASCII.GetString(xmfReader.ReadBytes(4));

			// v2.0 files have extra file type ID and file type revision IDs
			xmf.FileTypeID = 0;
			xmf.FileTypeRevisionID = 0;

			if (decimal.Parse(xmf.Version, CultureInfo.InvariantCulture) >= 2.00m) {
				xmf.FileTypeID = xmfReader.ReadBigEndianUInt32();
				xmf.FileTypeRevisionID = xmfReader.ReadBigEndianUInt32();
			}

			// total length
			ulong fileLength = xmfReader.ReadVLQ();

			// read the MetaDataTypesTable if present
			ulong metaDataTypesTableSize = xmfReader.ReadVLQ();

			if (metaDataTypesTableSize > 0) {

				ulong metaDataTypesTableNumberOfEntries = xmfReader.ReadVLQ();
				var metaDataTypesTable = new MetaDataType[checked((int)metaDataTypesTableNumberOfEntries)];

				for (int i = 0; i < metaDataTypesTable.Length; ++i) {
					metaDataTypesTable[i] = new MetaDataType {
						MetaDataTypeID = xmfReader.ReadVLQ(),
						StringFormatTypeID = checked((StringFormatTypeID)xmfReader.ReadVLQ()),
						LangCountrySpec = xmfReader.ReadXString(),
					};
				}

			}

			// where is the tree start and end?
			ulong treeStart = xmfReader.ReadVLQ();
			ulong treeEnd = xmfReader.ReadVLQ();

			// seek to the start of the tree
			xmfReader.BaseStream.Seek((long)treeStart, SeekOrigin.Begin);

			// read all nodes
			xmf.RootNode = Node.FromStream(xmfReader.BaseStream);

			return xmf;
		}


	}

}
