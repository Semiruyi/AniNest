import 'package:aninest_flutter/src/models/enums.dart';
import 'package:aninest_flutter/src/models/metadata_models.dart';

class HostEventEnvelopeDto {
  const HostEventEnvelopeDto({
    required this.type,
    required this.timestampUtc,
    required this.payload,
  });

  final String type;
  final DateTime? timestampUtc;
  final Object? payload;

  factory HostEventEnvelopeDto.fromJson(Map<String, dynamic> json) {
    return HostEventEnvelopeDto(
      type: json['type'] as String? ?? '',
      timestampUtc: DateTime.tryParse(json['timestampUtc'] as String? ?? ''),
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
    required this.title,
    required this.posterUrl,
    required this.coverUrl,
    required this.updatedAtUtc,
  });

  final String folderId;
  final MetadataState state;
  final MetadataFailureKind failureKind;
  final bool hasMetadata;
  final String? title;
  final String? posterUrl;
  final String? coverUrl;
  final DateTime? updatedAtUtc;

  factory MetadataFolderUpdatedEventDto.fromJson(Map<String, dynamic> json) {
    return MetadataFolderUpdatedEventDto(
      folderId: json['folderId'] as String? ?? '',
      state: MetadataState.fromJson(json['state']),
      failureKind: MetadataFailureKind.fromJson(json['failureKind']),
      hasMetadata: json['hasMetadata'] as bool? ?? false,
      title: json['title'] as String?,
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
