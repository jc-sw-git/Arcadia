using System.Net;
using Arcadia.ControllerSupport;

namespace Arcadia.Core.Tests;

/// <summary>Which controllers behind SISR's emulated ones count as a Steam Controller (#124).</summary>
public sealed class SisrDevicesTests
{
    [Theory]
    [InlineData(@"\\?\HID#VID_28DE&PID_1304&MI_02#8&1a2b3c4d&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}", "Steam Controller")]
    [InlineData(@"\\?\hid#vid_28de&pid_1142&mi_01#7&abc&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}", "Steam Controller")]
    [InlineData(@"\\?\HID#VID_28DE&PID_1205&MI_02#8&abc&0&0000", "Steam Deck")]
    [InlineData(null, "Steam Controller")]
    // Steam's virtual gamepad that SISR paired with itself because it opened before the physical controller.
    [InlineData(@"\\?\HID#VID_28DE&PID_11FF&IG_00#8&abc&0&0000", "Steam Controller")]
    public void A_Valve_device_is_a_Steam_Controller(string? path, string name)
    {
        Assert.True(new SisrGamepad(name, path).IsValveController);
    }

    [Theory]
    [InlineData("XInput#0", "Xbox 360 Controller")]
    [InlineData(@"\\?\HID#VID_045E&PID_0B12&IG_00#8&abc&0&0000", "Xbox Series X Controller")]
    [InlineData(@"\\?\HID#VID_054C&PID_0CE6&MI_03#8&abc&0&0000", "PS5 Controller")]
    // Steam's virtual gamepad has Valve's vendor id but stands for whatever Steam Input passes on.
    [InlineData(@"\\?\HID#VID_28DE&PID_11FF&IG_00#8&abc&0&0000", "Steam Virtual Gamepad")]
    [InlineData(null, "Xbox 360 Controller")]
    [InlineData(null, null)]
    public void Any_other_controller_is_not(string? path, string? name)
    {
        Assert.False(new SisrGamepad(name, path).IsValveController);
    }

    [Fact]
    public void The_log_names_the_USB_id_but_not_the_device_instance()
    {
        var pad = new SisrGamepad("Xbox Series X Controller", @"\\?\hid#vid_045e&pid_0b12&ig_00#8&2f3e4d5c&0&0000");

        Assert.Equal("Xbox Series X Controller (045E:0B12)", pad.Description);
        Assert.Equal("Xbox 360 Controller (ohne USB-ID)", new SisrGamepad("Xbox 360 Controller", "XInput#0").Description);
    }

    [Theory]
    [InlineData("#controller_xbox360", "Xbox 360 Controller")]
    [InlineData("#Controller_PS5", "PlayStation 5 Controller")]
    [InlineData("#controller_switch_pro", "Nintendo Switch Pro Controller")]
    [InlineData("#controller_something_new", "Controller (something new)")]
    [InlineData("8BitDo Ultimate 2", "8BitDo Ultimate 2")]
    [InlineData(null, "Unbekannter Controller")]
    public void The_settings_name_Steams_controller_types_in_plain_words(string? name, string shown)
    {
        Assert.Equal(shown, new SisrGamepad(name, "XInput#0").DisplayName);
    }

    [Fact]
    public async Task Only_emulated_devices_count_and_one_without_a_physical_device_counts_as_Steam_Controller()
    {
        const string devices = """
            [
              {"real_device":{"id":3,"name":"Steam Controller","path":"\\\\?\\HID#VID_28DE&PID_1304&MI_02#8&1&0&0000","player_index":0},"viiper_device":{"busId":1,"devId":"1"}},
              {"real_device":{"id":5,"name":"Xbox 360 Controller","path":"XInput#0","type":"xbox360","real_type":"xbox360","player_index":1},"viiper_device":{"busId":1,"devId":"2"}},
              {"real_device":{"id":7,"name":"PS5 Controller","path":"\\\\?\\HID#VID_054C&PID_0CE6&MI_03#8&1&0&0000","player_index":2}},
              {"viiper_device":{"busId":1,"devId":"3"}},
              null
            ]
            """;
        using var http = new HttpClient(new DevicesHandler(devices));

        var controllers = await new SisrApiClient(http, SisrHost.ApiPort).GetEmulatedControllersAsync();

        Assert.NotNull(controllers);
        Assert.Equal(2, controllers.SteamControllers);
        Assert.Equal("Xbox 360 Controller", Assert.Single(controllers.Others).Name);
        Assert.Equal(3, controllers.Total);
    }

    [Fact]
    public async Task A_pad_Steam_Input_hands_to_SISR_counts_before_SISR_emulates_it()
    {
        // SISR creates the VIIPER device only on the pad's first input; Steam's virtual gamepad is there before.
        const string devices = """
            [
              {"real_device":{"id":4,"name":"Steam Controller","path":"\\\\?\\HID#VID_28DE&PID_1304&MI_02#8&1&0&0000","player_index":0},"steam_virtual_device":{"id":8,"name":"Steam Controller","steam_handle":"557011650481269","player_index":0},"viiper_device":{"busId":1,"devId":"1"}},
              {"real_device":{"id":7,"name":"8BitDo Ultimate 2 Wireless Controller for PC","path":"XInput#1","player_index":1},"steam_virtual_device":{"id":9,"name":"8BitDo Ultimate 2 Wireless Controller for PC","steam_handle":"445232051150315420","player_index":1}},
              {"real_device":{"id":5,"name":"PS5 Controller","path":"\\\\?\\HID#VID_054C&PID_0CE6&MI_03#8&1&0&0000","player_index":2}}
            ]
            """;
        using var http = new HttpClient(new DevicesHandler(devices));

        var controllers = await new SisrApiClient(http, SisrHost.ApiPort).GetEmulatedControllersAsync();

        Assert.NotNull(controllers);
        Assert.Equal(1, controllers.SteamControllers);
        Assert.Equal("8BitDo Ultimate 2 Wireless Controller for PC", Assert.Single(controllers.Others).Name);
    }

    [Fact]
    public async Task SISR_with_no_devices_emulates_nothing()
    {
        using var http = new HttpClient(new DevicesHandler("null"));

        var controllers = await new SisrApiClient(http, SisrHost.ApiPort).GetEmulatedControllersAsync();

        Assert.Equal(0, controllers?.Total);
    }

    private sealed class DevicesHandler(string devices) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var json = request.RequestUri!.AbsolutePath.EndsWith("/devices") ? devices : """{"version":"1.0"}""";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") });
        }
    }
}
