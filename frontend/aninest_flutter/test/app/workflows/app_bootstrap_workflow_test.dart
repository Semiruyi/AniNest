import 'package:aninest_flutter/src/api/aninest_http_client.dart';
import 'package:aninest_flutter/src/app/coordination/host_event_coordinator.dart';
import 'package:aninest_flutter/src/app/coordination/library_metadata_selection_sync.dart';
import 'package:aninest_flutter/src/app/workflows/app_bootstrap_workflow.dart';
import 'package:aninest_flutter/src/features/library/application/library_controller.dart';
import 'package:aninest_flutter/src/features/metadata/application/metadata_controller.dart';
import 'package:aninest_flutter/src/services/host_event_service.dart';
import 'package:aninest_flutter/src/services/library_api.dart';
import 'package:aninest_flutter/src/services/metadata_api.dart';
import 'package:aninest_flutter/src/services/thumbnail_api.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  test(
    'reloadFromBackend starts host events and runs bootstrap steps in order',
    () async {
      final calls = <String>[];
      final hostEventCoordinator = _FakeHostEventCoordinator(calls);
      final selectionSync = _FakeLibraryMetadataSelectionSync(calls);
      final runOperation = _RunOperationSpy(calls);

      final workflow = AppBootstrapWorkflow(
        hostEventCoordinator: hostEventCoordinator,
        libraryMetadataSelectionSync: selectionSync,
        loadSettings: () async {
          calls.add('settings.load');
        },
        refreshLibrary: () async {
          calls.add('library.refresh');
        },
        restorePlayer: () async {
          calls.add('player.restore');
        },
        runOperation: runOperation.call,
        readLastError: () => 'last error',
      );

      final lastError = await workflow.reloadFromBackend();

      expect(lastError, 'last error');
      expect(runOperation.showSpinners, <bool>[true]);
      expect(calls, <String>[
        'host.start',
        'run.start',
        'settings.load',
        'sync.suspend.start',
        'library.refresh',
        'sync.suspend.end',
        'player.restore',
        'sync.refresh:true',
        'run.end',
      ]);
    },
  );

  test('reloadFromBackend restarts host events when requested', () async {
    final calls = <String>[];
    final hostEventCoordinator = _FakeHostEventCoordinator(calls);
    final selectionSync = _FakeLibraryMetadataSelectionSync(calls);
    final runOperation = _RunOperationSpy(calls);

    final workflow = AppBootstrapWorkflow(
      hostEventCoordinator: hostEventCoordinator,
      libraryMetadataSelectionSync: selectionSync,
      loadSettings: () async {
        calls.add('settings.load');
      },
      refreshLibrary: () async {
        calls.add('library.refresh');
      },
      restorePlayer: () async {
        calls.add('player.restore');
      },
      runOperation: runOperation.call,
      readLastError: () => null,
    );

    final lastError = await workflow.reloadFromBackend(restartHostEvents: true);

    expect(lastError, isNull);
    expect(runOperation.showSpinners, <bool>[true]);
    expect(calls.first, 'host.restart');
    expect(calls.where((entry) => entry == 'host.start'), isEmpty);
    expect(calls, <String>[
      'host.restart',
      'run.start',
      'settings.load',
      'sync.suspend.start',
      'library.refresh',
      'sync.suspend.end',
      'player.restore',
      'sync.refresh:true',
      'run.end',
    ]);
  });
}

class _RunOperationSpy {
  _RunOperationSpy(this._calls);

  final List<String> _calls;
  final showSpinners = <bool>[];

  Future<void> call(
    Future<void> Function() operation, {
    bool showSpinner = true,
  }) async {
    showSpinners.add(showSpinner);
    _calls.add('run.start');
    await operation();
    _calls.add('run.end');
  }
}

class _FakeHostEventCoordinator extends HostEventCoordinator {
  _FakeHostEventCoordinator(this._calls)
    : super(
        hostEventService: _NoopHostEventService(),
        library: _NoopLibraryController(),
        metadata: _NoopMetadataController(),
        selectedFolderId: () => null,
        refreshLibrary: () async {},
        refreshMetadataForSelection: ({bool force = false}) async {},
      );

  final List<String> _calls;

  @override
  void start() {
    _calls.add('host.start');
  }

  @override
  Future<void> restart() async {
    _calls.add('host.restart');
  }
}

class _FakeLibraryMetadataSelectionSync extends LibraryMetadataSelectionSync {
  _FakeLibraryMetadataSelectionSync(this._calls)
    : super(
        library: _NoopLibraryController(),
        metadata: _NoopMetadataController(),
      );

  final List<String> _calls;

  @override
  Future<T> runWithSelectionRefreshSuspended<T>(
    Future<T> Function() action,
  ) async {
    _calls.add('sync.suspend.start');
    final result = await action();
    _calls.add('sync.suspend.end');
    return result;
  }

  @override
  Future<void> refreshForCurrentSelection({bool force = false}) async {
    _calls.add('sync.refresh:$force');
  }
}

class _NoopHostEventService extends HostEventService {
  _NoopHostEventService()
    : super(AniNestHttpClient(baseUrl: 'http://localhost:5275'));
}

class _NoopLibraryController extends LibraryController {
  _NoopLibraryController()
    : super(LibraryApi(AniNestHttpClient(baseUrl: 'http://localhost:5275')));
}

class _NoopMetadataController extends MetadataController {
  _NoopMetadataController()
    : super(
        MetadataApi(AniNestHttpClient(baseUrl: 'http://localhost:5275')),
        ThumbnailApi(AniNestHttpClient(baseUrl: 'http://localhost:5275')),
      );
}
