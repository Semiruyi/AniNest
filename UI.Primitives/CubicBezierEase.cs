using System;
using System.Windows;
using System.Windows.Media.Animation;

namespace LocalPlayer.UI.Primitives;

public class CubicBezierEase : EasingFunctionBase
{
    public static readonly DependencyProperty X1Property =
        DependencyProperty.Register(nameof(X1), typeof(double), typeof(CubicBezierEase),
            new PropertyMetadata(0.25));
    public static readonly DependencyProperty Y1Property =
        DependencyProperty.Register(nameof(Y1), typeof(double), typeof(CubicBezierEase),
            new PropertyMetadata(0.1));
    public static readonly DependencyProperty X2Property =
        DependencyProperty.Register(nameof(X2), typeof(double), typeof(CubicBezierEase),
            new PropertyMetadata(0.25));
    public static readonly DependencyProperty Y2Property =
        DependencyProperty.Register(nameof(Y2), typeof(double), typeof(CubicBezierEase),
            new PropertyMetadata(1.0));

    public double X1 { get => (double)GetValue(X1Property); set => SetValue(X1Property, value); }
    public double Y1 { get => (double)GetValue(Y1Property); set => SetValue(Y1Property, value); }
    public double X2 { get => (double)GetValue(X2Property); set => SetValue(X2Property, value); }
    public double Y2 { get => (double)GetValue(Y2Property); set => SetValue(Y2Property, value); }

    protected override double EaseInCore(double normalizedTime)
    {
        double t = normalizedTime;
        for (int i = 0; i < 8; i++)
        {
            double dx = BezierX(t) - normalizedTime;
            double slope = BezierDX(t);
            if (Math.Abs(slope) < 1e-12)
                break;
            t -= dx / slope;
        }
        return BezierY(Clamp(t));
    }

    protected override Freezable CreateInstanceCore() =>
        new CubicBezierEase { X1 = X1, Y1 = Y1, X2 = X2, Y2 = Y2 };

    private double BezierX(double t) =>
        3.0 * (1.0 - t) * (1.0 - t) * t * X1 + 3.0 * (1.0 - t) * t * t * X2 + t * t * t;

    private double BezierY(double t) =>
        3.0 * (1.0 - t) * (1.0 - t) * t * Y1 + 3.0 * (1.0 - t) * t * t * Y2 + t * t * t;

    private double BezierDX(double t) =>
        3.0 * (1.0 - t) * (1.0 - t) * X1 + 6.0 * (1.0 - t) * t * (X2 - X1) + 3.0 * t * t * (1.0 - X2);

    private static double Clamp(double t) => t < 0.0 ? 0.0 : t > 1.0 ? 1.0 : t;
}
