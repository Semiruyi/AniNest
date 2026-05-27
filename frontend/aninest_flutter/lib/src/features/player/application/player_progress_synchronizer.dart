import 'package:aninest_flutter/src/features/player/application/player_runtime_state.dart';
import 'package:aninest_flutter/src/services/session_api.dart';

class PlayerProgressSnapshot {
  const PlayerProgressSnapshot({
    required this.itemId,
    required this.positionMs,
    required this.durationMs,
    required this.rate,
    required this.volume,
    required this.isPaused,
  });

  final String itemId;
  final int positionMs;
  final int durationMs;
  final double rate;
  final int volume;
  final bool isPaused;
}

class PlayerProgressSynchronizer {
  PlayerProgressSynchronizer(this._sessionApi);

  static const Duration _minimumReportInterval = Duration(seconds: 5);
  static const Duration _minimumPositionDelta = Duration(seconds: 10);

  SessionApi _sessionApi;
  String? _lastReportedItemId;
  Duration _lastReportedPosition = Duration.zero;
  DateTime? _lastReportedAt;
  String? _completedItemId;

  void rebind(SessionApi sessionApi) {
    _sessionApi = sessionApi;
  }

  void resetForItem(String? itemId) {
    if (_lastReportedItemId == itemId) {
      return;
    }

    _lastReportedItemId = itemId;
    _lastReportedPosition = Duration.zero;
    _lastReportedAt = null;
    if (_completedItemId != itemId) {
      _completedItemId = null;
    }
  }

  Future<PlayerProgressSnapshot?> reportIfDue({
    required String? itemId,
    required PlayerRuntimeState runtime,
  }) {
    final snapshot = _buildSnapshot(itemId: itemId, runtime: runtime);
    if (snapshot == null || runtime.isCompleted) {
      return Future<PlayerProgressSnapshot?>.value();
    }

    resetForItem(snapshot.itemId);

    final now = DateTime.now();
    final lastReportedAt = _lastReportedAt;
    final intervalDue =
        lastReportedAt == null ||
        now.difference(lastReportedAt) >= _minimumReportInterval;
    final movedEnough =
        _distanceFromLast(snapshot.positionMs) >=
        _minimumPositionDelta.inMilliseconds;

    if (!intervalDue && !movedEnough) {
      return Future<PlayerProgressSnapshot?>.value();
    }

    return _report(snapshot, now);
  }

  Future<PlayerProgressSnapshot?> reportNow({
    required String? itemId,
    required PlayerRuntimeState runtime,
  }) {
    final snapshot = _buildSnapshot(itemId: itemId, runtime: runtime);
    if (snapshot == null || runtime.isCompleted) {
      return Future<PlayerProgressSnapshot?>.value();
    }

    resetForItem(snapshot.itemId);
    return _report(snapshot, DateTime.now());
  }

  Future<bool> completeIfNeeded(String? itemId) async {
    if (itemId == null || itemId.isEmpty || _completedItemId == itemId) {
      return false;
    }

    _completedItemId = itemId;
    try {
      await _sessionApi.complete(itemId);
      return true;
    } catch (_) {
      _completedItemId = null;
      rethrow;
    }
  }

  Future<PlayerProgressSnapshot?> _report(
    PlayerProgressSnapshot snapshot,
    DateTime reportedAt,
  ) async {
    _lastReportedItemId = snapshot.itemId;
    _lastReportedPosition = Duration(milliseconds: snapshot.positionMs);
    _lastReportedAt = reportedAt;

    await _sessionApi.reportProgress(
      itemId: snapshot.itemId,
      positionMs: snapshot.positionMs,
      durationMs: snapshot.durationMs,
      rate: snapshot.rate,
      volume: snapshot.volume,
      isPaused: snapshot.isPaused,
    );
    return snapshot;
  }

  PlayerProgressSnapshot? _buildSnapshot({
    required String? itemId,
    required PlayerRuntimeState runtime,
  }) {
    if (itemId == null ||
        itemId.isEmpty ||
        !runtime.hasMedia ||
        runtime.duration <= Duration.zero ||
        runtime.position <= Duration.zero) {
      return null;
    }

    final clampedPosition = runtime.position > runtime.duration
        ? runtime.duration
        : runtime.position;

    return PlayerProgressSnapshot(
      itemId: itemId,
      positionMs: clampedPosition.inMilliseconds,
      durationMs: runtime.duration.inMilliseconds,
      rate: runtime.rate,
      volume: runtime.volume.round().clamp(0, 100).toInt(),
      isPaused: !runtime.isPlaying,
    );
  }

  int _distanceFromLast(int positionMs) {
    final lastPositionMs = _lastReportedPosition.inMilliseconds;
    final distance = positionMs - lastPositionMs;
    return distance < 0 ? -distance : distance;
  }
}
