# Offline Update Package

Root app layout:

```text
LocalPlayer/
  Launcher.exe
  app/
    LocalPlayer.exe
    LocalPlayer.dll
    manifest.json
    ffmpeg.exe
    libvlc/
    Data/
  data/
    config/
    logs/
    cache/
    updates/
    backup/
```

Patch package layout:

```text
LocalPlayer_1.2.3_to_1.2.4.zip
  manifest.json
  LocalPlayer.exe
  LocalPlayer.dll
  ffmpeg.exe
  libvlc/...
```

Manifest example:

```json
{
  "appId": "LocalPlayer",
  "packageType": "patch",
  "version": "1.2.4",
  "baseVersion": "1.2.3",
  "generatedAtUtc": "2026-05-05T00:00:00Z",
  "description": "Offline hotfix",
  "files": [
    { "path": "LocalPlayer.exe", "action": "replace", "sha256": "..." },
    { "path": "LocalPlayer.dll", "action": "replace", "sha256": "..." },
    { "path": "ffmpeg.exe", "action": "replace", "sha256": "..." },
    { "path": "old-file.txt", "action": "delete" }
  ]
}
```

Rules:

- `path` is relative to `app/`.
- `replace` and `add` entries must have payload files in the zip with the same relative path.
- `delete` entries do not need payload files.
- Launcher backs up `app/` to `backup/last-good/` before applying.
- User data under `data/` is never touched.
