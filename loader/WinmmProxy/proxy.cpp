#include <windows.h>
#include <winver.h>

#include <algorithm>
#include <cstdarg>
#include <cstdio>
#include <cstring>
#include <string>
#include <vector>

#include "exports.inc"

extern "C" FARPROC g_winmm[kWinmmCount] = {};

// A named namespace, not an unnamed one: an unnamed namespace's names carry a hash of this file's path, which would
// make the DLL's bytes depend on the folder it was built in.
namespace proxy {

struct LoaderCopy {
    std::wstring mod;  // the folder under mods\ that holds it
    std::wstring dir;  // <game>\mods\<mod>\loader
    ULONGLONG version;
    ULONGLONG written;
};

// Highest version, then written last, then first by folder name: the same copy on every start, in the game and in
// every worker it starts.
bool Better(const LoaderCopy& a, const LoaderCopy& b) {
    if (a.version != b.version) return a.version > b.version;
    if (a.written != b.written) return a.written > b.written;
    return CompareStringOrdinal(a.mod.c_str(), -1, b.mod.c_str(), -1, TRUE) == CSTR_LESS_THAN;
}

// hostfxr delegate types (hostfxr.h / coreclr_delegates.h)
using GetDelegateFn = int (*)(void* host, int type, void** fn);
using LoadAssemblyFn = int (*)(const wchar_t* path, void* context, void* reserved);
using GetFunctionPointerFn = int (*)(const wchar_t* type, const wchar_t* method, const wchar_t* delegateType,
                                     void* context, void* reserved, void** fn);
// The whole surface between a proxy and a Loader, the same in every version of both: any winmm.dll starts any copy.
using InitFn = void (*)(const wchar_t* copyFolder);

constexpr int kLoadAssemblyAndGetFunctionPointer = 5;
constexpr int kGetFunctionPointer = 6;
constexpr int kLoadAssembly = 7;
const auto kUnmanagedCallersOnly = reinterpret_cast<const wchar_t*>(-1);

// An installed mod is <game>\mods\<Id>, and brings its own copy of the Loader in <Id>\loader.
constexpr wchar_t kModsFolder[] = L"mods";
constexpr wchar_t kLoaderFolder[] = L"loader";
constexpr wchar_t kLoaderDll[] = L"MelvorMods.Loader.dll";

std::wstring g_copy;     // the chosen copy's folder; empty when no mod brought one
std::wstring g_logPath;  // <copy>\logs\proxy.log, once a copy is chosen
std::string g_pending;   // lines from before that
GetDelegateFn g_realGetDelegate;
bool g_loaded;

// A game with no Loader copy gets no file from us, so lines wait in memory until there is a copy to log beside.
void Log(const char* fmt, ...) {
    char line[1024];
    SYSTEMTIME t;
    GetLocalTime(&t);
    int n = snprintf(line, sizeof line, "%02d:%02d:%02d.%03d ", t.wHour, t.wMinute, t.wSecond, t.wMilliseconds);
    va_list args;
    va_start(args, fmt);
    vsnprintf(line + n, sizeof line - n, fmt, args);
    va_end(args);
    g_pending += line;
    g_pending += '\n';
    if (g_logPath.empty()) return;
    FILE* f = _wfopen(g_logPath.c_str(), L"a");
    if (!f) return;
    fwrite(g_pending.data(), 1, g_pending.size(), f);
    fclose(f);
    g_pending.clear();
}

std::string Utf8(const std::wstring& text) {
    const int size = WideCharToMultiByte(CP_UTF8, 0, text.data(), static_cast<int>(text.size()), nullptr, 0, nullptr, nullptr);
    std::string out(size, '\0');
    WideCharToMultiByte(CP_UTF8, 0, text.data(), static_cast<int>(text.size()), out.data(), size, nullptr, nullptr);
    return out;
}

std::string VersionText(ULONGLONG v) {
    char text[32];
    snprintf(text, sizeof text, "%u.%u.%u.%u", static_cast<unsigned>(v >> 48), static_cast<unsigned>(v >> 32 & 0xffff),
             static_cast<unsigned>(v >> 16 & 0xffff), static_cast<unsigned>(v & 0xffff));
    return text;
}

// The version the build wrote into the DLL's version resource; false for a file that has none or can't be read.
bool FileVersion(const std::wstring& path, ULONGLONG& version) {
    DWORD ignored;
    const DWORD size = GetFileVersionInfoSizeW(path.c_str(), &ignored);
    if (size == 0) return false;
    std::string data(size, '\0');
    VS_FIXEDFILEINFO* info = nullptr;
    UINT length = 0;
    if (!GetFileVersionInfoW(path.c_str(), 0, size, data.data())) return false;
    if (!VerQueryValueW(data.data(), L"\\", reinterpret_cast<void**>(&info), &length) || length < sizeof *info) return false;
    version = static_cast<ULONGLONG>(info->dwFileVersionMS) << 32 | info->dwFileVersionLS;
    return true;
}

// Every Loader copy under <game>\mods, best first. A copy that can't be read counts as not there.
std::vector<LoaderCopy> FindCopies(const std::wstring& game) {
    std::vector<LoaderCopy> copies;
    const std::wstring mods = game + L"\\" + kModsFolder;
    WIN32_FIND_DATAW entry;
    const HANDLE find = FindFirstFileW((mods + L"\\*").c_str(), &entry);
    if (find == INVALID_HANDLE_VALUE) return copies;
    do {
        if (!(entry.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) || entry.cFileName[0] == L'.') continue;
        LoaderCopy copy{entry.cFileName, mods + L"\\" + entry.cFileName + L"\\" + kLoaderFolder, 0, 0};
        const std::wstring dll = copy.dir + L"\\" + kLoaderDll;
        WIN32_FILE_ATTRIBUTE_DATA file;
        if (!GetFileAttributesExW(dll.c_str(), GetFileExInfoStandard, &file)) continue;
        if (!FileVersion(dll, copy.version)) continue;
        copy.written = static_cast<ULONGLONG>(file.ftLastWriteTime.dwHighDateTime) << 32 | file.ftLastWriteTime.dwLowDateTime;
        copies.push_back(copy);
    } while (FindNextFileW(find, &entry));
    FindClose(find);
    std::sort(copies.begin(), copies.end(), Better);
    return copies;
}

void LoadManaged(void* host) {
    const std::wstring path = g_copy + L"\\" + kLoaderDll;
    LoadAssemblyFn loadAssembly = nullptr;
    GetFunctionPointerFn getFunctionPointer = nullptr;
    int rc = g_realGetDelegate(host, kLoadAssembly, reinterpret_cast<void**>(&loadAssembly));
    if (rc == 0) rc = g_realGetDelegate(host, kGetFunctionPointer, reinterpret_cast<void**>(&getFunctionPointer));
    if (rc != 0) {
        Log("hostfxr delegates unavailable: 0x%08x", rc);
        return;
    }
    if ((rc = loadAssembly(path.c_str(), nullptr, nullptr)) != 0) {
        Log("load_assembly failed: 0x%08x", rc);
        return;
    }
    InitFn init = nullptr;
    rc = getFunctionPointer(L"MelvorMods.Loader.Entry, MelvorMods.Loader", L"Init", kUnmanagedCallersOnly, nullptr,
                            nullptr, reinterpret_cast<void**>(&init));
    if (rc != 0) {
        Log("Entry.Init not found: 0x%08x", rc);
        return;
    }
    Log("Loader loaded, calling Init");
    init(g_copy.c_str());
}

int HookedGetDelegate(void* host, int type, void** fn) {
    const int rc = g_realGetDelegate(host, type, fn);
    if (rc == 0 && type == kLoadAssemblyAndGetFunctionPointer && !g_loaded) {
        g_loaded = true;
        LoadManaged(host);
    }
    return rc;
}

FARPROC WINAPI HookedGetProcAddress(HMODULE module, LPCSTR name) {
    FARPROC proc = GetProcAddress(module, name);
    if (proc && !IS_INTRESOURCE(name) && strcmp(name, "hostfxr_get_runtime_delegate") == 0) {
        g_realGetDelegate = reinterpret_cast<GetDelegateFn>(proc);
        Log("hostfxr_get_runtime_delegate hooked");
        return reinterpret_cast<FARPROC>(&HookedGetDelegate);
    }
    return proc;
}

// Points the exe's GetProcAddress import at our hook.
int HookImport(void* target, void* replacement) {
    auto* base = reinterpret_cast<BYTE*>(GetModuleHandleW(nullptr));
    auto* nt = reinterpret_cast<IMAGE_NT_HEADERS*>(base + reinterpret_cast<IMAGE_DOS_HEADER*>(base)->e_lfanew);
    const auto& dir = nt->OptionalHeader.DataDirectory[IMAGE_DIRECTORY_ENTRY_IMPORT];
    int hooked = 0;
    for (auto* imp = reinterpret_cast<IMAGE_IMPORT_DESCRIPTOR*>(base + dir.VirtualAddress); imp->Name; ++imp) {
        for (auto* thunk = reinterpret_cast<IMAGE_THUNK_DATA*>(base + imp->FirstThunk); thunk->u1.Function; ++thunk) {
            if (reinterpret_cast<void*>(thunk->u1.Function) != target) continue;
            DWORD old;
            VirtualProtect(&thunk->u1.Function, sizeof(void*), PAGE_READWRITE, &old);
            thunk->u1.Function = reinterpret_cast<ULONG_PTR>(replacement);
            VirtualProtect(&thunk->u1.Function, sizeof(void*), old, &old);
            ++hooked;
        }
    }
    return hooked;
}

void Attach() {
    wchar_t buf[MAX_PATH];
    GetModuleFileNameW(nullptr, buf, MAX_PATH);
    std::wstring game = buf;
    game = game.substr(0, game.find_last_of(L'\\'));

    const std::vector<LoaderCopy> copies = FindCopies(game);
    if (!copies.empty()) {
        g_copy = copies[0].dir;
        CreateDirectoryW((g_copy + L"\\logs").c_str(), nullptr);
        g_logPath = g_copy + L"\\logs\\proxy.log";
        Log("Loader %s in %ls\\%s chosen", VersionText(copies[0].version).c_str(), kModsFolder, Utf8(copies[0].mod).c_str());
        for (size_t i = 1; i < copies.size(); ++i)
            Log("Loader %s in %ls\\%s passed over", VersionText(copies[i].version).c_str(), kModsFolder,
                Utf8(copies[i].mod).c_str());
    }

    GetSystemDirectoryW(buf, MAX_PATH);
    HMODULE real = LoadLibraryW((std::wstring(buf) + L"\\winmm.dll").c_str());
    if (!real) {
        Log("system winmm.dll failed to load: %lu", GetLastError());
        return;
    }
    for (int i = 0; i < kWinmmCount; ++i) g_winmm[i] = GetProcAddress(real, kWinmmNames[i]);

    // With no copy to start, the game runs as it does without us.
    if (g_copy.empty()) return;
    const int hooked = HookImport(reinterpret_cast<void*>(&GetProcAddress), reinterpret_cast<void*>(&HookedGetProcAddress));
    Log("proxy attached, GetProcAddress imports hooked: %d", hooked);
}

}  // namespace proxy

BOOL WINAPI DllMain(HINSTANCE instance, DWORD reason, LPVOID) {
    if (reason == DLL_PROCESS_ATTACH) {
        DisableThreadLibraryCalls(instance);
        proxy::Attach();
    }
    return TRUE;
}
