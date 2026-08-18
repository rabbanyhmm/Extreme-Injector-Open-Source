using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using ExtremeInjector.Config;

namespace ExtremeInjector.Core
{
    public static class ManualMapInjector
    {
        // =========================================================================
        // Position-Independent Manual Mapping Shellcodes (x64 & x86)
        // Handlers for Relocations, Imports, TLS Callbacks, SEH, and DllMain
        // =========================================================================
        private static readonly byte[] X64Shellcode = new byte[] {
            0x48, 0x85, 0xC9, 0x0F, 0x84, 0x55, 0x02, 0x00, 0x00, 0x48, 0x8B, 0xC4, 0x48, 0x89, 0x48, 0x08, 0x41, 0x54, 0x48, 0x83, 0xEC, 0x60, 0x48, 0x89, 0x68, 0xE8, 0x4C, 0x8B, 0xE1, 0x48, 0x8B, 0x69, 0x10, 0x48, 0x89, 0x70, 0xE0, 0x48, 0x8B, 0x71, 0x18, 0x48, 0x89, 0x78, 0xD8, 0x4C, 0x8B, 0xDE, 0x4C, 0x89, 0x68, 0xD0, 0x4C, 0x89, 0x78, 0xC0, 0x4C, 0x63, 0x6E, 0x3C, 0x4C, 0x8B, 0x79, 0x08, 0x4C, 0x03, 0xEE, 0x48, 0x89, 0x58, 0xF0, 0x4C, 0x89, 0x70, 0xC8, 0x48, 0x8B, 0x01, 0x41, 0x8B, 0x7D, 0x28, 0x48, 0x03, 0xFE, 0x48, 0x89, 0x44, 0x24, 0x78, 0x48, 0x89, 0xAC, 0x24, 0x88, 0x00, 0x00, 0x00, 0x48, 0x89, 0xBC, 0x24, 0x80, 0x00, 0x00, 0x00, 0x4D, 0x2B, 0x5D, 0x30, 0x0F, 0x84, 0x85, 0x00, 0x00, 0x00, 0x41, 0x83, 0xBD, 0xB4, 0x00, 0x00, 0x00, 0x00, 0x74, 0x7B, 0x45, 0x8B, 0x95, 0xB0, 0x00, 0x00, 0x00, 0x4C, 0x03, 0xD6, 0x41, 0x83, 0x3A, 0x00, 0x74, 0x6B, 0xBB, 0x00, 0xF0, 0x00, 0x00, 0x41, 0xBE, 0x00, 0xA0, 0x00, 0x00, 0x0F, 0x1F, 0x80, 0x00, 0x00, 0x00, 0x00, 0x41, 0x8B, 0x42, 0x04, 0x49, 0x8D, 0x52, 0x08, 0x48, 0x83, 0xE8, 0x08, 0x48, 0xD1, 0xE8, 0x85, 0xC0, 0x74, 0x39, 0x44, 0x8B, 0xC8, 0x66, 0x66, 0x0F, 0x1F, 0x84, 0x00, 0x00, 0x00, 0x00, 0x00, 0x0F, 0xB7, 0x02, 0x44, 0x0F, 0xB7, 0xC0, 0x66, 0x23, 0xC3, 0x66, 0x41, 0x3B, 0xC6, 0x75, 0x12, 0x41, 0x8B, 0x0A, 0x41, 0x81, 0xE0, 0xFF, 0x0F, 0x00, 0x00, 0x4A, 0x8D, 0x04, 0x06, 0x4C, 0x01, 0x1C, 0x01, 0x48, 0x83, 0xC2, 0x02, 0x49, 0x83, 0xE9, 0x01, 0x75, 0xD4, 0x41, 0x8B, 0x42, 0x04, 0x4C, 0x03, 0xD0, 0x41, 0x83, 0x3A, 0x00, 0x75, 0xA7, 0x41, 0x83, 0xBD, 0x94, 0x00, 0x00, 0x00, 0x00, 0x0F, 0x84, 0x93, 0x00, 0x00, 0x00, 0x45, 0x8B, 0xB5, 0x90, 0x00, 0x00, 0x00, 0x4C, 0x03, 0xF6, 0x41, 0x83, 0x7E, 0x0C, 0x00, 0x0F, 0x84, 0x7E, 0x00, 0x00, 0x00, 0x4C, 0x8B, 0x64, 0x24, 0x78, 0x41, 0x8B, 0x4E, 0x0C, 0x48, 0x03, 0xCE, 0x41, 0xFF, 0xD4, 0x48, 0x8B, 0xE8, 0x48, 0x85, 0xC0, 0x74, 0x47, 0x41, 0x8B, 0x06, 0x41, 0x8B, 0x7E, 0x10, 0x48, 0x03, 0xFE, 0x48, 0x8D, 0x1C, 0x06, 0x85, 0xC0, 0x75, 0x03, 0x48, 0x8B, 0xDF, 0x48, 0x8B, 0x03, 0x48, 0x85, 0xC0, 0x74, 0x2A, 0x48, 0x8B, 0xCD, 0x48, 0x85, 0xC0, 0x79, 0x05, 0x0F, 0xB7, 0xD0, 0xEB, 0x07, 0x48, 0x8D, 0x56, 0x02, 0x48, 0x03, 0xD0, 0x41, 0xFF, 0xD7, 0x48, 0x83, 0xC3, 0x08, 0x48, 0x89, 0x07, 0x48, 0x83, 0xC7, 0x08, 0x48, 0x8B, 0x03, 0x48, 0x85, 0xC0, 0x75, 0xD6, 0x49, 0x83, 0xC6, 0x14, 0x41, 0x83, 0x7E, 0x0C, 0x00, 0x75, 0x9C, 0x4C, 0x8B, 0x64, 0x24, 0x70, 0x48, 0x8B, 0xBC, 0x24, 0x80, 0x00, 0x00, 0x00, 0x48, 0x8B, 0xAC, 0x24, 0x88, 0x00, 0x00, 0x00, 0x41, 0x83, 0xBD, 0xD4, 0x00, 0x00, 0x00, 0x00, 0x4C, 0x8B, 0x7C, 0x24, 0x28, 0x4C, 0x8B, 0x74, 0x24, 0x30, 0x74, 0x3C, 0x41, 0x8B, 0x85, 0xD0, 0x00, 0x00, 0x00, 0x48, 0x8B, 0x5C, 0x30, 0x18, 0x48, 0x85, 0xDB, 0x74, 0x2B, 0x48, 0x8B, 0x03, 0x48, 0x85, 0xC0, 0x74, 0x23, 0x66, 0x0F, 0x1F, 0x84, 0x00, 0x00, 0x00, 0x00, 0x00, 0x45, 0x33, 0xC0, 0xBA, 0x01, 0x00, 0x00, 0x00, 0x48, 0x8B, 0xCE, 0xFF, 0xD0, 0x48, 0x8B, 0x43, 0x08, 0x48, 0x8D, 0x5B, 0x08, 0x48, 0x85, 0xC0, 0x75, 0xE6, 0x41, 0x83, 0x7C, 0x24, 0x38, 0x00, 0x48, 0x8B, 0x5C, 0x24, 0x58, 0x74, 0x32, 0x41, 0x8B, 0x85, 0xA4, 0x00, 0x00, 0x00, 0x85, 0xC0, 0x74, 0x27, 0x48, 0x85, 0xED, 0x74, 0x22, 0x8B, 0xC8, 0x4C, 0x8B, 0xC6, 0x48, 0xB8, 0xAB, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0x48, 0xF7, 0xE1, 0x41, 0x8B, 0x8D, 0xA0, 0x00, 0x00, 0x00, 0x48, 0xC1, 0xEA, 0x03, 0x48, 0x03, 0xCE, 0xFF, 0xD5, 0x41, 0x83, 0x7D, 0x28, 0x00, 0x4C, 0x8B, 0x6C, 0x24, 0x38, 0x48, 0x8B, 0x6C, 0x24, 0x50, 0x74, 0x0F, 0x4D, 0x8B, 0x44, 0x24, 0x30, 0x48, 0x8B, 0xCE, 0x41, 0x8B, 0x54, 0x24, 0x28, 0xFF, 0xD7, 0x48, 0x8B, 0x7C, 0x24, 0x40, 0x49, 0x89, 0x74, 0x24, 0x20, 0x48, 0x8B, 0x74, 0x24, 0x48, 0x48, 0x83, 0xC4, 0x60, 0x41, 0x5C, 0xC3
        };

        private static readonly byte[] X86Shellcode = new byte[] {
            0x8B, 0x4C, 0x24, 0x04, 0x83, 0xEC, 0x14, 0x85, 0xC9, 0x0F, 0x84, 0x5F, 0x01, 0x00, 0x00, 0x8B, 0x41, 0x04, 0x8B, 0x11, 0x53, 0x55, 0x56, 0x57, 0x8B, 0x79, 0x08, 0x8B, 0xEF, 0x89, 0x44, 0x24, 0x14, 0x89, 0x54, 0x24, 0x18, 0x8B, 0x5F, 0x3C, 0x03, 0xDF, 0x89, 0x5C, 0x24, 0x1C, 0x8B, 0x43, 0x28, 0x03, 0xC7, 0x89, 0x44, 0x24, 0x20, 0x2B, 0x6B, 0x34, 0x74, 0x6B, 0x83, 0xBB, 0xA4, 0x00, 0x00, 0x00, 0x00, 0x74, 0x62, 0x8B, 0xB3, 0xA0, 0x00, 0x00, 0x00, 0x03, 0xF7, 0x83, 0x3E, 0x00, 0x74, 0x55, 0x8B, 0x5E, 0x04, 0x8D, 0x46, 0x04, 0x83, 0xEB, 0x08, 0x89, 0x44, 0x24, 0x10, 0xD1, 0xEB, 0xBA, 0x00, 0x00, 0x00, 0x00, 0x74, 0x30, 0x0F, 0x1F, 0x84, 0x00, 0x00, 0x00, 0x00, 0x00, 0x0F, 0xB7, 0x44, 0x56, 0x08, 0x8B, 0xC8, 0x81, 0xE1, 0x00, 0xF0, 0x00, 0x00, 0x81, 0xF9, 0x00, 0x30, 0x00, 0x00, 0x75, 0x0A, 0x25, 0xFF, 0x0F, 0x00, 0x00, 0x03, 0x06, 0x01, 0x2C, 0x38, 0x42, 0x3B, 0xD3, 0x72, 0xDC, 0x8B, 0x44, 0x24, 0x10, 0x03, 0x30, 0x83, 0x3E, 0x00, 0x75, 0xB3, 0x8B, 0x5C, 0x24, 0x1C, 0x8B, 0x54, 0x24, 0x18, 0x83, 0xBB, 0x84, 0x00, 0x00, 0x00, 0x00, 0x74, 0x74, 0x8B, 0xB3, 0x80, 0x00, 0x00, 0x00, 0x03, 0xF7, 0x89, 0x74, 0x24, 0x10, 0x83, 0x7E, 0x0C, 0x00, 0x74, 0x62, 0x8B, 0x46, 0x0C, 0x03, 0xC7, 0x50, 0xFF, 0xD2, 0x8B, 0xE8, 0x85, 0xED, 0x74, 0x3F, 0x8B, 0x0E, 0x85, 0xC9, 0x8B, 0x56, 0x10, 0x0F, 0x44, 0xCA, 0x8B, 0x04, 0x39, 0x8D, 0x34, 0x39, 0x8D, 0x1C, 0x3A, 0x85, 0xC0, 0x74, 0x24, 0x8B, 0xC8, 0x79, 0x05, 0x0F, 0xB7, 0xC1, 0xEB, 0x05, 0x8D, 0x47, 0x02, 0x03, 0xC1, 0x50, 0x55, 0xFF, 0x54, 0x24, 0x1C, 0x83, 0xC6, 0x04, 0x89, 0x03, 0x83, 0xC3, 0x04, 0x8B, 0x06, 0x8B, 0xC8, 0x85, 0xC0, 0x75, 0xDE, 0x8B, 0x74, 0x24, 0x10, 0x8B, 0x54, 0x24, 0x18, 0x83, 0xC6, 0x14, 0x89, 0x74, 0x24, 0x10, 0x83, 0x7E, 0x0C, 0x00, 0x75, 0xA2, 0x8B, 0x5C, 0x24, 0x1C, 0x83, 0xBB, 0xC4, 0x00, 0x00, 0x00, 0x00, 0x74, 0x25, 0x8B, 0x83, 0xC0, 0x00, 0x00, 0x00, 0x8B, 0x74, 0x38, 0x0C, 0x85, 0xF6, 0x74, 0x17, 0x8B, 0x06, 0x85, 0xC0, 0x74, 0x11, 0x6A, 0x00, 0x6A, 0x01, 0x57, 0xFF, 0xD0, 0x8B, 0x46, 0x04, 0x8D, 0x76, 0x04, 0x85, 0xC0, 0x75, 0xEF, 0x83, 0x7B, 0x28, 0x00, 0x8B, 0x74, 0x24, 0x28, 0x74, 0x0B, 0xFF, 0x76, 0x14, 0xFF, 0x76, 0x10, 0x57, 0xFF, 0x54, 0x24, 0x2C, 0x89, 0x7E, 0x0C, 0x5F, 0x5E, 0x5D, 0x5B, 0x83, 0xC4, 0x14, 0xC2, 0x04, 0x00
        };

        public static bool Inject(int processId, string dllPath, OptionsConfig? options, out string errorMessage)
        {
            errorMessage = "";
            IntPtr hProcess = IntPtr.Zero;
            IntPtr remoteImageBase = IntPtr.Zero;
            IntPtr remoteLoaderMem = IntPtr.Zero;
            IntPtr hThread = IntPtr.Zero;
            bool shouldFreeLoader = true;
            bool injectionSucceeded = false;

            if (!File.Exists(dllPath))
            {
                errorMessage = $"DLL file does not exist: {dllPath}";
                return false;
            }

            // 1. Architecture Validation
            if (!ValidateArchitecture(processId, dllPath, out bool isTarget64, out string archError))
            {
                errorMessage = archError;
                return false;
            }

            // 2. Parse PE Image via PeParser
            PeParser pe;
            try
            {
                pe = new PeParser(dllPath);
            }
            catch (Exception ex)
            {
                errorMessage = $"Failed to parse PE binary '{Path.GetFileName(dllPath)}': {ex.Message}";
                return false;
            }

            // 3. Open Target Process with exact low-level access flags
            const uint PROCESS_ACCESS = NativeMethods.PROCESS_CREATE_THREAD |
                                         NativeMethods.THREAD_QUERY_INFORMATION |
                                         NativeMethods.PROCESS_VM_OPERATION |
                                         NativeMethods.PROCESS_VM_WRITE |
                                         NativeMethods.PROCESS_VM_READ;

            hProcess = NativeMethods.OpenProcess(PROCESS_ACCESS, false, processId);
            if (hProcess == IntPtr.Zero)
            {
                int err = Marshal.GetLastWin32Error();
                errorMessage = $"Failed to open target process (PID: {processId}).\nWin32 Error {err}: {new Win32Exception(err).Message}";
                return false;
            }

            try
            {
                // 4. Allocate Memory for PE Image (SizeOfImage) in Target Process
                remoteImageBase = NativeMethods.VirtualAllocEx(
                    hProcess,
                    IntPtr.Zero,
                    (UIntPtr)pe.SizeOfImage,
                    NativeMethods.MEM_COMMIT | NativeMethods.MEM_RESERVE,
                    NativeMethods.PAGE_EXECUTE_READWRITE
                );

                if (remoteImageBase == IntPtr.Zero)
                {
                    int err = Marshal.GetLastWin32Error();
                    errorMessage = $"VirtualAllocEx failed for {pe.SizeOfImage} bytes.\nWin32 Error {err}: {new Win32Exception(err).Message}";
                    return false;
                }

                // 5. Write PE Headers (SizeOfHeaders)
                if (!NativeMethods.WriteProcessMemory(hProcess, remoteImageBase, pe.RawBytes, (UIntPtr)pe.SizeOfHeaders, out _))
                {
                    int err = Marshal.GetLastWin32Error();
                    errorMessage = $"Failed to write PE headers into target process.\nWin32 Error {err}: {new Win32Exception(err).Message}";
                    return false;
                }

                // 6. Map All Sections into Remote Memory
                foreach (var sec in pe.Sections)
                {
                    if (sec.SizeOfRawData > 0 && sec.PointerToRawData > 0 && sec.PointerToRawData < pe.RawBytes.Length)
                    {
                        byte[] secBytes = new byte[sec.SizeOfRawData];
                        int copyLen = (int)Math.Min((long)sec.SizeOfRawData, (long)(pe.RawBytes.Length - sec.PointerToRawData));
                        Buffer.BlockCopy(pe.RawBytes, (int)sec.PointerToRawData, secBytes, 0, copyLen);

                        IntPtr secDest = (IntPtr)(remoteImageBase.ToInt64() + sec.VirtualAddress);
                        if (!NativeMethods.WriteProcessMemory(hProcess, secDest, secBytes, (UIntPtr)sec.SizeOfRawData, out _))
                        {
                            int err = Marshal.GetLastWin32Error();
                            errorMessage = $"Failed to write section '{sec.Name}' to 0x{secDest.ToInt64():X}.\nWin32 Error {err}: {new Win32Exception(err).Message}";
                            return false;
                        }
                    }
                }

                // 7. Allocate 4KB Loader Memory for Shellcode & Mapping Data
                const uint LOADER_MEM_SIZE = 0x1000;
                remoteLoaderMem = NativeMethods.VirtualAllocEx(
                    hProcess,
                    IntPtr.Zero,
                    (UIntPtr)LOADER_MEM_SIZE,
                    NativeMethods.MEM_COMMIT | NativeMethods.MEM_RESERVE,
                    NativeMethods.PAGE_EXECUTE_READWRITE
                );

                if (remoteLoaderMem == IntPtr.Zero)
                {
                    int err = Marshal.GetLastWin32Error();
                    errorMessage = $"Failed to allocate loader memory block.\nWin32 Error {err}: {new Win32Exception(err).Message}";
                    return false;
                }

                // 8. Resolve Host API Addresses
                IntPtr hKernel32 = NativeMethods.GetModuleHandle("kernel32.dll");
                IntPtr pLoadLibraryA = NativeMethods.GetProcAddress(hKernel32, "LoadLibraryA");
                IntPtr pGetProcAddress = NativeMethods.GetProcAddress(hKernel32, "GetProcAddress");

                IntPtr hNtdll = NativeMethods.GetModuleHandle("ntdll.dll");
                IntPtr pRtlAddFunctionTable = NativeMethods.GetProcAddress(hNtdll, "RtlAddFunctionTable");

                if (pLoadLibraryA == IntPtr.Zero || pGetProcAddress == IntPtr.Zero)
                {
                    errorMessage = "Failed to resolve required loader APIs (LoadLibraryA / GetProcAddress).";
                    return false;
                }

                // 9. Build Mapping Data Packet
                // Offset 0x000: Shellcode Stub
                // Offset 0x800: MANUAL_MAPPING_DATA structure
                IntPtr pDataAddress = (IntPtr)(remoteLoaderMem.ToInt64() + 0x800);
                byte[] dataBytes;

                bool sehSupport = !(options?.Advanced?.DisableExceptionSupport ?? false);

                if (isTarget64)
                {
                    // MANUAL_MAPPING_DATA_64 Layout (64 bytes):
                    // 0x00: pLoadLibraryA (8)
                    // 0x08: pGetProcAddress (8)
                    // 0x10: pRtlAddFunctionTable (8)
                    // 0x18: pBase (8)
                    // 0x20: hMod (8) - output
                    // 0x28: fdwReasonParam (4) = 1 (DLL_PROCESS_ATTACH)
                    // 0x2C: pad1 (4)
                    // 0x30: reservedParam (8) = 0
                    // 0x38: SEHSupport (4)
                    // 0x3C: pad2 (4)
                    dataBytes = new byte[64];
                    Buffer.BlockCopy(BitConverter.GetBytes(pLoadLibraryA.ToInt64()), 0, dataBytes, 0x00, 8);
                    Buffer.BlockCopy(BitConverter.GetBytes(pGetProcAddress.ToInt64()), 0, dataBytes, 0x08, 8);
                    Buffer.BlockCopy(BitConverter.GetBytes(pRtlAddFunctionTable.ToInt64()), 0, dataBytes, 0x10, 8);
                    Buffer.BlockCopy(BitConverter.GetBytes(remoteImageBase.ToInt64()), 0, dataBytes, 0x18, 8);
                    Buffer.BlockCopy(BitConverter.GetBytes((long)0), 0, dataBytes, 0x20, 8); // hMod out
                    Buffer.BlockCopy(BitConverter.GetBytes((int)1), 0, dataBytes, 0x28, 4);  // DLL_PROCESS_ATTACH
                    Buffer.BlockCopy(BitConverter.GetBytes((long)0), 0, dataBytes, 0x30, 8); // reservedParam
                    Buffer.BlockCopy(BitConverter.GetBytes(sehSupport ? 1 : 0), 0, dataBytes, 0x38, 4);
                }
                else
                {
                    // MANUAL_MAPPING_DATA_32 Layout (28 bytes):
                    // 0x00: pLoadLibraryA (4)
                    // 0x04: pGetProcAddress (4)
                    // 0x08: pBase (4)
                    // 0x0C: hMod (4) - output
                    // 0x10: fdwReasonParam (4) = 1
                    // 0x14: reservedParam (4) = 0
                    // 0x18: SEHSupport (4)
                    dataBytes = new byte[28];
                    Buffer.BlockCopy(BitConverter.GetBytes(pLoadLibraryA.ToInt32()), 0, dataBytes, 0x00, 4);
                    Buffer.BlockCopy(BitConverter.GetBytes(pGetProcAddress.ToInt32()), 0, dataBytes, 0x04, 4);
                    Buffer.BlockCopy(BitConverter.GetBytes(remoteImageBase.ToInt32()), 0, dataBytes, 0x08, 4);
                    Buffer.BlockCopy(BitConverter.GetBytes((int)0), 0, dataBytes, 0x0C, 4); // hMod out
                    Buffer.BlockCopy(BitConverter.GetBytes((int)1), 0, dataBytes, 0x10, 4); // DLL_PROCESS_ATTACH
                    Buffer.BlockCopy(BitConverter.GetBytes((int)0), 0, dataBytes, 0x14, 4); // reservedParam
                    Buffer.BlockCopy(BitConverter.GetBytes(sehSupport ? 1 : 0), 0, dataBytes, 0x18, 4);
                }

                byte[] shellcode = isTarget64 ? X64Shellcode : X86Shellcode;

                // 10. Write Shellcode and Data to Remote Loader Block
                if (!NativeMethods.WriteProcessMemory(hProcess, remoteLoaderMem, shellcode, (UIntPtr)shellcode.Length, out _) ||
                    !NativeMethods.WriteProcessMemory(hProcess, pDataAddress, dataBytes, (UIntPtr)dataBytes.Length, out _))
                {
                    int err = Marshal.GetLastWin32Error();
                    errorMessage = $"Failed to write manual mapping bootstrap to target memory.\nWin32 Error {err}: {new Win32Exception(err).Message}";
                    return false;
                }

                // 11. Execute Remote Thread (Supporting Stealth Inject & HideFromDebugger)
                bool stealthInject = options?.StealthInject ?? false;
                bool hideFromDebugger = options?.Advanced?.HideFromDebugger ?? false;

                if (!NativeMethods.CreateRemoteThreadSmart(
                    hProcess,
                    remoteLoaderMem,
                    pDataAddress,
                    stealthInject,
                    hideFromDebugger,
                    out hThread,
                    out string threadError))
                {
                    errorMessage = threadError;
                    return false;
                }

                // 12. Wait for Loader Completion (15s timeout for complex DLL imports/relocations)
                uint waitResult = NativeMethods.WaitForSingleObject(hThread, 15000);
                if (waitResult == 0x00000102 /* WAIT_TIMEOUT */)
                {
                    shouldFreeLoader = false; // Never unmap code under running thread
                    errorMessage = "Manual Map loader thread timed out after 15 seconds. DllMain or imports may still be processing.";
                    return false;
                }

                // 13. Read Result hMod from Data Packet
                byte[] resultBuffer = new byte[isTarget64 ? 8 : 4];
                IntPtr hModOffset = (IntPtr)(pDataAddress.ToInt64() + (isTarget64 ? 0x20 : 0x0C));
                NativeMethods.ReadProcessMemory(hProcess, hModOffset, resultBuffer, (UIntPtr)resultBuffer.Length, out _);

                long hMod = isTarget64 ? BitConverter.ToInt64(resultBuffer, 0) : BitConverter.ToUInt32(resultBuffer, 0);

                if (hMod == 0)
                {
                    errorMessage = "Manual Map failed: Module initialization returned NULL. Check if the DLL has missing dependencies or if DllMain returned FALSE.";
                    return false;
                }
                if (hMod == 0x404040)
                {
                    errorMessage = "Manual Map failed: Null bootstrap data pointer received inside target thread.";
                    return false;
                }
                if (hMod == 0x505050)
                {
                    errorMessage = "Manual Map failed: RtlAddFunctionTable failed to register x64 exception unwinding table.";
                    return false;
                }

                injectionSucceeded = true;

                // 14. Apply Post-Processing: Erase PE Header if requested
                if (options?.ErasePE ?? false)
                {
                    PostProcessor.ErasePEHeader(hProcess, remoteImageBase, out _);
                }

                return true;
            }
            catch (Exception ex)
            {
                errorMessage = $"Unexpected Manual Map exception: {ex.Message}";
                return false;
            }
            finally
            {
                if (hThread != IntPtr.Zero)
                    NativeMethods.CloseHandle(hThread);

                // Free temporary shellcode loader memory
                if (shouldFreeLoader && remoteLoaderMem != IntPtr.Zero && hProcess != IntPtr.Zero)
                    NativeMethods.VirtualFreeEx(hProcess, remoteLoaderMem, UIntPtr.Zero, NativeMethods.MEM_RELEASE);

                // If injection failed before thread started or on error, free mapped image to prevent memory leaks
                if (!injectionSucceeded && shouldFreeLoader && remoteImageBase != IntPtr.Zero && hProcess != IntPtr.Zero)
                    NativeMethods.VirtualFreeEx(hProcess, remoteImageBase, UIntPtr.Zero, NativeMethods.MEM_RELEASE);

                if (hProcess != IntPtr.Zero)
                    NativeMethods.CloseHandle(hProcess);
            }
        }

        private static bool ValidateArchitecture(int processId, string dllPath, out bool isTarget64, out string error)
        {
            isTarget64 = false;
            error = "";

            try
            {
                IntPtr hProcess = NativeMethods.OpenProcess(0x1000 /* PROCESS_QUERY_LIMITED_INFORMATION */, false, processId);
                if (hProcess == IntPtr.Zero)
                {
                    hProcess = NativeMethods.OpenProcess(NativeMethods.PROCESS_QUERY_INFORMATION, false, processId);
                }

                if (hProcess != IntPtr.Zero)
                {
                    try
                    {
                        if (Environment.Is64BitOperatingSystem)
                        {
                            if (NativeMethods.IsWow64Process(hProcess, out bool isWow64))
                            {
                                isTarget64 = !isWow64;
                            }
                            else
                            {
                                isTarget64 = Environment.Is64BitProcess;
                            }
                        }
                        else
                        {
                            isTarget64 = false;
                        }
                    }
                    finally
                    {
                        NativeMethods.CloseHandle(hProcess);
                    }
                }

                // Check DLL PE Header
                ushort machine = GetDllMachineType(dllPath);
                bool isDll64 = (machine == 0x8664 /* IMAGE_FILE_MACHINE_AMD64 */);
                bool isDll32 = (machine == 0x014C /* IMAGE_FILE_MACHINE_I386 */);

                if (isTarget64 && isDll32)
                {
                    error = $"Architecture Mismatch: Target process is 64-bit, but DLL '{Path.GetFileName(dllPath)}' is 32-bit (x86).";
                    return false;
                }

                if (!isTarget64 && isDll64)
                {
                    error = $"Architecture Mismatch: Target process is 32-bit, but DLL '{Path.GetFileName(dllPath)}' is 64-bit (x64).";
                    return false;
                }
            }
            catch (Exception ex)
            {
                error = $"Architecture check failed: {ex.Message}";
                return false;
            }

            return true;
        }

        private static ushort GetDllMachineType(string filePath)
        {
            try
            {
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var br = new BinaryReader(fs);

                if (br.ReadUInt16() != 0x5A4D) return 0; // 'MZ'
                fs.Seek(0x3C, SeekOrigin.Begin);
                uint e_lfanew = br.ReadUInt32();

                fs.Seek(e_lfanew, SeekOrigin.Begin);
                if (br.ReadUInt32() != 0x00004550) return 0; // 'PE\0\0'

                return br.ReadUInt16(); // Machine header
            }
            catch
            {
                return 0;
            }
        }
    }
}
