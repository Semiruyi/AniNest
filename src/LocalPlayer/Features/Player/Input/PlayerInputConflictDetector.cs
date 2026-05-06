namespace LocalPlayer.Features.Player.Input;

public static class PlayerInputConflictDetector
{
    public static List<PlayerInputConflict> FindConflicts(
        PlayerInputProfile profile,
        PlayerInputBinding incomingBinding,
        int ignoreIndex = -1)
    {
        var conflicts = new List<PlayerInputConflict>();

        for (int i = 0; i < profile.Bindings.Count; i++)
        {
            if (i == ignoreIndex)
                continue;

            var existing = profile.Bindings[i];
            if (!existing.IsEnabled || !incomingBinding.IsEnabled)
                continue;

            if (!AreTriggersEqual(existing, incomingBinding))
                continue;

            conflicts.Add(new PlayerInputConflict
            {
                ExistingIndex = i,
                ExistingBinding = existing.Clone(),
                IncomingBinding = incomingBinding.Clone()
            });
        }

        return conflicts;
    }

    public static bool AreTriggersEqual(PlayerInputBinding left, PlayerInputBinding right)
    {
        if (left.KeyTrigger is not null && right.KeyTrigger is not null)
        {
            return left.KeyTrigger.Key == right.KeyTrigger.Key
                && left.KeyTrigger.Modifiers == right.KeyTrigger.Modifiers;
        }

        if (left.MouseTrigger is not null && right.MouseTrigger is not null)
        {
            return left.MouseTrigger.Button == right.MouseTrigger.Button
                && left.MouseTrigger.Modifiers == right.MouseTrigger.Modifiers
                && left.MouseTrigger.Kind == right.MouseTrigger.Kind;
        }

        return false;
    }
}
