import 'package:aninest_flutter/src/core/window/window_service.dart';
import 'package:flutter/foundation.dart';
import 'package:window_manager/window_manager.dart';

class WindowFrameController extends ChangeNotifier with WindowListener {
  WindowFrameController(this._windowService);

  final WindowService _windowService;

  bool _isAttached = false;
  bool _isMaximized = false;

  bool get isMaximized => _isMaximized;

  Future<void> attach() async {
    if (_isAttached) {
      return;
    }

    _isAttached = true;
    _windowService.addListener(this);
    await syncWindowState();
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
  }

  @override
  void onWindowUnmaximize() {
    _updateMaximized(false);
  }

  void _updateMaximized(bool value) {
    if (_isMaximized == value) {
      return;
    }

    _isMaximized = value;
    notifyListeners();
  }

  @override
  void dispose() {
    detach();
    super.dispose();
  }
}
