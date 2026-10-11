using Arcadia.ControllerSupport;

namespace Arcadia.Core.Tests;

public class ControllerSupportRemovalTests
{
    [Theory]
    [InlineData(@"""C:\Program Files\USBip\uninstall.exe""", @"C:\Program Files\USBip\uninstall.exe", "")]
    [InlineData(@"""C:\Program Files\USBip\uninstall.exe"" /S", @"C:\Program Files\USBip\uninstall.exe", "/S")]
    [InlineData(@"C:\USBip\uninstall.exe", @"C:\USBip\uninstall.exe", "")]
    public void Uninstall_command_is_split_into_file_and_arguments(string command, string file, string arguments)
    {
        Assert.Equal((file, arguments), ControllerSupportRemoval.SplitCommand(command));
    }
}
