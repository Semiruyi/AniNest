import 'package:aninest_flutter/src/features/player/application/player_subtitle_track_option.dart';
import 'package:aninest_flutter/src/l10n/generated/app_localizations.dart';
import 'package:shadcn_flutter/shadcn_flutter.dart';

class PlayerSubtitleTrackMenu extends StatelessWidget {
  const PlayerSubtitleTrackMenu({
    super.key,
    required this.tracks,
    required this.selectedTrackId,
    required this.onTrackSelected,
    required this.onDismissRequested,
  });

  final List<PlayerSubtitleTrackOption> tracks;
  final String selectedTrackId;
  final ValueChanged<String> onTrackSelected;
  final VoidCallback onDismissRequested;

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final selectableTracks = tracks
        .where((PlayerSubtitleTrackOption track) => !track.isAutomatic)
        .toList();
    final hasRealTracks = selectableTracks.any(
      (PlayerSubtitleTrackOption track) => !track.isOff,
    );

    return ConstrainedBox(
      constraints: const BoxConstraints(minWidth: 220, maxWidth: 280),
      child: MenuGroup(
        itemPadding: EdgeInsets.zero,
        direction: Axis.vertical,
        onDismissed: onDismissRequested,
        builder: (BuildContext context, List<Widget> children) {
          return MenuPopup(children: children);
        },
        children: <MenuItem>[
          MenuRadioGroup<String>(
            value: selectedTrackId,
            onChanged: (BuildContext context, String value) {
              onTrackSelected(value);
              onDismissRequested();
            },
            children: <Widget>[
              MenuRadio<String>(
                value: PlayerSubtitleTrackOption.automaticId,
                enabled: hasRealTracks,
                child: Text(l10n.playerSubtitleAutomatic),
              ),
              MenuRadio<String>(
                value: PlayerSubtitleTrackOption.offId,
                enabled: hasRealTracks,
                child: Text(l10n.playerSubtitleOff),
              ),
              if (hasRealTracks) const MenuDivider(),
              if (hasRealTracks)
                for (final track in selectableTracks)
                  if (!track.isOff)
                    MenuRadio<String>(
                      value: track.id,
                      child: _SubtitleTrackText(
                        label: _labelForTrack(l10n, track),
                      ),
                    )
                  else
                    MenuButton(
                      enabled: false,
                      child: Text(l10n.playerSubtitleNoTracks),
                    ),
            ],
          ),
        ],
      ),
    );
  }

  String _labelForTrack(
    AppLocalizations l10n,
    PlayerSubtitleTrackOption track,
  ) {
    final title = _clean(track.title);
    final language = _clean(track.language);
    final codec = _clean(track.codec);
    final primary =
        title ??
        language ??
        codec ??
        l10n.playerSubtitleTrackFallback(track.index);
    final details = <String>[
      if (language != null && language != primary) language,
      if (codec != null && codec != primary) codec,
    ];

    if (details.isEmpty) {
      return primary;
    }

    return '$primary - ${details.join(' - ')}';
  }

  String? _clean(String? value) {
    final trimmed = value?.trim();
    if (trimmed == null || trimmed.isEmpty) {
      return null;
    }

    return trimmed;
  }
}

class _SubtitleTrackText extends StatelessWidget {
  const _SubtitleTrackText({required this.label});

  final String label;

  @override
  Widget build(BuildContext context) {
    return Text(label, maxLines: 1, overflow: TextOverflow.ellipsis);
  }
}
