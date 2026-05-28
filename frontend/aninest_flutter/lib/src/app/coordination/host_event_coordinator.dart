import 'dart:async';

import 'package:aninest_flutter/src/core/logging/app_logger.dart';
import 'package:aninest_flutter/src/features/library/application/library_controller.dart';
import 'package:aninest_flutter/src/features/metadata/application/metadata_controller.dart';
import 'package:aninest_flutter/src/models/host_event_models.dart';
import 'package:aninest_flutter/src/services/host_event_service.dart';

typedef SelectedFolderIdResolver = String? Function();
typedef RefreshLibraryCallback = Future<void> Function();
typedef RefreshMetadataSelectionCallback = Future<void> Function({bool force});

class HostEventCoordinator {
  HostEventCoordinator({
    required HostEventService hostEventService,
    required LibraryController library,
    required MetadataController metadata,
    required SelectedFolderIdResolver selectedFolderId,
    required RefreshLibraryCallback refreshLibrary,
    required RefreshMetadataSelectionCallback refreshMetadataForSelection,
  }) : _hostEventService = hostEventService,
       _library = library,
       _metadata = metadata,
       _selectedFolderId = selectedFolderId,
       _refreshLibrary = refreshLibrary,
       _refreshMetadataForSelection = refreshMetadataForSelection;

  final HostEventService _hostEventService;
  final LibraryController _library;
  final MetadataController _metadata;
  final SelectedFolderIdResolver _selectedFolderId;
  final RefreshLibraryCallback _refreshLibrary;
  final RefreshMetadataSelectionCallback _refreshMetadataForSelection;

  StreamSubscription<HostEventEnvelopeDto>? _hostEventSubscription;
  int? _lastProcessedHostEventSequence;

  void start() {
    _hostEventSubscription ??= _hostEventService.events.listen(
      _handleHostEvent,
    );
    _hostEventService.start();
  }

  Future<void> restart() async {
    await _hostEventSubscription?.cancel();
    _hostEventSubscription = null;
    await _hostEventService.restart();
    start();
  }

  void dispose() {
    final subscription = _hostEventSubscription;
    _hostEventSubscription = null;
    if (subscription != null) {
      unawaited(subscription.cancel());
    }
    unawaited(_hostEventService.dispose());
  }

  Future<void> _handleHostEvent(HostEventEnvelopeDto envelope) async {
    try {
      final sequence = envelope.sequence;
      if (sequence != null &&
          _lastProcessedHostEventSequence != null &&
          sequence <= _lastProcessedHostEventSequence!) {
        return;
      }
      if (sequence != null) {
        _lastProcessedHostEventSequence = sequence;
      }

      switch (envelope.type) {
        case 'library.folder_added':
          final payload = _coercePayloadMap(envelope.payload);
          if (payload == null) {
            return;
          }

          final added = LibraryFolderAddedEventDto.fromJson(payload);
          if (added.folderId.isEmpty) {
            return;
          }
          if (added.folder != null) {
            _library.applyFolderAdded(added.folder!);
          } else {
            await _refreshLibrary();
          }
          break;

        case 'library.folder_removed':
          final payload = _coercePayloadMap(envelope.payload);
          if (payload == null) {
            return;
          }

          final removed = LibraryFolderRemovedEventDto.fromJson(payload);
          if (removed.folderId.isEmpty) {
            return;
          }
          _library.applyFolderRemoved(removed.folderId);
          if (_selectedFolderId() == null) {
            await _refreshMetadataForSelection(force: true);
          }
          break;

        case 'library.folder_updated':
          final payload = _coercePayloadMap(envelope.payload);
          if (payload == null) {
            return;
          }

          final updated = LibraryFolderUpdatedEventDto.fromJson(payload);
          if (updated.folderId.isEmpty) {
            return;
          }
          if (updated.folder != null) {
            _library.applyFolderUpdated(updated.folder!);
          } else {
            await _refreshLibrary();
          }
          break;

        case 'library.folder_reordered':
          final payload = _coercePayloadMap(envelope.payload);
          if (payload == null) {
            return;
          }

          final reordered = LibraryFolderReorderedEventDto.fromJson(payload);
          if (reordered.folderId.isEmpty) {
            return;
          }
          _library.applyFolderReordered(
            reordered.folderId,
            reordered.position ?? 0,
            folder: reordered.folder,
          );
          break;

        case 'metadata.folder_updated':
          final payload = _coercePayloadMap(envelope.payload);
          if (payload == null) {
            AppLogger.warning(
              'HostEventCoordinator',
              'Skipping metadata.folder_updated due to unsupported payload type=${envelope.payload.runtimeType}',
            );
            return;
          }

          final update = MetadataFolderUpdatedEventDto.fromJson(payload);
          if (update.folderId.isEmpty) {
            return;
          }

          _library.applyMetadataFolderUpdate(update);
          final selectedFolderId = _selectedFolderId();
          _metadata.applyFolderUpdate(update, selectedFolderId);
          if (selectedFolderId == update.folderId) {
            await _metadata.refreshSelectedFolder(update.folderId);
          }
          break;

        case 'metadata.summary_changed':
          final payload = _coercePayloadMap(envelope.payload);
          if (payload == null) {
            AppLogger.warning(
              'HostEventCoordinator',
              'Skipping metadata.summary_changed due to unsupported payload type=${envelope.payload.runtimeType}',
            );
            return;
          }

          final update = MetadataSummaryChangedEventDto.fromJson(payload);
          _metadata.applySummary(update.summary);
          break;
      }
    } catch (error, stackTrace) {
      AppLogger.error(
        'HostEventCoordinator',
        'Failed to process host event ${envelope.type}.',
        error: error,
        stackTrace: stackTrace,
      );
    }
  }

  Map<String, dynamic>? _coercePayloadMap(Object? payload) {
    if (payload is Map<String, dynamic>) {
      return payload;
    }
    if (payload is Map) {
      return payload.map((key, value) => MapEntry(key.toString(), value));
    }
    return null;
  }
}
