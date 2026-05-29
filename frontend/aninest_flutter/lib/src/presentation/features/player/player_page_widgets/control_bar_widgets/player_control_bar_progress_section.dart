import 'package:aninest_flutter/src/features/player/application/player_controller.dart';
import 'package:shadcn_flutter/shadcn_flutter.dart';

import '../player_selector.dart';

class PlayerControlBarProgressSection extends StatefulWidget {
  const PlayerControlBarProgressSection({super.key, required this.controller});

  final PlayerController controller;

  @override
  State<PlayerControlBarProgressSection> createState() =>
      _PlayerControlBarProgressSectionState();
}

class _PlayerControlBarProgressSectionState
    extends State<PlayerControlBarProgressSection> {
  bool _isDragging = false;
  double? _dragFraction;

  @override
  Widget build(BuildContext context) {
    return PlayerSelector<
      ({bool hasMedia, Duration duration, Duration position})
    >(
      controller: widget.controller,
      selector: (state) => (
        hasMedia: state.runtime.hasMedia,
        duration: state.runtime.duration,
        position: state.runtime.position,
      ),
      builder: (BuildContext context, value) {
        final duration = value.duration;
        final isEnabled = value.hasMedia && duration > Duration.zero;
        final runtimeProgressFraction = duration <= Duration.zero
            ? 0.0
            : (value.position.inMicroseconds / duration.inMicroseconds).clamp(
                0.0,
                1.0,
              );
        final fraction = _isDragging
            ? _dragFraction ?? runtimeProgressFraction
            : runtimeProgressFraction;
        final previewPosition = _isDragging
            ? Duration(
                microseconds: (duration.inMicroseconds * fraction).round(),
              )
            : value.position;

        return Container(
          decoration: BoxDecoration(borderRadius: BorderRadius.circular(6)),
          padding: const EdgeInsets.symmetric(horizontal: 12),
          alignment: Alignment.center,
          child: Row(
            children: <Widget>[
              SizedBox(
                width: 52,
                child: Text(_formatDuration(previewPosition)).xSmall(),
              ),
              Expanded(
                child: Slider(
                  value: SliderValue.single(fraction),
                  onChangeStart: isEnabled
                      ? (SliderValue value) {
                          setState(() {
                            _isDragging = true;
                            _dragFraction = value.value;
                          });
                        }
                      : null,
                  onChanged: isEnabled
                      ? (SliderValue value) {
                          setState(() {
                            _dragFraction = value.value;
                          });
                        }
                      : null,
                  onChangeEnd: isEnabled
                      ? (SliderValue value) {
                          setState(() {
                            _isDragging = false;
                            _dragFraction = null;
                          });
                          widget.controller.seekToFraction(value.value);
                        }
                      : null,
                ),
              ),
              SizedBox(
                width: 52,
                child: Text(
                  _formatDuration(duration),
                  textAlign: TextAlign.right,
                ).xSmall(),
              ),
            ],
          ),
        );
      },
    );
  }

  String _formatDuration(Duration duration) {
    final totalSeconds = duration.inSeconds;
    final hours = totalSeconds ~/ 3600;
    final minutes = (totalSeconds % 3600) ~/ 60;
    final seconds = totalSeconds % 60;
    if (hours > 0) {
      return '${hours.toString().padLeft(2, '0')}:${minutes.toString().padLeft(2, '0')}:${seconds.toString().padLeft(2, '0')}';
    }
    return '${minutes.toString().padLeft(2, '0')}:${seconds.toString().padLeft(2, '0')}';
  }
}
