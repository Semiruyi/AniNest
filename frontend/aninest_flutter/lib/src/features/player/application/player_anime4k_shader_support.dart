import 'dart:io';

import 'package:aninest_flutter/src/core/logging/app_logger.dart';
import 'package:flutter/services.dart';
import 'package:path_provider/path_provider.dart';

import 'player_anime4k_mode.dart';

final class PlayerAnime4kShaderSupport {
  PlayerAnime4kShaderSupport._();

  static const Map<PlayerAnime4kMode, List<String>> _relativeShaderPaths =
      <PlayerAnime4kMode, List<String>>{
        PlayerAnime4kMode.fast: <String>[
          'Anime4K_Clamp_Highlights.glsl',
          'Anime4K_Restore_CNN_M.glsl',
          'Anime4K_Upscale_CNN_x2_M.glsl',
          'Anime4K_AutoDownscalePre_x2.glsl',
          'Anime4K_AutoDownscalePre_x4.glsl',
          'Anime4K_Upscale_CNN_x2_S.glsl',
        ],
        PlayerAnime4kMode.highQuality: <String>[
          'Anime4K_Clamp_Highlights.glsl',
          'Anime4K_Restore_CNN_VL.glsl',
          'Anime4K_Upscale_CNN_x2_VL.glsl',
          'Anime4K_AutoDownscalePre_x2.glsl',
          'Anime4K_AutoDownscalePre_x4.glsl',
          'Anime4K_Upscale_CNN_x2_M.glsl',
        ],
      };

  static Directory? _shaderDirectory;

  static bool get isSupported =>
      Platform.isWindows || Platform.isLinux || Platform.isMacOS;

  static Future<String?> shaderChainFor(PlayerAnime4kMode mode) async {
    if (!isSupported || !mode.isEnabled) {
      return null;
    }

    final directory = await _ensureShadersReady();
    if (directory == null) {
      return null;
    }

    final names = _relativeShaderPaths[mode];
    if (names == null || names.isEmpty) {
      return null;
    }

    final separator = Platform.isWindows ? ';' : ':';
    return names
        .map((String name) => '${directory.path}${Platform.pathSeparator}$name')
        .join(separator);
  }

  static Future<Directory?> _ensureShadersReady() async {
    if (_shaderDirectory != null && _shaderDirectory!.existsSync()) {
      return _shaderDirectory;
    }

    try {
      final supportDirectory = await getApplicationSupportDirectory();
      final shaderDirectory = Directory(
        '${supportDirectory.path}${Platform.pathSeparator}anime4k',
      );
      if (!shaderDirectory.existsSync()) {
        await shaderDirectory.create(recursive: true);
      }

      for (final entry in _assetBundleKeys.entries) {
        final target = File(
          '${shaderDirectory.path}${Platform.pathSeparator}${entry.key}',
        );
        final sourceBytes = (await rootBundle.load(
          entry.value,
        )).buffer.asUint8List();
        await target.writeAsBytes(sourceBytes, flush: true);
      }

      _shaderDirectory = shaderDirectory;
      return shaderDirectory;
    } catch (error, stackTrace) {
      AppLogger.error(
        'Anime4K',
        'Failed to prepare shader files.',
        error: error,
        stackTrace: stackTrace,
      );
      return null;
    }
  }

  static const Map<String, String> _assetBundleKeys = <String, String>{
    'Anime4K_Clamp_Highlights.glsl':
        'assets/anime4k/Anime4K_Clamp_Highlights.glsl',
    'Anime4K_Restore_CNN_M.glsl': 'assets/anime4k/Anime4K_Restore_CNN_M.glsl',
    'Anime4K_Upscale_CNN_x2_M.glsl':
        'assets/anime4k/Anime4K_Upscale_CNN_x2_M.glsl',
    'Anime4K_AutoDownscalePre_x2.glsl':
        'assets/anime4k/Anime4K_AutoDownscalePre_x2.glsl',
    'Anime4K_AutoDownscalePre_x4.glsl':
        'assets/anime4k/Anime4K_AutoDownscalePre_x4.glsl',
    'Anime4K_Upscale_CNN_x2_S.glsl':
        'assets/anime4k/Anime4K_Upscale_CNN_x2_S.glsl',
    'Anime4K_Restore_CNN_VL.glsl': 'assets/anime4k/Anime4K_Restore_CNN_VL.glsl',
    'Anime4K_Upscale_CNN_x2_VL.glsl':
        'assets/anime4k/Anime4K_Upscale_CNN_x2_VL.glsl',
  };
}
