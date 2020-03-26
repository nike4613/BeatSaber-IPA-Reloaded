/*
 * main.cpp -- The main "entry point" and the main logic of the DLL.
 *
 * Here, we define and initialize struct Main that contains the main code of this DLL.
 * 
 * The main procedure goes as follows:
 * 1. The loader checks that PatchLoader.dll and mono.dll exist
 * 2. mono.dll is loaded into memory and some of its functions are looked up
 * 3. mono_jit_init_version is hooked with the help of MinHook
 * 
 * Then, the loader waits until Unity creates its root domain for mono (which is done with mono_jit_init_version).
 * 
 * Inside mono_jit_init_version hook:
 * 1. Call the original mono_jit_init_version to get the Unity root domain
 * 2. Load PatchLoader.dll into the root domain
 * 3. Find and invoke PatchLoader.Loader.Run()
 * 
 * Rest of the work is done on the managed side.
 *
 */

#pragma warning( disable : 4267 100 152 6387 4456 6011 )

#include "winapi_util.h"
#include <Windows.h>

#include "config.h"
#include "mono.h"
#include "hook.h"
#include "assert_util.h"
#include "proxy.h"
#include <synchapi.h>

#include <intrin.h>

EXTERN_C IMAGE_DOS_HEADER __ImageBase; // This is provided by MSVC with the infomration about this DLL

HANDLE unhandledMutex;
void ownMonoJitParseOptions(int argc, char * argv[]);
BOOL setOptions = FALSE;
BOOL shouldBreakOnUnhandledException = TRUE;

__declspec(dllexport) void SetIgnoreUnhandledExceptions(BOOL ignore)
{
    shouldBreakOnUnhandledException = ignore;
}

void unhandledException(void* exc, void* data)
{
    WaitForSingleObject(unhandledMutex, INFINITE);

    void* exception = NULL;
    void* mstr = mono_object_to_string(exc, &exception);

    if (exception != NULL)
    {
#ifdef _VERBOSE
        void* monostr = mono_object_to_string(exception, &exception);
        if (exception != NULL)
        {
            DEBUG_BREAK;
            LOG("An error occurred while stringifying uncaught error, but the error could not be stringified.\n");
            ASSERT(FALSE, L"Uncaught exception; could not stringify");
        }
        else
        {
            char* str = mono_string_to_utf8(monostr);
            DEBUG_BREAK;
            LOG("An error occurred stringifying uncaught error: %s\n", str);

            /*size_t len = MultiByteToWideChar(CP_UTF8, 0, str, -1, NULL, 0);
            wchar_t* wstr = memalloc(sizeof(wchar_t) * len);
            MultiByteToWideChar(CP_UTF8, 0, str, -1, wstr, len);*/
            wchar_t* wstr = mono_string_to_utf16(monostr);

            ASSERT_F(FALSE, L"Uncaught exception; stringify failed: %wS", wstr);

            mono_free(wstr);
            mono_free(str);
        }
#else
        ASSERT(FALSE, L"Could not stringify uncaught exception");
#endif
    }

    char* str = mono_string_to_utf8(mstr);
    DEBUG_BREAK;
    LOG("Uncaught exception: %s\n", str);

    /*size_t len = MultiByteToWideChar(CP_UTF8, 0, str, -1, NULL, 0);
    wchar_t* wstr = memalloc(sizeof(wchar_t) * len);
    MultiByteToWideChar(CP_UTF8, 0, str, -1, wstr, len);*/
    wchar_t* wstr = mono_string_to_utf16(mstr);

    if (shouldBreakOnUnhandledException)
    {
#ifdef _VERBOSE
        ASSERT(FALSE, L"Uncaught exception; see doorstop.log for details");
#else
        ASSERT_F(FALSE, L"Uncaught exception: %wS", wstr);
#endif
    }

    mono_free(wstr);
    mono_free(str);

    ReleaseMutex(unhandledMutex);
}

// The hook for mono_jit_init_version
// We use this since it will always be called once to initialize Mono's JIT
void *ownMonoJitInitVersion(const char *root_domain_name, const char *runtime_version)
{
	// Call the original mono_jit_init_version to initialize the Unity Root Domain
	if (debug) {
		char* opts[1];
		opts[0] = "";
		ownMonoJitParseOptions(0, opts);
	}
#ifdef WIN32
    if (debug_info) {
        mono_debug_init(MONO_DEBUG_FORMAT_MONO);
    }
#endif

	void *domain = mono_jit_init_version(root_domain_name, runtime_version);

	if (debug_info) {
#ifdef WIN64
        mono_debug_init(MONO_DEBUG_FORMAT_MONO);
#endif
		mono_debug_domain_create(domain);
	}

	size_t len = WideCharToMultiByte(CP_UTF8, 0, targetAssembly, -1, NULL, 0, NULL, NULL);
	char *dll_path = memalloc(sizeof(char) * len);
	WideCharToMultiByte(CP_UTF8, 0, targetAssembly, -1, dll_path, len, NULL, NULL);

	LOG("Loading assembly: %s\n", dll_path);
	// Load our custom assembly into the domain
	void *assembly = mono_domain_assembly_open(domain, dll_path);

	if (assembly == NULL)
	LOG("Failed to load assembly\n");

	memfree(dll_path);
	ASSERT_SOFT(assembly != NULL, domain);

	// Get assembly's image that contains CIL code
	void *image = mono_assembly_get_image(assembly);
	ASSERT_SOFT(image != NULL, domain);

	// Note: we use the runtime_invoke route since jit_exec will not work on DLLs

	// Create a descriptor for a random Main method
	void *desc = mono_method_desc_new("*:Main", FALSE);

	// Find the first possible Main method in the assembly
	void *method = mono_method_desc_search_in_image(desc, image);
	ASSERT_SOFT(method != NULL, domain);

	void *signature = mono_method_signature(method);

	// Get the number of parameters in the signature
	UINT32 params = mono_signature_get_param_count(signature);

	void **args = NULL;
	wchar_t *app_path = NULL;
	if (params == 1)
	{
		// If there is a parameter, it's most likely a string[].
		// Populate it as follows
		// 0 => path to the game's executable
		// 1 => --doorstop-invoke

		get_module_path(NULL, &app_path, NULL, 0);

		void *exe_path = MONO_STRING(app_path);
		void *doorstop_handle = MONO_STRING(L"--doorstop-invoke");

		void *args_array = mono_array_new(domain, mono_get_string_class(), 2);

		SET_ARRAY_REF(args_array, 0, exe_path);
		SET_ARRAY_REF(args_array, 1, doorstop_handle);

		args = memalloc(sizeof(void*) * 1);
		_ASSERTE(args != nullptr);
		args[0] = args_array;
	}

    LOG("Installing uncaught exception handler\n");

    mono_install_unhandled_exception_hook(unhandledException, NULL);

    wchar_t* dll_path_w; // self path
    size_t dll_path_len = get_module_path((HINSTANCE)&__ImageBase, &dll_path_w, NULL, 0);
    char* self_dll_path = memalloc(dll_path_len + 1);
    WideCharToMultiByte(CP_UTF8, 0, dll_path_w, -1, self_dll_path, dll_path_len + 1, NULL, NULL);

    mono_dllmap_insert(NULL, "i:bsipa-doorstop", NULL, self_dll_path, NULL); // remap `bsipa-doorstop` to this assembly

    memfree(self_dll_path);
    memfree(dll_path_w);


    unhandledMutex = CreateMutexW(NULL, FALSE, NULL);

	LOG("Invoking method!\n");

    void* exception = NULL;
	mono_runtime_invoke(method, NULL, args, &exception);

    WaitForSingleObject(unhandledMutex, INFINITE); // if the EH is triggered, wait for it

	if (args != NULL)
	{
		memfree(app_path);
		memfree(args);
		NULL;
	}

#ifdef _VERBOSE
    if (exception != NULL)
    {
        void* monostr = mono_object_to_string(exception, &exception);
        if (exception != NULL)
            LOG("An error occurred while invoking the injector, but the error could not be stringified.\n")
        else 
        {
            char* str = mono_string_to_utf8(monostr);
            LOG("An error occurred invoking the injector: %s\n", str);
            mono_free(str);
        }
    }
#endif

	cleanupConfig();

	free_logger();

    ReleaseMutex(unhandledMutex);

	return domain;
}

void ownMonoJitParseOptions(int argc, char * argv[])
{
	setOptions = TRUE;

	int size = argc;
#ifdef WIN64
	if (debug) size += 2;
#elif defined(WIN32)
    if (debug) size += 1;
#endif

	char** arguments = memalloc(sizeof(char*) * size);
	_ASSERTE(arguments != nullptr);
	memcpy(arguments, argv, sizeof(char*) * argc);
	if (debug) {
		//arguments[argc++] = "--debug";
#ifdef WIN64
        arguments[argc++] = "--soft-breakpoints";
#endif
		if (debug_server)
			arguments[argc] = "--debugger-agent=transport=dt_socket,address=0.0.0.0:10000,server=y";
		else
			arguments[argc] = "--debugger-agent=transport=dt_socket,address=127.0.0.1:10000,server=n";
	}

	mono_jit_parse_options(size, arguments);

	memfree(arguments);
}

BOOL initialized = FALSE;

void init(HMODULE module)
{
	if (!initialized)
	{
		initialized = TRUE;
		LOG("Got mono.dll at %p\n", module);
		loadMonoFunctions(module);
	}
}

void * WINAPI hookGetProcAddress(HMODULE module, char const *name)
{
	if (lstrcmpA(name, "mono_jit_init_version") == 0)
	{
		init(module);
		return (void*)&ownMonoJitInitVersion;
	}
	if (lstrcmpA(name, "mono_jit_parse_options") == 0 && debug)
	{
		init(module);
		return (void*)&ownMonoJitParseOptions;
	}
	return (void*)GetProcAddress(module, name);
}

BOOL hookGetMessage(
    BOOL isW,
    LPMSG msg,
    HWND hwnd,
    UINT wMsgFilterMin,
    UINT wMsgFilterMax
);

BOOL WINAPI hookGetMessageA(LPMSG msg, HWND hwnd, UINT wMsgFilterMin, UINT wMsgFilterMax)
{
    return hookGetMessage(FALSE, msg, hwnd, wMsgFilterMin, wMsgFilterMax);
}
BOOL WINAPI hookGetMessageW(LPMSG msg, HWND hwnd, UINT wMsgFilterMin, UINT wMsgFilterMax)
{
    return hookGetMessage(TRUE, msg, hwnd, wMsgFilterMin, wMsgFilterMax);
}

typedef BOOL(*GetMessageHook)(BOOL isW, BOOL result, LPMSG msg, HWND hwnd, UINT filterMin, UINT filterMax);

GetMessageHook getMessageHook = NULL;

__declspec(dllexport) void __stdcall SetGetMessageHook(GetMessageHook hook) {
    getMessageHook = hook;
}

BOOL hookGetMessage(
    BOOL isW,
    LPMSG msg,
    HWND hwnd,
    UINT wMsgFilterMin,
    UINT wMsgFilterMax
)
{
    BOOL loop = FALSE;

    BOOL result;

    do {
        if (isW) {
            result = GetMessageW(msg, hwnd, wMsgFilterMin, wMsgFilterMax);
        } else {
            result = GetMessageA(msg, hwnd, wMsgFilterMin, wMsgFilterMax);
        }

        if (getMessageHook) {
            loop = getMessageHook(isW, result, msg, hwnd, wMsgFilterMin, wMsgFilterMax);
        }
    } while (loop);

    return result;
}

BOOL hookPeekMessage(
    BOOL isW,
    LPMSG msg,
    HWND hwnd,
    UINT wMsgFilterMin,
    UINT wMsgFilterMax,
    UINT wRemoveMsg
);

BOOL WINAPI hookPeekMessageA(LPMSG msg, HWND hwnd, UINT wMsgFilterMin, UINT wMsgFilterMax, UINT wRemoveMsg)
{
    return hookPeekMessage(FALSE, msg, hwnd, wMsgFilterMin, wMsgFilterMax, wRemoveMsg);
}
BOOL WINAPI hookPeekMessageW(LPMSG msg, HWND hwnd, UINT wMsgFilterMin, UINT wMsgFilterMax, UINT wRemoveMsg)
{
    return hookPeekMessage(TRUE, msg, hwnd, wMsgFilterMin, wMsgFilterMax, wRemoveMsg);
}

typedef BOOL(*PeekMessageHook)(BOOL isW, BOOL result, LPMSG msg, HWND hwnd, UINT filterMin, UINT filterMax, UINT* wRemoveMsg);

PeekMessageHook peekMessageHook = NULL;

__declspec(dllexport) void __stdcall SetPeekMessageHook(PeekMessageHook hook) {
    peekMessageHook = hook;
}

BOOL hookPeekMessage(
    BOOL isW,
    LPMSG msg,
    HWND hwnd,
    UINT wMsgFilterMin,
    UINT wMsgFilterMax,
    UINT wRemoveMsg
)
{
    BOOL loop = FALSE;

    BOOL result;

    do {
        if (isW) {
            result = PeekMessageW(msg, hwnd, wMsgFilterMin, wMsgFilterMax, wRemoveMsg);
        }
        else {
            result = PeekMessageA(msg, hwnd, wMsgFilterMin, wMsgFilterMax, wRemoveMsg);
        }

        if (peekMessageHook) {
            loop = peekMessageHook(isW, result, msg, hwnd, wMsgFilterMin, wMsgFilterMax, &wRemoveMsg);
        }
    } while (loop);

    return result;
}

BOOL WINAPI DllMain(HINSTANCE hInstDll, DWORD reasonForDllLoad, LPVOID reserved)
{
	if (reasonForDllLoad != DLL_PROCESS_ATTACH)
		return TRUE;

	hHeap = GetProcessHeap();

	init_logger();

	LOG("Doorstop started!\n");

	wchar_t *dll_path = NULL;
	size_t dll_path_len = get_module_path((HINSTANCE)&__ImageBase, &dll_path, NULL, 0);

	LOG("DLL Path: %S\n", dll_path);

	wchar_t *dll_name = get_file_name_no_ext(dll_path, dll_path_len);

	LOG("Doorstop DLL Name: %S\n", dll_name);

	loadProxy(dll_name);
	loadConfig();

	// If the loader is disabled, don't inject anything.
	if (enabled)
	{
		LOG("Doorstop enabled!\n");
		ASSERT_SOFT(GetFileAttributesW(targetAssembly) != INVALID_FILE_ATTRIBUTES, TRUE);

		HMODULE targetModule = GetModuleHandleA("UnityPlayer");

		if(targetModule == NULL)
		{
			LOG("No UnityPlayer.dll; using EXE as the hook target.");
			targetModule = GetModuleHandleA(NULL);
		}

		LOG("Installing IAT hook\n");
		if (!iat_hook(targetModule, "kernel32.dll", &GetProcAddress, &hookGetProcAddress))
		{
			LOG("Failed to install IAT hook!\n");
			free_logger();
		}

		LOG("Hook installed!\n");

        LOG("Attempting to install GetMessageA and GetMessageW hooks\n");

        if (!iat_hook(targetModule, "user32.dll", &GetMessageA, &hookGetMessageA)) {
            LOG("Could not hook GetMessageA! (not an error)\n");
        }
        if (!iat_hook(targetModule, "user32.dll", &GetMessageW, &hookGetMessageW)) {
            LOG("Could not hook GetMessageW! (not an error)\n");
        }

        LOG("Attempting to install PeekMessageA and PeekMessageW hooks\n");

        if (!iat_hook(targetModule, "user32.dll", &PeekMessageA, &hookPeekMessageA)) {
            LOG("Could not hook PeekMessageA! (not an error)\n");
        }
        if (!iat_hook(targetModule, "user32.dll", &PeekMessageW, &hookPeekMessageW)) {
            LOG("Could not hook PeekMessageW! (not an error)\n");
        }
	}
	else
	{
		LOG("Doorstop disabled! memfreeing resources\n");
		free_logger();
	}

	memfree(dll_name);
	memfree(dll_path);

	return TRUE;
}
