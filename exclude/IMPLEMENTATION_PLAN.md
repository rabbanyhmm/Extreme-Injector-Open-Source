# Extreme Injector Replica — Full Implementation Plan
> Architecture: Pure C# (.NET Framework 4.8 WinForms) · Single `Extreme Injector v3.exe` · Only dependency: `mscoree.dll` (built into Windows) · Zero files written to disk during operation
> All low-level Win32/NTAPI calls via C# P/Invoke — exactly how the original works.

---

## Architecture Overview

```
Extreme Injector v3.exe  (single file)
│
├── UI Layer          (WinForms)          MainForm, ProcessSelectForm,
│                                         ProcessInformationForm, SettingsForm,
│                                         AdvancedInjectionSettings, AdvancedScrambleSettings
│
├── Core Layer        (C# P/Invoke)       StandardInjector, LdrLoadDllInjector,
│                                         ThreadHijackInjector, ManualMapInjector
│
├── Post-Processing   (C# P/Invoke)       PeEraser, ModuleHider, PeScrambler
│
├── Config Layer                          Settings.cs, SettingsManager (XML)
│
├── Security / Privileges                    PrivilegeManager.cs (SeDebugPrivilege, Token Elevation)
│
└── Win32 / NTAPI                         NativeMethods.cs (kernel32, ntdll, psapi, user32)
```

**Key rule:** All injectors run inside the C# process. No DLL is dropped to disk. No child process is launched. Everything goes through `VirtualAllocEx` / `WriteProcessMemory` / shellcode stubs or NTAPI in the remote process.

---

## Dynamic Privilege Adaptation Architecture (Process Hacker Style)

```
                     ┌───────────────────────────────────────────────┐
                     │          Application Launch                   │
                     └───────────────────────┬───────────────────────┘
                                             │
                       Enable SeDebugPrivilege (RtlAdjustPrivilege)
                                             │
               ┌─────────────────────────────┼─────────────────────────────┐
               ▼                             ▼                             ▼
       Standard User Mode              Admin Privilege Mode           SYSTEM Privilege Mode
  (Non-Elevated Account)             (Elevated Administrator)      (NT AUTHORITY\SYSTEM)
 ─────────────────────────────     ───────────────────────────   ─────────────────────────────
 • Uses QUERY_LIMITED_INFO         • Access to all user & admin  • Full access to protected &
 • Enumerates all visible procs      processes                     system processes (LSASS, etc)
 • Queries modules & threads via   • Enables injection & module  • Maximum level operations
   Toolhelp32 snapshots              unloading across all users
 • Graceful fallback UI
```

### Multi-Tier Process Access Opening Cascade:
When opening a process handle for information, enumeration, module unloading, or injection, the engine uses a 4-level cascading fallback mechanism:

1. **Tier 1 (Maximum Access)**: `PROCESS_ALL_ACCESS` (`0x1F0FFF`)
2. **Tier 2 (Injection Access)**: `PROCESS_CREATE_THREAD | PROCESS_VM_OPERATION | PROCESS_VM_WRITE | PROCESS_VM_READ | PROCESS_QUERY_INFORMATION` (`0x043A`)
3. **Tier 3 (Limited Info Access)**: `PROCESS_QUERY_INFORMATION | PROCESS_VM_READ` (`0x0410`)
4. **Tier 4 (Minimum Query Access)**: `PROCESS_QUERY_LIMITED_INFORMATION` (`0x1000`)

---

## Function Completion Status

| # | Feature / Function | Status | Notes |
|---|---|---|---|
| **PROCESS & WINDOW UI** | | | |
| 1 | Process list — name, PID, icon | ✅ Done | `ProcessSelectForm`, Toolhelp32 snapshot |
| 2 | Process list — 32/64-bit badge | ✅ Done | `IsWow64Process` |
| 3 | Process list — architecture column | ✅ Done | |
| 4 | Window list — title, exe name | ✅ Done | `EnumWindows` |
| 5 | Select by window / process toggle | ✅ Done | |
| 6 | Auto-refresh process list | ✅ Done | Timer-based |
| **PROCESS INFORMATION** | | | |
| 7 | Process icon, name, PID display | ✅ Done | |
| 8 | Module list — name, base, size | ✅ Done | `EnumProcessModulesEx` + `TH32CS_SNAPMODULE` fallback |
| 9 | Module full path | ✅ Done | `GetModuleFileNameEx` |
| 10 | Unload module (FreeLibrary remote) | ✅ Done | `CreateRemoteThread` → `FreeLibrary` |
| 11 | Thread list — thread ID, priority | ✅ Done | `TH32CS_SNAPTHREAD` |
| 12 | **Thread start address — raw hex** | ✅ Done | Direct NTAPI `NtQueryInformationThread` (`ThreadQuerySetWin32StartAddress = 9`) via low-level P/Invoke |
| 13 | **Thread start address — symbol resolve** | ✅ Done | Fully resolves raw start address against exported functions (`Module!Export+0xOffset` / `Module+0xRVA`) |
| 14 | Thread suspend / resume | ✅ Done | `SuspendThread` / `ResumeThread` |
| 15 | Thread kill | ✅ Done | `TerminateThread` |
| 16 | Kill process | ✅ Done | `TerminateProcess` |
| 17 | Modules / Threads count summary label | ✅ Done | Displayed as `Modules: X | Threads: Y` in Process group box |
| 18 | Module column sort (click header) | ✅ Done | |
| 19 | Thread column sort (click header) | ✅ Done | |
| **INJECTION METHODS** | | | |
| 22 | **Standard Inject** (LoadLibraryW via CRT) | ✅ Done | PE architecture validation, `VirtualAllocEx` + `WriteProcessMemory` + `CreateRemoteThread` → `LoadLibraryW`, thread exit code verification (`GetExitCodeThread`), `VirtualFreeEx` cleanup, exact Win32 error formatting, and Extreme Injector error popup |
| 23 | **LdrLoadDll Inject** | ❌ Not Done | Allocate UNICODE_STRING + path in remote, `CreateRemoteThread` → `LdrLoadDll` from ntdll |
| 24 | **Thread Hijacking Inject** | ❌ Not Done | Snapshot threads, pick one, `SuspendThread`, `GetThreadContext`, write shellcode at `RIP`/`EIP`, `SetThreadContext`, `ResumeThread` |
| 25 | **Manual Map Inject** | ❌ Not Done | Parse PE, allocate in remote, fix relocations, resolve imports, write shellcode to call DllMain |
| **INJECTION OPTIONS** | | | |
| 26 | **Close on Inject** | ⚠️ Wired in Settings | `Application.Exit()` after inject — needs hookup in `MainForm` inject button handler |
| 27 | **Stealth Inject** | ❌ Not Done | Suppress `DLL_THREAD_ATTACH` callbacks. Patch `LdrShutdownThread` or use `NtCreateThreadEx` with `SKIP_THREAD_ATTACH` flag (0x2) |
| 28 | **Inject Delay** (ms before injection) | ✅ Done | `Task.Delay(options.Delay)` in `InjectionOrchestrator` |
| 29 | **Delay Between** (ms between each DLL) | ✅ Done | `Task.Delay(options.DelayBetween)` |
| 30 | **Auto Inject** (on process appear) | ❌ Not Done | Background polling timer watching for process name to appear, then fires injection automatically |
| **POST-INJECTION OPTIONS** | | | |
| 31 | **Erase PE Header** | ❌ Not Done | After injection: `OpenProcess(VM_WRITE)` + `VirtualProtectEx` to make header writable + `WriteProcessMemory` zero bytes over MZ/PE header |
| 32 | **Hide Module** (remove from PEB LDR list) | ❌ Not Done | Walk `PEB.Ldr.InMemoryOrderModuleList` in remote process, unlink the module's `LDR_DATA_TABLE_ENTRY` from all three list heads (InLoad, InMemory, InInit) |
| **SCRAMBLE OPTIONS** | | | |
| 33 | **Scramble DLL** (master switch) | ⚠️ Wired in Settings | Checkbox exists, not executed post-inject |
| 34 | ↳ Rename Sections | ❌ Not Done | |
| 35 | ↳ Remove Debug Data | ❌ Not Done | |
| 36 | ↳ Remove Useless Data | ❌ Not Done | |
| 37 | ↳ Shift Section Data | ❌ Not Done | |
| 38 | ↳ Shift Section Memory | ❌ Not Done | |
| 39 | ↳ Create Fake Debug Directory | ❌ Not Done | |
| 40 | ↳ Create New Entry Point | ❌ Not Done | |
| 41 | ↳ Insert Extra Sections | ❌ Not Done | |
| 42 | ↳ Modify Assembly Code (NOP padding) | ❌ Not Done | |
| 43 | ↳ Modify Import Table | ❌ Not Done | |
| 44 | ↳ Move Relocation Table | ❌ Not Done | |
| 45 | ↳ Scramble Header Fields | ❌ Not Done | |
| 46 | ↳ Strip Section Characteristics | ❌ Not Done | |
| **ADVANCED INJECTION OPTIONS** | | | |
| 47 | Disable Exception Support | ❌ Not Done | Only applies to Manual Map: skip SEH registration in shellcode |
| 48 | Disable SEH Validation | ❌ Not Done | Only applies to Manual Map: skip SafeSEH/SEHOP chain walk |
| 49 | Hide From Debugger | ❌ Not Done | Write shellcode to call `NtSetInformationThread(ThreadHideFromDebugger)` on the injected thread |
| 50 | Manual Resolve Imports | ❌ Not Done | Only applies to Manual Map: resolve each IAT entry by walking PEB module list manually |
| **START IN SECURE MODE** | | | |
| 51 | Start in Secure Mode | ❌ Not Done | Spawn self with `CREATE_PROTECTED_PROCESS` flag + disable any AV hooks on startup |
| **SETTINGS / UI** | | | |
| 52 | Color themes (Background 1, Background 2, Text Color) | ✅ Done | `ThemeManager` + `ThemeChanged` event |
| 53 | Theme live preview | ✅ Done | Fires on picker change |
| 54 | Settings save / load (XML) | ✅ Done | `SettingsManager` |
| 55 | DLL list (enable/disable per item) | ✅ Done | Checkbox list |
| 56 | Export / Parameters per DLL item | ✅ Done | `DllItemConfigForm` |
| 57 | About form | ✅ Done | |
| **ADVANCED SCRAMBLE SETTINGS DEFAULTS** | | | |
| 58 | All scramble checkboxes default to TRUE | ❌ Not Done | `ScrambleConfig` defaults all false — need flip to true |

---

## Summary Counts

| Category | Done | In Progress | Not Done | Total |
|---|---|---|---|---|
| Process & Window UI | 6 | 0 | 0 | 6 |
| Process Information | 13 | 2 | 3 | 18 |
| Injection Methods | 1 | 0 | 3 | 4 |
| Injection Options | 3 | 1 | 1 | 5 |
| Post-Injection Options | 0 | 0 | 2 | 2 |
| Scramble Options | 0 | 1 | 12 | 13 |
| Advanced Injection Options | 0 | 0 | 4 | 4 |
| Start in Secure Mode | 0 | 0 | 1 | 1 |
| Settings / UI | 6 | 0 | 1 | 7 |
| **TOTAL** | **29** | **4** | **27** | **60** |

---

## Detailed Feature Specs

---

### 🟢 Standard Inject
**How it works:**
1. `OpenProcess(PROCESS_ALL_ACCESS)` on target PID
2. `VirtualAllocEx` → allocate `len(dllPath * 2 + 2)` bytes with `PAGE_READWRITE`
3. `WriteProcessMemory` → write UTF-16 DLL path string
4. `GetProcAddress(kernel32, "LoadLibraryW")` → resolve address in our own process (same address in target since kernel32 is always at same base via ASLR-shared mapping)
5. `CreateRemoteThread` → start thread at `LoadLibraryW`, pass remote path buffer as arg
6. `WaitForSingleObject(hThread, 5000)` → wait for it
7. `VirtualFreeEx` → clean up remote path buffer
8. `CloseHandle` everything

**Compatible with:** Erase PE, Hide Module, Scramble, all post-injection options
**Not compatible with:** Stealth Inject in its raw form (uses standard thread creation visible to debuggers)

---

### 🔴 LdrLoadDll Inject
**How it works:**
1. Resolve `ntdll!LdrLoadDll` address in the target process
2. Allocate remote memory for:
   - Wide string DLL path buffer
   - `UNICODE_STRING` struct pointing to path
   - `HMODULE` output variable
3. Write shellcode stub (x86/x64) that:
   - Calls `LdrLoadDll(NULL, 0, &unicodeStr, &hModule)` 
   - Returns cleanly
4. `CreateRemoteThread` / `NtCreateThreadEx` to execute the stub

**Compatible with:** Erase PE, Hide Module, Scramble
**Not compatible with:** Stealth Inject (LdrLoadDll itself triggers Ldr notifications)

> **Note:** Requires knowing whether target is 32-bit or 64-bit to use correct calling convention in shellcode

---

### 🔴 Thread Hijacking Inject
**How it works:**
1. Enumerate threads with `TH32CS_SNAPTHREAD`, pick a non-critical one
2. `OpenThread(THREAD_GET_CONTEXT | THREAD_SET_CONTEXT | THREAD_SUSPEND_RESUME)`
3. `SuspendThread` the target thread
4. `GetThreadContext` to capture current register state (save `RIP`/`EIP`)
5. Allocate shellcode in remote process that:
   - Saves all volatile registers (`pushad`/`push rax`...) 
   - Calls `LoadLibraryW(remotePath)` 
   - Restores registers
   - Jumps back to original `RIP`/`EIP`
6. `SetThreadContext` → redirect `RIP`/`EIP` to shellcode
7. `ResumeThread` → execute shellcode

**Compatible with:** Erase PE, Hide Module, Scramble
**Not compatible with:** Stealth Inject (thread hijacking is detectable by timing tools)
**Important:** Must use correct context struct (`CONTEXT` for x86, `CONTEXT64` for x64). Requires the target bitness to match or WOW64 aware patching

---

### 🔴 Manual Map Inject
**How it works:**
1. Read DLL file into memory (never on disk in target)
2. Parse PE headers (`IMAGE_DOS_HEADER` → `IMAGE_NT_HEADERS`)
3. Allocate `SizeOfImage` bytes in target at preferred base (or anywhere)
4. Copy PE sections into remote memory
5. **Fix relocations:** apply base relocation delta for each `IMAGE_BASE_RELOCATION` entry
6. **Resolve imports:** walk `IMAGE_IMPORT_DESCRIPTOR`, for each DLL:
   - Find it in target via PEB module list walking
   - Resolve each function address
   - Write into remote IAT
7. **Shellcode:** write x86/x64 stub that calls `DllMain(hModule, DLL_PROCESS_ATTACH, NULL)`
8. `CreateRemoteThread` / `NtCreateThreadEx` to execute stub

**Compatible with:** Erase PE (always, since we control the mapping), Scramble
**Not compatible with:** Hide Module (already not in PEB), standard LDR tracking
**Advanced Options (Manual Map only):**
- `DisableExceptionSupport`: skip `RtlAddFunctionTable` in shellcode
- `DisableSEHValidation`: skip writing SEH registration on x86
- `ManualResolveImports`: walk PEB manually instead of `GetProcAddress`

---

### 🔴 Stealth Inject
**How it works:**
- Uses `NtCreateThreadEx` (NTAPI) instead of `CreateRemoteThread`
- Passes flag `0x0004` (`THREAD_CREATE_FLAGS_SKIP_THREAD_ATTACH`) in `CreateFlags` parameter
- This suppresses `DLL_THREAD_ATTACH` notification to already-loaded DLLs
- Additionally patches `ntdll!LdrpDllNotificationList` head to skip notifications

**Compatible with:** Standard Inject, LdrLoadDll Inject
**Not compatible with:** Thread Hijacking (no thread creation), Manual Map (use `NtCreateThreadEx` directly in that method)

> **P/Invoke signature:**
> ```csharp
> [DllImport("ntdll.dll")]
> static extern int NtCreateThreadEx(
>     out IntPtr hThread, uint DesiredAccess, IntPtr ObjectAttributes,
>     IntPtr ProcessHandle, IntPtr lpStartAddress, IntPtr lpParameter,
>     uint Flags,  // 0x0004 = skip thread attach
>     UIntPtr StackZeroBits, UIntPtr SizeOfStackCommit, UIntPtr SizeOfStackReserve,
>     IntPtr lpBytesBuffer);
> ```

---

### 🔴 Erase PE Header
**How it works:**
After successful injection:
1. Find the module base in target (scan `PEB.Ldr` or use `EnumProcessModulesEx` result)
2. `VirtualProtectEx(hProcess, moduleBase, 0x1000, PAGE_EXECUTE_READWRITE, &old)` — make header writable
3. `WriteProcessMemory` — write 0x1000 zero bytes over the MZ/PE header
4. `VirtualProtectEx` — restore original protection

**Compatible with:** Standard, LdrLoadDll, Thread Hijacking
**Not compatible with:** Manual Map (PE never mapped with standard header — you control the memory)

---

### 🔴 Hide Module (Remove from PEB LDR)
**How it works:**
1. Find the module's `LDR_DATA_TABLE_ENTRY` in target process PEB
   - `NtQueryInformationProcess(ProcessBasicInformation)` → get `PEB` address
   - Read `PEB.Ldr` → `PEB_LDR_DATA`
   - Walk all three `LIST_ENTRY` chains: `InLoadOrder`, `InMemoryOrder`, `InInitializationOrder`
2. For each chain: write the forward/back link pointers to skip over this module's entry (`entry.Flink.Blink = entry.Blink`, `entry.Blink.Flink = entry.Flink`)
3. Zero out the `LDR_DATA_TABLE_ENTRY.BaseDllName`, `FullDllName` strings

**Compatible with:** Standard, LdrLoadDll, Thread Hijacking
**Not compatible with:** Manual Map (module was never in PEB, nothing to unlink)
**Note:** Does NOT make module invisible to `EnumProcessModulesEx` (kernel-side). Only hides from user-mode PEB walkers and debuggers

---

### 🔴 Scramble DLL (PeScrambler)
Applied to the in-memory image in the **target process** after injection (not the file on disk).

Each sub-option:

| Option | How it works | Method compat |
|---|---|---|
| **Rename Sections** | `WriteProcessMemory` → overwrite `.text`, `.rdata`, `.data` strings in section headers with random names | All |
| **Remove Debug Data** | Zero out `IMAGE_DATA_DIRECTORY[6]` (Debug directory) RVA+size in remote PE header | All |
| **Remove Useless Data** | Zero `IMAGE_DATA_DIRECTORY[4]` (Security/Certificate) and `[5]` (Base Reloc after fixup) | All |
| **Shift Section Data** | Allocate new remote buffer, copy section data with random padding prepended, update section `PointerToRawData` | Manual Map only |
| **Shift Section Memory** | `VirtualAllocEx` new region, `ReadProcessMemory` each section, write to new location with delta, update section `VirtualAddress` | All |
| **Create Fake Debug Directory** | Write a fake `IMAGE_DEBUG_DIRECTORY` with garbage `TimeDateStamp` and `PdbFileName` | All |
| **Create New Entry Point** | Write a small stub at new `VirtualAddress`, update `OptionalHeader.AddressOfEntryPoint` | All |
| **Insert Extra Sections** | Append fake section headers with random names and sizes | All |
| **Modify Assembly Code** | Walk `.text` section bytes, insert random NOP sleds at function boundaries | All |
| **Modify Import Table** | Scramble `OriginalFirstThunk` names/ordinals while preserving `FirstThunk` (actual IAT) | Standard, LdrLoadDll, Thread Hijacking |
| **Move Relocation Table** | Allocate new region, copy reloc table there, update `IMAGE_DATA_DIRECTORY[5]` | All |
| **Scramble Header Fields** | Randomize `FileHeader.TimeDateStamp`, `FileHeader.Checksum`, `OptionalHeader.MajorImageVersion` etc | All |
| **Strip Section Characteristics** | Zero out `IMAGE_SECTION_HEADER.Characteristics` flags (doesn't affect execution but confuses static analyzers) | All |

> **All Advanced Scramble Settings must default to `true` (checked)**

---

### 🔴 Advanced Injection Settings (per method)

| Option | How it works | Compatible Methods |
|---|---|---|
| **Disable Exception Support** | In Manual Map shellcode: skip `RtlAddFunctionTable` call (x64) / skip SEH chain registration (x86) | Manual Map only |
| **Disable SEH Validation** | Skip writing the SEH handler chain pointer in x86 shellcode stub | Manual Map (x86 only) |
| **Hide From Debugger** | After injecting: remote thread calls `NtSetInformationThread(hThread, ThreadHideFromDebugger=17, NULL, 0)` | All |
| **Manual Resolve Imports** | In Manual Map: walk `PEB.Ldr` module list manually instead of `GetProcAddress` to resolve IAT | Manual Map only |

---

### 🔴 Start in Secure Mode
**How it works:**
- On startup: re-launch self with modified ACL (restrict `PROCESS_VM_READ` from other processes)
- Optionally: unlink `ntdll!LdrpDllNotificationList` head to suppress our own load notifications

---

### 🔴 Auto Inject
**How it works:**
1. `MainForm` timer fires every 500ms
2. Check if target process name is running via `TH32CS_SNAPPROCESS` 
3. If found and not yet injected: trigger `InjectionOrchestrator.ExecuteInjectionAsync()`
4. Optionally show tray notification on success

---

## Implementation Priority Order

```
Phase 1 — Fix existing bugs / complete UI
  [1a] Thread start address: add NtQueryInformationThread P/Invoke → replace ProcessThread.StartAddress
  [1b] Process info: add Working Set size + elevation badge + create time
  [1c] ScrambleConfig: flip all defaults to true
  [1d] Close on Inject: wire up in MainForm inject handler

Phase 2 — Injection methods
  [2a] LdrLoadDll Inject (C# shellcode stub, x86 + x64)
  [2b] Thread Hijacking Inject (CONTEXT capture, shellcode, SetThreadContext)
  [2c] Stealth Inject flag (NtCreateThreadEx wrapper)

Phase 3 — Post-injection processing
  [3a] Erase PE Header
  [3b] Hide Module (PEB LDR unlink via ReadProcessMemory/WriteProcessMemory)

Phase 4 — PE Scrambler
  [4a] PeScrambler class — each option as separate method
  [4b] Wire into InjectionOrchestrator post-inject

Phase 5 — Manual Map
  [5a] PE parser already in PeParser.cs — extend for reloc + import resolution
  [5b] Shellcode stubs (x86 / x64 DllMain caller)
  [5c] Advanced options (disable exception support, manual imports)

Phase 6 — Auto Inject + Secure Mode
```

---

## Low-Level C# P/Invoke Additions Needed (NativeMethods.cs)

```csharp
// Thread start address (real low-level address, works on all processes)
[DllImport("ntdll.dll")] static extern int NtQueryInformationThread(
    IntPtr ThreadHandle, uint ThreadInformationClass,
    ref IntPtr ThreadInformation, uint ThreadInformationLength, out uint ReturnLength);
// ThreadQuerySetWin32StartAddress = 9

// Process basic info → PEB address (for Hide Module)
[DllImport("ntdll.dll")] static extern int NtQueryInformationProcess(
    IntPtr ProcessHandle, uint ProcessInformationClass,
    ref PROCESS_BASIC_INFORMATION ProcessInformation,
    uint ProcessInformationLength, out uint ReturnLength);

// Stealth thread creation
[DllImport("ntdll.dll")] static extern int NtCreateThreadEx(
    out IntPtr hThread, uint DesiredAccess, IntPtr ObjectAttributes,
    IntPtr ProcessHandle, IntPtr lpStartAddress, IntPtr lpParameter,
    uint Flags, UIntPtr StackZeroBits, UIntPtr SizeOfStackCommit,
    UIntPtr SizeOfStackReserve, IntPtr lpBytesBuffer);

// Hide thread from debugger
[DllImport("ntdll.dll")] static extern int NtSetInformationThread(
    IntPtr ThreadHandle, uint ThreadInformationClass,
    IntPtr ThreadInformation, uint ThreadInformationLength);
// ThreadHideFromDebugger = 17

// Memory info for working set
[DllImport("psapi.dll")] static extern bool GetProcessMemoryInfo(
    IntPtr hProcess, out PROCESS_MEMORY_COUNTERS counters, uint cb);

// Elevation check
[DllImport("advapi32.dll")] static extern bool GetTokenInformation(
    IntPtr TokenHandle, TOKEN_INFORMATION_CLASS TokenInformationClass,
    IntPtr TokenInformation, uint TokenInformationLength, out uint ReturnLength);

// Process times
[DllImport("kernel32.dll")] static extern bool GetProcessTimes(
    IntPtr hProcess, out FILETIME lpCreationTime, out FILETIME lpExitTime,
    out FILETIME lpKernelTime, out FILETIME lpUserTime);
```
