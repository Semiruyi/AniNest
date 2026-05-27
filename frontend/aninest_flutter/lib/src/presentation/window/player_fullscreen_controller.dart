import 'package:aninest_flutter/src/core/platform/app_platform.dart';
import 'package:aninest_flutter/src/core/window/window_service.dart';
import 'package:flutter/foundation.dart';
import 'package:window_manager/window_manager.dart';

class PlayerFullscreenController extends ChangeNotifier with WindowListener {
  PlayerFullscreenController(this._windowService);

  final WindowService _windowService;

  bool _isAttached = false;
  bool _isFullscreen = false;

  bool get isFullscreen => _isFullscreen;

  Future<void> attach() async {
    if (_isAttached) {
      return;
    }

    _isAttached = true;
    if (AppPlatform.isDesktop) {
      _windowService.addListener(this);
      await syncFullscreenState();
    }
  }

  void detach() {
    if (!_isAttached) {
      return;
    }

    if (AppPlatform.isDesktop) {
      _windowService.removeListener(this);
    }
    _isAttached = false;
  }

  Future<void> syncFullscreenState() async {
    if (!AppPlatform.isDesktop) {
      return;
    }

    final isFullscreen = await _windowService.isFullScreen();
    _updateFullscreen(isFullscreen);
  }

  Future<void> toggleFullscreen() => setFullscreen(!_isFullscreen);

  Future<void> setFullscreen(bool value) async {
    if (!AppPlatform.isDesktop) {
      _updateFullscreen(value);
      return;
    }

    await _windowService.setFullScreen(value);
    _updateFullscreen(value);
    await syncFullscreenState();
  }

  @override
  void onWindowEnterFullScreen() {
    _updateFullscreen(true);
  }

  @override
  void onWindowLeaveFullScreen() {
    _updateFullscreen(false);
  }

  void _updateFullscreen(bool value) {
    if (_isFullscreen == value) {
      return;
    }

    _isFullscreen = value;
    notifyListeners();
  }

  @override
  void dispose() {
    detach();
    super.dispose();
  }
}
