import 'package:shadcn_flutter/shadcn_flutter.dart';

class LibraryInspectorArtworkCard extends StatelessWidget {
  const LibraryInspectorArtworkCard({
    super.key,
    required this.title,
    required this.imageUrl,
  });

  final String title;
  final String? imageUrl;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return SurfaceCard(
      padding: const EdgeInsets.all(10),
      child: AspectRatio(
        aspectRatio: 0.72,
        child: Container(
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(10),
            gradient: LinearGradient(
              begin: Alignment.topLeft,
              end: Alignment.bottomRight,
              colors: <Color>[
                colorScheme.secondary,
                colorScheme.secondary.withValues(alpha: 0.72),
              ],
            ),
          ),
          clipBehavior: Clip.antiAlias,
          alignment: Alignment.center,
          child: imageUrl == null
              ? _LibraryInspectorArtworkFallback(title: title)
              : Image.network(
                  imageUrl!,
                  width: double.infinity,
                  height: double.infinity,
                  fit: BoxFit.cover,
                  errorBuilder: (context, error, stackTrace) =>
                      _LibraryInspectorArtworkFallback(title: title),
                  loadingBuilder: (context, child, loadingProgress) {
                    if (loadingProgress == null) {
                      return child;
                    }
                    return _LibraryInspectorArtworkFallback(title: title);
                  },
                ),
        ),
      ),
    );
  }
}

class _LibraryInspectorArtworkFallback extends StatelessWidget {
  const _LibraryInspectorArtworkFallback({required this.title});

  final String title;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 20),
      child: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        children: <Widget>[
          Icon(
            BootstrapIcons.image,
            size: 30,
            color: colorScheme.mutedForeground,
          ),
          const Gap(10),
          Text(
            title,
            maxLines: 3,
            overflow: TextOverflow.ellipsis,
            textAlign: TextAlign.center,
            style: TextStyle(
              fontSize: 13,
              color: colorScheme.mutedForeground,
            ),
          ),
        ],
      ),
    );
  }
}
