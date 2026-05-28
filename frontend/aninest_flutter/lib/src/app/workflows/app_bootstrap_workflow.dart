import 'package:aninest_flutter/src/app/coordination/host_event_coordinator.dart';
import 'package:aninest_flutter/src/app/coordination/library_metadata_selection_sync.dart';
import 'package:aninest_flutter/src/core/logging/app_performance_logger.dart';
import 'dart:async';

typedef RunBootstrapOperation =
    Future<void> Function(
      Future<void> Function() operation, {
      bool showSpinner,
    });
typedef LoadAppSettings = Future<void> Function();
typedef RefreshLibraryFolders = Future<void> Function();
typedef RestorePlayerSession = Future<void> Function();
typedef ReadBootstrapError = String? Function();

class AppBootstrapWorkflow {
  AppBootstrapWorkflow({
    required HostEventCoordinator hostEventCoordinator,
    required LibraryMetadataSelectionSync libraryMetadataSelectionSync,
    required LoadAppSettings loadSettings,
    required RefreshLibraryFolders refreshLibrary,
    required RestorePlayerSession restorePlayer,
    required RunBootstrapOperation runOperation,
    required ReadBootstrapError readLastError,
  }) : _hostEventCoordinator = hostEventCoordinator,
       _libraryMetadataSelectionSync = libraryMetadataSelectionSync,
       _loadSettings = loadSettings,
       _refreshLibrary = refreshLibrary,
       _restorePlayer = restorePlayer,
       _runOperation = runOperation,
       _readLastError = readLastError;

  final HostEventCoordinator _hostEventCoordinator;
  final LibraryMetadataSelectionSync _libraryMetadataSelectionSync;
  final LoadAppSettings _loadSettings;
  final RefreshLibraryFolders _refreshLibrary;
  final RestorePlayerSession _restorePlayer;
  final RunBootstrapOperation _runOperation;
  final ReadBootstrapError _readLastError;

  Future<String?> reloadFromBackend({bool restartHostEvents = false}) async {
    if (restartHostEvents) {
      await AppPerformanceLogger.measure(
        'Startup.Workflow',
        'hostEventCoordinator.restart',
        _hostEventCoordinator.restart,
      );
    } else {
      AppPerformanceLogger.mark(
        'Startup.Workflow',
        'hostEventCoordinator.start triggered',
      );
      _hostEventCoordinator.start();
    }

    await _runOperation(() async {
      await AppPerformanceLogger.measure(
        'Startup.Workflow',
        'loadSettings',
        _loadSettings,
      );
      await AppPerformanceLogger.measure(
        'Startup.Workflow',
        'refreshLibrary',
        () => _libraryMetadataSelectionSync.runWithSelectionRefreshSuspended(
          _refreshLibrary,
        ),
      );
      await AppPerformanceLogger.measure(
        'Startup.Workflow',
        'refreshForCurrentSelection',
        () => _libraryMetadataSelectionSync.refreshForCurrentSelection(
          force: true,
        ),
      );
    });
    unawaited(
      AppPerformanceLogger.measure(
        'Startup.Workflow',
        'restorePlayer',
        _restorePlayer,
      ),
    );
    return _readLastError();
  }
}
