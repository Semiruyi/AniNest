import 'package:aninest_flutter/src/models/enums.dart';
import 'package:aninest_flutter/src/models/library_models.dart';
import 'package:aninest_flutter/src/models/metadata_models.dart';

class HostEventEnvelopeDto {
  const HostEventEnvelopeDto({
    required this.type,
    required this.timestampUtc,
    required this.sequence,
    required this.payload,
  });

  final String type;
  final DateTime? timestampUtc;
  final int? sequence;
  final Object? payload;

  factory HostEventEnvelopeDto.fromJson(Map<String, dynamic> json) {
    return HostEventEnvelopeDto(
      type: json['type'] as String? ?? '',
      timestampUtc: DateTime.tryParse(json['timestampUtc'] as String? ?? ''),
      sequence: (json['sequence'] as num?)?.toInt(),
      payload: json['payload'],
    );
  }
}

class MetadataFolderUpdatedEventDto {
  const MetadataFolderUpdatedEventDto({
    required this.folderId,
    required this.state,
    required this.failureKind,
    required this.hasMetadata,
    required this.matchedTitle,
    required this.originalTitle,
    required this.posterUrl,
    required this.coverUrl,
    required this.updatedAtUtc,
  });

  final String folderId;
  final MetadataState state;
  final MetadataFailureKind failureKind;
  final bool hasMetadata;
  final String? matchedTitle;
  final String? originalTitle;
  final String? posterUrl;
  final String? coverUrl;
  final DateTime? updatedAtUtc;

  factory MetadataFolderUpdatedEventDto.fromJson(Map<String, dynamic> json) {
    return MetadataFolderUpdatedEventDto(
      folderId: json['folderId'] as String? ?? '',
      state: MetadataState.fromJson(json['state']),
      failureKind: MetadataFailureKind.fromJson(json['failureKind']),
      hasMetadata: json['hasMetadata'] as bool? ?? false,
      matchedTitle: json['matchedTitle'] as String? ?? json['title'] as String?,
      originalTitle: json['originalTitle'] as String?,
      posterUrl: json['posterUrl'] as String?,
      coverUrl: json['coverUrl'] as String?,
      updatedAtUtc: DateTime.tryParse(json['updatedAtUtc'] as String? ?? ''),
    );
  }
}

class MetadataSummaryChangedEventDto {
  const MetadataSummaryChangedEventDto({required this.summary});

  final MetadataStatusSummaryDto summary;

  factory MetadataSummaryChangedEventDto.fromJson(Map<String, dynamic> json) {
    return MetadataSummaryChangedEventDto(
      summary: MetadataStatusSummaryDto.fromJson(json),
    );
  }
}

class LibraryFolderAddedEventDto {
  const LibraryFolderAddedEventDto({
    required this.folderId,
    required this.path,
    required this.folder,
  });

  final String folderId;
  final String? path;
  final LibraryFolderDto? folder;

  factory LibraryFolderAddedEventDto.fromJson(Map<String, dynamic> json) {
    return LibraryFolderAddedEventDto(
      folderId: json['folderId'] as String? ?? '',
      path: json['path'] as String?,
      folder: json['folder'] is Map<String, dynamic>
          ? LibraryFolderDto.fromJson(json['folder'] as Map<String, dynamic>)
          : null,
    );
  }
}

class LibraryFolderRemovedEventDto {
  const LibraryFolderRemovedEventDto({required this.folderId});

  final String folderId;

  factory LibraryFolderRemovedEventDto.fromJson(Map<String, dynamic> json) {
    return LibraryFolderRemovedEventDto(
      folderId: json['folderId'] as String? ?? '',
    );
  }
}

class LibraryFolderUpdatedEventDto {
  const LibraryFolderUpdatedEventDto({
    required this.folderId,
    required this.isFavorite,
    required this.watchStatus,
    required this.folder,
  });

  final String folderId;
  final bool? isFavorite;
  final WatchStatus? watchStatus;
  final LibraryFolderDto? folder;

  factory LibraryFolderUpdatedEventDto.fromJson(Map<String, dynamic> json) {
    return LibraryFolderUpdatedEventDto(
      folderId: json['folderId'] as String? ?? '',
      isFavorite: json.containsKey('isFavorite')
          ? json['isFavorite'] as bool?
          : null,
      watchStatus: json.containsKey('watchStatus')
          ? WatchStatus.fromJson(json['watchStatus'])
          : null,
      folder: json['folder'] is Map<String, dynamic>
          ? LibraryFolderDto.fromJson(json['folder'] as Map<String, dynamic>)
          : null,
    );
  }
}

class LibraryFolderReorderedEventDto {
  const LibraryFolderReorderedEventDto({
    required this.folderId,
    required this.position,
    required this.folder,
  });

  final String folderId;
  final int? position;
  final LibraryFolderDto? folder;

  factory LibraryFolderReorderedEventDto.fromJson(Map<String, dynamic> json) {
    return LibraryFolderReorderedEventDto(
      folderId: json['folderId'] as String? ?? '',
      position: json['position'] as int?,
      folder: json['folder'] is Map<String, dynamic>
          ? LibraryFolderDto.fromJson(json['folder'] as Map<String, dynamic>)
          : null,
    );
  }
}
