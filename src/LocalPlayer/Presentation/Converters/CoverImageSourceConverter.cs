using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LocalPlayer.Infrastructure.Logging;

namespace LocalPlayer.Presentation.Converters;

public sealed class CoverImageSourceConverter : IValueConverter
{
    private static readonly Logger Log = AppLog.For<CoverImageSourceConverter>();
    private static readonly Dictionary<string, ImageSource> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object SyncRoot = new();

    public int DecodePixelWidth { get; set; } = 380;

    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string path || string.IsNullOrWhiteSpace(path))
            return null;

        lock (SyncRoot)
        {
            if (Cache.TryGetValue(path, out var cached))
                return cached;
        }

        if (!File.Exists(path))
            return null;

        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            if (DecodePixelWidth > 0)
                bitmap.DecodePixelWidth = DecodePixelWidth;
            bitmap.EndInit();
            bitmap.Freeze();

            lock (SyncRoot)
                Cache[path] = bitmap;

            return bitmap;
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to load cover image: {path}", ex);
            return null;
        }
    }

    public object? ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}
