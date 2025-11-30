using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Evergine.Bindings.Vulkan;

namespace VulkanTestCSharp;

public readonly unsafe struct UnmanagedString(string str) : IDisposable, IEquatable<UnmanagedString>, IEquatable<string>
{
    public readonly byte* Pointer = (byte*)Marshal.StringToHGlobalAnsi(str);
    public void Dispose() => Free(Pointer);
    private static void Free(byte* pointer) => Marshal.FreeHGlobal((IntPtr)pointer);

    public bool Equals(UnmanagedString other) => Equals(Pointer, other.Pointer);
    public bool Equals(byte* other) => Equals(Pointer, other);
    public bool Equals(string? other)
    {
        if (other is null) return false;
        using var unmanagedOther = new UnmanagedString(other);
        return Equals(unmanagedOther);
    }

    private static bool Equals(byte* a, byte* b)
    {
        var i = 0;
        while (true)
        {
            if (a[i] == '\0') break;
            if (b[i] == '\0') break;
            if (a[i] != b[i]) return false;
            i++;
        }

        return true;
    }

    public override bool Equals(object? obj)
    {
        return obj is UnmanagedString other && Equals(other);
    }

    public override int GetHashCode()
    {
        return unchecked((int)(long)Pointer);
    }
}

#nullable disable
public readonly struct Result<T>
{
    public readonly T Value;
    public readonly string Error;
    public readonly bool Ok;

    public bool IsOk => Ok;
    public bool IsError => !Ok;

    public Result(string error)
    {
        Ok = false;
        Value = default;
        Error = error;
    }

    public Result(T value)
    {
        Ok = true;
        Value = value;
        Error = null;
    }

    public static Result<T> NewOk(T v) => new(v);
    public static Result<T> NewError(string error) => new(error);
    public static implicit operator Result<T>(T v) => new(v);
    public static implicit operator Result<T>(string err) => new(err);
}
#nullable enable

internal static unsafe class Helpers
{
    // public static byte* ToPointer(this string text)
    // {
    //     return (byte*)System.Runtime.InteropServices.Marshal.StringToHGlobalAnsi(text);
    // }

    public static uint Version(uint major, uint minor, uint patch)
    {
        return (major << 22) | (minor << 12) | patch;
    }

    public static VkMemoryType GetMemoryType(this VkPhysicalDeviceMemoryProperties memoryProperties, uint index)
    {
        return (&memoryProperties.memoryTypes_0)[index];
    }

    public static string GetString(byte* stringStart)
    {
        int characters = 0;
        while (stringStart[characters] != 0) characters++;
        return System.Text.Encoding.UTF8.GetString(stringStart, characters);
    }

    [Conditional("DEBUG")]
    public static void CheckErrors(VkResult result)
    {
        if (result != VkResult.VK_SUCCESS)
            throw new InvalidOperationException(result.ToString());
    }
}

public static class VulkanHelperExtensions
{
    // public static void CheckErrors(this VkResult result) => Helpers.CheckErrors(result);
}
