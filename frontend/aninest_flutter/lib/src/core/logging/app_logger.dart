import 'dart:developer' as developer;
import 'dart:io';

import 'package:path_provider/path_provider.dart';

final class AppLogger {
  AppLogger._();

  static String? _logFilePath;
  static bool _isWriting = false;
  static final List<String> _pendingWrites = <String>[];

  static String? get logFilePath => _logFilePath;

  static Future<void> initialize() async {
    final supportDirectory = await getApplicationSupportDirectory();
    final logDirectory = Directory(
      '${supportDirectory.path}${Platform.pathSeparator}logs',
    );
    if (!logDirectory.existsSync()) {
      await logDirectory.create(recursive: true);
    }

    final logFile = File(
      '${logDirectory.path}${Platform.pathSeparator}aninest.log',
    );
    await logFile.writeAsString('', flush: true);
    _logFilePath = logFile.path;

    info('AppLogger', 'Initialized local log file at ${logFile.path}');
  }

  static Future<void> dispose() async {}

  static void info(String scope, String message) {
    _write(scope, message, level: 800);
  }

  static void warning(String scope, String message) {
    _write(scope, message, level: 900);
  }

  static void error(
    String scope,
    String message, {
    Object? error,
    StackTrace? stackTrace,
  }) {
    _write(
      scope,
      message,
      level: 1000,
      error: error,
      stackTrace: stackTrace,
    );
  }

  static void _write(
    String scope,
    String message, {
    required int level,
    Object? error,
    StackTrace? stackTrace,
  }) {
    developer.log(
      message,
      name: scope,
      level: level,
      error: error,
      stackTrace: stackTrace,
    );

    final timestamp = DateTime.now().toIso8601String();
    final buffer = StringBuffer()
      ..write('[')
      ..write(timestamp)
      ..write(']')
      ..write('[')
      ..write(_levelName(level))
      ..write(']')
      ..write('[')
      ..write(scope)
      ..write('] ')
      ..write(message);

    if (error != null) {
      buffer
        ..write(' | error=')
        ..write(error);
    }

    if (stackTrace != null) {
      buffer
        ..write('\n')
        ..write(stackTrace);
    }

    _enqueueWrite(buffer.toString());
  }

  static String _levelName(int level) {
    if (level >= 1000) {
      return 'ERROR';
    }
    if (level >= 900) {
      return 'WARN';
    }
    return 'INFO';
  }

  static void _enqueueWrite(String line) {
    if (_logFilePath == null) {
      return;
    }

    _pendingWrites.add(line);
    if (_isWriting) {
      return;
    }

    _isWriting = true;
    _flushPendingWrites();
  }

  static Future<void> _flushPendingWrites() async {
    try {
      while (_pendingWrites.isNotEmpty) {
        final batch = _pendingWrites.join('\n');
        _pendingWrites.clear();
        await File(_logFilePath!).writeAsString(
          '$batch\n',
          mode: FileMode.append,
          flush: true,
        );
      }
    } catch (error, stackTrace) {
      developer.log(
        'Failed to persist log file.',
        name: 'AppLogger',
        level: 1000,
        error: error,
        stackTrace: stackTrace,
      );
    } finally {
      _isWriting = false;
      if (_pendingWrites.isNotEmpty) {
        _enqueueWrite('');
      }
    }
  }
}
