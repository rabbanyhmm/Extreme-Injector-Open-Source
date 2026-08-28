using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using ExtremeInjector.Config;

namespace ExtremeInjector.Core
{
    /// <summary>
    /// Advanced DLL Injection Engine via Thread Context Hijacking.
    /// Operates without calling CreateRemoteThread / NtCreateThreadEx by borrowing an existing target thread,
    /// redirecting its execution pointer (RIP/EIP) to a register-preserving trampoline, and resuming normal execution seamlessly.
    /// </summary>
    public static class ThreadHijackInjector
    {
        public static bool Inject(int processId, string dllPath, OptionsConfig? options, out string errorMessage)
        {
            errorMessage = "";
            IntPtr hProcess = IntPtr.Zero;
            IntPtr remoteMem = IntPtr.Zero;
            IntPtr hThread = IntPtr.Zero;
            bool shouldFreeMem = true;

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

            // 2. Open Target Process
            const uint PROCESS_ACCESS = NativeMethods.PROCESS_VM_OPERATION |
                                         NativeMethods.PROCESS_VM_WRITE |
                                         NativeMethods.PROCESS_VM_READ |
                                         NativeMethods.PROCESS_QUERY_INFORMATION;

            hProcess = HandleHijacker.OpenProcessSmart(processId, PROCESS_ACCESS, out _);
            if (hProcess == IntPtr.Zero)
            {
                int err = Marshal.GetLastWin32Error();
                errorMessage = $"Failed to open or hijack handle to target process (PID: {processId}).\nWin32 Error {err}: {new Win32Exception(err).Message}";
                return false;
            }

            try
            {
                // 3. Resolve LoadLibraryW Address in Remote Process (Supports native x64 and WoW64 x86)
                IntPtr pLoadLibraryW = RemoteExportResolver.GetProcAddress(processId, isTarget64, "kernel32.dll", "LoadLibraryW");
                if (pLoadLibraryW == IntPtr.Zero)
                {
                    errorMessage = "Failed to resolve kernel32!LoadLibraryW export in target process.";
                    return false;
                }

                // 4. Select a Suitable Thread in Target Process
                hThread = FindTargetThread(processId, isTarget64, out uint threadId);
                if (hThread == IntPtr.Zero)
                {
                    errorMessage = $"Could not locate an active, hijackable thread in target process (PID: {processId}).";
                    return false;
                }

                // 5. Suspend Target Thread
                if (NativeMethods.SuspendThread(hThread) == unchecked((uint)-1))
                {
                    int err = Marshal.GetLastWin32Error();
                    errorMessage = $"Failed to suspend target thread (TID: {threadId}).\nWin32 Error {err}: {new Win32Exception(err).Message}";
                    return false;
                }

                try
                {
                    // 6. Allocate Remote Memory for DLL Path, Status Flag, and Shellcode Trampoline
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
                        errorMessage = $"Failed to allocate executable memory block in target process.\nWin32 Error {err}: {new Win32Exception(err).Message}";
                        return false;
                    }

                    // Layout:
                    // Offset 0x000: DLL Path (UTF-16 with Null Terminator)
                    // Offset 0x100: Status Flag (0 = Initialized, 1 = Loaded/HMODULE, 0xFFFFFFFF = Failed)
                    // Offset 0x200: Shellcode Trampoline
                    byte[] pathBytes = Encoding.Unicode.GetBytes(dllPath + "\0");
                    byte[] remoteBuffer = new byte[ALLOC_SIZE];
                    Buffer.BlockCopy(pathBytes, 0, remoteBuffer, 0x000, pathBytes.Length);

                    IntPtr pDllPathRemote = remoteMem;
                    IntPtr pStatusRemote = (IntPtr)(remoteMem.ToInt64() + 0x100);
                    IntPtr pShellcodeRemote = (IntPtr)(remoteMem.ToInt64() + 0x200);

                    if (isTarget64)
                    {
                        // 7a. Capture 64-bit Thread Context
                        var ctx = new NativeMethods.CONTEXT_X64
                        {
                            ContextFlags = 0x0010001F // CONTEXT_AMD64_ALL (Full GPR, Control, Float, Debug)
                        };

                        if (!NativeMethods.GetThreadContext(hThread, ref ctx))
                        {
                            int err = Marshal.GetLastWin32Error();
                            errorMessage = $"Failed to get x64 thread context.\nWin32 Error {err}: {new Win32Exception(err).Message}";
                            return false;
                        }

                        ulong origRip = ctx.Rip;

                        // Build Context-Safe x64 Shellcode Trampoline
                        var stub = new List<byte>();

                        // 1. Save all 16 GPRs and Flags (RAX, RBX, RCX, RDX, RSI, RDI, RBP, R8-R15, RFLAGS)
                        byte[] gprPushes = new byte[] { 0x50, 0x53, 0x51, 0x52, 0x56, 0x57, 0x55, 0x41, 0x50, 0x41, 0x51, 0x41, 0x52, 0x41, 0x53, 0x41, 0x54, 0x41, 0x55, 0x41, 0x56, 0x41, 0x57, 0x9C };
                        stub.AddRange(gprPushes);

                        // 2. Save XMM Registers (sub rsp, 256 + movdqu xmm0..xmm15)
                        stub.AddRange(new byte[] { 0x48, 0x81, 0xEC, 0x00, 0x01, 0x00, 0x00 });
                        for (int i = 0; i < 8; i++)
                        {
                            stub.AddRange(new byte[] { 0x0F, (byte)(0x11), (byte)(0x44 + (i * 8)), 0x24, (byte)(i * 16) });
                        }
                        for (int i = 0; i < 8; i++)
                        {
                            stub.AddRange(new byte[] { 0x44, 0x0F, 0x11, (byte)(0x84 + (i * 8)), 0x24, (byte)(0x80 + (i * 16)), 0x00, 0x00, 0x00 });
                        }

                        // 3. Align Stack and Call LoadLibraryW
                        stub.AddRange(new byte[] { 0x48, 0x89, 0xE3 }); // mov rbx, rsp
                        stub.AddRange(new byte[] { 0x48, 0x83, 0xE4, 0xF0 }); // and rsp, -16
                        stub.AddRange(new byte[] { 0x48, 0x83, 0xEC, 0x20 }); // sub rsp, 0x20 (32-byte shadow space for Win64 ABI)

                        // mov rcx, pDllPathRemote
                        stub.AddRange(new byte[] { 0x48, 0xB9 });
                        stub.AddRange(BitConverter.GetBytes((ulong)pDllPathRemote.ToInt64()));

                        // mov rax, pLoadLibraryW
                        stub.AddRange(new byte[] { 0x48, 0xB8 });
                        stub.AddRange(BitConverter.GetBytes((ulong)pLoadLibraryW.ToInt64()));

                        // call rax
                        stub.AddRange(new byte[] { 0xFF, 0xD0 });

                        // mov rdx, pStatusRemote
                        stub.AddRange(new byte[] { 0x48, 0xBA });
                        stub.AddRange(BitConverter.GetBytes((ulong)pStatusRemote.ToInt64()));

                        // mov [rdx], rax (store result HMODULE / status)
                        stub.AddRange(new byte[] { 0x48, 0x89, 0x02 });

                        // 4. Restore Stack & XMM Registers
                        stub.AddRange(new byte[] { 0x48, 0x89, 0xDC }); // mov rsp, rbx
                        for (int i = 0; i < 8; i++)
                        {
                            stub.AddRange(new byte[] { 0x0F, 0x10, (byte)(0x44 + (i * 8)), 0x24, (byte)(i * 16) });
                        }
                        for (int i = 0; i < 8; i++)
                        {
                            stub.AddRange(new byte[] { 0x44, 0x0F, 0x10, (byte)(0x84 + (i * 8)), 0x24, (byte)(0x80 + (i * 16)), 0x00, 0x00, 0x00 });
                        }
                        stub.AddRange(new byte[] { 0x48, 0x81, 0xC4, 0x00, 0x01, 0x00, 0x00 }); // add rsp, 256

                        // 5. Restore GPRs & Flags
                        byte[] gprPops = new byte[] { 0x9D, 0x41, 0x5F, 0x41, 0x5E, 0x41, 0x5D, 0x41, 0x5C, 0x41, 0x5B, 0x41, 0x5A, 0x41, 0x59, 0x41, 0x58, 0x5D, 0x5F, 0x5E, 0x5A, 0x59, 0x5B, 0x58 };
                        stub.AddRange(gprPops);

                        // 6. Jump back to original RIP
                        stub.AddRange(new byte[] { 0x48, 0xB8 });
                        stub.AddRange(BitConverter.GetBytes(origRip));
                        stub.AddRange(new byte[] { 0xFF, 0xE0 }); // jmp rax

                        byte[] stubBytes = stub.ToArray();
                        Buffer.BlockCopy(stubBytes, 0, remoteBuffer, 0x200, stubBytes.Length);

                        // Write to remote process
                        if (!NativeMethods.WriteProcessMemory(hProcess, remoteMem, remoteBuffer, (UIntPtr)ALLOC_SIZE, out _))
                        {
                            int err = Marshal.GetLastWin32Error();
                            errorMessage = $"Failed to write shellcode trampoline to target memory.\nWin32 Error {err}: {new Win32Exception(err).Message}";
                            return false;
                        }

                        // Redirect RIP to Trampoline
                        ctx.Rip = (ulong)pShellcodeRemote.ToInt64();
                        if (!NativeMethods.SetThreadContext(hThread, ref ctx))
                        {
                            int err = Marshal.GetLastWin32Error();
                            errorMessage = $"Failed to set x64 thread context.\nWin32 Error {err}: {new Win32Exception(err).Message}";
                            return false;
                        }
                    }
                    else
                    {
                        // 7b. Capture 32-bit Thread Context (x86 / WoW64)
                        var ctx32 = new NativeMethods.CONTEXT_X86
                        {
                            ContextFlags = 0x0001003F // CONTEXT_X86_ALL
                        };

                        bool gotCtx;
                        if (Environment.Is64BitProcess)
                        {
                            gotCtx = NativeMethods.Wow64GetThreadContext(hThread, ref ctx32);
                        }
                        else
                        {
                            gotCtx = NativeMethods.GetThreadContext(hThread, ref ctx32);
                        }

                        if (!gotCtx)
                        {
                            int err = Marshal.GetLastWin32Error();
                            errorMessage = $"Failed to get x86 thread context.\nWin32 Error {err}: {new Win32Exception(err).Message}";
                            return false;
                        }

                        uint origEip = ctx32.Eip;

                        // Build Context-Safe x86 Shellcode Trampoline
                        var stub = new List<byte>();

                        // 1. pushad (Save EAX, ECX, EDX, EBX, ESP, EBP, ESI, EDI) + pushfd (Flags)
                        stub.Add(0x60); // pushad
                        stub.Add(0x9C); // pushfd

                        // 2. push pDllPathRemote
                        stub.Add(0x68);
                        stub.AddRange(BitConverter.GetBytes((uint)pDllPathRemote.ToInt32()));

                        // 3. mov eax, pLoadLibraryW
                        stub.Add(0xB8);
                        stub.AddRange(BitConverter.GetBytes((uint)pLoadLibraryW.ToInt32()));

                        // 4. call eax
                        stub.AddRange(new byte[] { 0xFF, 0xD0 });

                        // 5. mov [pStatusRemote], eax
                        stub.Add(0xA3);
                        stub.AddRange(BitConverter.GetBytes((uint)pStatusRemote.ToInt32()));

                        // 6. popfd + popad
                        stub.Add(0x9D); // popfd
                        stub.Add(0x61); // popad

                        // 7. push origEip + ret (Jump back to original EIP cleanly)
                        stub.Add(0x68);
                        stub.AddRange(BitConverter.GetBytes(origEip));
                        stub.Add(0xC3); // ret

                        byte[] stubBytes = stub.ToArray();
                        Buffer.BlockCopy(stubBytes, 0, remoteBuffer, 0x200, stubBytes.Length);

                        // Write to remote process
                        if (!NativeMethods.WriteProcessMemory(hProcess, remoteMem, remoteBuffer, (UIntPtr)ALLOC_SIZE, out _))
                        {
                            int err = Marshal.GetLastWin32Error();
                            errorMessage = $"Failed to write 32-bit shellcode trampoline.\nWin32 Error {err}: {new Win32Exception(err).Message}";
                            return false;
                        }

                        // Redirect EIP to Trampoline
                        ctx32.Eip = (uint)pShellcodeRemote.ToInt32();
                        bool setCtx;
                        if (Environment.Is64BitProcess)
                        {
                            setCtx = NativeMethods.Wow64SetThreadContext(hThread, ref ctx32);
                        }
                        else
                        {
                            setCtx = NativeMethods.SetThreadContext(hThread, ref ctx32);
                        }

                        if (!setCtx)
                        {
                            int err = Marshal.GetLastWin32Error();
                            errorMessage = $"Failed to set 32-bit thread context.\nWin32 Error {err}: {new Win32Exception(err).Message}";
                            return false;
                        }
                    }

                    // 8. Resume Hijacked Thread
                    NativeMethods.ResumeThread(hThread);

                    // 9. Wait for Injected Module to Initialize
                    var sw = Stopwatch.StartNew();
                    byte[] statusBuf = new byte[isTarget64 ? 8 : 4];
                    bool loaded = false;

                    while (sw.ElapsedMilliseconds < 8000)
                    {
                        if (NativeMethods.ReadProcessMemory(hProcess, pStatusRemote, statusBuf, (UIntPtr)statusBuf.Length, out _))
                        {
                            long statusVal = isTarget64 ? BitConverter.ToInt64(statusBuf, 0) : BitConverter.ToUInt32(statusBuf, 0);
                            if (statusVal != 0)
                            {
                                loaded = true;
                                break;
                            }
                        }
                        System.Threading.Thread.Sleep(20);
                    }

                    if (!loaded)
                    {
                        shouldFreeMem = false; // Keep memory mapped in case DLL is still loading
                        errorMessage = "Thread Hijacking timed out after 8 seconds. The thread may be blocked in a system wait or DllMain is still processing.";
                        return false;
                    }

                    return true;
                }
                finally
                {
                    // Ensure thread is resumed if an exception occurred before resume
                    NativeMethods.ResumeThread(hThread);
                }
            }
            catch (Exception ex)
            {
                errorMessage = $"Unexpected Thread Hijacking exception: {ex.Message}";
                return false;
            }
            finally
            {
                if (hThread != IntPtr.Zero)
                    NativeMethods.CloseHandle(hThread);

                if (shouldFreeMem && remoteMem != IntPtr.Zero && hProcess != IntPtr.Zero)
                    NativeMethods.VirtualFreeEx(hProcess, remoteMem, UIntPtr.Zero, NativeMethods.MEM_RELEASE);

                if (hProcess != IntPtr.Zero)
                    NativeMethods.CloseHandle(hProcess);
            }
        }

        private static IntPtr FindTargetThread(int processId, bool isTarget64, out uint threadId)
        {
            threadId = 0;
            IntPtr hSnap = NativeMethods.CreateToolhelp32Snapshot(0x00000004 /* TH32CS_SNAPTHREAD */, 0);
            if (hSnap == IntPtr.Zero || hSnap == (IntPtr)(-1))
                return IntPtr.Zero;

            IntPtr fallbackThread = IntPtr.Zero;
            uint fallbackId = 0;

            try
            {
                var te = new NativeMethods.THREADENTRY32 { dwSize = (uint)Marshal.SizeOf(typeof(NativeMethods.THREADENTRY32)) };
                if (NativeMethods.Thread32First(hSnap, ref te))
                {
                    do
                    {
                        if (te.th32OwnerProcessID == processId)
                        {
                            const uint THREAD_ACCESS = NativeMethods.THREAD_SUSPEND_RESUME |
                                                        NativeMethods.THREAD_GET_CONTEXT |
                                                        NativeMethods.THREAD_SET_CONTEXT |
                                                        NativeMethods.THREAD_QUERY_INFORMATION;

                            IntPtr hTh = NativeMethods.OpenThread(THREAD_ACCESS, false, te.th32ThreadID);
                            if (hTh != IntPtr.Zero)
                            {
                                if (isTarget64)
                                {
                                    var testCtx = new NativeMethods.CONTEXT_X64 { ContextFlags = 0x00100001 /* CONTEXT_CONTROL */ };
                                    if (NativeMethods.GetThreadContext(hTh, ref testCtx))
                                    {
                                        if (testCtx.Rip > 0x10000 && testCtx.Rip < 0x00007FFFFFFEFFFF)
                                        {
                                            threadId = te.th32ThreadID;
                                            if (fallbackThread != IntPtr.Zero) NativeMethods.CloseHandle(fallbackThread);
                                            return hTh;
                                        }
                                    }
                                }
                                else
                                {
                                    var testCtx32 = new NativeMethods.CONTEXT_X86 { ContextFlags = 0x00010001 /* CONTEXT_CONTROL */ };
                                    bool got = Environment.Is64BitProcess
                                        ? NativeMethods.Wow64GetThreadContext(hTh, ref testCtx32)
                                        : NativeMethods.GetThreadContext(hTh, ref testCtx32);
                                    if (got && testCtx32.Eip > 0x10000 && testCtx32.Eip < 0x7FFEFFFF)
                                    {
                                        threadId = te.th32ThreadID;
                                        if (fallbackThread != IntPtr.Zero) NativeMethods.CloseHandle(fallbackThread);
                                        return hTh;
                                    }
                                }

                                if (fallbackThread == IntPtr.Zero)
                                {
                                    fallbackThread = hTh;
                                    fallbackId = te.th32ThreadID;
                                }
                                else
                                {
                                    NativeMethods.CloseHandle(hTh);
                                }
                            }
                        }
                    } while (NativeMethods.Thread32Next(hSnap, ref te));
                }
            }
            finally
            {
                NativeMethods.CloseHandle(hSnap);
            }

            if (fallbackThread != IntPtr.Zero)
            {
                threadId = fallbackId;
                return fallbackThread;
            }

            return IntPtr.Zero;
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
