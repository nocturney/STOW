using System.Reflection;
using System.Runtime.InteropServices;

namespace STOW.IntegrationTests;

public sealed class Win32InteropContractTests
{
    [Theory]
    [InlineData("IsWindowNative", "IsWindow")]
    [InlineData("IsWindowVisibleNative", "IsWindowVisible")]
    [InlineData("IsIconicNative", "IsIconic")]
    public void Renamed_pinvoke_wrappers_keep_explicit_entrypoints(string methodName, string entryPoint)
    {
        Assembly assembly = typeof(STOW.Platform.Windows.Compatibility.Win32WindowRuntime).Assembly;
        Type runtimeType = assembly.GetType("STOW.Platform.Windows.Runtime.Win32TrayWindowRuntime", throwOnError: true)!;
        MethodInfo method = runtimeType.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)!;
        DllImportAttribute attribute = method.GetCustomAttribute<DllImportAttribute>()!;

        Assert.Equal("user32.dll", attribute.Value, ignoreCase: true);
        Assert.Equal(entryPoint, attribute.EntryPoint);
    }
}
