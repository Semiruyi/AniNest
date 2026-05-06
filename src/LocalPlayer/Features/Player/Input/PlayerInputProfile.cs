namespace LocalPlayer.Features.Player.Input;

public sealed class PlayerInputProfile
{
    public List<PlayerInputBinding> Bindings { get; set; } = new();

    public bool HasBindings => Bindings.Count > 0;

    public PlayerInputProfile Clone()
    {
        var clone = new PlayerInputProfile();
        foreach (var binding in Bindings)
            clone.Bindings.Add(binding.Clone());
        return clone;
    }
}
