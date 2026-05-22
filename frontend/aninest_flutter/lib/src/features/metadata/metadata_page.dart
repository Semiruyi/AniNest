import 'package:aninest_flutter/src/app/app_controller.dart';
import 'package:flutter/material.dart';

class MetadataPage extends StatelessWidget {
  const MetadataPage({super.key, required this.controller});

  final AppController controller;

  @override
  Widget build(BuildContext context) {
    return AnimatedBuilder(
      animation: controller,
      builder: (context, _) {
        return Padding(
          padding: const EdgeInsets.all(16),
          child: Column(
            children: [
              Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Expanded(
                    child: _Card(
                      title: 'Metadata Summary',
                      child: controller.metadataSummary == null
                          ? const Text('No summary loaded.')
                          : Wrap(
                              spacing: 8,
                              runSpacing: 8,
                              children: [
                                _metricChip(
                                  'Needs Metadata',
                                  controller.metadataSummary!.needsMetadata,
                                ),
                                _metricChip(
                                  'Queued',
                                  controller.metadataSummary!.queued,
                                ),
                                _metricChip(
                                  'Scraping',
                                  controller.metadataSummary!.scraping,
                                ),
                                _metricChip(
                                  'Ready',
                                  controller.metadataSummary!.ready,
                                ),
                                _metricChip(
                                  'Needs Review',
                                  controller.metadataSummary!.needsReview,
                                ),
                              ],
                            ),
                    ),
                  ),
                  const SizedBox(width: 16),
                  Expanded(
                    child: _Card(
                      title: 'Thumbnail Summary',
                      child: controller.thumbnailSummary == null
                          ? const Text('No thumbnail summary loaded.')
                          : Wrap(
                              spacing: 8,
                              runSpacing: 8,
                              children: [
                                _metricChip(
                                  'Pending',
                                  controller.thumbnailSummary!.pending,
                                ),
                                _metricChip(
                                  'Generating',
                                  controller.thumbnailSummary!.generating,
                                ),
                                _metricChip(
                                  'Ready',
                                  controller.thumbnailSummary!.ready,
                                ),
                                _metricChip(
                                  'Failed',
                                  controller.thumbnailSummary!.failed,
                                ),
                              ],
                            ),
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 16),
              Expanded(
                child: Row(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Expanded(
                      child: _Card(
                        title: 'Selected Folder Metadata',
                        child: controller.metadata == null
                            ? const Text(
                                'No metadata found for the selected folder.',
                              )
                            : Column(
                                crossAxisAlignment: CrossAxisAlignment.start,
                                children: [
                                  Text(
                                    controller.metadata!.title ??
                                        controller.metadata!.folderId,
                                    style: Theme.of(
                                      context,
                                    ).textTheme.titleMedium,
                                  ),
                                  const SizedBox(height: 8),
                                  Text(
                                    'State: ${controller.metadata!.state.name}',
                                  ),
                                  Text(
                                    'Failure: ${controller.metadata!.failureKind.name}',
                                  ),
                                  Text(
                                    'Source: ${controller.metadata!.source ?? 'n/a'}',
                                  ),
                                  if (controller.metadata!.summary
                                      case final summary?) ...[
                                    const SizedBox(height: 12),
                                    Text(summary),
                                  ],
                                ],
                              ),
                      ),
                    ),
                    const SizedBox(width: 16),
                    Expanded(
                      child: _Card(
                        title: 'Selected Folder Thumbnails',
                        child: controller.thumbnails.isEmpty
                            ? const Text('No thumbnail records loaded.')
                            : ListView.separated(
                                itemCount: controller.thumbnails.length,
                                separatorBuilder: (_, index) =>
                                    const Divider(height: 1),
                                itemBuilder: (context, index) {
                                  final item = controller.thumbnails[index];
                                  return ListTile(
                                    dense: true,
                                    title: Text(item.targetId),
                                    subtitle: Text(
                                      item.imagePath ?? 'Pending output',
                                    ),
                                    trailing: Text(item.state.name),
                                  );
                                },
                              ),
                      ),
                    ),
                  ],
                ),
              ),
            ],
          ),
        );
      },
    );
  }
}

class _Card extends StatelessWidget {
  const _Card({required this.title, required this.child});

  final String title;
  final Widget child;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(title, style: Theme.of(context).textTheme.titleMedium),
            const SizedBox(height: 12),
            Expanded(child: child),
          ],
        ),
      ),
    );
  }
}

Widget _metricChip(String label, int value) {
  return Chip(label: Text('$label: $value'));
}
