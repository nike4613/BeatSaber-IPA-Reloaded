/*
 * mono.h -- Definitions for Mono C API functions.
 * 
 * The file contains definitions for some of functions provided by Mono C API.
 * 
 * Note: Since we don't use any mono headers, all pointers to mono-related structs are
 * replaced with void *.
 */

#pragma once

#pragma warning( disable : 4152 )

#include <windows.h>

// Creates a MonoString based from a C wide string
#define MONO_STRING(str) mono_string_new_utf16(domain, str, wcslen(str))

// Set MonoArray's index to a reference type value (i.e. string)
#define SET_ARRAY_REF(arr, index, refVal) \
	{ \
		void **p = (void**) mono_array_addr_with_size(arr, sizeof(void*), index); \
		mono_gc_wbarrier_set_arrayref(arr, p, refVal); \
	}

// Here we define the pointers to some functions within mono.dll
// Note to C learners: these are not signature definitions, but rather "variable"
// definitions with the function pointer type.

// Note: we use void* instead of the real intented structs defined in mono API
// This way we don't need to include or define any of Mono's structs, which saves space
// This, obviously, comes with a drawback of not being able to easily access the contents of the structs

typedef enum {
	MONO_DEBUG_FORMAT_NONE,
	MONO_DEBUG_FORMAT_MONO,
	/* Deprecated, the mdb debugger is not longer supported. */
	MONO_DEBUG_FORMAT_DEBUGGER
} MonoDebugFormat;

void (*mono_jit_parse_options)(int argc, char * argv[]);
void (*mono_debug_init)(MonoDebugFormat format);
void (*mono_debug_domain_create)(void*);

void *(*mono_jit_init_version)(const char *root_domain_name, const char *runtime_version);
void *(*mono_domain_assembly_open)(void *domain, const char *name);
void *(*mono_assembly_get_image)(void *assembly);
void *(*mono_runtime_invoke)(void *method, void *obj, void **params, void **exc);

void *(*mono_method_desc_new)(const char *name, int include_namespace);
void *(*mono_method_desc_search_in_image)(void *desc, void *image);
void *(*mono_method_signature)(void *method);
UINT32 (*mono_signature_get_param_count)(void *sig);

void *(*mono_array_new)(void *domain, void *eclass, uintptr_t n);
void (*mono_gc_wbarrier_set_arrayref)(void *arr, void *slot_ptr, void *value);
char *(*mono_array_addr_with_size)(void *arr, int size, uintptr_t idx);

void *(*mono_get_string_class)();
void *(*mono_string_new_utf16)(void *domain, const wchar_t *text, INT32 len);

void* (*mono_object_to_string)(void* obj, void** exc);

void (*mono_dllmap_insert)(void* assemblyImage, const char* dll, const char* func, const char* tdll, const char* tfunc);

char* (*mono_string_to_utf8)(void* str);
void (*mono_free)(void*);

/* Installs a function which is called when the runtime encounters an unhandled exception.
 * This hook isn't expected to return.
 * If no hook has been installed, the runtime will print a message before aborting.
 */
typedef void (*MonoUnhandledExceptionFunc)(void* exc, void* user_data);
void (*mono_install_unhandled_exception_hook)(MonoUnhandledExceptionFunc func, void* user_data);

/**
* \brief Loads Mono C API function pointers so that the above definitions can be called.
* \param monoLib Mono.dll module.
*/
inline void loadMonoFunctions(HMODULE monoLib)
{
	// Enjoy the fact that C allows such sloppy casting
	// In C++ you would have to cast to the precise function pointer type
#define GET_MONO_PROC(name) name = (void*)GetProcAddress(monoLib, #name)
	
	// Find and assign all our functions that we are going to use
	GET_MONO_PROC(mono_debug_domain_create);
	GET_MONO_PROC(mono_domain_assembly_open);
	GET_MONO_PROC(mono_assembly_get_image);
	GET_MONO_PROC(mono_runtime_invoke);
	GET_MONO_PROC(mono_debug_init);
	GET_MONO_PROC(mono_jit_init_version);
	GET_MONO_PROC(mono_jit_parse_options);
	GET_MONO_PROC(mono_method_desc_new);
	GET_MONO_PROC(mono_method_desc_search_in_image);
	GET_MONO_PROC(mono_method_signature);
	GET_MONO_PROC(mono_signature_get_param_count);
	GET_MONO_PROC(mono_array_new);
	GET_MONO_PROC(mono_get_string_class);
	GET_MONO_PROC(mono_string_new_utf16);
	GET_MONO_PROC(mono_gc_wbarrier_set_arrayref);
	GET_MONO_PROC(mono_array_addr_with_size);
    GET_MONO_PROC(mono_object_to_string);
    GET_MONO_PROC(mono_string_to_utf8);
    GET_MONO_PROC(mono_free);
    GET_MONO_PROC(mono_dllmap_insert);
    GET_MONO_PROC(mono_install_unhandled_exception_hook);
}
