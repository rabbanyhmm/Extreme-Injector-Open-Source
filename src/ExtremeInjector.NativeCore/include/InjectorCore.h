#pragma once
#ifndef INJECTORCORE_H
#define INJECTORCORE_H

#define WIN32_LEAN_AND_MEAN
#include <windows.h>

#ifdef INJECTORCORE_EXPORTS
#define CORE_API extern "C" __declspec(dllexport)
#else
#define CORE_API extern "C" __declspec(dllimport)
#endif

// ---------------------------------------------------------------------------
// Process Information Structures
// ---------------------------------------------------------------------------

#pragma pack(push, 1)
typedef struct _PROCESS_ENTRY
{
    DWORD  ProcessId;
    DWORD  ParentProcessId;
    WCHAR  ExeName[260];
    WCHAR  FullPath[1024];
    WCHAR  Description[512];
    BOOL   Is64Bit;
    HICON  hIcon;           // Caller responsible for DestroyIcon()
} PROCESS_ENTRY;

typedef struct _WINDOW_ENTRY
{
    HWND   hWnd;
    DWORD  ProcessId;
    WCHAR  WindowTitle[512];
    WCHAR  ClassName[256];
    WCHAR  ExeName[260];
} WINDOW_ENTRY;

typedef struct _MODULE_ENTRY
{
    PVOID  BaseAddress;
    SIZE_T SizeOfImage;
    WCHAR  ModuleName[260];
    WCHAR  FullPath[1024];
} MODULE_ENTRY;

typedef struct _THREAD_ENTRY
{
    DWORD ThreadId;
    DWORD BasePriority;
    PVOID StartAddress;
    WCHAR StateDescription[64];
} THREAD_ENTRY;

typedef struct _PROCESS_DETAIL
{
    DWORD       ProcessId;
    WCHAR       ExeName[260];
    WCHAR       FullPath[1024];
    WCHAR       Description[512];
    BOOL        Is64Bit;
    BOOL        IsElevated;
    DWORD       ThreadCount;
    DWORD       ModuleCount;
    SIZE_T      WorkingSetSize;
    ULONGLONG   CreateTime;
} PROCESS_DETAIL;
#pragma pack(pop)

// ---------------------------------------------------------------------------
// Process & Window Enumeration Exports
// ---------------------------------------------------------------------------

// Enumerate all running processes.
// pOut     : caller-allocated array of PROCESS_ENTRY
// maxCount : capacity of pOut array
// pActual  : receives number of entries written
// Returns TRUE on success.
CORE_API BOOL EnumProcessList(
    PROCESS_ENTRY* pOut,
    DWORD          maxCount,
    DWORD*         pActual
);

// Enumerate all top-level visible windows.
CORE_API BOOL EnumWindowList(
    WINDOW_ENTRY* pOut,
    DWORD         maxCount,
    DWORD*        pActual
);

// ---------------------------------------------------------------------------
// Process Detail & Module/Thread Enumeration Exports
// ---------------------------------------------------------------------------

// Get full detail for a specific process by PID.
CORE_API BOOL GetProcessDetail(
    DWORD           processId,
    PROCESS_DETAIL* pDetail
);

// Enumerate all loaded modules inside a process.
CORE_API BOOL EnumModuleList(
    DWORD        processId,
    MODULE_ENTRY* pOut,
    DWORD         maxCount,
    DWORD*        pActual
);

// Enumerate all threads of a process with base priority and start address.
CORE_API BOOL EnumThreadList(
    DWORD        processId,
    THREAD_ENTRY* pOut,
    DWORD         maxCount,
    DWORD*        pActual
);

// Unload a module from a remote process by its base address.
CORE_API BOOL UnloadRemoteModule(
    DWORD processId,
    PVOID moduleBase
);

// Suspend / Resume all threads of a remote process.
CORE_API BOOL SuspendProcess(DWORD processId);
CORE_API BOOL ResumeProcess(DWORD processId);

// Kill a remote process cleanly.
CORE_API BOOL KillProcess(DWORD processId, DWORD exitCode);

#endif // INJECTORCORE_H
