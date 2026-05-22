import 'package:aninest_flutter/src/api/api_exception.dart';
import 'package:aninest_flutter/src/models/playlist_models.dart';
import 'package:aninest_flutter/src/models/session_models.dart';
import 'package:aninest_flutter/src/services/playlist_api.dart';
import 'package:aninest_flutter/src/services/session_api.dart';
import 'package:flutter/foundation.dart';

class PlayerController extends ChangeNotifier {
  PlayerController(this._sessionApi, this._playlistApi);

  SessionApi _sessionApi;
  PlaylistApi _playlistApi;

  SessionStateDto? _session;
  PlaylistDto? _playlist;
  PlaybackTargetDto? _playbackTarget;

  SessionStateDto? get session => _session;
  PlaylistDto? get playlist => _playlist;
  PlaybackTargetDto? get playbackTarget => _playbackTarget;

  String? get selectedFolderId => _session?.folderId;
  String? get selectedItemId => _session?.currentItemId ?? _playlist?.currentItemId;

  void rebind(SessionApi sessionApi, PlaylistApi playlistApi) {
    _sessionApi = sessionApi;
    _playlistApi = playlistApi;
  }

  Future<void> restore() async {
    try {
      _session = await _sessionApi.getCurrent();
    } on ApiException {
      _session = null;
    }

    if (_session == null) {
      _playlist = null;
      _playbackTarget = null;
      notifyListeners();
      return;
    }

    await _refreshPlaylist();
    notifyListeners();
  }

  Future<void> openFolder(String folderId) async {
    final result = await _sessionApi.openFolder(folderId);
    _session = result.session;
    _playbackTarget = result.playbackTarget;
    await _refreshPlaylist();
    notifyListeners();
  }

  Future<void> selectItem(String itemId) async {
    final result = await _sessionApi.selectItem(itemId);
    _session = result.session;
    _playbackTarget = result.playbackTarget;
    await _refreshPlaylist();
    notifyListeners();
  }

  Future<void> moveNext() async {
    final result = await _sessionApi.moveNext();
    _session = result.session;
    _playbackTarget = result.playbackTarget;
    await _refreshPlaylist();
    notifyListeners();
  }

  Future<void> movePrevious() async {
    final result = await _sessionApi.movePrevious();
    _session = result.session;
    _playbackTarget = result.playbackTarget;
    await _refreshPlaylist();
    notifyListeners();
  }

  Future<void> closeSession() async {
    await _sessionApi.close();
    clear();
  }

  void clear() {
    final hadState =
        _session != null || _playlist != null || _playbackTarget != null;
    _session = null;
    _playlist = null;
    _playbackTarget = null;
    if (hadState) {
      notifyListeners();
    }
  }

  Future<void> _refreshPlaylist() async {
    if (_session == null) {
      _playlist = null;
      return;
    }

    try {
      _playlist = await _playlistApi.getCurrent();
    } on ApiException {
      _playlist = null;
    }
  }
}
