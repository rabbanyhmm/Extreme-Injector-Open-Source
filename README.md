# Extreme Injector (Open Source Replica)

<p align="center">
  <img src="Screenshot.png" alt="Extreme Injector v3 UI" width="480">
</p>

<p align="center">
  <a href="https://github.com/rabbanyhmm/Extreme-Injector-Open-Source"><img src="https://img.shields.io/badge/Status-Active%20Development-success" alt="Status"></a>
  <a href="https://dotnet.microsoft.com/"><img src="https://img.shields.io/badge/.NET%20Framework-4.8-512BD4?logo=dotnet" alt=".NET Framework 4.8"></a>
  <a href="https://microsoft.com/windows"><img src="https://img.shields.io/badge/Platform-Windows%20(x86%20%2F%20x64)-0078D6?logo=windows" alt="Platform"></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/License-MIT-green.svg" alt="License"></a>
  <img src="https://img.shields.io/badge/Dependencies-Zero%20External%20DLLs-brightgreen" alt="Dependencies">
</p>

An open-source, high-fidelity recreation of **Extreme Injector v3**, written entirely in **pure C# (.NET Framework 4.8 / Windows Forms)** utilizing direct low-level Win32 and NT kernel system APIs. 

Built as a **single standalone executable** (`Extreme Injector v3.exe`) with **zero external native DLL drops or disk dependencies**.

---

## Key Features & Architecture

```
                                  [ Extreme Injector v3 ]
                                             │
               ┌─────────────────────────────┼─────────────────────────────┐
               ▼                             ▼                             ▼
       [ Process Engine ]           [ Injection Matrix ]          [ Cloaking & Safety ]
       • Dual List (Proc/Win)       • Standard (LoadLibraryW)     • Stealth (SKIP_THREAD_ATTACH)
       • Window Crosshair Picker    • LdrLoadDll Stub (NTAPI)     • Hide From Debugger
       • Process Memory & Info      • Manual Map (In-Memory PE)   • Erase PE Header (4KB 0-fill)
       • SeDebugPrivilege Escalation • Cross-Bitness (WoW64/x64)   • PEB LDR Unlink (3 Lists)
```

### 1. Advanced Injection Engines
* **Standard Injection:** Classic remote thread creation (`CreateRemoteThread` / `NtCreateThreadEx` + `LoadLibraryW`).
* **LdrLoadDll / LdrpLoadDll Execution:** Allocates in-memory `UNICODE_STRING` structures and executes a position-independent `ntdll!LdrLoadDll` stub with human-readable `NTSTATUS` status decoding.
* **Manual Map PE Loader:** Pure C# in-memory PE loader with relocation fixing (`IMAGE_REL_BASED_DIR64` / `HIGHLOW`), import table resolution (IAT), TLS callbacks execution, structured exception handling (`RtlAddFunctionTable`), and `DllMain` dispatching.
* **Stealth Inject:** Direct kernel invocation via `NtCreateThreadEx` using `THREAD_CREATE_FLAGS_SKIP_THREAD_ATTACH (0x02)` to prevent loaded modules from detecting thread attachment.
* **Cross-Architecture Resolver:** Built-in `RemoteExportResolver` parses 32-bit export tables directly from `SysWOW64` to prevent 64-bit to 32-bit pointer truncation crashes when injecting into WoW64 processes.

### 2. Post-Injection Cloaking & Hardening
* **Erase PE Header:** Changes memory page permissions via `VirtualProtectEx` and zero-fills the initial 4096 bytes (`IMAGE_DOS_HEADER` & `IMAGE_NT_HEADERS`) in the target process.
* **Hide Module (PEB LDR Unlinking):** Traverses remote `PEB_LDR_DATA` chains across both native 64-bit and 32-bit (WoW64) PEBs to unlink `LDR_DATA_TABLE_ENTRY` from `InLoadOrderModuleList`, `InMemoryOrderModuleList`, and `InInitializationOrderModuleList`.
* **Process Concurrency Protection:** Synchronizes memory mutations using `NtSuspendProcess` / `NtResumeProcess` to eliminate race conditions.
* **Anti-Debugging:** Masks injection threads from attached debuggers using `NtSetInformationThread(ThreadHideFromDebugger)`.

### 3. User Interface & Workflow
* **Window Crosshair Drag Tool:** Drag-and-drop target window selector (`WindowPickerForm`) with real-time rectangle tracking.
* **Process Information Viewer:** Inspect memory regions, thread IDs, loaded modules, environment variables, and process mitigation policies.
* **Auto Inject & Delay Timers:** 400ms background polling watcher with PID deduplication, pre-injection delays, and inter-DLL execution delays.
* **Theme Customization & Persistence:** Full gradient styling (Background 1, Background 2, Text Color) with real-time live preview saved directly to `settings.xml`.

---

## Technical Feature Matrix

| Feature | Subsystem | Low-Level NTAPI / Mechanism | Status |
|---|---|---|:---:|
| **Standard Inject** | Core Engine | `CreateRemoteThreadSmart` + `kernel32!LoadLibraryW` | ✅ Implemented |
| **LdrLoadDll Inject** | Core Engine | In-Memory `UNICODE_STRING` + `ntdll!LdrLoadDll` FastCall/StdCall Stub | ✅ Implemented |
| **Manual Mapping** | Core Engine | In-Memory PE Parser + Relocations + IAT + TLS + SEH Unwinding | ✅ Implemented |
| **Cross-Bitness (WoW64)** | Core Engine | `RemoteExportResolver` via ToolHelp + SysWOW64 PE Export Parser | ✅ Implemented |
| **Stealth Inject** | Anti-Detection | `NtCreateThreadEx` with `THREAD_CREATE_FLAGS_SKIP_THREAD_ATTACH (0x02)` | ✅ Implemented |
| **Hide From Debugger** | Anti-Detection | `NtSetInformationThread(ThreadHideFromDebugger = 17)` | ✅ Implemented |
| **Erase PE Header** | Post-Processing | `VirtualProtectEx` + 0-fill remote `IMAGE_DOS_HEADER` & `IMAGE_NT_HEADERS` | ✅ Implemented |
| **Hide Module** | Post-Processing | Dual x86/x64 PEB `LDR_DATA_TABLE_ENTRY` Unlinking (3 lists) + Process Freeze | ✅ Implemented |
| **Auto Inject** | Automation | Async polling timer with PID deduplication and lifecycle management | ✅ Implemented |
| **Window Drag Picker** | UI / Tooling | Global mouse capture (`SetCapture`/`ReleaseCapture`) + Desktop DC Invalidation | ✅ Implemented |
| **Process Information** | Diagnostics | PEB / DEP / ASLR / Thread / Module / Memory region inspection | ✅ Implemented |
| **Thread Hijacking** | Core Engine | `SuspendThread` → `GetThreadContext` → Trampoline → `ResumeThread` | ⏳ In Progress |
| **PE Scrambler Engine** | Scrambler | 13 Binary Image Transformations (Header, Section, Directory, Imports) | ⏳ In Progress |

---

## Repository Structure

```text
Extreme-Injector-Open-Source/
├── src/
│   └── ExtremeInjectorReplica/
│       ├── Config/
│       │   ├── Settings.cs                     # XML Configuration model & serialization
│       │   └── SettingsManager.cs              # Persistence management (settings.xml)
│       ├── Core/
│       │   ├── NativeMethods.cs                # Win32, NTDLL, and Kernel32 P/Invoke signatures
│       │   ├── PrivilegeManager.cs             # SeDebugPrivilege & security token adjustments
│       │   ├── RemoteExportResolver.cs         # Cross-bitness (WoW64/x64) PE export resolution
│       │   ├── PeParser.cs                     # In-memory PE32 / PE32+ header & section parser
│       │   ├── StandardInjector.cs             # LoadLibraryW remote thread injection
│       │   ├── LdrLoadDllInjector.cs           # LdrLoadDll PIC shellcode engine
│       │   ├── ManualMapInjector.cs            # In-memory PE manual mapper with PIC shellcode
│       │   ├── PostProcessor.cs                # PE header erasing & PEB LDR unlinking
│       │   └── InjectionOrchestrator.cs        # Injection pipeline dispatcher & batch runner
│       ├── UI/
│       │   ├── MainForm.cs                     # Primary user interface & control routing
│       │   ├── ProcessSelectForm.cs            # Process & Window selector dialog
│       │   ├── WindowPickerForm.cs             # Crosshair drag-and-drop window target picker
│       │   ├── SettingsForm.cs                 # Main injection & theme settings dialog
│       │   ├── AdvancedInjectionSettingsForm.cs# Advanced anti-debug & manual map settings
│       │   ├── AdvancedScrambleSettingsForm.cs # 13-point PE scrambler configuration dialog
│       │   ├── ProcessInformationForm.cs       # Remote process diagnostics & module inspection
│       │   ├── DllItemConfigForm.cs            # Per-DLL custom export invocation settings
│       │   ├── AboutForm.cs                    # About & repository information
│       │   └── ThemeManager.cs                 # Custom GDI+ gradient control styling
│       ├── ExtremeInjector.ico                 # Application embedded icon
│       └── ExtremeInjectorReplica.csproj       # .NET Framework 4.8 project manifest
├── .gitignore
├── LICENSE
└── README.md
```

---

## Building from Source

### Prerequisites
* **Operating System:** Windows 10 or Windows 11 (64-bit recommended)
* **SDK:** [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) (or Visual Studio 2022 with .NET Desktop Development workload)

### Build Commands

```powershell
# 1. Clone the repository
git clone https://github.com/rabbanyhmm/Extreme-Injector-Open-Source.git
cd Extreme-Injector-Open-Source

# 2. Build Release configuration
dotnet build ./src/ExtremeInjectorReplica/ExtremeInjectorReplica.csproj -c Release

# 3. Output Binary Location
# ./src/ExtremeInjectorReplica/bin/Release/net48/Extreme Injector v3.exe
```

---

## License & Disclaimer

This project is licensed under the [MIT License](LICENSE).

> **Educational and Research Purpose:** This codebase is developed strictly for educational research, software reverse engineering, malware analysis defense, and system internals exploration. All trademarks and visual designs belong to their respective original authors.
