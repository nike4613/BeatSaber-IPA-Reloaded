#pragma once

#pragma comment(lib, "ucrt.lib")

#include <windows.h>
#include <stdio.h>

#ifdef _VERBOSE

static HANDLE log_handle;
static char buffer[8192];

inline void init_logger()
{
	log_handle = CreateFileA("doorstop.log", GENERIC_WRITE, FILE_SHARE_READ, NULL, CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL,
	                         NULL);
}

inline void free_logger()
{
	CloseHandle(log_handle);
}

#define LOG(message, ...)                                                                                                 \
	{                                                                                                                     \
		int len = _snprintf_s(buffer, sizeof(buffer)/sizeof(char), _TRUNCATE, message, __VA_ARGS__);    \
		WriteFile(log_handle, buffer, len, NULL, NULL); \
	}
#else
inline void init_logger()
{
}

inline void free_logger()
{
}

#define LOG(message, ...) 
#endif

static wchar_t bufferW[8192];

#define ASSERT_F(test, message, ...)											        	                            \
	if(!(test))																		                                    \
	{																				                                    \
		_snwprintf_s(bufferW, sizeof(bufferW)/sizeof(wchar_t), _TRUNCATE, message, __VA_ARGS__);	\
		MessageBoxW(NULL, bufferW, L"Doorstop: Fatal", MB_OK | MB_ICONERROR);                                         	\
		ExitProcess(EXIT_FAILURE);                                                                                      \
	}

// A helper for cleaner error logging
#define ASSERT(test, message)                                                   \
	if(!(test))                                                                 \
	{                                                                           \
		MessageBoxW(NULL, message, L"Doorstop: Fatal", MB_OK | MB_ICONERROR);   \
		ExitProcess(EXIT_FAILURE);                                              \
	}

#define ASSERT_SOFT(test, ...)                   \
	if(!(test))                                  \
	{                                            \
		return __VA_ARGS__;                      \
	}
