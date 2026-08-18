using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace ExtremeInjector.Core
{
    /// <summary>
    /// Accurately resolves module base addresses and exported function pointers in both native (x64) and WOW64 (x86) target processes.
    /// Prevents 64-bit to 32-bit pointer truncation crashes.
    /// </summary>
    public static class RemoteExportResolver
    {
        public static IntPtr GetProcAddress(int processId, bool isTarget64, string moduleName, string functionName)
        {
            // Case 1: Same bitness as the injector process
            if (isTarget64 == Environment.Is64BitProcess)
            {
                IntPtr hMod = NativeMethods.GetModuleHandle(moduleName);
                if (hMod != IntPtr.Zero)
                {
                    IntPtr pFunc = NativeMethods.GetProcAddress(hMod, functionName);
                    if (pFunc != IntPtr.Zero)
                        return pFunc;
                }
            }

            // Case 2: Cross-bitness (e.g. 64-bit injector targeting 32-bit WoW64 process)
            IntPtr remoteBase = GetRemoteModuleBase(processId, moduleName);
            if (remoteBase == IntPtr.Zero)
                return IntPtr.Zero;

            string systemDir = Environment.GetFolderPath(
                isTarget64 ? Environment.SpecialFolder.System : Environment.SpecialFolder.SystemX86
            );
            string diskDllPath = Path.Combine(systemDir, moduleName);

            if (!File.Exists(diskDllPath))
            {
                // Fallback to standard System32
                diskDllPath = Path.Combine(Environment.SystemDirectory, moduleName);
            }

            if (!File.Exists(diskDllPath))
                return IntPtr.Zero;

            uint rva = GetExportRva(diskDllPath, functionName);
            if (rva == 0)
                return IntPtr.Zero;

            return (IntPtr)(remoteBase.ToInt64() + rva);
        }

        public static IntPtr GetRemoteModuleBase(int processId, string moduleName)
        {
            const uint TH32CS_SNAPMODULE = 0x00000008;
            const uint TH32CS_SNAPMODULE32 = 0x00000010;

            IntPtr hSnap = NativeMethods.CreateToolhelp32Snapshot(TH32CS_SNAPMODULE | TH32CS_SNAPMODULE32, (uint)processId);
            if (hSnap == IntPtr.Zero || hSnap == (IntPtr)(-1))
                return IntPtr.Zero;

            try
            {
                var me = new NativeMethods.MODULEENTRY32();
                me.dwSize = (uint)Marshal.SizeOf(typeof(NativeMethods.MODULEENTRY32));

                if (NativeMethods.Module32First(hSnap, ref me))
                {
                    do
                    {
                        if (string.Equals(me.szModule, moduleName, StringComparison.OrdinalIgnoreCase))
                        {
                            return me.modBaseAddr;
                        }
                    } while (NativeMethods.Module32Next(hSnap, ref me));
                }
            }
            finally
            {
                NativeMethods.CloseHandle(hSnap);
            }

            return IntPtr.Zero;
        }

        public static uint GetExportRva(string filePath, string functionName)
        {
            try
            {
                byte[] bytes = File.ReadAllBytes(filePath);
                if (bytes.Length < 0x40) return 0;

                ushort mz = BitConverter.ToUInt16(bytes, 0);
                if (mz != 0x5A4D) return 0;

                int peOffset = BitConverter.ToInt32(bytes, 0x3C);
                if (peOffset + 0x18 > bytes.Length) return 0;

                uint peSig = BitConverter.ToUInt32(bytes, peOffset);
                if (peSig != 0x00004550) return 0;

                ushort magic = BitConverter.ToUInt16(bytes, peOffset + 24);
                bool is64 = (magic == 0x20B);

                int optionalHeaderOffset = peOffset + 24;
                int exportDirOffset = is64 ? (optionalHeaderOffset + 112) : (optionalHeaderOffset + 96);

                if (exportDirOffset + 8 > bytes.Length) return 0;

                uint exportRva = BitConverter.ToUInt32(bytes, exportDirOffset);
                uint exportSize = BitConverter.ToUInt32(bytes, exportDirOffset + 4);

                if (exportRva == 0 || exportSize == 0) return 0;

                int exportFileOffset = (int)RvaToFileOffset(bytes, peOffset, exportRva);
                if (exportFileOffset == 0 || exportFileOffset + 40 > bytes.Length) return 0;

                uint numberOfFunctions = BitConverter.ToUInt32(bytes, exportFileOffset + 20);
                uint numberOfNames = BitConverter.ToUInt32(bytes, exportFileOffset + 24);
                uint addressOfFunctions = BitConverter.ToUInt32(bytes, exportFileOffset + 28);
                uint addressOfNames = BitConverter.ToUInt32(bytes, exportFileOffset + 32);
                uint addressOfNameOrdinals = BitConverter.ToUInt32(bytes, exportFileOffset + 36);

                int functionsFileOffset = (int)RvaToFileOffset(bytes, peOffset, addressOfFunctions);
                int namesFileOffset = (int)RvaToFileOffset(bytes, peOffset, addressOfNames);
                int ordinalsFileOffset = (int)RvaToFileOffset(bytes, peOffset, addressOfNameOrdinals);

                for (uint i = 0; i < numberOfNames; i++)
                {
                    if (namesFileOffset + (i * 4) + 4 > bytes.Length) break;
                    uint nameRva = BitConverter.ToUInt32(bytes, namesFileOffset + (int)(i * 4));
                    int nameFileOffset = (int)RvaToFileOffset(bytes, peOffset, nameRva);

                    if (nameFileOffset > 0 && nameFileOffset < bytes.Length)
                    {
                        string name = ReadNullTerminatedAscii(bytes, nameFileOffset);
                        if (string.Equals(name, functionName, StringComparison.Ordinal))
                        {
                            ushort ordinalIndex = BitConverter.ToUInt16(bytes, ordinalsFileOffset + (int)(i * 2));
                            if (functionsFileOffset + (ordinalIndex * 4) + 4 <= bytes.Length)
                            {
                                return BitConverter.ToUInt32(bytes, functionsFileOffset + (ordinalIndex * 4));
                            }
                        }
                    }
                }
            }
            catch
            {
                return 0;
            }

            return 0;
        }

        private static uint RvaToFileOffset(byte[] bytes, int peOffset, uint rva)
        {
            ushort numberOfSections = BitConverter.ToUInt16(bytes, peOffset + 6);
            ushort sizeOfOptionalHeader = BitConverter.ToUInt16(bytes, peOffset + 20);
            int sectionTableOffset = peOffset + 24 + sizeOfOptionalHeader;

            for (int i = 0; i < numberOfSections; i++)
            {
                int secOffset = sectionTableOffset + (i * 40);
                if (secOffset + 40 > bytes.Length) break;

                uint virtualSize = BitConverter.ToUInt32(bytes, secOffset + 8);
                uint virtualAddress = BitConverter.ToUInt32(bytes, secOffset + 12);
                uint sizeOfRawData = BitConverter.ToUInt32(bytes, secOffset + 16);
                uint pointerToRawData = BitConverter.ToUInt32(bytes, secOffset + 20);

                if (rva >= virtualAddress && rva < virtualAddress + Math.Max(virtualSize, sizeOfRawData))
                {
                    return (rva - virtualAddress) + pointerToRawData;
                }
            }

            return 0;
        }

        private static string ReadNullTerminatedAscii(byte[] bytes, int offset)
        {
            int end = offset;
            while (end < bytes.Length && bytes[end] != 0)
            {
                end++;
            }
            return Encoding.ASCII.GetString(bytes, offset, end - offset);
        }
    }
}
