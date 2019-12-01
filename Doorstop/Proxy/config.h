#pragma once

#pragma warning( disable : 4267 )

#include <Windows.h>
#include <shellapi.h>
#include "winapi_util.h"
#include "assert_util.h"
#include "crt.h"

#define CONFIG_NAME L"doorstop_config"
#define DEFAULT_TARGET_ASSEMBLY L"Doorstop.dll"
#define EXE_EXTENSION_LENGTH 4

BOOL enabled = FALSE;
BOOL debug = FALSE;
BOOL debug_server = FALSE;
BOOL debug_info = FALSE;
wchar_t *targetAssembly = NULL;

#define STR_EQUAL(str1, str2) (lstrcmpiW(str1, str2) == 0)

inline void initConfigFile()
{
	enabled = TRUE;

	WIN32_FIND_DATAW findData;
	HANDLE findHandle = FindFirstFileW(L"*_Data", &findData);
	if (findHandle == INVALID_HANDLE_VALUE)
	{
		MessageBoxW(NULL, L"Could not locate game being injected!", L"No files found in current directory matching '*_Data'", 
			MB_OK | MB_ICONERROR | MB_SYSTEMMODAL | MB_TOPMOST | MB_SETFOREGROUND);

		ExitProcess(GetLastError());
	}

	do
	{
		if (findData.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY)
		{ // must be a directory
			wchar_t* target = memalloc(MAX_PATH * sizeof(wchar_t));

			const int EXIT_FAILURE = 1;
			ASSERT(target != NULL, L"Address returned by memalloc was NULL!");

			wmemset(target, 0, MAX_PATH);

			wmemcpy(target, findData.cFileName, wcslen(findData.cFileName));
			wmemcpy(target + wcslen(target), L"/Managed/IPA.Injector.dll", 26);

			targetAssembly = target;
			FindClose(findHandle);
			break;
		}
	} 
	while (FindNextFileW(findHandle, &findData) != 0);

	if (targetAssembly == NULL)
	{
		MessageBoxW(NULL, L"Could not locate game being injected!", L"No valid directories matching '*_Data'",
			MB_OK | MB_ICONERROR | MB_SYSTEMMODAL | MB_TOPMOST | MB_SETFOREGROUND);

		ExitProcess(GetLastError());
	}
}

inline void initCmdArgs()
{
	wchar_t *args = GetCommandLineW();
	int argc = 0;
	wchar_t **argv = CommandLineToArgvW(args, &argc);

#define IS_ARGUMENT(arg_name) STR_EQUAL(arg, arg_name) && i < argc

	for (int i = 0; i < argc; i++)
	{
		wchar_t *arg = argv[i];
		/*if (IS_ARGUMENT(L"--doorstop-enable"))
		{
			wchar_t *par = argv[++i];

			if (STR_EQUAL(par, L"true"))
				enabled = TRUE;
			else if (STR_EQUAL(par, L"false"))
				enabled = FALSE;
		}
		else if (IS_ARGUMENT(L"--doorstop-target"))
		{
			if (targetAssembly != NULL)
				memfree(targetAssembly);
			const size_t len = wcslen(argv[i + 1]) + 1;
			targetAssembly = memalloc(sizeof(wchar_t) * len);
			lstrcpynW(targetAssembly, argv[++i], len);
			LOG("Args; Target assembly: %S\n", targetAssembly);
		}
		else */if (IS_ARGUMENT(L"--mono-debug"))
		{
			debug = TRUE;
			debug_info = TRUE;
			LOG("Enabled debugging\n");
		}
		else if (IS_ARGUMENT(L"--debug"))
		{
			debug_info = TRUE;
			LOG("Enabled loading of debug info\n");
		}
		else if (IS_ARGUMENT(L"--server"))
		{
			debug_server = TRUE;
			LOG("Server-mode debugging enabled\n");
		}
	}

	LocalFree(argv);
}

inline void initEnvVars()
{
	if (GetEnvironmentVariableW(L"DOORSTOP_DISABLE", NULL, 0) != 0)
	{
		LOG("DOORSTOP_DISABLE is set! Disabling Doorstop!\n");
		enabled = FALSE;
	}
}

inline void loadConfig()
{
	initConfigFile();
	initCmdArgs();
	initEnvVars();
}

inline void cleanupConfig()
{
	if (targetAssembly != NULL)
		memfree(targetAssembly);
}
