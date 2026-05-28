import 'package:aninest_flutter/src/api/api_exception.dart';
import 'package:aninest_flutter/src/app/state/app_action_state.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  test('run toggles loading state and clears errors after success', () async {
    final state = AppActionState();
    final loadingStates = <bool>[];
    final errors = <String?>[];

    state.addListener(() {
      loadingStates.add(state.isLoading);
      errors.add(state.lastError);
    });

    await state.run(() async {});

    expect(state.isLoading, isFalse);
    expect(state.lastError, isNull);
    expect(loadingStates, <bool>[true, false]);
    expect(errors, <String?>[null, null]);
  });

  test('run formats ApiException into a user-facing error', () async {
    final state = AppActionState();

    await state.run(() async {
      throw ApiException(
        statusCode: 503,
        code: 'service_unavailable',
        message: 'Backend offline',
      );
    });

    expect(state.isLoading, isFalse);
    expect(state.lastError, 'service_unavailable: Backend offline');
  });

  test(
    'run preserves non-spinner operations and reports generic errors',
    () async {
      final state = AppActionState();
      final loadingStates = <bool>[];

      state.addListener(() {
        loadingStates.add(state.isLoading);
      });

      await state.run(() async {
        throw StateError('boom');
      }, showSpinner: false);

      expect(state.isLoading, isFalse);
      expect(state.lastError, 'Bad state: boom');
      expect(loadingStates, <bool>[false, false]);
    },
  );
}
