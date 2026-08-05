using System.Threading;
using System.Threading.Tasks;
using TrackMeUp.Services;
using Xunit;

namespace TrackMeUp.Core.Tests;

public sealed class DeviceContextServiceTests
{
    [Fact]
    public async Task CaptureAsync_UsesThePlatformValuesAndKeepsWindowsLocationProvenance()
    {
        var platform = new FakeDeviceContextPlatform(
            new DeviceContextValue("Asia/Ho_Chi_Minh", "windows-time-zone", "available"),
            new DeviceContextValue("en-US", "windows-user-ui-language", "available"),
            new DeviceContextValue("it-IT", "windows-foreground-keyboard-layout", "available"),
            new DeviceLocationSnapshot(10.7769, 106.7009, 25, "windows-geolocator", "available"));
        var service = new DeviceContextService(platform);

        var snapshot = await service.CaptureAsync();

        Assert.Equal("Asia/Ho_Chi_Minh", snapshot.TimeZone.Value);
        Assert.Equal("en-US", snapshot.WindowsUiLanguage.Value);
        Assert.Equal("it-IT", snapshot.InputLanguage.Value);
        Assert.Equal(10.7769, snapshot.Location.Latitude);
        Assert.Equal(106.7009, snapshot.Location.Longitude);
        Assert.Equal("windows-geolocator", snapshot.Location.Source);
        Assert.Equal("available", snapshot.Location.Status);
    }

    [Fact]
    public async Task CaptureAsync_RemovesInvalidCoordinatesWithoutAWebFallback()
    {
        var platform = new FakeDeviceContextPlatform(
            new DeviceContextValue("UTC", "windows-time-zone", "available"),
            new DeviceContextValue("en-US", "windows-user-ui-language", "available"),
            new DeviceContextValue("en-US", "windows-foreground-keyboard-layout", "available"),
            new DeviceLocationSnapshot(91, 10, -1, "windows-geolocator", "available"));
        var service = new DeviceContextService(platform);

        var snapshot = await service.CaptureAsync();

        Assert.Null(snapshot.Location.Latitude);
        Assert.Null(snapshot.Location.Longitude);
        Assert.Null(snapshot.Location.AccuracyMeters);
        Assert.Equal("windows-geolocator", snapshot.Location.Source);
        Assert.Equal("invalid_coordinates", snapshot.Location.Status);
    }

    private sealed class FakeDeviceContextPlatform : IDeviceContextPlatform
    {
        private readonly DeviceContextValue _timeZone;
        private readonly DeviceContextValue _windowsUiLanguage;
        private readonly DeviceContextValue _inputLanguage;
        private readonly DeviceLocationSnapshot _location;

        public FakeDeviceContextPlatform(
            DeviceContextValue timeZone,
            DeviceContextValue windowsUiLanguage,
            DeviceContextValue inputLanguage,
            DeviceLocationSnapshot location)
        {
            _timeZone = timeZone;
            _windowsUiLanguage = windowsUiLanguage;
            _inputLanguage = inputLanguage;
            _location = location;
        }

        public DeviceContextValue GetTimeZone() => _timeZone;

        public DeviceContextValue GetWindowsUiLanguage() => _windowsUiLanguage;

        public DeviceContextValue GetActiveInputLanguage() => _inputLanguage;

        public Task<DeviceLocationSnapshot> GetCurrentLocationAsync(CancellationToken cancellationToken)
            => Task.FromResult(_location);

        public Task<DeviceLocationAccessResult> RequestLocationAccessAsync(CancellationToken cancellationToken)
            => Task.FromResult(new DeviceLocationAccessResult("windows-geolocator", "allowed"));
    }
}
