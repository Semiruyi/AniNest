import 'dart:async';
import 'dart:convert';

import 'package:aninest_flutter/src/api/aninest_http_client.dart';
import 'package:aninest_flutter/src/core/logging/app_logger.dart';
import 'package:aninest_flutter/src/models/host_event_models.dart';
import 'package:http/http.dart' as http;

class HostEventService {
  HostEventService(this._client);

  final AniNestHttpClient _client;
  final StreamController<HostEventEnvelopeDto> _events =
      StreamController<HostEventEnvelopeDto>.broadcast();

  StreamSubscription<String>? _lineSubscription;
  Future<void>? _runner;
  Completer<void>? _consumeCompletion;
  Timer? _reconnectTimer;
  bool _disposed = false;
  bool _isStarted = false;

  Stream<HostEventEnvelopeDto> get events => _events.stream;

  void start() {
    if (_disposed || _isStarted) {
      return;
    }

    _isStarted = true;
    _runner = _run();
  }

  Future<void> restart() async {
    await stop();
    if (_disposed) {
      return;
    }

    start();
  }

  Future<void> stop() async {
    _isStarted = false;
    _reconnectTimer?.cancel();
    _reconnectTimer = null;
    await _lineSubscription?.cancel();
    _lineSubscription = null;
    if (_consumeCompletion != null && !_consumeCompletion!.isCompleted) {
      _consumeCompletion!.complete();
    }
    await _runner;
    _runner = null;
  }

  Future<void> dispose() async {
    _disposed = true;
    await stop();
    await _events.close();
  }

  Future<void> _run() async {
    while (_isStarted && !_disposed) {
      try {
        AppLogger.info('HostEventService', 'Connecting to host event stream.');
        final response = await _client.openGetStream(
          '/api/events',
          headers: const {'Accept': 'text/event-stream'},
        );
        if (response.statusCode >= 400) {
          AppLogger.warning(
            'HostEventService',
            'Host event stream returned status ${response.statusCode}.',
          );
          await _delayReconnect();
          continue;
        }

        await _consume(response);
      } catch (error, stackTrace) {
        AppLogger.warning(
          'HostEventService',
          'Host event stream disconnected and will retry. error=$error',
        );
        AppLogger.error(
          'HostEventService',
          'Host event stream exception.',
          error: error,
          stackTrace: stackTrace,
        );
      }

      if (_isStarted && !_disposed) {
        await _delayReconnect();
      }
    }
  }

  Future<void> _consume(http.StreamedResponse response) async {
    String? eventType;
    String? eventId;
    final dataLines = <String>[];
    final done = Completer<void>();
    _consumeCompletion = done;

    _lineSubscription = response.stream
        .transform(utf8.decoder)
        .transform(const LineSplitter())
        .listen(
          (line) {
            if (line.isEmpty) {
              _emitEnvelope(eventType, eventId, dataLines);
              eventType = null;
              eventId = null;
              dataLines.clear();
              return;
            }

            if (line.startsWith('event:')) {
              eventType = line.substring(6).trim();
              return;
            }

            if (line.startsWith('id:')) {
              eventId = line.substring(3).trim();
              return;
            }

            if (line.startsWith('data:')) {
              dataLines.add(line.substring(5).trimLeft());
            }
          },
          onDone: () {
            _emitEnvelope(eventType, eventId, dataLines);
            if (!done.isCompleted) {
              done.complete();
            }
          },
          onError: (Object error, StackTrace stackTrace) {
            if (!done.isCompleted) {
              done.completeError(error, stackTrace);
            }
          },
          cancelOnError: true,
        );

    await done.future;
    if (identical(_consumeCompletion, done)) {
      _consumeCompletion = null;
    }
  }

  void _emitEnvelope(
    String? eventType,
    String? eventId,
    List<String> dataLines,
  ) {
    if (dataLines.isEmpty) {
      return;
    }

    try {
      final decoded = jsonDecode(dataLines.join('\n'));
      if (decoded is! Map<String, dynamic>) {
        return;
      }

      final envelope = HostEventEnvelopeDto.fromJson(decoded);
      if (envelope.type.isEmpty && (eventType == null || eventType.isEmpty)) {
        return;
      }

      AppLogger.info(
        'HostEventService',
        'Decoded SSE envelope. eventType=$eventType, eventId=$eventId, envelopeType=${envelope.type}, sequence=${envelope.sequence}',
      );

      _events.add(
        envelope.type.isNotEmpty
            ? envelope
            : HostEventEnvelopeDto(
                type: eventType ?? '',
                timestampUtc: envelope.timestampUtc,
                sequence: envelope.sequence,
                payload: envelope.payload,
              ),
      );
    } catch (error, stackTrace) {
      AppLogger.error(
        'HostEventService',
        'Failed to decode host event payload.',
        error: error,
        stackTrace: stackTrace,
      );
    }
  }

  Future<void> _delayReconnect() {
    final completer = Completer<void>();
    _reconnectTimer?.cancel();
    _reconnectTimer = Timer(const Duration(seconds: 2), () {
      if (!completer.isCompleted) {
        completer.complete();
      }
    });
    return completer.future;
  }
}
