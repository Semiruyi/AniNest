import 'package:aninest_flutter/src/app/composition/app_runtime_support.dart';
import 'package:aninest_flutter/src/app/coordination/library_metadata_selection_sync.dart';
import 'package:aninest_flutter/src/app/state/app_action_state.dart';
import 'package:aninest_flutter/src/features/library/application/library_controller.dart';
import 'package:aninest_flutter/src/features/metadata/application/metadata_controller.dart';
import 'package:aninest_flutter/src/api/aninest_http_client.dart';
import 'package:aninest_flutter/src/services/library_api.dart';
import 'package:aninest_flutter/src/services/metadata_api.dart';
import 'package:aninest_flutter/src/services/thumbnail_api.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  test('createAppRuntimeOperationRunner delegates to AppActionState', () async {
    final actionState = AppActionState();
    final runOperation = createAppRuntimeOperationRunner(actionState);
    final loadingStates = <bool>[];

    actionState.addListener(() {
      loadingStates.add(actionState.isLoading);
    });

    await runOperation(() async {});

    expect(actionState.isLoading, isFalse);
    expect(actionState.lastError, isNull);
    expect(loadingStates, <bool>[true, false]);
  });

  test(
    'refreshLibraryFromHostEvent runs without spinner and refreshes selection',
    () async {
      final calls = <String>[];
      final sync = _FakeLibraryMetadataSelectionSync(calls);

      await refreshLibraryFromHostEvent(
        runOperation:
            (
              Future<void> Function() operation, {
              bool showSpinner = true,
            }) async {
              calls.add('run:$showSpinner');
              await operation();
            },
        libraryMetadataSelectionSync: sync,
        refreshLibrary: () async {
          calls.add('library.refresh');
        },
      );

      expect(calls, <String>[
        'run:false',
        'sync.suspend.start',
        'library.refresh',
        'sync.suspend.end',
        'sync.refresh:true',
      ]);
    },
  );

  test('buildAddFolderFailureResultForBaseUrl maps network errors', () {
    final result = buildAddFolderFailureResultForBaseUrl(
      '/anime/Folder 01',
      'SocketException: Connection refused',
      baseUrl: 'http://localhost:5275',
    );

    expect(result.reasonCode, 'network_error');
    expect(
      result.message,
      'Unable to connect to the backend at http://localhost:5275. Please make sure AniNest.Host is running, then try again.',
    );
  });
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
