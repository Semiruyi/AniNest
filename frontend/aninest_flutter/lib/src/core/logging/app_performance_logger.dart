import 'dart:async';

import 'package:aninest_flutter/src/core/logging/app_logger.dart';

typedef PerformanceOperation<T> = FutureOr<T> Function();

final class AppPerformanceLogger {
  AppPerformanceLogger._();

  static T instant<T>(String scope, String message, T Function() action) {
    final stopwatch = Stopwatch()..start();
    try {
      final result = action();
      stopwatch.stop();
      AppLogger.info(scope, '$message completed in ${stopwatch.elapsedMilliseconds}ms');
      return result;
    } catch (error, stackTrace) {
      stopwatch.stop();
      AppLogger.error(
        scope,
        '$message failed after ${stopwatch.elapsedMilliseconds}ms',
        error: error,
        stackTrace: stackTrace,
      );
      rethrow;
    }
  }

  static Future<T> measure<T>(
    String scope,
    String message,
    PerformanceOperation<T> operation,
  ) async {
    final stopwatch = Stopwatch()..start();
    try {
      final result = await operation();
      stopwatch.stop();
      AppLogger.info(scope, '$message completed in ${stopwatch.elapsedMilliseconds}ms');
      return result;
    } catch (error, stackTrace) {
      stopwatch.stop();
      AppLogger.error(
        scope,
        '$message failed after ${stopwatch.elapsedMilliseconds}ms',
        error: error,
        stackTrace: stackTrace,
      );
      rethrow;
    }
  }

  static void mark(String scope, String message) {
    AppLogger.info(scope, message);
  }
}
