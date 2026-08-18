//
// ProcessEnum.cpp — Low-level process, window, module and thread enumeration
// Uses only: kernel32.dll, ntdll.dll, user32.dll, psapi.lib, version.lib
// Zero external dependencies. Zero disk writes.
//
#include <windows.h>
#include <tlhelp32.h>
#include <psapi.h>
#include <strsafe.h>

// winternl.h has its own NtQueryInformationThread forward declare — include BEFORE our typedef
// We will NOT use winternl.h and instead forward-declare what we need manually below

#include "../include/InjectorCore.h"

// ---------------------------------------------------------------------------
// Manual NTDLL forward declarations (avoids winternl.h collision)
// ---------------------------------------------------------------------------
typedef LONG (NTAPI* pfnNtQueryInformationThread)(
    HANDLE  ThreadHandle,
    DWORD   ThreadInformationClass,
    PVOID   ThreadInformation,
    ULONG   ThreadInformationLength,
    PULONG  ReturnLength
);
#define ThreadQuerySetWin32StartAddress 9

typedef LONG (NTAPI* pfnNtSuspendThread)(HANDLE, PULONG);

// ---------------------------------------------------------------------------
// Internal helpers
// ---------------------------------------------------------------------------

static BOOL IsProcessElevated(HANDLE hProcess)
{
    HANDLE hToken = NULL;
    if (!OpenProcessToken(hProcess, TOKEN_QUERY, &hToken)) return FALSE;
    TOKEN_ELEVATION elev = {};
    DWORD cb = sizeof(elev);
    BOOL ok = GetTokenInformation(hToken, TokenElevation, &elev, cb, &cb);
    CloseHandle(hToken);
    return ok ? (BOOL)elev.TokenIsElevated : FALSE;
}

static BOOL IsProcess64Bit(HANDLE hProcess)
{
#if defined(_WIN64)
    BOOL isWow64 = FALSE;
    if (!IsWow64Process(hProcess, &isWow64)) return TRUE;
    return !isWow64;
#else
    (void)hProcess;
    return FALSE;
#endif
}

static void GetProcessDescriptionFromPath(const WCHAR* path, WCHAR* descOut, DWORD descMax)
{
    descOut[0] = L'\0';
    DWORD dummy = 0;
    DWORD sz = GetFileVersionInfoSizeW(path, &dummy);
    if (sz == 0) return;

    void* pBuf = HeapAlloc(GetProcessHeap(), 0, sz);
    if (!pBuf) return;

    if (GetFileVersionInfoW(path, 0, sz, pBuf))
    {
        struct LANGCODEPAGE { WORD language; WORD codepage; } *pTranslate = NULL;
        UINT cbTranslate = 0;
        if (VerQueryValueW(pBuf, L"\\VarFileInfo\\Translation", (LPVOID*)&pTranslate, &cbTranslate)
            && cbTranslate >= sizeof(*pTranslate))
        {
            WCHAR subBlock[64] = {};
            StringCchPrintfW(subBlock, 64, L"\\StringFileInfo\\%04x%04x\\FileDescription",
                pTranslate[0].language, pTranslate[0].codepage);
            WCHAR* pDesc = NULL;
            UINT cbDesc = 0;
            if (VerQueryValueW(pBuf, subBlock, (LPVOID*)&pDesc, &cbDesc)
                && cbDesc > 1 && pDesc[0] != L'\0')
            {
                StringCchCopyNW(descOut, descMax, pDesc, cbDesc);
            }
        }
    }
    HeapFree(GetProcessHeap(), 0, pBuf);
}

static BOOL GetProcessFullPath(DWORD pid, WCHAR* pathOut, DWORD pathMax)
{
    pathOut[0] = L'\0';
    HANDLE hProc = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, FALSE, pid);
    if (!hProc) return FALSE;
    BOOL ok = QueryFullProcessImageNameW(hProc, 0, pathOut, &pathMax);
    CloseHandle(hProc);
    return ok;
}

// ---------------------------------------------------------------------------
// EnumProcessList
// ---------------------------------------------------------------------------
CORE_API BOOL EnumProcessList(PROCESS_ENTRY* pOut, DWORD maxCount, DWORD* pActual)
{
    if (!pOut || !pActual || maxCount == 0) return FALSE;
    *pActual = 0;

    HANDLE hSnap = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
    if (hSnap == INVALID_HANDLE_VALUE) return FALSE;

    PROCESSENTRY32W pe = {};
    pe.dwSize = sizeof(pe);

    if (!Process32FirstW(hSnap, &pe)) { CloseHandle(hSnap); return FALSE; }

    do
    {
        if (*pActual >= maxCount) break;
        PROCESS_ENTRY* ent = &pOut[*pActual];
        ZeroMemory(ent, sizeof(*ent));

        ent->ProcessId       = pe.th32ProcessID;
        ent->ParentProcessId = pe.th32ParentProcessID;
        StringCchCopyW(ent->ExeName, 260, pe.szExeFile);

        WCHAR fullPath[1024] = {};
        if (GetProcessFullPath(pe.th32ProcessID, fullPath, ARRAYSIZE(fullPath)))
        {
            StringCchCopyW(ent->FullPath, 1024, fullPath);
            GetProcessDescriptionFromPath(fullPath, ent->Description, 512);
        }

        HANDLE hProc = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, FALSE, pe.th32ProcessID);
        if (hProc)
        {
            ent->Is64Bit = IsProcess64Bit(hProc);
            CloseHandle(hProc);
        }

        (*pActual)++;
    }
    while (Process32NextW(hSnap, &pe));

    CloseHandle(hSnap);
    return TRUE;
}

// ---------------------------------------------------------------------------
// EnumWindowList
// ---------------------------------------------------------------------------
struct WndEnumCtx
{
    WINDOW_ENTRY* pOut;
    DWORD         maxCount;
    DWORD         actual;
};

static BOOL CALLBACK WndEnumProc(HWND hWnd, LPARAM lParam)
{
    WndEnumCtx* ctx = (WndEnumCtx*)lParam;
    if (ctx->actual >= ctx->maxCount) return FALSE;
    if (!IsWindowVisible(hWnd)) return TRUE;

    WCHAR title[512] = {};
    if (GetWindowTextW(hWnd, title, 512) == 0) return TRUE;

    WINDOW_ENTRY* ent = &ctx->pOut[ctx->actual];
    ZeroMemory(ent, sizeof(*ent));

    ent->hWnd = hWnd;
    StringCchCopyW(ent->WindowTitle, 512, title);
    GetWindowThreadProcessId(hWnd, &ent->ProcessId);

    GetClassNameW(hWnd, ent->ClassName, 256);

    HANDLE hProc = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, FALSE, ent->ProcessId);
    if (hProc)
    {
        WCHAR exePath[1024] = {};
        DWORD sz = ARRAYSIZE(exePath);
        if (QueryFullProcessImageNameW(hProc, 0, exePath, &sz))
        {
            const WCHAR* lastName = wcsrchr(exePath, L'\\');
            StringCchCopyW(ent->ExeName, 260, lastName ? lastName + 1 : exePath);
        }
        CloseHandle(hProc);
    }

    ctx->actual++;
    return TRUE;
}

CORE_API BOOL EnumWindowList(WINDOW_ENTRY* pOut, DWORD maxCount, DWORD* pActual)
{
    if (!pOut || !pActual || maxCount == 0) return FALSE;
    *pActual = 0;
    WndEnumCtx ctx = {pOut, maxCount, 0};
    EnumWindows(WndEnumProc, (LPARAM)&ctx);
    *pActual = ctx.actual;
    return TRUE;
}

// ---------------------------------------------------------------------------
// GetProcessDetail
// ---------------------------------------------------------------------------
CORE_API BOOL GetProcessDetail(DWORD processId, PROCESS_DETAIL* pDetail)
{
    if (!pDetail) return FALSE;
    ZeroMemory(pDetail, sizeof(*pDetail));
    pDetail->ProcessId = processId;

    HANDLE hProc = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, FALSE, processId);
    if (!hProc) hProc = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, FALSE, processId);
    if (!hProc) return FALSE;

    DWORD pathLen = ARRAYSIZE(pDetail->FullPath);
    QueryFullProcessImageNameW(hProc, 0, pDetail->FullPath, &pathLen);

    const WCHAR* lastName = wcsrchr(pDetail->FullPath, L'\\');
    StringCchCopyW(pDetail->ExeName, 260, lastName ? lastName + 1 : pDetail->FullPath);

    if (pDetail->FullPath[0])
        GetProcessDescriptionFromPath(pDetail->FullPath, pDetail->Description, 512);

    pDetail->Is64Bit   = IsProcess64Bit(hProc);
    pDetail->IsElevated = IsProcessElevated(hProc);

    PROCESS_MEMORY_COUNTERS pmc = {sizeof(pmc)};
    if (GetProcessMemoryInfo(hProc, &pmc, sizeof(pmc)))
        pDetail->WorkingSetSize = pmc.WorkingSetSize;

    FILETIME ftCreate = {}, ftExit = {}, ftKernel = {}, ftUser = {};
    if (GetProcessTimes(hProc, &ftCreate, &ftExit, &ftKernel, &ftUser))
        pDetail->CreateTime = (ULONGLONG)ftCreate.dwHighDateTime << 32 | ftCreate.dwLowDateTime;

    CloseHandle(hProc);

    HANDLE hSnap = CreateToolhelp32Snapshot(TH32CS_SNAPTHREAD | TH32CS_SNAPMODULE | TH32CS_SNAPMODULE32, processId);
    if (hSnap != INVALID_HANDLE_VALUE)
    {
        THREADENTRY32 te = {sizeof(te)};
        if (Thread32First(hSnap, &te)) {
            do { if (te.th32OwnerProcessID == processId) pDetail->ThreadCount++; } while (Thread32Next(hSnap, &te));
        }
        MODULEENTRY32W me = {sizeof(me)};
        if (Module32FirstW(hSnap, &me)) {
            do { pDetail->ModuleCount++; } while (Module32NextW(hSnap, &me));
        }
        CloseHandle(hSnap);
    }
    return TRUE;
}

// ---------------------------------------------------------------------------
// EnumModuleList
// ---------------------------------------------------------------------------
CORE_API BOOL EnumModuleList(DWORD processId, MODULE_ENTRY* pOut, DWORD maxCount, DWORD* pActual)
{
    if (!pOut || !pActual || maxCount == 0) return FALSE;
    *pActual = 0;

    HANDLE hSnap = CreateToolhelp32Snapshot(TH32CS_SNAPMODULE | TH32CS_SNAPMODULE32, processId);
    if (hSnap == INVALID_HANDLE_VALUE) return FALSE;

    MODULEENTRY32W me = {sizeof(me)};
    if (!Module32FirstW(hSnap, &me)) { CloseHandle(hSnap); return FALSE; }

    do
    {
        if (*pActual >= maxCount) break;
        MODULE_ENTRY* ent = &pOut[*pActual];
        ZeroMemory(ent, sizeof(*ent));
        ent->BaseAddress = (PVOID)me.modBaseAddr;
        ent->SizeOfImage = me.modBaseSize;
        StringCchCopyW(ent->ModuleName, 260, me.szModule);
        StringCchCopyW(ent->FullPath, 1024, me.szExePath);
        (*pActual)++;
    }
    while (Module32NextW(hSnap, &me));

    CloseHandle(hSnap);
    return TRUE;
}

// ---------------------------------------------------------------------------
// EnumThreadList — start address via NtQueryInformationThread dynamically loaded
// ---------------------------------------------------------------------------
CORE_API BOOL EnumThreadList(DWORD processId, THREAD_ENTRY* pOut, DWORD maxCount, DWORD* pActual)
{
    if (!pOut || !pActual || maxCount == 0) return FALSE;
    *pActual = 0;

    pfnNtQueryInformationThread fnNtQIT = (pfnNtQueryInformationThread)(PVOID)
        GetProcAddress(GetModuleHandleW(L"ntdll.dll"), "NtQueryInformationThread");

    HANDLE hSnap = CreateToolhelp32Snapshot(TH32CS_SNAPTHREAD, 0);
    if (hSnap == INVALID_HANDLE_VALUE) return FALSE;

    THREADENTRY32 te = {sizeof(te)};
    if (!Thread32First(hSnap, &te)) { CloseHandle(hSnap); return FALSE; }

    do
    {
        if (te.th32OwnerProcessID != processId) continue;
        if (*pActual >= maxCount) break;

        THREAD_ENTRY* ent = &pOut[*pActual];
        ZeroMemory(ent, sizeof(*ent));
        ent->ThreadId     = te.th32ThreadID;
        ent->BasePriority = (DWORD)te.tpBasePri;

        HANDLE hThread = OpenThread(THREAD_QUERY_INFORMATION | THREAD_SUSPEND_RESUME, FALSE, te.th32ThreadID);
        if (hThread)
        {
            if (fnNtQIT)
                fnNtQIT(hThread, ThreadQuerySetWin32StartAddress, &ent->StartAddress, sizeof(PVOID), NULL);

            // Probe suspend count to determine state
            DWORD suspendCount = SuspendThread(hThread);
            if (suspendCount != (DWORD)-1)
            {
                ResumeThread(hThread);
                StringCchCopyW(ent->StateDescription, 64, suspendCount > 0 ? L"Suspended" : L"Running");
            }
            CloseHandle(hThread);
        }

        (*pActual)++;
    }
    while (Thread32Next(hSnap, &te));

    CloseHandle(hSnap);
    return TRUE;
}

// ---------------------------------------------------------------------------
// UnloadRemoteModule
// ---------------------------------------------------------------------------
CORE_API BOOL UnloadRemoteModule(DWORD processId, PVOID moduleBase)
{
    HANDLE hProc = OpenProcess(
        PROCESS_CREATE_THREAD | PROCESS_QUERY_INFORMATION | PROCESS_VM_OPERATION,
        FALSE, processId);
    if (!hProc) return FALSE;

    LPTHREAD_START_ROUTINE pFreeLibrary = (LPTHREAD_START_ROUTINE)(PVOID)
        GetProcAddress(GetModuleHandleW(L"kernel32.dll"), "FreeLibrary");

    HANDLE hThread = CreateRemoteThread(hProc, NULL, 0, pFreeLibrary, moduleBase, 0, NULL);
    if (!hThread) { CloseHandle(hProc); return FALSE; }

    WaitForSingleObject(hThread, 5000);
    CloseHandle(hThread);
    CloseHandle(hProc);
    return TRUE;
}

// ---------------------------------------------------------------------------
// SuspendProcess / ResumeProcess / KillProcess
// ---------------------------------------------------------------------------
CORE_API BOOL SuspendProcess(DWORD processId)
{
    HANDLE hSnap = CreateToolhelp32Snapshot(TH32CS_SNAPTHREAD, 0);
    if (hSnap == INVALID_HANDLE_VALUE) return FALSE;
    THREADENTRY32 te = {sizeof(te)};
    BOOL any = FALSE;
    if (Thread32First(hSnap, &te)) {
        do {
            if (te.th32OwnerProcessID == processId) {
                HANDLE ht = OpenThread(THREAD_SUSPEND_RESUME, FALSE, te.th32ThreadID);
                if (ht) { SuspendThread(ht); CloseHandle(ht); any = TRUE; }
            }
        } while (Thread32Next(hSnap, &te));
    }
    CloseHandle(hSnap);
    return any;
}

CORE_API BOOL ResumeProcess(DWORD processId)
{
    HANDLE hSnap = CreateToolhelp32Snapshot(TH32CS_SNAPTHREAD, 0);
    if (hSnap == INVALID_HANDLE_VALUE) return FALSE;
    THREADENTRY32 te = {sizeof(te)};
    BOOL any = FALSE;
    if (Thread32First(hSnap, &te)) {
        do {
            if (te.th32OwnerProcessID == processId) {
                HANDLE ht = OpenThread(THREAD_SUSPEND_RESUME, FALSE, te.th32ThreadID);
                if (ht) { ResumeThread(ht); CloseHandle(ht); any = TRUE; }
            }
        } while (Thread32Next(hSnap, &te));
    }
    CloseHandle(hSnap);
    return any;
}

CORE_API BOOL KillProcess(DWORD processId, DWORD exitCode)
{
    HANDLE hProc = OpenProcess(PROCESS_TERMINATE, FALSE, processId);
    if (!hProc) return FALSE;
    BOOL ok = TerminateProcess(hProc, exitCode);
    CloseHandle(hProc);
    return ok;
}

// DllMain — no initialization needed
BOOL WINAPI DllMain(HINSTANCE hinstDLL, DWORD fdwReason, LPVOID lpvReserved)
{
    (void)hinstDLL; (void)fdwReason; (void)lpvReserved;
    return TRUE;
}
