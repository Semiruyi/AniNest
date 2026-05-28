import 'package:aninest_flutter/src/api/api_exception.dart';
import 'package:aninest_flutter/src/api/aninest_http_client.dart';
import 'package:aninest_flutter/src/core/logging/app_logger.dart';
import 'package:aninest_flutter/src/core/storage/app_preferences.dart';

typedef ReloadBackendCallback =
    Future<String?> Function({bool restartHostEvents});
typedef CreateProbeClient = AniNestHttpClient Function(String baseUrl);

class BackendConnectionCoordinator {
  BackendConnectionCoordinator({
    String? launchBaseUrl,
    required AppPreferences appPreferences,
    required AniNestHttpClient client,
    required ReloadBackendCallback reloadFromBackend,
    CreateProbeClient? createProbeClient,
  }) : _launchBaseUrl = launchBaseUrl?.trim(),
       _appPreferences = appPreferences,
       _client = client,
       _reloadFromBackend = reloadFromBackend,
       _createProbeClient =
           createProbeClient ??
           ((baseUrl) => AniNestHttpClient(baseUrl: baseUrl));

  static const String defaultBaseUrl = 'http://localhost:5275';

  final String? _launchBaseUrl;
  final AppPreferences _appPreferences;
  final AniNestHttpClient _client;
  final ReloadBackendCallback _reloadFromBackend;
  final CreateProbeClient _createProbeClient;

  bool _didHydrateBaseUrl = false;

  String get baseUrl => _client.baseUrl;

  Future<void> hydrateBaseUrl() async {
    if (_didHydrateBaseUrl) {
      return;
    }

    _didHydrateBaseUrl = true;
    final resolvedBaseUrl = await _resolveStartupBaseUrl();
    if (resolvedBaseUrl == null || resolvedBaseUrl == baseUrl) {
      return;
    }

    _client.updateBaseUrl(resolvedBaseUrl);
  }

  Future<String?> testBaseUrl(String nextBaseUrl) async {
    final validationError = _validateBaseUrl(nextBaseUrl);
    if (validationError != null) {
      return validationError;
    }

    final normalizedBaseUrl = AniNestHttpClient.normalizeBaseUrl(nextBaseUrl);
    final probeClient = _createProbeClient(normalizedBaseUrl);
    try {
      await probeClient.getObject('/api/settings');
      return null;
    } on ApiException catch (error) {
      return '${error.code}: ${error.message}';
    } catch (error) {
      return _buildConnectionFailureMessage(normalizedBaseUrl, error);
    } finally {
      probeClient.close();
    }
  }

  Future<String?> updateBaseUrl(String nextBaseUrl) async {
    final validationError = _validateBaseUrl(nextBaseUrl);
    if (validationError != null) {
      return validationError;
    }

    final normalizedBaseUrl = AniNestHttpClient.normalizeBaseUrl(nextBaseUrl);
    if (normalizedBaseUrl == baseUrl) {
      await _appPreferences.saveBaseUrl(normalizedBaseUrl);
      return null;
    }

    final previousBaseUrl = baseUrl;
    _client.updateBaseUrl(normalizedBaseUrl);
    final reloadError = await _reloadFromBackend(restartHostEvents: true);

    if (reloadError == null) {
      await _appPreferences.saveBaseUrl(normalizedBaseUrl);
      return null;
    }

    AppLogger.warning(
      'BackendConnectionCoordinator',
      'Failed to switch backend to $normalizedBaseUrl. Restoring $previousBaseUrl.',
    );

    _client.updateBaseUrl(previousBaseUrl);
    await _reloadFromBackend(restartHostEvents: true);
    return reloadError;
  }

  String? _validateBaseUrl(String candidate) {
    if (!AniNestHttpClient.isValidBaseUrl(candidate)) {
      return 'Please enter a full http:// or https:// backend address.';
    }

    return null;
  }

  Future<String?> _resolveStartupBaseUrl() async {
    final launchBaseUrl = _launchBaseUrl;
    if (launchBaseUrl != null && launchBaseUrl.isNotEmpty) {
      return _normalizeStartupBaseUrl(launchBaseUrl, source: 'launch override');
    }

    final storedBaseUrl = await _appPreferences.loadBaseUrl();
    if (storedBaseUrl == null || storedBaseUrl.trim().isEmpty) {
      return null;
    }

    return _normalizeStartupBaseUrl(storedBaseUrl, source: 'saved preference');
  }

  String? _normalizeStartupBaseUrl(String candidate, {required String source}) {
    if (!AniNestHttpClient.isValidBaseUrl(candidate)) {
      AppLogger.warning(
        'BackendConnectionCoordinator',
        'Ignoring invalid $source: $candidate',
      );
      return null;
    }

    return AniNestHttpClient.normalizeBaseUrl(candidate);
  }

  String _buildConnectionFailureMessage(String targetBaseUrl, Object error) {
    final normalized = error.toString().toLowerCase();
    if (normalized.contains('socketexception') ||
        normalized.contains('clientexception')) {
      return 'Unable to connect to the backend at $targetBaseUrl.';
    }

    return 'Unable to connect to the backend at $targetBaseUrl. $error';
  }
}
