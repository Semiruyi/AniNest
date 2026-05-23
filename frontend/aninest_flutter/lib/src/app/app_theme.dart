import 'package:google_fonts/google_fonts.dart';
import 'package:shadcn_flutter/shadcn_flutter.dart' as s;

final class AppTheme {
  AppTheme._();

  static final s.ThemeData shadcnDark = s.ThemeData.dark(
    colorScheme: s.ColorSchemes.darkZinc,
    radius: 0.65,
  ).copyWith(
    typography: () => _buildTypography(),
  );

  static s.Typography _buildTypography() {
    final base = const s.Typography.geist();
    return base.copyWith(
      sans: () => _notoSans(),
      xSmall: () => _notoSans(fontSize: 12),
      small: () => _notoSans(fontSize: 14),
      base: () => _notoSans(fontSize: 16),
      large: () => _notoSans(fontSize: 18),
      xLarge: () => _notoSans(fontSize: 20),
      normal: () => _notoSans(fontWeight: s.FontWeight.w400),
      medium: () => _notoSans(fontWeight: s.FontWeight.w500),
      semiBold: () => _notoSans(fontWeight: s.FontWeight.w600),
      bold: () => _notoSans(fontWeight: s.FontWeight.w700),
      textSmall: () => _notoSans(
        fontSize: 14,
        fontWeight: s.FontWeight.w500,
      ),
      textMuted: () => _notoSans(
        fontSize: 14,
        fontWeight: s.FontWeight.w400,
      ),
    );
  }

  static s.TextStyle _notoSans({
    double? fontSize,
    s.FontWeight? fontWeight,
  }) {
    return GoogleFonts.notoSansSc(
      textStyle: s.TextStyle(
        fontSize: fontSize,
        fontWeight: fontWeight,
      ),
    );
  }
}
