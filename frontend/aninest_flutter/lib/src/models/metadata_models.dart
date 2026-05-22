import 'package:aninest_flutter/src/models/enums.dart';

class MetadataDto {
  const MetadataDto({
    required this.folderId,
    required this.title,
    required this.originalTitle,
    required this.summary,
    required this.tags,
    required this.posterPath,
    required this.season,
    required this.episodeCount,
    required this.source,
    required this.state,
    required this.failureKind,
  });

  final String folderId;
  final String? title;
  final String? originalTitle;
  final String? summary;
  final List<String> tags;
  final String? posterPath;
  final String? season;
  final int? episodeCount;
  final String? source;
  final MetadataState state;
  final MetadataFailureKind failureKind;

  factory MetadataDto.fromJson(Map<String, dynamic> json) {
    return MetadataDto(
      folderId: json['folderId'] as String,
      title: json['title'] as String?,
      originalTitle: json['originalTitle'] as String?,
      summary: json['summary'] as String?,
      tags: (json['tags'] as List<dynamic>? ?? const [])
          .map((item) => item.toString())
          .toList(),
      posterPath: json['posterPath'] as String?,
      season: json['season'] as String?,
      episodeCount: json['episodeCount'] as int?,
      source: json['source'] as String?,
      state: MetadataState.fromJson(json['state']),
      failureKind: MetadataFailureKind.fromJson(json['failureKind']),
    );
  }
}

class MetadataStatusSummaryDto {
  const MetadataStatusSummaryDto({
    required this.needsMetadata,
    required this.queued,
    required this.scraping,
    required this.ready,
    required this.needsReview,
    required this.disabled,
    required this.networkError,
    required this.noMatch,
    required this.providerError,
  });

  final int needsMetadata;
  final int queued;
  final int scraping;
  final int ready;
  final int needsReview;
  final int disabled;
  final int networkError;
  final int noMatch;
  final int providerError;

  factory MetadataStatusSummaryDto.fromJson(Map<String, dynamic> json) {
    return MetadataStatusSummaryDto(
      needsMetadata: json['needsMetadata'] as int? ?? 0,
      queued: json['queued'] as int? ?? 0,
      scraping: json['scraping'] as int? ?? 0,
      ready: json['ready'] as int? ?? 0,
      needsReview: json['needsReview'] as int? ?? 0,
      disabled: json['disabled'] as int? ?? 0,
      networkError: json['networkError'] as int? ?? 0,
      noMatch: json['noMatch'] as int? ?? 0,
      providerError: json['providerError'] as int? ?? 0,
    );
  }
}
