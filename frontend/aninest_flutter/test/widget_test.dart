import 'package:aninest_flutter/src/api/aninest_http_client.dart';
import 'package:aninest_flutter/src/models/enums.dart';
import 'package:aninest_flutter/src/models/library_models.dart';
import 'package:aninest_flutter/src/presentation/features/library/library_page_widgets/content_widgets/library_folder_card.dart';
import 'package:aninest_flutter/src/presentation/features/library/library_page_widgets/inspector_widgets/details/library_inspector_title_block.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:shadcn_flutter/shadcn_flutter.dart';

void main() {
  test('AniNestHttpClient keeps a normalized base url', () {
    final client = AniNestHttpClient(baseUrl: 'http://localhost:5275/');

    expect(client.baseUrl, 'http://localhost:5275');

    client.updateBaseUrl('http://127.0.0.1:5275/');

    expect(client.baseUrl, 'http://127.0.0.1:5275');
  });

  testWidgets('LibraryFolderCard shows folder name instead of metadata title', (
    tester,
  ) async {
    const folder = LibraryFolderDto(
      folderId: 'bocchi-the-rock',
      name: 'Bocchi The Rock Folder',
      videoCount: 12,
      coverUrl: null,
      playedCount: 0,
      watchStatus: WatchStatus.planned,
      isFavorite: false,
      metadataSummary: LibraryMetadataSummaryDto(
        matchedTitle: 'Bocchi the Rock!',
        originalTitle: 'Bocchi the Rock!',
        posterUrl: null,
        state: 'Ready',
        hasMetadata: true,
      ),
    );

    await tester.pumpWidget(
      ShadcnApp(
        home: Material(
          child: LibraryFolderCard(
            folder: folder,
            imageUrl: null,
            isSelected: false,
            onPressed: () {},
          ),
        ),
      ),
    );

    expect(find.text('Bocchi The Rock Folder'), findsWidgets);
    expect(find.text('Bocchi the Rock!'), findsNothing);
  });

  testWidgets('LibraryInspectorTitleBlock shows folder name as primary title', (
    tester,
  ) async {
    const folder = LibraryFolderDto(
      folderId: 'bocchi-the-rock',
      name: 'Bocchi The Rock Folder',
      videoCount: 12,
      coverUrl: null,
      playedCount: 0,
      watchStatus: WatchStatus.planned,
      isFavorite: false,
      metadataSummary: LibraryMetadataSummaryDto(
        matchedTitle: 'Bocchi the Rock!',
        originalTitle: 'Bocchi the Rock!',
        posterUrl: null,
        state: 'Ready',
        hasMetadata: true,
      ),
    );

    await tester.pumpWidget(
      ShadcnApp(
        home: Material(child: LibraryInspectorTitleBlock(folder: folder)),
      ),
    );

    expect(find.text('Bocchi The Rock Folder'), findsOneWidget);
    expect(find.text('Bocchi the Rock!'), findsOneWidget);
  });
}
