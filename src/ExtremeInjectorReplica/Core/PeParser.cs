using System;
using System.IO;
using System.Runtime.InteropServices;

namespace ExtremeInjector.Core
{
    public class PeParser
    {
        public bool Is64Bit { get; private set; }
        public uint SizeOfImage { get; private set; }
        public uint SizeOfHeaders { get; private set; }
        public ulong ImageBase { get; private set; }
        public uint AddressOfEntryPoint { get; private set; }
        public SectionHeader[] Sections { get; private set; } = Array.Empty<SectionHeader>();
        public byte[] RawBytes { get; private set; }

        public PeParser(string filePath)
        {
            RawBytes = File.ReadAllBytes(filePath);
            Parse();
        }

        public PeParser(byte[] rawBytes)
        {
            RawBytes = rawBytes;
            Parse();
        }

        private void Parse()
        {
            if (RawBytes.Length < 0x40)
                throw new InvalidOperationException("Invalid PE file size.");

            ushort mz = BitConverter.ToUInt16(RawBytes, 0);
            if (mz != 0x5A4D) // 'MZ'
                throw new InvalidOperationException("Invalid DOS signature.");

            int peOffset = BitConverter.ToInt32(RawBytes, 0x3C);
            if (peOffset + 0x18 > RawBytes.Length)
                throw new InvalidOperationException("Invalid NT header offset.");

            uint peSig = BitConverter.ToUInt32(RawBytes, peOffset);
            if (peSig != 0x00004550) // 'PE\0\0'
                throw new InvalidOperationException("Invalid PE signature.");

            ushort machine = BitConverter.ToUInt16(RawBytes, peOffset + 4);
            ushort numberOfSections = BitConverter.ToUInt16(RawBytes, peOffset + 6);
            ushort sizeOfOptionalHeader = BitConverter.ToUInt16(RawBytes, peOffset + 20);

            int optionalHeaderOffset = peOffset + 24;
            ushort magic = BitConverter.ToUInt16(RawBytes, optionalHeaderOffset);

            if (magic == 0x20B) // PE32+ (64-bit)
            {
                Is64Bit = true;
                AddressOfEntryPoint = BitConverter.ToUInt32(RawBytes, optionalHeaderOffset + 16);
                ImageBase = BitConverter.ToUInt64(RawBytes, optionalHeaderOffset + 24);
                SizeOfImage = BitConverter.ToUInt32(RawBytes, optionalHeaderOffset + 56);
                SizeOfHeaders = BitConverter.ToUInt32(RawBytes, optionalHeaderOffset + 60);
            }
            else if (magic == 0x10B) // PE32 (32-bit)
            {
                Is64Bit = false;
                AddressOfEntryPoint = BitConverter.ToUInt32(RawBytes, optionalHeaderOffset + 16);
                ImageBase = BitConverter.ToUInt32(RawBytes, optionalHeaderOffset + 28);
                SizeOfImage = BitConverter.ToUInt32(RawBytes, optionalHeaderOffset + 56);
                SizeOfHeaders = BitConverter.ToUInt32(RawBytes, optionalHeaderOffset + 60);
            }
            else
            {
                throw new InvalidOperationException("Unsupported PE architecture magic.");
            }

            int sectionTableOffset = optionalHeaderOffset + sizeOfOptionalHeader;
            Sections = new SectionHeader[numberOfSections];

            for (int i = 0; i < numberOfSections; i++)
            {
                int secOffset = sectionTableOffset + (i * 40);
                string name = System.Text.Encoding.ASCII.GetString(RawBytes, secOffset, 8).TrimEnd('\0');
                uint virtualSize = BitConverter.ToUInt32(RawBytes, secOffset + 8);
                uint virtualAddress = BitConverter.ToUInt32(RawBytes, secOffset + 12);
                uint sizeOfRawData = BitConverter.ToUInt32(RawBytes, secOffset + 16);
                uint pointerToRawData = BitConverter.ToUInt32(RawBytes, secOffset + 20);
                uint characteristics = BitConverter.ToUInt32(RawBytes, secOffset + 36);

                Sections[i] = new SectionHeader
                {
                    Name = name,
                    VirtualSize = virtualSize,
                    VirtualAddress = virtualAddress,
                    SizeOfRawData = sizeOfRawData,
                    PointerToRawData = pointerToRawData,
                    Characteristics = characteristics
                };
            }
        }
    }

    public class SectionHeader
    {
        public string Name { get; set; } = "";
        public uint VirtualSize { get; set; }
        public uint VirtualAddress { get; set; }
        public uint SizeOfRawData { get; set; }
        public uint PointerToRawData { get; set; }
        public uint Characteristics { get; set; }
    }
}
