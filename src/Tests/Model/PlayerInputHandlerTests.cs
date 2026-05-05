using System.Windows.Input;
using FluentAssertions;
using LocalPlayer.Infrastructure.Model;
using Moq;
using Xunit;

namespace LocalPlayer.Tests.Model;

public class PlayerInputHandlerTests
{
    private readonly Dictionary<string, Key> _bindings;
    private readonly Mock<ISettingsService> _settingsMock = new();
    private readonly PlayerInputHandler _handler;

    public PlayerInputHandlerTests()
    {
        _bindings = new Dictionary<string, Key>
        {
            ["TogglePlayPause"] = Key.Space,
            ["SeekBackward"] = Key.Left,
            ["SeekBackwardAlt"] = Key.J,
            ["SeekForward"] = Key.Right,
            ["SeekForwardAlt"] = Key.L,
            ["Back"] = Key.Escape,
            ["NextEpisode"] = Key.N,
            ["PreviousEpisode"] = Key.P,
        };

        _settingsMock.Setup(s => s.GetAllKeyBindings()).Returns(() => _bindings);
        _settingsMock.Setup(s => s.GetKeyBinding(It.IsAny<string>()))
            .Returns<string>(name => _bindings.TryGetValue(name, out var key) ? key : Key.None);
        _settingsMock.Setup(s => s.SetKeyBinding(It.IsAny<string>(), It.IsAny<Key>()))
            .Callback<string, Key>((name, key) => _bindings[name] = key);
        _handler = new PlayerInputHandler(_settingsMock.Object);
        _handler.ReloadBindings();
    }

    [Fact]
    public void HandleKeyDown_Space_FiresTogglePlayPause()
    {
        var fired = false;
        _handler.TogglePlayPause += (_, _) => fired = true;

        _handler.HandleKeyDown(CreateKeyArgs(Key.Space));

        fired.Should().BeTrue();
    }

    [Fact]
    public void HandleKeyDown_Left_FiresSeekBackward()
    {
        var fired = false;
        _handler.SeekBackward += (_, _) => fired = true;

        _handler.HandleKeyDown(CreateKeyArgs(Key.Left));

        fired.Should().BeTrue();
    }

    [Fact]
    public void HandleKeyDown_Right_FiresSeekForward()
    {
        var fired = false;
        _handler.SeekForward += (_, _) => fired = true;

        _handler.HandleKeyDown(CreateKeyArgs(Key.Right));

        fired.Should().BeTrue();
    }

    [Fact]
    public void HandleKeyDown_Escape_FiresBack()
    {
        var fired = false;
        _handler.Back += (_, _) => fired = true;

        _handler.HandleKeyDown(CreateKeyArgs(Key.Escape));

        fired.Should().BeTrue();
    }

    [Fact]
    public void HandleKeyDown_N_FiresNextEpisode()
    {
        var fired = false;
        _handler.NextEpisode += (_, _) => fired = true;

        _handler.HandleKeyDown(CreateKeyArgs(Key.N));

        fired.Should().BeTrue();
    }

    [Fact]
    public void HandleKeyDown_P_FiresPreviousEpisode()
    {
        var fired = false;
        _handler.PreviousEpisode += (_, _) => fired = true;

        _handler.HandleKeyDown(CreateKeyArgs(Key.P));

        fired.Should().BeTrue();
    }

    [Fact]
    public void HandleKeyDown_AltSeek_J_FiresSeekBackward()
    {
        var fired = false;
        _handler.SeekBackward += (_, _) => fired = true;

        _handler.HandleKeyDown(CreateKeyArgs(Key.J));

        fired.Should().BeTrue();
    }

    [Fact]
    public void HandleKeyDown_AltSeek_L_FiresSeekForward()
    {
        var fired = false;
        _handler.SeekForward += (_, _) => fired = true;

        _handler.HandleKeyDown(CreateKeyArgs(Key.L));

        fired.Should().BeTrue();
    }

    [Fact]
    public void HandleKeyDown_UnboundKey_ReturnsFalse()
    {
        _handler.HandleKeyDown(CreateKeyArgs(Key.Z)).Should().BeFalse();
    }

    [Fact]
    public void SetBinding_UpdatesMapping()
    {
        var fired = false;
        _handler.TogglePlayPause += (_, _) => fired = true;

        _handler.SetBinding("TogglePlayPause", Key.Enter);
        _handler.HandleKeyDown(CreateKeyArgs(Key.Enter));

        fired.Should().BeTrue();
    }

    [Fact]
    public void ReloadBindings_FiresBindingsChanged()
    {
        var fired = false;
        _handler.BindingsChanged += () => fired = true;

        _handler.ReloadBindings();

        fired.Should().BeTrue();
    }

    [Fact]
    public void GetDefaultBindings_ReturnsEightBindings()
    {
        PlayerInputHandler.GetDefaultBindings().Should().HaveCount(8);
    }

    [Fact]
    public void GetCurrentBindings_ReturnsFromSettings()
    {
        var bindings = _handler.GetCurrentBindings();
        bindings.Should().ContainKey("TogglePlayPause");
        bindings["TogglePlayPause"].Should().Be(Key.Space);
    }

    private static KeyEventArgs CreateKeyArgs(Key key)
    {
        var args = (KeyEventArgs)System.Runtime.CompilerServices.RuntimeHelpers
            .GetUninitializedObject(typeof(KeyEventArgs));
        typeof(KeyEventArgs)
            .GetField("_key",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(args, key);
        return args;
    }
}

