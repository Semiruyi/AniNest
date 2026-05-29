import 'package:aninest_flutter/src/app/app_controller.dart';
import 'package:aninest_flutter/src/models/playlist_models.dart';
import 'package:flutter/foundation.dart';

class PlayerViewStateController extends ChangeNotifier {
  PlayerViewStateController({required AppController appController})
    : _appController = appController,
      _playlist = appController.playlist,
      _selectedItemId = appController.selectedItemId,
      _playbackTargetItemId = appController.playbackTarget?.itemId,
      _canTogglePlayback = appController.canTogglePlayback {
    _appController.addListener(_handleAppControllerChanged);
  }

  final AppController _appController;

  PlaylistDto? _playlist;
  String? _selectedItemId;
  String? _playbackTargetItemId;
  bool _canTogglePlayback;

  PlaylistDto? get playlist => _playlist;
  String? get selectedItemId => _selectedItemId;
  String? get playbackTargetItemId => _playbackTargetItemId;
  bool get canTogglePlayback => _canTogglePlayback;

  AppController get appController => _appController;

  void _handleAppControllerChanged() {
    final nextPlaylist = _appController.playlist;
    final nextSelectedItemId = _appController.selectedItemId;
    final nextPlaybackTargetItemId = _appController.playbackTarget?.itemId;
    final nextCanTogglePlayback = _appController.canTogglePlayback;

    final hasChanged =
        !identical(nextPlaylist, _playlist) ||
        nextSelectedItemId != _selectedItemId ||
        nextPlaybackTargetItemId != _playbackTargetItemId ||
        nextCanTogglePlayback != _canTogglePlayback;
    if (!hasChanged) {
      return;
    }

    _playlist = nextPlaylist;
    _selectedItemId = nextSelectedItemId;
    _playbackTargetItemId = nextPlaybackTargetItemId;
    _canTogglePlayback = nextCanTogglePlayback;
    notifyListeners();
  }

  @override
  void dispose() {
    _appController.removeListener(_handleAppControllerChanged);
    super.dispose();
  }
}
