using System.Collections.Generic;

namespace LocalPlayer.View.Diagnostics;

public static class PerfScenes
{
    public static PerfSceneSession Begin(
        string sceneName,
        IReadOnlyDictionary<string, string>? tags = null,
        int sampleCapacity = 32_768)
    {
        if (!PerfLogger.Enabled)
            return PerfSceneSession.Noop;
        return new PerfSceneSession(sceneName, tags, sampleCapacity);
    }
}
