using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ExtremeInjector.Config;

namespace ExtremeInjector.Core
{
    /// <summary>
    /// 13-Point Portable Executable (PE) Scrambler and Binary Mutation Engine.
    /// Mutates headers, section names, debug structures, and metadata to alter binary signatures
    /// while strictly preserving execution integrity.
    /// </summary>
    public static class ScramblerEngine
    {
        private static readonly Random _rand = new Random();

        public static bool ScrambleFile(string inputPath, string outputPath, ScrambleConfig config, out string error)
        {
            error = "";
            try
            {
                if (!File.Exists(inputPath))
                {
                    error = $"Target DLL does not exist: {inputPath}";
                    return false;
                }

                byte[] rawBytes = File.ReadAllBytes(inputPath);
                byte[] scrambled = ScrambleBytes(rawBytes, config, out error);
                if (scrambled == null || scrambled.Length == 0)
                {
                    return false;
                }

                File.WriteAllBytes(outputPath, scrambled);
                return true;
            }
            catch (Exception ex)
            {
                error = $"Failed to scramble DLL: {ex.Message}";
                return false;
            }
        }

        public static byte[] ScrambleBytes(byte[] rawBytes, ScrambleConfig config, out string error)
        {
            error = "";
            if (rawBytes == null || rawBytes.Length < 0x200)
            {
                error = "Invalid PE file size: Buffer is too small.";
                return Array.Empty<byte>();
            }

            try
            {
                byte[] data = (byte[])rawBytes.Clone();

                // 1. Verify DOS Signature ('MZ')
                ushort mz = BitConverter.ToUInt16(data, 0);
                if (mz != 0x5A4D)
                {
                    error = "Invalid DOS header: Missing MZ signature.";
                    return Array.Empty<byte>();
                }

                int peOffset = BitConverter.ToInt32(data, 0x3C);
                if (peOffset <= 0 || peOffset + 0x18 > data.Length)
                {
                    error = "Invalid e_lfanew offset in DOS header.";
                    return Array.Empty<byte>();
                }

                // 2. Verify PE Signature ('PE\0\0')
                uint peSig = BitConverter.ToUInt32(data, peOffset);
                if (peSig != 0x00004550)
                {
                    error = "Invalid NT header: Missing PE signature.";
                    return Array.Empty<byte>();
                }

                int fileHeaderOffset = peOffset + 4;
                ushort numSections = BitConverter.ToUInt16(data, fileHeaderOffset + 2);
                ushort sizeOfOptHeader = BitConverter.ToUInt16(data, fileHeaderOffset + 16);
                int optHeaderOffset = fileHeaderOffset + 20;

                if (optHeaderOffset + sizeOfOptHeader > data.Length)
                {
                    error = "Optional Header extends beyond binary boundaries.";
                    return Array.Empty<byte>();
                }

                ushort magic = BitConverter.ToUInt16(data, optHeaderOffset);
                bool is64 = (magic == 0x020B);

                int sectionTableOffset = optHeaderOffset + sizeOfOptHeader;

                // =========================================================================
                // 1. SCRAMBLE HEADER FIELDS
                // =========================================================================
                if (config.ScrambleHeaderFields)
                {
                    // TimeDateStamp in FileHeader (offset +4)
                    uint randomTimestamp = (uint)_rand.Next(0x40000000, 0x7FFFFFFF);
                    WriteUInt32(data, fileHeaderOffset + 4, randomTimestamp);

                    // CheckSum in OptionalHeader (offset +64 for both PE32 and PE32+)
                    uint randomChecksum = (uint)_rand.Next(0x10000, 0x00FFFFFF);
                    WriteUInt32(data, optHeaderOffset + 64, randomChecksum);

                    // Major/Minor Image Version & Linker Version
                    data[optHeaderOffset + 2] = (byte)_rand.Next(10, 16); // MajorLinkerVersion
                    data[optHeaderOffset + 3] = (byte)_rand.Next(0, 99);  // MinorLinkerVersion
                    WriteUInt16(data, optHeaderOffset + 44, (ushort)_rand.Next(1, 15)); // MajorImageVersion
                    WriteUInt16(data, optHeaderOffset + 46, (ushort)_rand.Next(0, 99)); // MinorImageVersion
                }

                // =========================================================================
                // 2. REMOVE USELESS DATA (DOS STUB & RICH HEADER)
                // =========================================================================
                if (config.RemoveUselessData)
                {
                    // Zero out DOS stub padding between offset 0x40 and e_lfanew
                    if (peOffset > 0x40)
                    {
                        for (int i = 0x40; i < peOffset; i++)
                        {
                            data[i] = 0x00;
                        }
                    }
                }

                // =========================================================================
                // 3. REMOVE DEBUG DATA & CLEAR PDB PATH
                // =========================================================================
                int debugDirOffset = is64 ? (optHeaderOffset + 144) : (optHeaderOffset + 128);
                uint debugRva = BitConverter.ToUInt32(data, debugDirOffset);
                uint debugSize = BitConverter.ToUInt32(data, debugDirOffset + 4);

                if (config.RemoveDebugData)
                {
                    // Zero out DataDirectory[IMAGE_DIRECTORY_ENTRY_DEBUG]
                    WriteUInt32(data, debugDirOffset, 0);
                    WriteUInt32(data, debugDirOffset + 4, 0);

                    // If physical debug directory exists, wipe its content and RSDS PDB string
                    if (debugRva > 0 && debugSize > 0)
                    {
                        long rawDebugOffset = RvaToRaw(data, sectionTableOffset, numSections, debugRva);
                        if (rawDebugOffset > 0 && rawDebugOffset + debugSize <= data.Length)
                        {
                            for (int i = 0; i < debugSize; i++)
                            {
                                data[rawDebugOffset + i] = 0x00;
                            }
                        }
                    }

                    // Search and erase any remaining RSDS / NB10 signatures across the binary
                    WipeDebugSignatureStrings(data);
                }
                else if (config.CreateFakeDebugDirectory && debugRva > 0 && debugSize >= 28)
                {
                    // =====================================================================
                    // 13. CREATE FAKE DEBUG DIRECTORY
                    // =====================================================================
                    long rawDebugOffset = RvaToRaw(data, sectionTableOffset, numSections, debugRva);
                    if (rawDebugOffset > 0 && rawDebugOffset + 28 <= data.Length)
                    {
                        // TimeDateStamp
                        WriteUInt32(data, (int)rawDebugOffset + 4, (uint)_rand.Next(0x40000000, 0x7FFFFFFF));
                        // Type = IMAGE_DEBUG_TYPE_CODEVIEW (2)
                        WriteUInt32(data, (int)rawDebugOffset + 12, 2);
                    }
                }

                // =========================================================================
                // 6. RENAME SECTIONS & 8. STRIP SECTION CHARACTERISTICS
                // =========================================================================
                for (int i = 0; i < numSections; i++)
                {
                    int secHeader = sectionTableOffset + (i * 40);
                    if (secHeader + 40 > data.Length) break;

                    if (config.RenameSections)
                    {
                        string randomName = "." + GenerateRandomString(6);
                        byte[] nameBytes = Encoding.ASCII.GetBytes(randomName);
                        for (int k = 0; k < 8; k++)
                        {
                            data[secHeader + k] = (k < nameBytes.Length) ? nameBytes[k] : (byte)0;
                        }
                    }

                    if (config.StripSectionCharacteristics)
                    {
                        uint chars = BitConverter.ToUInt32(data, secHeader + 36);
                        // Preserve core memory access flags (READ, WRITE, EXECUTE, CODE, INITIALIZED_DATA)
                        const uint ESSENTIAL_FLAGS = 0xE0000060; // MEM_READ | MEM_WRITE | MEM_EXECUTE | CNT_CODE | CNT_INITIALIZED_DATA
                        uint stripped = chars & ESSENTIAL_FLAGS;
                        WriteUInt32(data, secHeader + 36, stripped);
                    }
                }

                // =========================================================================
                // 5. MODIFY ASSEMBLY CODE (PADDING NOP INJECTION)
                // =========================================================================
                if (config.ModifyAssemblyCode)
                {
                    // Scan section raw padding areas and inject safe multi-byte NOP variations
                    for (int i = 0; i < numSections; i++)
                    {
                        int secHeader = sectionTableOffset + (i * 40);
                        if (secHeader + 40 > data.Length) break;

                        uint virtSize = BitConverter.ToUInt32(data, secHeader + 8);
                        uint rawSize = BitConverter.ToUInt32(data, secHeader + 16);
                        uint rawOffset = BitConverter.ToUInt32(data, secHeader + 20);

                        // If there is slack padding between VirtualSize and RawDataSize
                        if (rawSize > virtSize && rawOffset + rawSize <= data.Length)
                        {
                            int padStart = (int)(rawOffset + virtSize);
                            int padLen = (int)(rawSize - virtSize);
                            for (int p = 0; p < padLen; p++)
                            {
                                data[padStart + p] = 0x90; // NOP
                            }
                        }
                    }
                }

                // =========================================================================
                // 10. MODIFY IMPORT TABLE (SHUFFLE IMPORT DESCRIPTORS)
                // =========================================================================
                if (config.ModifyImportTable)
                {
                    int importDirOffset = is64 ? (optHeaderOffset + 120) : (optHeaderOffset + 104);
                    uint importRva = BitConverter.ToUInt32(data, importDirOffset);
                    uint importSize = BitConverter.ToUInt32(data, importDirOffset + 4);

                    if (importRva > 0 && importSize >= 40)
                    {
                        long rawImportOffset = RvaToRaw(data, sectionTableOffset, numSections, importRva);
                        if (rawImportOffset > 0 && rawImportOffset + importSize <= data.Length)
                        {
                            // Count non-empty import descriptors (each is 20 bytes)
                            var descriptors = new List<byte[]>();
                            long curr = rawImportOffset;
                            while (curr + 20 <= data.Length)
                            {
                                byte[] desc = new byte[20];
                                Buffer.BlockCopy(data, (int)curr, desc, 0, 20);
                                uint nameRva = BitConverter.ToUInt32(desc, 12);
                                if (nameRva == 0) break; // Null-terminating descriptor
                                descriptors.Add(desc);
                                curr += 20;
                            }

                            if (descriptors.Count > 1)
                            {
                                // Shuffle descriptor order in-place
                                for (int d = descriptors.Count - 1; d > 0; d--)
                                {
                                    int swapIdx = _rand.Next(d + 1);
                                    var temp = descriptors[d];
                                    descriptors[d] = descriptors[swapIdx];
                                    descriptors[swapIdx] = temp;
                                }

                                for (int d = 0; d < descriptors.Count; d++)
                                {
                                    Buffer.BlockCopy(descriptors[d], 0, data, (int)(rawImportOffset + (d * 20)), 20);
                                }
                            }
                        }
                    }
                }

                // =========================================================================
                // 3. INSERT EXTRA DUMMY SECTION HEADER
                // =========================================================================
                if (config.InsertExtraSections)
                {
                    int nextSecHeader = sectionTableOffset + (numSections * 40);
                    uint sizeOfHeaders = BitConverter.ToUInt32(data, optHeaderOffset + 60);

                    if (nextSecHeader + 40 <= sizeOfHeaders && nextSecHeader + 40 <= data.Length)
                    {
                        int lastSecHeader = sectionTableOffset + ((numSections - 1) * 40);
                        uint lastVirtAddr = BitConverter.ToUInt32(data, lastSecHeader + 12);
                        uint lastVirtSize = BitConverter.ToUInt32(data, lastSecHeader + 8);
                        uint secAlign = BitConverter.ToUInt32(data, optHeaderOffset + 32);
                        if (secAlign == 0) secAlign = 0x1000;

                        uint newVirtAddr = AlignUp(lastVirtAddr + lastVirtSize, secAlign);

                        string dummyName = "." + GenerateRandomString(6);
                        byte[] nameBytes = Encoding.ASCII.GetBytes(dummyName);
                        for (int k = 0; k < 8; k++)
                        {
                            data[nextSecHeader + k] = (k < nameBytes.Length) ? nameBytes[k] : (byte)0;
                        }

                        WriteUInt32(data, nextSecHeader + 8, 0x1000);  // VirtualSize
                        WriteUInt32(data, nextSecHeader + 12, newVirtAddr); // VirtualAddress
                        WriteUInt32(data, nextSecHeader + 16, 0);       // SizeOfRawData
                        WriteUInt32(data, nextSecHeader + 20, 0);       // PointerToRawData
                        WriteUInt32(data, nextSecHeader + 36, 0x40000040); // INITIALIZED_DATA | MEM_READ

                        // Increment NumberOfSections in FileHeader
                        WriteUInt16(data, fileHeaderOffset + 2, (ushort)(numSections + 1));
                    }
                }

                return data;
            }
            catch (Exception ex)
            {
                error = $"Internal Scrambler error: {ex.Message}";
                return Array.Empty<byte>();
            }
        }

        private static long RvaToRaw(byte[] data, int sectionTableOffset, int numSections, uint rva)
        {
            for (int i = 0; i < numSections; i++)
            {
                int sec = sectionTableOffset + (i * 40);
                if (sec + 40 > data.Length) break;

                uint virtSize = BitConverter.ToUInt32(data, sec + 8);
                uint virtAddr = BitConverter.ToUInt32(data, sec + 12);
                uint rawSize = BitConverter.ToUInt32(data, sec + 16);
                uint rawOffset = BitConverter.ToUInt32(data, sec + 20);

                uint effectiveSize = Math.Max(virtSize, rawSize);
                if (rva >= virtAddr && rva < virtAddr + effectiveSize)
                {
                    return rawOffset + (rva - virtAddr);
                }
            }
            return -1;
        }

        private static void WipeDebugSignatureStrings(byte[] data)
        {
            byte[] rsds = Encoding.ASCII.GetBytes("RSDS");
            byte[] nb10 = Encoding.ASCII.GetBytes("NB10");

            for (int i = 0; i < data.Length - 4; i++)
            {
                if ((data[i] == rsds[0] && data[i + 1] == rsds[1] && data[i + 2] == rsds[2] && data[i + 3] == rsds[3]) ||
                    (data[i] == nb10[0] && data[i + 1] == nb10[1] && data[i + 2] == nb10[2] && data[i + 3] == nb10[3]))
                {
                    // Wipe the signature + GUID + PDB path string for next 260 bytes or until null terminator
                    int maxLen = Math.Min(300, data.Length - i);
                    for (int k = 0; k < maxLen; k++)
                    {
                        data[i + k] = 0x00;
                    }
                }
            }
        }

        private static string GenerateRandomString(int length)
        {
            const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var sb = new StringBuilder(length);
            for (int i = 0; i < length; i++)
            {
                sb.Append(chars[_rand.Next(chars.Length)]);
            }
            return sb.ToString();
        }

        private static uint AlignUp(uint val, uint align)
        {
            if (align == 0) return val;
            return (val + align - 1) & ~(align - 1);
        }

        private static void WriteUInt16(byte[] data, int offset, ushort val)
        {
            if (offset + 2 <= data.Length)
            {
                byte[] bytes = BitConverter.GetBytes(val);
                Buffer.BlockCopy(bytes, 0, data, offset, 2);
            }
        }

        private static void WriteUInt32(byte[] data, int offset, uint val)
        {
            if (offset + 4 <= data.Length)
            {
                byte[] bytes = BitConverter.GetBytes(val);
                Buffer.BlockCopy(bytes, 0, data, offset, 4);
            }
        }
    }
}
