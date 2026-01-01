// Keys.cs

namespace BusLibrary02.Core;

public static class Keys
{
    public static string Of(string service, string handler, string? version = null) => version is null ? $"{service}:{handler}" : $"{service}:{handler}:{version}";
    public static class A { public const string Ping = "a:ping:v1"; public const string Notify = "a:notify"; }
    public static class B { public const string Pong = "b:pong:v1"; public const string Audit = "b:audit"; }
   // public static class C { public const string Job = "c:job"; }
}
