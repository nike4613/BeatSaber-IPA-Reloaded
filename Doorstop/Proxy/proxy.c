/* ==================================
 * COMPUTER GENERATED -- DO NOT EDIT
 * ==================================
 * 
 * This file contains the definitions for all proxy functions this DLL supports.
 * 
 * The proxies are very simple functions that should be optimizied into a 
 * single JMP instruction without editing the stack at all.
 * 
 * NOTE: While this works, this is a somewhat hackish approach that is based on how 
 * the compiler optimizes the code. That said, the proxy will not work on Debug build currently
 * (that can be fixed by changing the appropriate compile flag that I am yet to locate).
 */

#pragma warning( disable : 4244 )
 
#include <windows.h>

#define ADD_ORIGINAL(i, name) originalFunctions[i] = GetProcAddress(dll, #name)

#define PROXY(i, name) \
	__declspec(dllexport) ULONG __stdcall name() \
	{ \
		return originalFunctions[i](); \
	}

FARPROC originalFunctions[50] = {0};

void loadFunctions(HMODULE dll)
{
ADD_ORIGINAL(0, WinHttpAddRequestHeaders);
ADD_ORIGINAL(1, WinHttpAutoProxySvcMain);
ADD_ORIGINAL(2, WinHttpCheckPlatform);
ADD_ORIGINAL(3, WinHttpCloseHandle);
ADD_ORIGINAL(4, WinHttpConnect);
ADD_ORIGINAL(5, WinHttpConnectionDeleteProxyInfo);
ADD_ORIGINAL(6, WinHttpConnectionFreeNameList);
ADD_ORIGINAL(7, WinHttpConnectionFreeProxyInfo);
ADD_ORIGINAL(8, WinHttpConnectionFreeProxyList);
ADD_ORIGINAL(9, WinHttpConnectionGetNameList);
ADD_ORIGINAL(10, WinHttpConnectionGetProxyInfo);
ADD_ORIGINAL(11, WinHttpConnectionGetProxyList);
ADD_ORIGINAL(12, WinHttpConnectionSetProxyInfo);
ADD_ORIGINAL(13, WinHttpCrackUrl);
ADD_ORIGINAL(14, WinHttpCreateProxyResolver);
ADD_ORIGINAL(15, WinHttpCreateUrl);
ADD_ORIGINAL(16, WinHttpDetectAutoProxyConfigUrl);
ADD_ORIGINAL(17, WinHttpFreeProxyResult);
ADD_ORIGINAL(18, WinHttpGetDefaultProxyConfiguration);
ADD_ORIGINAL(19, WinHttpGetIEProxyConfigForCurrentUser);
ADD_ORIGINAL(20, WinHttpGetProxyForUrl);
ADD_ORIGINAL(21, WinHttpGetProxyForUrlEx);
ADD_ORIGINAL(22, WinHttpGetProxyResult);
ADD_ORIGINAL(23, WinHttpGetTunnelSocket);
ADD_ORIGINAL(24, WinHttpOpen);
ADD_ORIGINAL(25, WinHttpOpenRequest);
ADD_ORIGINAL(26, WinHttpProbeConnectivity);
ADD_ORIGINAL(27, WinHttpQueryAuthSchemes);
ADD_ORIGINAL(28, WinHttpQueryDataAvailable);
ADD_ORIGINAL(29, WinHttpQueryHeaders);
ADD_ORIGINAL(30, WinHttpQueryOption);
ADD_ORIGINAL(31, WinHttpReadData);
ADD_ORIGINAL(32, WinHttpReceiveResponse);
ADD_ORIGINAL(33, WinHttpResetAutoProxy);
ADD_ORIGINAL(34, WinHttpSaveProxyCredentials);
ADD_ORIGINAL(35, WinHttpSendRequest);
ADD_ORIGINAL(36, WinHttpSetCredentials);
ADD_ORIGINAL(37, WinHttpSetDefaultProxyConfiguration);
ADD_ORIGINAL(38, WinHttpSetOption);
ADD_ORIGINAL(39, WinHttpSetStatusCallback);
ADD_ORIGINAL(40, WinHttpSetTimeouts);
ADD_ORIGINAL(41, WinHttpTimeFromSystemTime);
ADD_ORIGINAL(42, WinHttpTimeToSystemTime);
ADD_ORIGINAL(43, WinHttpWebSocketClose);
ADD_ORIGINAL(44, WinHttpWebSocketCompleteUpgrade);
ADD_ORIGINAL(45, WinHttpWebSocketQueryCloseStatus);
ADD_ORIGINAL(46, WinHttpWebSocketReceive);
ADD_ORIGINAL(47, WinHttpWebSocketSend);
ADD_ORIGINAL(48, WinHttpWebSocketShutdown);
ADD_ORIGINAL(49, WinHttpWriteData);

}

PROXY(0, WinHttpAddRequestHeaders);
PROXY(1, WinHttpAutoProxySvcMain);
PROXY(2, WinHttpCheckPlatform);
PROXY(3, WinHttpCloseHandle);
PROXY(4, WinHttpConnect);
PROXY(5, WinHttpConnectionDeleteProxyInfo);
PROXY(6, WinHttpConnectionFreeNameList);
PROXY(7, WinHttpConnectionFreeProxyInfo);
PROXY(8, WinHttpConnectionFreeProxyList);
PROXY(9, WinHttpConnectionGetNameList);
PROXY(10, WinHttpConnectionGetProxyInfo);
PROXY(11, WinHttpConnectionGetProxyList);
PROXY(12, WinHttpConnectionSetProxyInfo);
PROXY(13, WinHttpCrackUrl);
PROXY(14, WinHttpCreateProxyResolver);
PROXY(15, WinHttpCreateUrl);
PROXY(16, WinHttpDetectAutoProxyConfigUrl);
PROXY(17, WinHttpFreeProxyResult);
PROXY(18, WinHttpGetDefaultProxyConfiguration);
PROXY(19, WinHttpGetIEProxyConfigForCurrentUser);
PROXY(20, WinHttpGetProxyForUrl);
PROXY(21, WinHttpGetProxyForUrlEx);
PROXY(22, WinHttpGetProxyResult);
PROXY(23, WinHttpGetTunnelSocket);
PROXY(24, WinHttpOpen);
PROXY(25, WinHttpOpenRequest);
PROXY(26, WinHttpProbeConnectivity);
PROXY(27, WinHttpQueryAuthSchemes);
PROXY(28, WinHttpQueryDataAvailable);
PROXY(29, WinHttpQueryHeaders);
PROXY(30, WinHttpQueryOption);
PROXY(31, WinHttpReadData);
PROXY(32, WinHttpReceiveResponse);
PROXY(33, WinHttpResetAutoProxy);
PROXY(34, WinHttpSaveProxyCredentials);
PROXY(35, WinHttpSendRequest);
PROXY(36, WinHttpSetCredentials);
PROXY(37, WinHttpSetDefaultProxyConfiguration);
PROXY(38, WinHttpSetOption);
PROXY(39, WinHttpSetStatusCallback);
PROXY(40, WinHttpSetTimeouts);
PROXY(41, WinHttpTimeFromSystemTime);
PROXY(42, WinHttpTimeToSystemTime);
PROXY(43, WinHttpWebSocketClose);
PROXY(44, WinHttpWebSocketCompleteUpgrade);
PROXY(45, WinHttpWebSocketQueryCloseStatus);
PROXY(46, WinHttpWebSocketReceive);
PROXY(47, WinHttpWebSocketSend);
PROXY(48, WinHttpWebSocketShutdown);
PROXY(49, WinHttpWriteData);
