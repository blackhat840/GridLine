using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace GridLine
{
    internal sealed partial class LauncherWindow
    {
        private static readonly Geometry PlayCircuit = Geometry.Parse(
            "M-24,797 H288 L509,624 H951 L1247,382 H1696 " +
            "M-24,829 H316 L538,656 H1007 L1303,414 H1696 " +
            "M-24,861 H344 L567,688 H1063 L1359,446 H1696");

        private readonly Canvas _playBackdrop = new Canvas
        {
            Width = WidthOfStage,
            Height = 941,
            IsHitTestVisible = false,
            Clip = new RectangleGeometry(new Rect(0, 70, WidthOfStage, 801))
        };
        private TranslateTransform _playSweep;

        private void BuildPlayBackdrop()
        {
            SetPlayBackdropMotion(false);
            _playBackdrop.Children.Clear();
            _layer = _playBackdrop;

            var accent = ((SolidColorBrush)_accent).Color;
            var transparent = Color.FromArgb(0, accent.R, accent.G, accent.B);
            var wash = new RadialGradientBrush(Color.FromArgb(21, accent.R, accent.G, accent.B), transparent)
            {
                Center = new Point(.84, .62),
                GradientOrigin = new Point(.84, .62),
                RadiusX = .70,
                RadiusY = .86
            };
            wash.Freeze();
            Box(new Rect(0, 70, WidthOfStage, 801), wash);

            var watermark = new SolidColorBrush(Color.FromArgb(10, accent.R, accent.G, accent.B));
            watermark.Freeze();
            Glyph(Brand.Mark, new Rect(1050, 254, 530, 376), watermark, true);

            var lane = new SolidColorBrush(Color.FromArgb(28, accent.R, accent.G, accent.B));
            lane.Freeze();
            _playBackdrop.Children.Add(new Path { Data = PlayCircuit, Stroke = lane, StrokeThickness = 1.2 });

            const double sweepWidth = 320;
            _playSweep = new TranslateTransform(-sweepWidth, 0);
            var glint = new LinearGradientBrush
            {
                MappingMode = BrushMappingMode.Absolute,
                StartPoint = new Point(0, 0),
                EndPoint = new Point(sweepWidth, 0),
                Transform = _playSweep
            };
            glint.GradientStops.Add(new GradientStop(transparent, 0));
            glint.GradientStops.Add(new GradientStop(Color.FromArgb(76, accent.R, accent.G, accent.B), .68));
            glint.GradientStops.Add(new GradientStop(transparent, 1));
            _playBackdrop.Children.Add(new Path { Data = PlayCircuit, Stroke = glint, StrokeThickness = 1.8 });
        }

        private void UpdatePlayBackdrop()
        {
            bool playing = _screen == Screen.Play;
            _playBackdrop.Visibility = playing ? Visibility.Visible : Visibility.Collapsed;
            SetPlayBackdropMotion(playing && IsActive && _startupIntro == null && _overlay == Overlay.None && SystemParameters.ClientAreaAnimation);
        }

        private void SetPlayBackdropMotion(bool enabled)
        {
            if (_playSweep == null || _playSweep.HasAnimatedProperties == enabled) return;
            var animation = enabled ? new DoubleAnimation(WidthOfStage, TimeSpan.FromSeconds(18))
            {
                RepeatBehavior = RepeatBehavior.Forever
            } : null;
            _playSweep.BeginAnimation(TranslateTransform.XProperty, animation);
        }
    }
}
