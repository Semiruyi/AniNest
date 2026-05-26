import 'dart:async';

import 'package:aninest_flutter/src/core/storage/app_preferences.dart';
import 'package:aninest_flutter/src/core/window/window_service.dart';
import 'package:aninest_flutter/src/core/window/window_state_snapshot.dart';
import 'package:flutter/widgets.dart';
import 'package:window_manager/window_manager.dart';

class WindowFrameController extends ChangeNotifier with WindowListener {
  WindowFrameController(this._windowService, this._appPreferences);

  final WindowService _windowService;
  final AppPreferences _appPreferences;

  bool _isAttached = false;
  bool _isMaximized = false;
  Rect? _restoredBounds;
  Timer? _persistTimer;

  bool get isMaximized => _isMaximized;

  Future<void> attach() async {
    if (_isAttached) {
      return;
    }

    _isAttached = true;
    _windowService.addListener(this);
    await syncWindowState();
    _scheduleWindowStatePersist();
  }

  void detach() {
    if (!_isAttached) {
      return;
    }

    _windowService.removeListener(this);
    _isAttached = false;
  }

  Future<void> syncWindowState() async {
    final isMaximized = await _windowService.isMaximized();
    if (!isMaximized) {
      _restoredBounds = await _windowService.getBounds();
    }
    if (_isMaximized == isMaximized) {
      return;
    }

    _isMaximized = isMaximized;
    notifyListeners();
  }

  Future<void> toggleMaximize() async {
    if (_isMaximized) {
      await _windowService.unmaximize();
    } else {
      await _windowService.maximize();
    }
  }

  Future<void> minimize() => _windowService.minimize();

  Future<void> close() => _windowService.close();

  @override
  void onWindowMaximize() {
    _updateMaximized(true);
    _scheduleWindowStatePersist();
  }

  @override
  void onWindowUnmaximize() {
    _updateMaximized(false);
    _scheduleWindowStatePersist();
  }

  @override
  void onWindowMoved() {
    _scheduleWindowStatePersist();
  }

  @override
  void onWindowResized() {
    _scheduleWindowStatePersist();
  }

  @override
  void onWindowClose() {
    _persistTimer?.cancel();
    unawaited(_persistWindowState());
  }

  void _updateMaximized(bool value) {
    if (_isMaximized == value) {
      return;
    }

    _isMaximized = value;
    notifyListeners();
  }

  void _scheduleWindowStatePersist() {
    _persistTimer?.cancel();
    _persistTimer = Timer(
      const Duration(milliseconds: 180),
      () => unawaited(_persistWindowState()),
    );
  }

  Future<void> _persistWindowState() async {
    if (!_isAttached) {
      return;
    }

    Rect? bounds = _restoredBounds;
    if (!_isMaximized) {
      bounds = await _windowService.getBounds();
      _restoredBounds = bounds;
    }

    if (bounds == null) {
      return;
    }

    await _appPreferences.saveWindowState(
      WindowStateSnapshot.fromBounds(bounds, isMaximized: _isMaximized),
    );
  }

  @override
  void dispose() {
    _persistTimer?.cancel();
    if (_isAttached) {
      unawaited(_persistWindowState());
    }
    detach();
    super.dispose();
  }
}
