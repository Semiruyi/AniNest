import 'package:aninest_flutter/src/models/enums.dart';

class ThumbnailStatusDto {
  const ThumbnailStatusDto({
    required this.targetId,
    required this.state,
    required this.progressPercent,
    required this.imagePath,
    required this.updatedAtUtc,
  });

  final String targetId;
  final ThumbnailState state;
  final double progressPercent;
  final String? imagePath;
  final DateTime? updatedAtUtc;

  factory ThumbnailStatusDto.fromJson(Map<String, dynamic> json) {
    return ThumbnailStatusDto(
      targetId: json['targetId'] as String,
      state: ThumbnailState.fromJson(json['state']),
      progressPercent: (json['progressPercent'] as num?)?.toDouble() ?? 0,
      imagePath: json['imagePath'] as String?,
      updatedAtUtc: json['updatedAtUtc'] is String
          ? DateTime.tryParse(json['updatedAtUtc'] as String)
          : null,
    );
  }
}

class ThumbnailFolderSummaryDto {
  const ThumbnailFolderSummaryDto({
    required this.folderId,
    required this.total,
    required this.pending,
    required this.generating,
    required this.ready,
    required this.failed,
    required this.completionPercent,
    required this.updatedAtUtc,
  });

  final String folderId;
  final int total;
  final int pending;
  final int generating;
  final int ready;
  final int failed;
  final double completionPercent;
  final DateTime? updatedAtUtc;

  factory ThumbnailFolderSummaryDto.fromJson(Map<String, dynamic> json) {
    return ThumbnailFolderSummaryDto(
      folderId: json['folderId'] as String,
      total: json['total'] as int? ?? 0,
      pending: json['pending'] as int? ?? 0,
      generating: json['generating'] as int? ?? 0,
      ready: json['ready'] as int? ?? 0,
      failed: json['failed'] as int? ?? 0,
      completionPercent: (json['completionPercent'] as num?)?.toDouble() ?? 0,
      updatedAtUtc: json['updatedAtUtc'] is String
          ? DateTime.tryParse(json['updatedAtUtc'] as String)
          : null,
    );
  }
}
