import 'package:aninest_flutter/src/api/api_exception.dart';
import 'package:aninest_flutter/src/core/logging/app_logger.dart';
import 'package:flutter/foundation.dart';

class AppActionState extends ChangeNotifier {
  bool _isLoading = false;
  String? _lastError;

  bool get isLoading => _isLoading;
  String? get lastError => _lastError;

  Future<void> run(
    Future<void> Function() operation, {
    bool showSpinner = true,
  }) async {
    if (showSpinner) {
      _isLoading = true;
    }
    _lastError = null;
    notifyListeners();

    try {
      await operation();
    } on ApiException catch (error) {
      AppLogger.error(
        'AppController.Run',
        'ApiException during operation.',
        error: error,
        stackTrace: StackTrace.current,
      );
      _lastError = '${error.code}: ${error.message}';
    } catch (error) {
      AppLogger.error(
        'AppController.Run',
        'Unhandled exception during operation.',
        error: error,
        stackTrace: StackTrace.current,
      );
      _lastError = error.toString();
    } finally {
      _isLoading = false;
      notifyListeners();
    }
  }
}
