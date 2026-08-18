using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using ExtremeInjector.Config;

namespace ExtremeInjector.Core
{
    public static class LdrLoadDllInjector
    {
        public static bool Inject(int processId, string dllPath, OptionsConfig? options, out string errorMessage)
        {
            errorMessage = "";
            IntPtr hProcess = IntPtr.Zero;
            IntPtr remoteMem = IntPtr.Zero;
            IntPtr hThread = IntPtr.Zero;

            if (!File.Exists(dllPath))
            {
                errorMessage = $"DLL file does not exist: {dllPath}";
                return false;
            }

            // 1. Architecture Check (PE Machine Header vs Target Process Architecture)
            if (!ValidateArchitecture(processId, dllPath, out bool isTarget64, out string archError))
            {
                errorMessage = archError;
                return false;
            }

            // 2. Open Target Process with Required Permissions
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
                // 3. Resolve ntdll!LdrLoadDll Export Address
                IntPtr hNtdll = NativeMethods.GetModuleHandle("ntdll.dll");
                if (hNtdll == IntPtr.Zero)
                {
                    int err = Marshal.GetLastWin32Error();
                    errorMessage = $"Failed to get module handle for ntdll.dll.\nWin32 Error {err}: {new Win32Exception(err).Message}";
                    return false;
                }

                IntPtr pLdrLoadDll = NativeMethods.GetProcAddress(hNtdll, "LdrLoadDll");
                if (pLdrLoadDll == IntPtr.Zero)
                {
                    int err = Marshal.GetLastWin32Error();
                    errorMessage = $"Failed to resolve ntdll!LdrLoadDll export.\nWin32 Error {err}: {new Win32Exception(err).Message}";
                    return false;
                }

                // 4. Allocate 4096 bytes in Target Process
                const uint ALLOC_SIZE = 0x1000;
                remoteMem = NativeMethods.VirtualAllocEx(
                    hProcess,
                    IntPtr.Zero,
                    (UIntPtr)ALLOC_SIZE,
                    NativeMethods.MEM_COMMIT | NativeMethods.MEM_RESERVE,
                    NativeMethods.PAGE_EXECUTE_READWRITE
                );

                if (remoteMem == IntPtr.Zero)
                {
                    int err = Marshal.GetLastWin32Error();
                    errorMessage = $"Failed to allocate {ALLOC_SIZE} bytes in target process.\nWin32 Error {err}: {new Win32Exception(err).Message}";
                    return false;
                }

                // 5. Construct Remote Memory Layout
                // Offset 0x000: Shellcode Stub
                // Offset 0x100: UNICODE_STRING Structure
                // Offset 0x120: Output HMODULE (8/4 bytes)
                // Offset 0x128: Output NTSTATUS (4 bytes)
                // Offset 0x200: DLL Path UTF-16 String
                byte[] pathBytes = Encoding.Unicode.GetBytes(dllPath + "\0");
                ushort pathByteLenWithoutNull = (ushort)(pathBytes.Length - 2);
                ushort pathByteLenWithNull = (ushort)pathBytes.Length;

                byte[] remoteBuffer = new byte[ALLOC_SIZE];

                // Copy DLL path string to offset 0x200
                Buffer.BlockCopy(pathBytes, 0, remoteBuffer, 0x200, pathBytes.Length);

                byte[] stubBytes;
                if (isTarget64)
                {
                    ulong pPathString = (ulong)remoteMem.ToInt64() + 0x200;
                    ulong pUnicodeString = (ulong)remoteMem.ToInt64() + 0x100;
                    ulong pOutputHModule = (ulong)remoteMem.ToInt64() + 0x120;
                    ulong pOutputNtStatus = (ulong)remoteMem.ToInt64() + 0x128;
                    ulong pLdrLoadDllAddr = (ulong)pLdrLoadDll.ToInt64();

                    // UNICODE_STRING 64-bit structure at offset 0x100:
                    // ushort Length; ushort MaximumLength; uint Pad; ulong Buffer;
                    byte[] usBytes = new byte[16];
                    Buffer.BlockCopy(BitConverter.GetBytes(pathByteLenWithoutNull), 0, usBytes, 0, 2);
                    Buffer.BlockCopy(BitConverter.GetBytes(pathByteLenWithNull), 0, usBytes, 2, 2);
                    Buffer.BlockCopy(BitConverter.GetBytes(pPathString), 0, usBytes, 8, 8);
                    Buffer.BlockCopy(usBytes, 0, remoteBuffer, 0x100, 16);

                    // Build x64 Shellcode Stub
                    var stub = new List<byte>();
                    // sub rsp, 0x28
                    stub.AddRange(new byte[] { 0x48, 0x83, 0xEC, 0x28 });
                    // xor rcx, rcx (DllPath = NULL)
                    stub.AddRange(new byte[] { 0x48, 0x31, 0xC9 });
                    // xor rdx, rdx (DllCharacteristics = NULL)
                    stub.AddRange(new byte[] { 0x48, 0x31, 0xD2 });
                    // mov r8, pUnicodeString (Arg 3)
                    stub.AddRange(new byte[] { 0x49, 0xB8 });
                    stub.AddRange(BitConverter.GetBytes(pUnicodeString));
                    // mov r9, pOutputHModule (Arg 4)
                    stub.AddRange(new byte[] { 0x49, 0xB9 });
                    stub.AddRange(BitConverter.GetBytes(pOutputHModule));
                    // mov rax, pLdrLoadDllAddr
                    stub.AddRange(new byte[] { 0x48, 0xB8 });
                    stub.AddRange(BitConverter.GetBytes(pLdrLoadDllAddr));
                    // call rax
                    stub.AddRange(new byte[] { 0xFF, 0xD0 });
                    // mov rdx, pOutputNtStatus
                    stub.AddRange(new byte[] { 0x48, 0xBA });
                    stub.AddRange(BitConverter.GetBytes(pOutputNtStatus));
                    // mov [rdx], eax
                    stub.AddRange(new byte[] { 0x89, 0x02 });
                    // add rsp, 0x28
                    stub.AddRange(new byte[] { 0x48, 0x83, 0xC4, 0x28 });
                    // ret
                    stub.Add(0xC3);

                    stubBytes = stub.ToArray();
                }
                else
                {
                    uint pPathString = (uint)remoteMem.ToInt32() + 0x200;
                    uint pUnicodeString = (uint)remoteMem.ToInt32() + 0x100;
                    uint pOutputHModule = (uint)remoteMem.ToInt32() + 0x120;
                    uint pOutputNtStatus = (uint)remoteMem.ToInt32() + 0x128;
                    uint pLdrLoadDllAddr = (uint)pLdrLoadDll.ToInt32();

                    // UNICODE_STRING 32-bit structure at offset 0x100:
                    // ushort Length; ushort MaximumLength; uint Buffer;
                    byte[] usBytes = new byte[8];
                    Buffer.BlockCopy(BitConverter.GetBytes(pathByteLenWithoutNull), 0, usBytes, 0, 2);
                    Buffer.BlockCopy(BitConverter.GetBytes(pathByteLenWithNull), 0, usBytes, 2, 2);
                    Buffer.BlockCopy(BitConverter.GetBytes(pPathString), 0, usBytes, 4, 4);
                    Buffer.BlockCopy(usBytes, 0, remoteBuffer, 0x100, 8);

                    // Build x86 Shellcode Stub
                    var stub = new List<byte>();
                    // push pOutputHModule (Arg 4)
                    stub.Add(0x68);
                    stub.AddRange(BitConverter.GetBytes(pOutputHModule));
                    // push pUnicodeString (Arg 3)
                    stub.Add(0x68);
                    stub.AddRange(BitConverter.GetBytes(pUnicodeString));
                    // push 0 (Arg 2: DllCharacteristics)
                    stub.AddRange(new byte[] { 0x6A, 0x00 });
                    // push 0 (Arg 1: DllPath)
                    stub.AddRange(new byte[] { 0x6A, 0x00 });
                    // mov eax, pLdrLoadDllAddr
                    stub.Add(0xB8);
                    stub.AddRange(BitConverter.GetBytes(pLdrLoadDllAddr));
                    // call eax
                    stub.AddRange(new byte[] { 0xFF, 0xD0 });
                    // mov [pOutputNtStatus], eax
                    stub.Add(0xA3);
                    stub.AddRange(BitConverter.GetBytes(pOutputNtStatus));
                    // ret 4
                    stub.AddRange(new byte[] { 0xC2, 0x04, 0x00 });

                    stubBytes = stub.ToArray();
                }

                // Copy Shellcode Stub to offset 0x000
                Buffer.BlockCopy(stubBytes, 0, remoteBuffer, 0, stubBytes.Length);

                // 6. Write Entire Payload to Remote Memory
                if (!NativeMethods.WriteProcessMemory(hProcess, remoteMem, remoteBuffer, (UIntPtr)ALLOC_SIZE, out _))
                {
                    int err = Marshal.GetLastWin32Error();
                    errorMessage = $"Failed to write LdrLoadDll payload into target process.\nWin32 Error {err}: {new Win32Exception(err).Message}";
                    return false;
                }

                // 7. Create Remote Thread Pointing to Stub
                bool hideFromDebugger = options?.Advanced?.HideFromDebugger ?? false;
                uint creationFlags = hideFromDebugger ? NativeMethods.CREATE_SUSPENDED : 0;

                hThread = NativeMethods.CreateRemoteThread(
                    hProcess,
                    IntPtr.Zero,
                    UIntPtr.Zero,
                    remoteMem,
                    IntPtr.Zero,
                    creationFlags,
                    out _
                );

                if (hThread == IntPtr.Zero)
                {
                    int err = Marshal.GetLastWin32Error();
                    errorMessage = $"CreateRemoteThread failed for LdrLoadDll.\nWin32 Error {err}: {new Win32Exception(err).Message}";
                    return false;
                }

                if (hideFromDebugger)
                {
                    try
                    {
                        NativeMethods.NtSetInformationThread(
                            hThread,
                            NativeMethods.ThreadHideFromDebugger,
                            IntPtr.Zero,
                            0
                        );
                    }
                    catch { }

                    NativeMethods.ResumeThread(hThread);
                }

                // 8. Wait for Thread Completion
                uint waitResult = NativeMethods.WaitForSingleObject(hThread, 10000);
                if (waitResult == 0x00000102 /* WAIT_TIMEOUT */)
                {
                    errorMessage = "Remote LdrLoadDll thread timed out after 10 seconds.";
                    return false;
                }

                // 9. Read Back NTSTATUS and Output HMODULE
                byte[] statusBuffer = new byte[4];
                byte[] hModuleBuffer = new byte[isTarget64 ? 8 : 4];

                NativeMethods.ReadProcessMemory(hProcess, (IntPtr)(remoteMem.ToInt64() + 0x128), statusBuffer, (UIntPtr)4, out _);
                NativeMethods.ReadProcessMemory(hProcess, (IntPtr)(remoteMem.ToInt64() + 0x120), hModuleBuffer, (UIntPtr)hModuleBuffer.Length, out _);

                uint ntStatus = BitConverter.ToUInt32(statusBuffer, 0);
                long hModuleVal = isTarget64 ? BitConverter.ToInt64(hModuleBuffer, 0) : BitConverter.ToUInt32(hModuleBuffer, 0);

                if (ntStatus != 0)
                {
                    errorMessage = $"LdrLoadDll returned NTSTATUS 0x{ntStatus:X8}: {GetNtStatusErrorMessage(ntStatus)}";
                    return false;
                }

                if (hModuleVal == 0)
                {
                    errorMessage = "LdrLoadDll succeeded but returned a NULL module base address.";
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                errorMessage = $"Unexpected LdrLoadDll exception: {ex.Message}";
                return false;
            }
            finally
            {
                if (hThread != IntPtr.Zero)
                    NativeMethods.CloseHandle(hThread);

                if (remoteMem != IntPtr.Zero && hProcess != IntPtr.Zero)
                    NativeMethods.VirtualFreeEx(hProcess, remoteMem, UIntPtr.Zero, NativeMethods.MEM_RELEASE);

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

        private static string GetNtStatusErrorMessage(uint status)
        {
            return status switch
            {
                0x00000000 => "STATUS_SUCCESS (Operation completed successfully)",
                0xC0000135 => "STATUS_DLL_NOT_FOUND (The specified module or one of its dependencies could not be found)",
                0xC0000139 => "STATUS_ENTRYPOINT_NOT_FOUND (A required export entry point was not found in a dependency)",
                0xC0000142 => "STATUS_DLL_INIT_FAILED (DllMain failed or threw an unhandled exception during initialization)",
                0xC0000022 => "STATUS_ACCESS_DENIED (Insufficient access permissions to read or initialize the module)",
                0x4000000E => "STATUS_IMAGE_MACHINE_TYPE_MISMATCH (32-bit/64-bit architecture mismatch)",
                0xC0000018 => "STATUS_CONFLICTING_ADDRESSES (Address space conflict during image mapping)",
                0xC0000005 => "STATUS_ACCESS_VIOLATION (Access violation reading/writing memory during loading)",
                _ => $"Native NT error code 0x{status:X8}"
            };
        }
    }
}
