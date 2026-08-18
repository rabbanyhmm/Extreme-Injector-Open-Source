# Extreme Injector Replica — Full Implementation Plan & Settings Blueprint

> **Architecture:** Pure C# (.NET Framework 4.8 WinForms) · Single standalone `Extreme Injector v3.exe` · Only native dependency: `mscoree.dll` (built into Windows) · Zero external DLL drops to disk  
> All low-level Win32/NTAPI calls executed via C# P/Invoke.

---

## 1. Dynamic Privilege & Security Architecture (Process Hacker Style)

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
 • Enumerates visible procs          processes                     system processes
 • Filtered Process/Window list    • Enables injection & module  • Maximum level operations
 • Graceful User Mode fallback       unloading across all users    & security privilege tokens
```

---

## 2. Complete Settings & UI Controls Blueprint

Every single control across all dialogs is cataloged below with its persistence and functional status:

### A. Main Settings Dialog (`SettingsForm`)

| Category | UI Control | Control Type | XML Setting Key | Status | Description / Action |
|---|---|---|---|---|---|
| **Injection Method** | Method Selector | `ComboBox` (5 items) | `<Method>` | ⚠️ Partial | `0: Standard`, `1: Thread Hijacking`, `2: LdrLoadDll`, `3: LdrpLoadDll`, `4: Manual Map` |
| | Advanced Button | `Button` | — | ✅ Done | Opens `AdvancedInjectionSettingsForm` |
| **Scrambling Options** | Preset Selector | `ComboBox` (5 items) | `<Scramble>` preset | ⚠️ Partial | `None`, `Basic`, `Standard`, `Extreme`, `Custom` presets |
| | Advanced Button | `Button` | — | ✅ Done | Opens `AdvancedScrambleSettingsForm` |
| **Injection Options** | Auto Inject | `CheckBox` | `<AutoInject>` | ✅ Done | 400ms polling watcher with PID deduplication & lifecycle cleanup |
| | Close on inject | `CheckBox` | `<CloseOnInject>` | ✅ Done | Auto-closes application upon successful injection |
| | Stealth Inject | `CheckBox` | `<StealthInject>` | ❌ Not Done | Uses `NtCreateThreadEx` with `SKIP_THREAD_ATTACH (0x04)` |
| | Inject delay | `NumericUpDown` (0–60000ms) | `<Delay>` | ✅ Done | Delay before initial injection batch (`Task.Delay`) |
| | Delay between | `NumericUpDown` (0–60000ms) | `<DelayBetween>` | ✅ Done | Delay between consecutive DLL injections (`Task.Delay`) |
| **Post-Inject Options** | Erase PE | `CheckBox` | `<ErasePE>` | ❌ Not Done | Zero-fills remote `IMAGE_DOS_HEADER` & `IMAGE_NT_HEADERS` |
| | Hide Module | `CheckBox` | `<HideModule>` | ❌ Not Done | Unlinks `LDR_DATA_TABLE_ENTRY` from remote PEB loader lists |
| **Theme Options** | Text Color | `Panel` / ColorDialog | `<TextColor>` | ✅ Done | Real-time live preview & persistence across forms |
| | Background Color #1 | `Panel` / ColorDialog | `<Background1>` | ✅ Done | Gradient starting color with live preview |
| | Background Color #2 | `Panel` / ColorDialog | `<Background2>` | ✅ Done | Gradient ending color with live preview |
| **Tools** | View Process Info | `Button` | — | ✅ Done | Launches `ProcessInformationForm` for selected or picked PID |
| | Scramble DLL | `Button` | — | ❌ Not Done | Standalone DLL file scrambler tool (`SaveFileDialog`) |
| | Start in Secure Mode | `Button` | — | ❌ Not Done | Restricts process security descriptors & token ACLs |
| **Bottom Bar** | Reset | `Button` | — | ✅ Done | Restores all settings and colors to default values |
| | OK | `Button` | — | ✅ Done | Saves settings to XML and applies theme |

---

### B. Advanced Injection Settings Dialog (`AdvancedInjectionSettingsForm`)

| GroupBox | UI Control | Control Type | XML Setting Key | Status | Low-Level Kernel Mechanism |
|---|---|---|---|---|---|
| **General** | Hide threads from debugger | `CheckBox` | `<HideFromDebugger>` | ✅ Done | `NtSetInformationThread(hThread, ThreadHideFromDebugger = 17, NULL, 0)` |
| **Manual Map Options** | Manually map imports | `CheckBox` | `<ManualResolveImports>` | ❌ Not Done | Remote PEB module walk & custom export resolution |
| | Disable exception support | `CheckBox` | `<DisableExceptionSupport>` | ❌ Not Done | Omits exception directory registration in remote runtime |
| | Disable SEH validation | `CheckBox` | `<DisableSEHValidation>` | ❌ Not Done | Disables structured exception handler validation table |

---

### C. Advanced Scramble Settings Dialog (`AdvancedScrambleSettingsForm`) — All 13 Sub-Options

| GroupBox | # | Scramble Option Name | Control Type | XML Setting Key | Status | PE Manipulation Technique |
|---|---|---|---|---|---|---|
| **Header Options** | 1 | Scramble header fields | `CheckBox` | `<ScrambleHeaderFields>` | ❌ Not Done | Randomize PE checksum, TimeDateStamp, OS/linker version fields |
| | 2 | Remove useless data | `CheckBox` | `<RemoveUselessData>` | ❌ Not Done | Zero-fill DOS stub padding, Rich Header, and unused header fields |
| **Section Options** | 3 | Insert extra sections | `CheckBox` | `<InsertExtraSections>` | ❌ Not Done | Append dummy section headers filled with randomized byte sequences |
| | 4 | Shift section data | `CheckBox` | `<ShiftSectionData>` | ❌ Not Done | Shift file offsets (`PointerToRawData`) and pad raw alignment |
| | 5 | Modify assembly code | `CheckBox` | `<ModifyAssemblyCode>` | ❌ Not Done | Inject junk NOP sequences & harmless instruction permutations |
| | 6 | Rename sections | `CheckBox` | `<RenameSections>` | ❌ Not Done | Randomize standard names (`.text`, `.data`, `.rdata`) to random chars |
| | 7 | Shift section memory | `CheckBox` | `<ShiftSectionMemory>` | ❌ Not Done | Alter `VirtualAddress` offsets within allowed section alignments |
| | 8 | Strip section characteristics | `CheckBox` | `<StripSectionCharacteristics>` | ❌ Not Done | Strip non-essential `IMAGE_SCN_*` flags from section headers |
| | 9 | Create new entrypoint | `CheckBox` | `<CreateNewEntryPoint>` | ❌ Not Done | Generate a redirection trampoline stub as the new `AddressOfEntryPoint` |
| **Directory Options** | 10 | Modify import table | `CheckBox` | `<ModifyImportTable>` | ❌ Not Done | Shuffle import descriptor order and scramble module descriptor names |
| | 11 | Remove debug data | `CheckBox` | `<RemoveDebugData>` | ❌ Not Done | Zero out `IMAGE_DIRECTORY_ENTRY_DEBUG` and CodeView PDB path string |
| | 12 | Move relocation table | `CheckBox` | `<MoveRelocationTable>` | ❌ Not Done | Relocate `IMAGE_DIRECTORY_ENTRY_BASERELOC` to a relocated section |
| | 13 | Create fake debug directory | `CheckBox` | `<CreateFakeDebugDirectory>` | ❌ Not Done | Insert a synthetic PDB file path and fake debug GUID directory |

---

## 3. Prioritized Implementation Roadmap (Simplest to Hardest)

Below is the ordered implementation backlog arranged strictly from **simplest, easiest tasks** up to **most complex engineering tasks**:

| Priority | Task Name | Complexity | Category | Implementation Strategy | Status |
|---|---|---|---|---|---|
| **1** | **Auto Inject Timer** | 🟢 Easy | Feature | 400ms background polling timer with PID deduplication & lifecycle cleanup | ✅ **Done** |
| **2** | **Hide From Debugger** | 🟢 Easy | Advanced Option | `NtSetInformationThread(hThread, ThreadHideFromDebugger = 17)` on remote thread | ✅ **Done** |
| **3** | **Erase PE Header** | 🟡 Moderate | Post-Processing | `VirtualProtectEx` + zero-fill 0x1000 bytes over remote `IMAGE_DOS_HEADER` & `IMAGE_NT_HEADERS` | ⏳ Up Next |
| **4** | **Stealth Inject (`NtCreateThreadEx`)** | 🟡 Moderate | Injection Option | Call `NtCreateThreadEx` passing `THREAD_CREATE_FLAGS_SKIP_THREAD_ATTACH (0x0004)` flag | ⏳ Pending |
| **5** | **LdrLoadDll Inject** | 🟡 Moderate | Injection Method | Allocate `UNICODE_STRING` + DLL path in remote process; execute `ntdll!LdrLoadDll` stub | ⏳ Pending |
| **6** | **Hide Module (PEB LDR Unlink)** | 🟠 High | Post-Processing | Query PEB via `NtQueryInformationProcess`; walk `PEB_LDR_DATA` chains and unlink pointers | ⏳ Pending |
| **7** | **Thread Hijacking Inject** | 🟠 High | Injection Method | Suspend thread → capture `CONTEXT` (`GetThreadContext`) → write shellcode → update `RIP`/`EIP` → resume | ⏳ Pending |
| **8** | **PE Scrambler Engine (13 Options)** | 🔴 Complex | PE Scrambler | Implement `PeScrambler.cs` covering Header, Section, Directory, and Import table transformations | ⏳ Pending |
| **9** | **Manual Map Inject** | 🔴 Complex | Injection Method | Pure C# in-memory PE loader: section allocation, relocation fixing, IAT resolution, `DllMain` stub | ⏳ Pending |
| **10** | **Manual Map Advanced Options** | 🔴 Complex | Advanced Option | Manual import resolution via remote PEB walk and SEH/Exception directory registration | ⏳ Pending |
| **11** | **Start in Secure Mode** | 🔴 Complex | Protection | Restrict process security descriptors & token ACLs to prevent unauthorized inspection | ⏳ Pending |
