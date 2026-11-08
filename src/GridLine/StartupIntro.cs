using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace GridLine
{
    internal sealed class StartupIntro : Canvas
    {
        internal const double DurationSeconds = 2.65;
        private const double FrameWidth = 1672;
        private const double FrameHeight = 941;
        private const double LogoWidth = 980;
        private const double LogoHeight = 127;
        private static readonly Color SignalGreen = Color.FromRgb(119, 255, 71);
        private readonly Canvas _signalLines = new Canvas();
        private readonly ScaleTransform[] _signalScales = new ScaleTransform[3];
        private readonly GradientStop _signalGreen = new GradientStop(SignalGreen, -.14);
        private readonly GradientStop _signalRed = new GradientStop(Color.FromRgb(255, 62, 54), 0);
        private readonly FrameworkElement[] _streaks = new FrameworkElement[7];
        private readonly Canvas _logo = new Canvas { Width = LogoWidth, Height = LogoHeight };
        private readonly RectangleGeometry _logoClip = new RectangleGeometry();
        private readonly TranslateTransform _logoShift = new TranslateTransform();
        private readonly FrameworkElement _tagline;
        private readonly FrameworkElement _rule;
        private readonly FrameworkElement _launchGlow;

        internal StartupIntro(Brush accent, Brush ink)
        {
            Width = FrameWidth;
            Height = FrameHeight;
            ClipToBounds = true;
            Background = Paint(Color.FromRgb(8, 11, 15));
            var tint = ((SolidColorBrush)accent).Color;

            Add(new Rectangle { Fill = Halo(tint, 40) }, new Rect(236, 118, 1200, 696));
            AddCircuitLines(tint);
            AddCheckerBand(tint);
            Add(Caption("ESC   SKIP", 16, Paint(Color.FromRgb(136, 154, 171))), new Rect(1460, 850, 150, 30));

            _launchGlow = Add(new Rectangle { Fill = Halo(SignalGreen, 58), IsHitTestVisible = false }, new Rect(294, 182, 1084, 536));
            for (int i = 0; i < _streaks.Length; i++)
            {
                double width = 365 + (i % 3) * 145;
                double height = i % 3 == 0 ? 17 : 6;
                var trail = new LinearGradientBrush(Tint(SignalGreen, 0), Tint(SignalGreen, i % 3 == 0 ? (byte)195 : (byte)128), 0);
                trail.Freeze();
                var streak = new Polygon
                {
                    Points = new PointCollection { new Point(34, 0), new Point(width, 0),
                        new Point(width - 25, height), new Point(0, height) },
                    Fill = trail,
                    RenderTransform = new TranslateTransform()
                };
                _streaks[i] = Add(streak, new Rect(0, 162 + i * 94, width, height));
            }

            Add(_signalLines, new Rect(0, 230, FrameWidth, 96));
            var signal = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 0),
                GradientStops = new GradientStopCollection { _signalGreen, _signalRed }
            };
            double[] widths = { 436, 610, 492 };
            for (int i = 0; i < _signalScales.Length; i++)
            {
                double width = widths[i];
                double height = i == 1 ? 11 : 6;
                var scale = new ScaleTransform(0, 1);
                var line = new Polygon
                {
                    Points = new PointCollection { new Point(15, 0), new Point(width, 0),
                        new Point(width - 15, height), new Point(0, height) },
                    Fill = signal,
                    RenderTransform = scale
                };
                Put(_signalLines, line, new Rect((FrameWidth - width) / 2 + (i - 1) * 37, i * 24, width, height));
                _signalScales[i] = scale;
            }

            _logo.Clip = _logoClip;
            _logo.RenderTransform = _logoShift;
            Put(_logo, new Image { Source = Brand.Lockup(accent, ink), Stretch = Stretch.Uniform },
                new Rect(0, 0, LogoWidth, LogoHeight));
            Add(_logo, new Rect((FrameWidth - LogoWidth) / 2, 374, LogoWidth, LogoHeight));
            _tagline = Add(Caption("S I M   L A U N C H E R", 25, ink, TextAlignment.Center), new Rect(474, 538, 724, 34));
            _rule = Add(new Rectangle
            {
                Fill = new LinearGradientBrush(Tint(tint, 0), tint, 0),
                RenderTransformOrigin = new Point(.5, .5),
                RenderTransform = new ScaleTransform(0, 1)
            }, new Rect(495, 520, 682, 2));
            RenderFrame(0);
        }

        internal void RenderFrame(double seconds)
        {
            for (int i = 0; i < _signalScales.Length; i++)
            {
                double start = .06 + i * .085;
                _signalScales[i].ScaleX = EaseOut(Progress(seconds, start, start + .40));
            }
            double front = 1.14 * Progress(seconds, .68, .92);
            _signalGreen.Offset = front - .14;
            _signalRed.Offset = front;
            _signalLines.Opacity = 1 - Progress(seconds, .98, 1.23);

            for (int i = 0; i < _streaks.Length; i++)
            {
                double start = .90 + i * .043;
                double travel = Progress(seconds, start, start + .68);
                var streak = _streaks[i];
                ((TranslateTransform)streak.RenderTransform).X = -streak.Width + (FrameWidth + streak.Width) * travel;
                streak.Opacity = Math.Sin(travel * Math.PI);
            }
            _launchGlow.Opacity = Progress(seconds, .91, 1.10) * (1 - Progress(seconds, 1.10, 1.54));

            double reveal = EaseOut(Progress(seconds, .94, 1.49));
            _logoClip.Rect = new Rect(0, 0, LogoWidth * reveal, LogoHeight);
            _logoShift.X = -73 * (1 - reveal);
            _tagline.Opacity = Progress(seconds, 1.35, 1.71);
            double rule = EaseOut(Progress(seconds, 1.27, 1.72));
            ((ScaleTransform)_rule.RenderTransform).ScaleX = rule;
            _rule.Opacity = rule;
            Opacity = 1 - Progress(seconds, 2.21, DurationSeconds);
        }

        private void AddCircuitLines(Color tint)
        {
            var lines = new[]
            {
                "M0,198 H254 L411,74 H1116",
                "M0,222 H264 L421,98 H1072",
                "M451,941 L620,798 H1104 L1403,566 H1672",
                "M519,941 L650,825 H1115 L1420,590 H1672"
            };
            var stroke = Paint(Tint(tint, 36));
            foreach (string route in lines)
                Children.Add(new Path { Data = Geometry.Parse(route), Stroke = stroke, StrokeThickness = 1.2 });
        }

        private void AddCheckerBand(Color tint)
        {
            var square = Paint(Tint(tint, 18));
            for (int row = 0; row < 2; row++)
                for (int column = 0; column < 7; column++)
                {
                    var tile = new Polygon
                    {
                        Fill = square,
                        Points = new PointCollection { new Point(11, 0), new Point(39, 0),
                            new Point(28, 22), new Point(0, 22) }
                    };
                    Add(tile, new Rect(1121 + column * 56 + row * 17, 703 + row * 22, 39, 22));
                }
        }

        private FrameworkElement Add(FrameworkElement element, Rect bounds)
        {
            return Put(this, element, bounds);
        }

        private static FrameworkElement Put(Canvas parent, FrameworkElement element, Rect bounds)
        {
            element.Width = bounds.Width;
            element.Height = bounds.Height;
            SetLeft(element, bounds.X);
            SetTop(element, bounds.Y);
            parent.Children.Add(element);
            return element;
        }

        private static TextBlock Caption(string text, double size, Brush brush, TextAlignment alignment = TextAlignment.Left)
        {
            return new TextBlock
            {
                Text = text, Foreground = brush, FontFamily = new FontFamily("Segoe UI"), FontWeight = FontWeights.SemiBold,
                FontSize = size, TextAlignment = alignment, IsHitTestVisible = false
            };
        }

        private static Brush Paint(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        private static Brush Halo(Color color, byte strength)
        {
            var brush = new RadialGradientBrush(Tint(color, strength), Tint(color, 0));
            brush.Freeze();
            return brush;
        }

        private static Color Tint(Color color, byte alpha)
        {
            return Color.FromArgb(alpha, color.R, color.G, color.B);
        }

        private static double Progress(double seconds, double start, double end)
        {
            return Math.Max(0, Math.Min(1, (seconds - start) / (end - start)));
        }

        private static double EaseOut(double value)
        {
            double rest = 1 - value;
            return 1 - rest * rest * rest;
        }
    }

    internal sealed partial class LauncherWindow
    {
        private StartupIntro _startupIntro;
        private readonly Stopwatch _startupClock = new Stopwatch();

        private void PrepareStartupIntro()
        {
            if (!SystemParameters.ClientAreaAnimation) return;
            _startupIntro = new StartupIntro(_accent, _ink);
            _startupIntro.PreviewMouseDown += delegate(object sender, System.Windows.Input.MouseButtonEventArgs args)
            {
                args.Handled = true;
                EndStartupIntro();
            };
            _stage.Children.Add(_startupIntro);
            _bodyLayer.IsEnabled = false;
            _headerLayer.IsEnabled = false;
            _footerLayer.IsEnabled = false;
        }

        private void BeginStartupIntro()
        {
            if (_startupIntro == null || _startupClock.IsRunning || !IsLoaded ||
                !IsActive || !IsVisible || WindowState == WindowState.Minimized) return;
            _startupClock.Start();
            CompositionTarget.Rendering += AdvanceStartupIntro;
        }

        private void PauseStartupIntro()
        {
            if (!_startupClock.IsRunning) return;
            CompositionTarget.Rendering -= AdvanceStartupIntro;
            _startupClock.Stop();
        }

        private void AdvanceStartupIntro(object sender, EventArgs args)
        {
            if (_startupIntro == null) return;
            if (!IsActive || !IsVisible || WindowState == WindowState.Minimized)
            {
                PauseStartupIntro();
                return;
            }
            double seconds = _startupClock.Elapsed.TotalSeconds;
            if (seconds >= StartupIntro.DurationSeconds) EndStartupIntro();
            else _startupIntro.RenderFrame(seconds);
        }

        private void EndStartupIntro()
        {
            if (_startupIntro == null) return;
            DiscardStartupIntro();
            Draw(redrawBody: false);
            if (IsActive) RunHeaderSweep();
        }

        private void DiscardStartupIntro()
        {
            if (_startupIntro == null) return;
            PauseStartupIntro();
            _startupClock.Reset();
            _stage.Children.Remove(_startupIntro);
            _startupIntro = null;
        }
    }
}
