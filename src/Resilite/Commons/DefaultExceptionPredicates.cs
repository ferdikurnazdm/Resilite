using System;
using System.Net.Sockets;

namespace Resilite;

internal static class DefaultExceptionPredicates
{
    public static bool ShouldHandle(Exception exception)
    {
        return exception is TimeoutException
            or IOException
            or SocketException
            or HttpRequestException;
    }
}