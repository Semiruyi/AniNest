import 'package:aninest_flutter/src/models/enums.dart';

class LibraryMetadataSummaryDto {
  const LibraryMetadataSummaryDto({
    required this.title,
    required this.posterUrl,
  });

  final String? title;
  final String? posterUrl;

  factory LibraryMetadataSummaryDto.fromJson(Map<String, dynamic> json) {
    return LibraryMetadataSummaryDto(
      title: json['title'] as String?,
      posterUrl: json['posterUrl'] as String?,
    );
  }

  LibraryMetadataSummaryDto copyWith({String? title, String? posterUrl}) {
    return LibraryMetadataSummaryDto(
      title: title ?? this.title,
      posterUrl: posterUrl ?? this.posterUrl,
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
    required this.metadataSummary,
  });

  final String folderId;
  final String name;
  final int videoCount;
  final String? coverUrl;
  final int playedCount;
  final WatchStatus watchStatus;
  final bool isFavorite;
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
      metadataSummary: metadataSummary ?? this.metadataSummary,
    );
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
