import 'package:aninest_flutter/src/models/enums.dart';

class LibraryMetadataSummaryDto {
  const LibraryMetadataSummaryDto({
    required this.matchedTitle,
    required this.originalTitle,
    required this.posterUrl,
    required this.state,
    required this.hasMetadata,
  });

  final String? matchedTitle;
  final String? originalTitle;
  final String? posterUrl;
  final String state;
  final bool hasMetadata;

  factory LibraryMetadataSummaryDto.fromJson(Map<String, dynamic> json) {
    return LibraryMetadataSummaryDto(
      matchedTitle: json['matchedTitle'] as String? ?? json['title'] as String?,
      originalTitle: json['originalTitle'] as String?,
      posterUrl: json['posterUrl'] as String?,
      state: json['state'] as String? ?? 'Unknown',
      hasMetadata: json['hasMetadata'] as bool? ?? false,
    );
  }

  LibraryMetadataSummaryDto copyWith({
    String? matchedTitle,
    String? originalTitle,
    String? posterUrl,
    String? state,
    bool? hasMetadata,
  }) {
    return LibraryMetadataSummaryDto(
      matchedTitle: matchedTitle ?? this.matchedTitle,
      originalTitle: originalTitle ?? this.originalTitle,
      posterUrl: posterUrl ?? this.posterUrl,
      state: state ?? this.state,
      hasMetadata: hasMetadata ?? this.hasMetadata,
    );
  }
}

class LibraryFolderDto {
  const LibraryFolderDto({
    required this.folderId,
    required this.name,
    required this.videoCount,
    required this.coverUrl,
    required this.playedCount,
    required this.watchStatus,
    required this.isFavorite,
    required this.addedAtUtc,
    required this.metadataSummary,
  });

  final String folderId;
  final String name;
  final int videoCount;
  final String? coverUrl;
  final int playedCount;
  final WatchStatus watchStatus;
  final bool isFavorite;
  final DateTime? addedAtUtc;
  final LibraryMetadataSummaryDto? metadataSummary;

  factory LibraryFolderDto.fromJson(Map<String, dynamic> json) {
    return LibraryFolderDto(
      folderId: json['folderId'] as String,
      name: json['name'] as String,
      videoCount: json['videoCount'] as int? ?? 0,
      coverUrl: json['coverUrl'] as String?,
      playedCount: json['playedCount'] as int? ?? 0,
      watchStatus: WatchStatus.fromJson(json['watchStatus']),
      isFavorite: json['isFavorite'] as bool? ?? false,
      addedAtUtc: _parseDateTime(json['addedAtUtc']),
      metadataSummary: json['metadataSummary'] is Map<String, dynamic>
          ? LibraryMetadataSummaryDto.fromJson(
              json['metadataSummary'] as Map<String, dynamic>,
            )
          : null,
    );
  }

  LibraryFolderDto copyWith({
    String? folderId,
    String? name,
    int? videoCount,
    String? coverUrl,
    int? playedCount,
    WatchStatus? watchStatus,
    bool? isFavorite,
    DateTime? addedAtUtc,
    LibraryMetadataSummaryDto? metadataSummary,
  }) {
    return LibraryFolderDto(
      folderId: folderId ?? this.folderId,
      name: name ?? this.name,
      videoCount: videoCount ?? this.videoCount,
      coverUrl: coverUrl ?? this.coverUrl,
      playedCount: playedCount ?? this.playedCount,
      watchStatus: watchStatus ?? this.watchStatus,
      isFavorite: isFavorite ?? this.isFavorite,
      addedAtUtc: addedAtUtc ?? this.addedAtUtc,
      metadataSummary: metadataSummary ?? this.metadataSummary,
    );
  }

  static DateTime? _parseDateTime(Object? value) {
    if (value is! String || value.isEmpty) {
      return null;
    }

    return DateTime.tryParse(value)?.toUtc();
  }
}

class AddLibraryFolderResultDto {
  const AddLibraryFolderResultDto({
    required this.status,
    required this.message,
    required this.reasonCode,
    required this.folder,
  });

  final String status;
  final String message;
  final String? reasonCode;
  final LibraryFolderDto? folder;

  bool get isAdded => status == 'added';
  bool get isAlreadyExists => status == 'alreadyExists';
  bool get isFailed => status == 'failed';

  factory AddLibraryFolderResultDto.fromJson(Map<String, dynamic> json) {
    return AddLibraryFolderResultDto(
      status: json['status'] as String? ?? 'failed',
      message: json['message'] as String? ?? 'Unknown result.',
      reasonCode: json['reasonCode'] as String?,
      folder: json['folder'] is Map<String, dynamic>
          ? LibraryFolderDto.fromJson(json['folder'] as Map<String, dynamic>)
          : null,
    );
  }
}

class LibraryBrowserDirectoryDto {
  const LibraryBrowserDirectoryDto({required this.name, required this.path});

  final String name;
  final String path;

  factory LibraryBrowserDirectoryDto.fromJson(Map<String, dynamic> json) {
    return LibraryBrowserDirectoryDto(
      name: json['name'] as String? ?? '',
      path: json['path'] as String? ?? '',
    );
  }
}

class LibraryBrowserResponse {
  const LibraryBrowserResponse({
    required this.rootPath,
    required this.currentPath,
    required this.parentPath,
    required this.canSelect,
    required this.directories,
  });

  final String rootPath;
  final String currentPath;
  final String? parentPath;
  final bool canSelect;
  final List<LibraryBrowserDirectoryDto> directories;

  factory LibraryBrowserResponse.fromJson(Map<String, dynamic> json) {
    return LibraryBrowserResponse(
      rootPath: json['rootPath'] as String? ?? '',
      currentPath: json['currentPath'] as String? ?? '',
      parentPath: json['parentPath'] as String?,
      canSelect: json['canSelect'] as bool? ?? false,
      directories: (json['directories'] as List<dynamic>? ?? const [])
          .whereType<Map<String, dynamic>>()
          .map(LibraryBrowserDirectoryDto.fromJson)
          .toList(growable: false),
    );
  }
}
