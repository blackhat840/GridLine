using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Animation;
using System.Windows.Input;
using System.Windows.Shapes;

namespace GridLine
{
    internal sealed partial class LauncherWindow
    {
        private const double WidthOfStage = 1672;
        private static readonly FontFamily DisplayFont = new FontFamily("Arial Black, Segoe UI");
        private static readonly FontFamily BodyFont = new FontFamily("Segoe UI");
        private static readonly ControlTemplate HitTemplate = CreateHitTemplate();
        private static readonly Geometry SettingsCog = CreateSettingsCog();
        private static readonly int[] LibraryFilterWidths = { 112, 106, 90, 106, 126, 166, 127, 100, 112 };

        private enum Motion { None, Screen, Selection, Overlay }

        private readonly Canvas _bodyLayer = new Canvas { Width = 1672, Height = 941 };
        private readonly Canvas _headerLayer = new Canvas { Width = 1672, Height = 941 };
        private readonly Canvas _footerLayer = new Canvas { Width = 1672, Height = 941 };
        private readonly Canvas _overlayLayer = new Canvas { Width = 1672, Height = 941 };
        private Canvas _layer;
        private Screen? _drawnScreen;
        private string _drawnTheme;
        private TextBlock _statusText;
        private Button _footerSelectButton;
        private Button _footerDetailsButton;
        private Polyline _headerAccentLine;
        private Canvas _headerGlow;
        private TranslateTransform _headerGlowShift;
        private Brush _shell;
        private Brush _panel;
        private Brush _ink;
        private Brush _muted;
        private Brush _accent;
        private bool _fadeHero;

        private void Draw(Motion motion = Motion.None, bool redrawBody = true)
        {
            bool firstDraw = _drawnScreen == null;
            bool paletteChanged = firstDraw || _drawnTheme != _settings.Theme;
            bool screenChanged = firstDraw || _drawnScreen != _screen;
            if (firstDraw)
            {
                _stage.Children.Add(_playBackdrop);
                _stage.Children.Add(_bodyLayer);
                _stage.Children.Add(_headerLayer);
                _stage.Children.Add(_footerLayer);
                _stage.Children.Add(_overlayLayer);
                _bodyLayer.Clip = new RectangleGeometry(new Rect(0, 70, WidthOfStage, 801));
            }

            if (paletteChanged)
            {
                UpdatePalette();
                _stage.Background = _shell;
                BuildPlayBackdrop();
                _drawnTheme = _settings.Theme;
            }
            if (paletteChanged || screenChanged)
            {
                StopHeaderSweep();
                _layer = _headerLayer;
                _layer.Children.Clear();
                DrawHeader();
                _layer = _footerLayer;
                _layer.Children.Clear();
                DrawFooter();
                _drawnScreen = _screen;
            }
            _statusText.Text = _message ?? String.Empty;

            List<Game> visible = VisibleGames();
            Game selected = CurrentGame(visible);
            _footerSelectButton.IsEnabled = selected != null && !_scanning;
            _footerDetailsButton.IsEnabled = selected != null;
            if (redrawBody || paletteChanged || screenChanged)
            {
                _layer = _bodyLayer;
                _layer.Children.Clear();
                _fadeHero = motion == Motion.Selection;
                if (_screen == Screen.Play) DrawPlay(visible, selected);
                else DrawLibrary(visible, selected);
                _fadeHero = false;
                if (motion == Motion.Screen && !firstDraw) Reveal(_bodyLayer, 0.8, 7, 180);
            }

            UpdatePlayBackdrop();
            bool modal = _overlay != Overlay.None;
            _bodyLayer.IsEnabled = !modal && _startupIntro == null;
            _headerLayer.IsEnabled = !modal && _startupIntro == null;
            _footerLayer.IsEnabled = !modal && _startupIntro == null;
            _layer = _overlayLayer;
            _layer.Children.Clear();
            if (modal)
            {
                DrawOverlay(selected);
                if (motion == Motion.Overlay) Reveal(_overlayLayer, 0.68, 0, 155);
            }
        }

        private static void Reveal(FrameworkElement element, double fromOpacity, double offsetY, int milliseconds)
        {
            Animate(element, OpacityProperty, fromOpacity, 1, milliseconds);
            if (offsetY == 0 || !SystemParameters.ClientAreaAnimation) return;
            var transform = new TranslateTransform();
            element.RenderTransform = transform;
            Animate(transform, TranslateTransform.YProperty, offsetY, 0, milliseconds);
        }

        private static void Animate(Animatable target, DependencyProperty property, double from, double to, int milliseconds)
        {
            target.SetValue(property, to);
            if (!SystemParameters.ClientAreaAnimation) return;
            var animation = new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(milliseconds))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.Stop
            };
            target.BeginAnimation(property, animation, HandoffBehavior.SnapshotAndReplace);
        }

        private static void Animate(UIElement target, DependencyProperty property, double from, double to, int milliseconds)
        {
            target.SetValue(property, to);
            if (!SystemParameters.ClientAreaAnimation) return;
            target.BeginAnimation(property, new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(milliseconds))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.Stop
            }, HandoffBehavior.SnapshotAndReplace);
        }

        private void UpdatePalette()
        {
            _shell = ColorBrush(_settings.Theme == "amber" ? "#101112" :
                _settings.Theme == "blue" ? "#0A111A" : "#0B1015");
            _panel = ColorBrush(_settings.Theme == "amber" ? "#1D1C1A" :
                _settings.Theme == "blue" ? "#121D2A" : "#141C23");
            _accent = ColorBrush(_settings.Theme == "amber" ? "#FFB441" :
                _settings.Theme == "blue" ? "#4BCBFF" : "#CEFF26");
            _ink = ColorBrush("#F4F6F8");
            _muted = ColorBrush("#A2B3C4");
        }

        private void DrawHeader()
        {
            Box(new Rect(0, 0, WidthOfStage, 70), _shell);
            Polygon(new Rect(0, 70, 375, 17), _shell, null, new[]
            {
                new Point(0, 0), new Point(375, 0), new Point(357, 17), new Point(0, 17)
            });
            DrawBrand();

            DrawTab("Play", 448, _screen == Screen.Play, () => ShowScreen(Screen.Play));
            DrawTab("Library", 645, _screen == Screen.Library, () => ShowScreen(Screen.Library));

            Line(new[] { new Point(1395, 20), new Point(1395, 54) }, _muted, 1);
            Glyph(Brand.Wheel, new Rect(1415, 20, 34, 34), _ink);
            Label("RIG 01", new Rect(1471, 18, 102, 38), 25, _ink, true);
            DrawSettingsButton();

            // The active tab ends at Y=62 on every screen. This shared separator begins at Y=70.
            _headerAccentLine = Line(new[] { new Point(0, 87), new Point(357, 87),
                new Point(375, 70), new Point(WidthOfStage, 70) }, _accent, 2);
        }

        private void RunHeaderSweep()
        {
            if (_startupIntro != null || !SystemParameters.ClientAreaAnimation || _headerAccentLine == null || _headerGlow != null) return;

            const double highlightWidth = 280;
            var shift = new TranslateTransform(WidthOfStage, 0);
            var glow = new Canvas
            {
                Width = WidthOfStage, Height = 110, IsHitTestVisible = false,
                Clip = new RectangleGeometry(new Rect(0, 64, WidthOfStage, 46))
            };
            const double highlightTint = 0.35;
            var accent = ((SolidColorBrush)_accent).Color;
            var highlight = Color.FromRgb(
                (byte)(accent.R + (255 - accent.R) * highlightTint),
                (byte)(accent.G + (255 - accent.G) * highlightTint),
                (byte)(accent.B + (255 - accent.B) * highlightTint));
            glow.Children.Add(GlowStroke(shift, highlightWidth, highlight, 52, 22));
            glow.Children.Add(GlowStroke(shift, highlightWidth, highlight, 134, 11));
            glow.Children.Add(GlowStroke(shift, highlightWidth, highlight, 255, 4));
            _headerLayer.Children.Add(glow);
            _headerGlow = glow;
            _headerGlowShift = shift;

            var animation = new DoubleAnimation(-highlightWidth, WidthOfStage, TimeSpan.FromMilliseconds(1450))
            {
                FillBehavior = FillBehavior.Stop
            };
            animation.Completed += delegate
            {
                if (ReferenceEquals(_headerGlow, glow)) StopHeaderSweep();
            };
            shift.BeginAnimation(TranslateTransform.XProperty, animation, HandoffBehavior.SnapshotAndReplace);
        }

        private Polyline GlowStroke(TranslateTransform shift, double width, Color color, byte peak, double thickness)
        {
            var stroke = new LinearGradientBrush
            {
                MappingMode = BrushMappingMode.Absolute,
                StartPoint = new Point(0, 0),
                EndPoint = new Point(width, 0),
                SpreadMethod = GradientSpreadMethod.Pad,
                Transform = shift
            };
            Color clear = Color.FromArgb(0, color.R, color.G, color.B);
            stroke.GradientStops.Add(new GradientStop(clear, 0));
            stroke.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(peak / 3), color.R, color.G, color.B), 0.46));
            stroke.GradientStops.Add(new GradientStop(Color.FromArgb(peak, color.R, color.G, color.B), 0.72));
            stroke.GradientStops.Add(new GradientStop(clear, 1));

            return new Polyline
            {
                Points = _headerAccentLine.Points, Stroke = stroke, StrokeThickness = thickness,
                StrokeLineJoin = PenLineJoin.Miter, IsHitTestVisible = false
            };
        }

        private void StopHeaderSweep()
        {
            if (_headerGlow == null) return;
            var shift = _headerGlowShift;
            _headerLayer.Children.Remove(_headerGlow);
            _headerGlow = null;
            _headerGlowShift = null;
            shift.BeginAnimation(TranslateTransform.XProperty, null);
        }

        private void DrawSettingsButton()
        {
            var bounds = new Rect(1591, 15, 51, 47);
            Point[] shape = Slant(bounds.Width, bounds.Height, 7);
            Polygon(bounds, _shell, _muted, shape);
            Place(new System.Windows.Shapes.Path
            {
                Data = SettingsCog, Stroke = _ink, StrokeThickness = 2,
                StrokeLineJoin = PenLineJoin.Round, Stretch = Stretch.Uniform, IsHitTestVisible = false
            }, new Rect(1603, 24, 27, 28));
            Hit(bounds, "Settings", () => ShowOverlay(Overlay.Settings), shape);
        }

        private static Geometry CreateSettingsCog()
        {
            var points = new List<Point>();
            double[] offsets = { -0.36, -0.21, 0.21, 0.36 };
            for (int tooth = 0; tooth < 8; tooth++)
            {
                for (int i = 0; i < offsets.Length; i++)
                {
                    double angle = (tooth + offsets[i]) * Math.PI / 4;
                    double radius = i == 1 || i == 2 ? 14 : 11;
                    points.Add(new Point(15 + radius * Math.Cos(angle), 15 + radius * Math.Sin(angle)));
                }
            }
            var icon = new GeometryGroup();
            icon.Children.Add(GeometryFor(points.ToArray()));
            icon.Children.Add(new EllipseGeometry(new Point(15, 15), 4.5, 4.5));
            icon.Freeze();
            return icon;
        }

        private void DrawTab(string name, double left, bool selected, Action action)
        {
            var bounds = new Rect(left, 15, 184, 47);
            if (selected) Polygon(bounds, _accent, null, Slant(bounds.Width, bounds.Height, 22));
            Brush ink = selected ? Brushes.Black : _ink;
            Glyph(name == "Play" ? Brand.Play : Brand.Library, new Rect(left + 23, 29, 21, 19), ink, true);
            Label(name.ToUpperInvariant(), new Rect(left + 48, bounds.Y, 127, bounds.Height), 28, ink, true, TextAlignment.Center);
            Hit(bounds, name, action, Slant(bounds.Width, bounds.Height, 22));
        }

        private void DrawPlay(List<Game> games, Game selected)
        {
            if (selected == null)
            {
                Label("NO GAMES TO DISPLAY", new Rect(48, 239, 1000, 76), 56, _ink, true);
                Label("Add a game or open Hidden Games in Settings to restore one.",
                    new Rect(48, 327, 1000, 45), 25, _muted, false);
                return;
            }
            var favorites = games.FindAll(IsFavorite);
            bool fullFeature = favorites.Count == 0;
            BitmapSource hero = _art.Hero(selected);
            if (hero != null)
            {
                var area = fullFeature ? new Rect(560, 70, 1112, 801) : new Rect(633, 87, 1039, 540);
                Point[] frame = { new Point(0, 0), new Point(area.Width, 0),
                    new Point(area.Width, area.Height), new Point(0, area.Height) };
                var foreground = DrawLibraryArtwork(hero, area, frame, Stretch.Uniform, fade: _fadeHero);
                foreground.OpacityMask = PlayArtworkMask(hero, area, fullFeature);
            }

            double badgeTop = fullFeature ? 289 : 149;
            if (fullFeature)
                Label("FEATURED GAME", new Rect(59, badgeTop - 49, 300, 30), 20, _accent, false);
            Badge(selected.HeroLabel.ToUpperInvariant(), new Rect(59, badgeTop, 218, 41));
            string title = selected.Id == "805550" ? "ASSETTO CORSA\nCOMPETIZIONE" : selected.Title.ToUpperInvariant();
            FittedTitle(title, new Rect(53, badgeTop + 50, 686, 198), selected.Id == "805550" ? 79 : 70, 32);
            ButtonShape(PrimaryGameLabel(selected), new Rect(40, badgeTop + 275, 324, 67),
                true, !_scanning, () => PrimaryGameAction(selected), selected.Installed ? 32 : 27);
            ButtonShape("Game Details", new Rect(369, badgeTop + 275, 292, 67), false, true, () => ShowOverlay(Overlay.Details));

            if (fullFeature) return;

            Glyph(Brand.Star, new Rect(48, 602, 24, 24), _accent, true);
            Label("FAVORITES", new Rect(80, 588, 210, 46), 34, _ink, true);
            Line(new[] { new Point(293, 619), new Point(1646, 619) }, _muted, 1);
            int start = Math.Max(0, favorites.IndexOf(selected)) / 4 * 4;
            for (int index = 0; index < 4 && start + index < favorites.Count; index++)
            {
                Game game = favorites[start + index];
                DrawHomeTile(game, new Rect(23 + index * 410, 647, 396, 183), game == selected);
            }
            DrawPager(start, 4, favorites.Count, 1440, 585);
        }

        private void DrawLibrary(List<Game> games, Game selected)
        {
            LibraryTitleAccent(30, 139, 51);
            FittedTitle("GAME LIBRARY", new Rect(51, 114, 738, 94), 69, 45);

            double filterLeft = 17;
            for (int i = 0; i < Filters.Length; i++)
            {
                int index = i;
                double width = LibraryFilterWidths[i];
                var bounds = new Rect(filterLeft, 220, width, 46);
                ButtonShape(Filters[i], bounds, i == _filter, true, () => ChangeFilter(index), 18, labelInset: 8);
                filterLeft += width + 11;
            }

            if (selected == null)
            {
                var empty = new Rect(16, 296, 1138, 245);
                DrawLibraryBackdrop(empty, Chamfer(empty.Width, empty.Height, 10));
                Label("NO GAMES IN THIS VIEW", new Rect(48, 350, 1000, 58), 41, _ink, true);
                Label("Try another filter, or add or restore a game in Settings.",
                    new Rect(52, 418, 1040, 34), 24, _muted, false);
                return;
            }

            int start = (games.IndexOf(selected) / 6) * 6;
            for (int index = 0; index < 6 && start + index < games.Count; index++)
            {
                Game game = games[start + index];
                DrawLibraryTile(game, new Rect(16 + index % 3 * 383, 296 + index / 3 * 275, 372, 263),
                    game == selected);
            }

            DrawLibraryFeature(selected);
            string count = games.Count.ToString(CultureInfo.InvariantCulture) + (games.Count == 1 ? " GAME" : " GAMES");
            Label(count, new Rect(17, 834, 175, 31), 17, _muted, false);
            DrawPager(start, 6, games.Count, 960, 834);
        }

        private void DrawLibraryFeature(Game game)
        {
            var detail = new Rect(1170, 105, 487, 735);
            Point[] frame = { new Point(28, 0), new Point(487, 0), new Point(487, 707),
                new Point(459, 735), new Point(0, 735), new Point(0, 28) };
            Polygon(detail, _panel, null, frame);
            var area = new Rect(1172, 107, 483, 495);
            Point[] outline = { new Point(27, 0), new Point(483, 0), new Point(483, 495),
                new Point(0, 495), new Point(0, 27) };

            BitmapSource photo = _art.LibraryFeature(game);
            if (photo == null)
            {
                DrawLibraryBackdrop(area, outline);
                Label("ARTWORK UNAVAILABLE", new Rect(1194, 320, 440, 40), 18, _muted, false, TextAlignment.Center);
            }
            else
            {
                double aspect = photo.Width / photo.Height;
                if (aspect >= .82 && aspect <= 1.28)
                {
                    DrawLibraryArtwork(photo, area, outline, Stretch.UniformToFill, fade: _fadeHero);
                }
                else
                {
                    DrawLibraryArtwork(photo, area, outline, Stretch.UniformToFill, .18);
                    var inset = new Rect(area.X + 10, area.Y + 10, area.Width - 20, area.Height - 20);
                    DrawLibraryArtwork(photo, inset, Chamfer(inset.Width, inset.Height, 17),
                        Stretch.Uniform, fade: _fadeHero);
                }
            }
            Polygon(area, Gradient(((SolidColorBrush)_panel).Color, 1, .72, true), null, outline);
            Polygon(detail, null, _muted, frame);

            LibraryTitleAccent(1195, 625, 33);
            FittedTitle(game.Title.ToUpperInvariant(), new Rect(1208, 566, 430, 96), 44, 22, VerticalAlignment.Bottom);

            Brush statusInk = game.Installed ? _accent : _muted;
            var status = new Rect(1194, 677, 172, 32);
            Polygon(status, _shell, statusInk, Slant(status.Width, status.Height, 10));
            Place(new Ellipse { Fill = statusInk }, new Rect(1209, 690, 6, 6));
            Label(game.Installed ? "INSTALLED" : game.Owned ? "OWNED" : game.FreeToPlay ? "FREE TO PLAY" : "NOT INSTALLED", new Rect(1224, 677, 135, 32),
                17, statusInk, false);
            Label(game.Category.ToUpperInvariant(), new Rect(1382, 677, 250, 32), 17, _muted, false);

            ButtonShape(PrimaryGameLabel(game), new Rect(1193, 722, 445, 62), true, !_scanning,
                () => PrimaryGameAction(game), game.Installed ? 36 : 30);
            ButtonShape("Game Details", new Rect(1193, 791, 445, 37), false, true,
                () => ShowOverlay(Overlay.Details), 23);
        }

        private void DrawPager(int start, int pageSize, int count, double left, double top)
        {
            if (count <= pageSize) return;
            ButtonShape("‹", new Rect(left, top, 41, 31), false, start > 0, () => MoveSelection(-pageSize), 21, "Previous page");
            ButtonShape("›", new Rect(left + 153, top, 41, 31), false, start + pageSize < count,
                () => MoveSelection(pageSize), 21, "Next page");
            Label(String.Format(CultureInfo.InvariantCulture, "{0} / {1}", (start / pageSize) + 1,
                (count + pageSize - 1) / pageSize), new Rect(left + 43, top, 108, 31),
                19, _muted, false, TextAlignment.Center);
        }

        private void DrawHomeTile(Game game, Rect bounds, bool selected)
        {
            Point[] outline = Slant(bounds.Width, bounds.Height, 19);
            var frame = Polygon(bounds, selected ? _accent : _muted, null, outline);
            if (selected && _fadeHero) Animate(frame, OpacityProperty, 0.63, 1, 155);
            double inset = selected ? 5 : 1;
            Rect inner = new Rect(bounds.X + inset, bounds.Y + inset,
                bounds.Width - 2 * inset, bounds.Height - 2 * inset);
            Point[] shape = Slant(inner.Width, inner.Height, 19);
            Polygon(inner, _panel, null, shape);
            BitmapSource photo = _art.Tile(game, true);
            if (photo != null)
            {
                var artBounds = new Rect(inner.X, inner.Y, inner.Width, inner.Height - 48);
                Photo(photo, artBounds, GeometryFor(shape));
            }
            else
            {
                Label(game.Category.ToUpperInvariant(), new Rect(inner.X + 23, inner.Y + inner.Height * .26,
                    inner.Width - 47, 61), 31, _muted, true, TextAlignment.Center, TextWrapping.Wrap);
            }
            Polygon(inner, Gradient(Colors.Black, .76, .53, true), null, shape);
            double captionLeft = inner.X + 21;
            FittedTitle(game.CardTitle.ToUpperInvariant(), new Rect(captionLeft, inner.Bottom - 64,
                inner.Right - captionLeft - 12, 60), 31, 17);
            Hit(bounds, game.Title, () => SelectGame(game), outline);
        }

        private void DrawLibraryTile(Game game, Rect bounds, bool selected)
        {
            Point[] outline = Chamfer(bounds.Width, bounds.Height, 9);
            Polygon(bounds, _panel, null, outline);
            BitmapSource photo = _art.Tile(game, false);
            if (photo != null) DrawLibraryArtwork(photo, bounds, outline, Stretch.UniformToFill);
            else DrawLibraryBackdrop(bounds, outline);

            Polygon(bounds, Gradient(Color.FromArgb(245, 6, 10, 15), 1, .44, true), null, outline);
            if (photo == null)
                Label(game.Category.ToUpperInvariant(), new Rect(bounds.X + 20, bounds.Y + 18, bounds.Width - 40, 30),
                    17, _muted, false);
            if (IsFavorite(game))
            {
                var badge = new Rect(bounds.Right - 56, bounds.Y + 12, 42, 36);
                Polygon(badge, ColorBrush("#E60B1015"), null, Chamfer(badge.Width, badge.Height, 5));
                Glyph(Brand.Star, new Rect(badge.X + 10, badge.Y + 7, 22, 22), _accent, true);
            }

            LibraryTitleAccent(bounds.X + 12, bounds.Bottom - 45, 27);
            FittedTitle(game.CardTitle.ToUpperInvariant(), new Rect(bounds.X + 20, bounds.Bottom - 94,
                bounds.Width - 34, 78), 32, 18, VerticalAlignment.Bottom);

            if (selected)
            {
                var glow = Polygon(bounds, null, _accent, outline);
                glow.StrokeThickness = 8;
                glow.Opacity = .24;
            }
            var border = Polygon(bounds, null, selected ? _accent : _muted, outline);
            border.StrokeThickness = selected ? 3.5 : 1;
            border.Opacity = selected ? 1 : .44;
            Hit(bounds, game.Title, () => SelectGame(game), outline);
        }

        private void LibraryTitleAccent(double left, double top, double height)
        {
            double slope = height * .42;
            var bounds = new Rect(left, top, slope + 3, height);
            Polygon(bounds, _accent, null, new[] { new Point(slope, 0), new Point(slope + 3, 0),
                new Point(3, height), new Point(0, height) });
        }

        private Polygon DrawLibraryArtwork(BitmapSource photo, Rect bounds, Point[] outline, Stretch stretch,
            double opacity = 1, bool fade = false)
        {
            var brush = new ImageBrush(photo) { Stretch = stretch, Opacity = opacity };
            brush.Freeze();
            var art = Polygon(bounds, brush, null, outline);
            if (fade) Reveal(art, .58, 0, 175);
            return art;
        }

        private static Brush PlayArtworkMask(BitmapSource photo, Rect area, bool fullFeature)
        {
            double ratio = (photo.Width / photo.Height) / (area.Width / area.Height);
            var horizontal = FeatherAxis(Math.Min(1, ratio), fullFeature ? .38 : .24, .05, false);
            var vertical = FeatherAxis(Math.Min(1, 1 / ratio), .07, .17, true);
            var drawing = new DrawingGroup { OpacityMask = horizontal };
            drawing.Children.Add(new GeometryDrawing(vertical, null, new RectangleGeometry(new Rect(0, 0, 1, 1))));
            var mask = new DrawingBrush(drawing);
            mask.Freeze();
            return mask;
        }

        private static Brush FeatherAxis(double coverage, double fadeIn, double fadeOut, bool vertical)
        {
            // Uniformly fitted artwork has equal insets on the sides that letterbox.
            double inset = (1 - coverage) / 2;
            var stops = new GradientStopCollection
            {
                new GradientStop(Colors.Transparent, inset),
                new GradientStop(Colors.White, inset + Math.Min(fadeIn, coverage * .42)),
                new GradientStop(Colors.White, 1 - inset - Math.Min(fadeOut, coverage * .23)),
                new GradientStop(Colors.Transparent, 1 - inset)
            };
            return new LinearGradientBrush(stops, new Point(0, 0), vertical ? new Point(0, 1) : new Point(1, 0));
        }

        private void DrawLibraryBackdrop(Rect bounds, Point[] outline)
        {
            var accent = ((SolidColorBrush)_accent).Color;
            var tint = Color.FromArgb(36, accent.R, accent.G, accent.B);
            Polygon(bounds, _panel, null, outline);
            Polygon(bounds, Gradient(tint, 1, 0, false), null, outline);

            for (int index = 0; index < 3; index++)
            {
                double shift = index * .12;
                Line(new[]
                {
                    new Point(bounds.X + bounds.Width * (.16 + shift), bounds.Y + bounds.Height * .88),
                    new Point(bounds.X + bounds.Width * (.50 + shift), bounds.Y + bounds.Height * .24),
                    new Point(bounds.Right - bounds.Width * .07, bounds.Y + bounds.Height * .24)
                }, ColorBrush("#327C8E9F"), 1.5);
            }
        }

        private void DrawFooter()
        {
            Box(new Rect(0, 871, WidthOfStage, 70), _shell);
            Line(new[] { new Point(0, 871), new Point(1314, 871), new Point(1335, 890), new Point(1672, 890) },
                _accent, 2);
            _footerSelectButton = FooterAction("A", "SELECT", 41, 139, Key.Enter, "Select game (A)");
            FooterAction("B", "BACK", 209, 116, Key.Escape, "Back to Play (B)", _screen == Screen.Library);
            if (_screen == Screen.Library)
            {
                FooterAction("LB", null, 347, 55, Key.PageUp, "Previous filter (LB)");
                FooterAction("RB", "CHANGE FILTER", 402, 231, Key.PageDown, "Next filter (RB)");
            }
            else FooterAction("LB / RB", "LIBRARY", 347, 218, Key.PageDown, "Open Library (LB / RB)");
            _footerDetailsButton = FooterAction("Y", "GAME DETAILS", 647, 238, Key.Y, "Open game details (Y)");
            _statusText = Label(String.Empty, new Rect(903, 889, 733, 44), 19, _muted, false, TextAlignment.Right, TextWrapping.Wrap);
        }

        private Button FooterAction(string key, string title, double x, double hitWidth, Key shortcut,
            string accessibleName, bool enabled = true)
        {
            int width = key.Length == 1 ? 31 : key.Length == 2 ? 43 : 78;
            var badge = new Rect(x, 889, width, 31);
            Brush ink = enabled ? _ink : _muted;
            bool select = key == "A";
            if (key.Length == 1)
                Place(new Ellipse { Fill = select ? _accent : Brushes.Transparent,
                    Stroke = select ? _accent : ink, StrokeThickness = 1.5 }, badge);
            else Box(badge, Brushes.Transparent, ink);
            Label(key, new Rect(x, 890, width, 29), 17, select ? Brushes.Black : ink, false, TextAlignment.Center);
            if (title != null)
                Label(title, new Rect(x + width + 13, 887, 190, 34), 20, ink, false);

            var target = new Rect(x - 9, 880, hitWidth, 49);
            var button = Hit(target, accessibleName, () => HandleNavigation(shortcut),
                Chamfer(target.Width, target.Height, 7));
            button.IsEnabled = enabled;
            return button;
        }

        private void DrawOverlay(Game selected)
        {
            Box(new Rect(0, 0, WidthOfStage, 941), ColorBrush("#B4000000"));
            if (_overlay == Overlay.Details || _overlay == Overlay.Gallery || _overlay == Overlay.ArtPicker || _overlay == Overlay.LaunchSetup)
            {
                if (selected != null)
                {
                    if (_overlay == Overlay.Details) DrawGameDetails(selected);
                    else if (_overlay == Overlay.Gallery) DrawGallery(selected);
                    else if (_overlay == Overlay.ArtPicker) DrawArtworkPicker(selected);
                    else DrawLaunchSetup(selected);
                }
                return;
            }
            Box(new Rect(430, 161, 812, _overlay == Overlay.Settings ? 676 : 614), _panel, _accent);
            if (_overlay == Overlay.Settings)
            {
                Label("SETTINGS", new Rect(478, 193, 705, 68), 56, _ink, true);
                Label("COLOR SCHEME", new Rect(481, 282, 680, 34), 25, _muted, true);
                ButtonShape("Acid Lime", new Rect(479, 322, 221, 54), _settings.Theme != "amber" && _settings.Theme != "blue",
                    true, () => ChangeTheme("lime"), 25);
                ButtonShape("Signal Amber", new Rect(727, 322, 221, 54), _settings.Theme == "amber",
                    true, () => ChangeTheme("amber"), 25);
                ButtonShape("Ice Blue", new Rect(975, 322, 221, 54), _settings.Theme == "blue",
                    true, () => ChangeTheme("blue"), 25);

                Label("GAME LIBRARY", new Rect(481, 414, 680, 34), 25, _muted, true);
                ButtonShape("Rescan Games", new Rect(479, 456, 342, 63), false, !_scanning, () => Refresh(), 28);
                ButtonShape("Add Game", new Rect(853, 456, 343, 63), false, !_scanning, () => AddGame(null), 28);
                ButtonShape("Hidden Games", new Rect(479, 537, 342, 54), false, true,
                    () => ShowOverlay(Overlay.HiddenGames), 26);
                ButtonShape("Steam Library", new Rect(853, 537, 343, 54), false, true,
                    () => ShowOverlay(Overlay.Steam), 26);
                ButtonShape(_settings.InstalledOnly ? "Installed Only / On" : "Installed Only / Off",
                    new Rect(479, 607, 221, 50), _settings.InstalledOnly, true, ChangeInstalledOnly, 18);
                ButtonShape(_settings.IgdbEnabled ? "IGDB Metadata / On" : "IGDB Metadata / Off",
                    new Rect(727, 607, 221, 50), _settings.IgdbEnabled, true, ChangeIgdb, 18);
                bool atLogin = ReadLoginStartup();
                ButtonShape(atLogin ? "Start at Login / On" : "Start at Login / Off",
                    new Rect(975, 607, 221, 50), atLogin, true, ToggleLoginStartup, 18);
                ButtonShape(_settings.KioskHandoff ? "Kiosk Handoff / On" : "Kiosk Handoff / Off",
                    new Rect(479, 668, 342, 50), _settings.KioskHandoff, true, ToggleKioskHandoff, 20);
                if (!String.IsNullOrWhiteSpace(_message))
                    Label(_message, new Rect(480, 720, 715, 29), 16, _muted, false);
                ButtonShape("Back", new Rect(479, 748, 342, 63), true, true, Back, 28);
                ButtonShape("Exit GridLine", new Rect(853, 748, 343, 63), false, true, Close, 28);
            }
            else if (_overlay == Overlay.HiddenGames)
            {
                DrawHiddenGames();
            }
            else if (_overlay == Overlay.Steam) DrawSteamSettings();
            else if (_overlay == Overlay.SteamPreview) DrawSteamPreview();
            else if (_overlay == Overlay.SteamCategory) DrawSteamCategory();
            if (!String.IsNullOrWhiteSpace(_message) && _overlay != Overlay.Settings)
                Label(_message, new Rect(480, 633, 717, 43), 19, _muted, false, TextAlignment.Left, TextWrapping.Wrap);
        }

        private void DrawHiddenGames()
        {
            const int pageSize = 5;
            var hidden = _games.FindAll(game => !IsGameVisible(game));
            int lastPage = Math.Max(0, (hidden.Count - 1) / pageSize);
            _hiddenPage = Math.Min(_hiddenPage, lastPage);

            Label("HIDDEN GAMES", new Rect(478, 193, 705, 68), 52, _ink, true);
            Label(hidden.Count == 0 ? "No games are hidden." : "Restore a game to show it in Play and Library.",
                new Rect(481, 263, 700, 37), 24, _muted, false);

            int start = _hiddenPage * pageSize;
            for (int index = 0; index < pageSize && start + index < hidden.Count; index++)
            {
                Game game = hidden[start + index];
                int top = 307 + index * 54;
                Label(game.Title, new Rect(481, top, 501, 46), 23, _ink, true,
                    TextAlignment.Left, TextWrapping.Wrap);
                ButtonShape("Restore", new Rect(1000, top, 196, 46), false, true,
                    () => SetGameHidden(game, false), 23, "Restore " + game.Title);
            }

            if (lastPage > 0)
            {
                ButtonShape("<", new Rect(1000, 585, 41, 34), false, _hiddenPage > 0,
                    () => ChangeHiddenPage(-1), 21, "Previous hidden games page");
                Label(String.Format(CultureInfo.InvariantCulture, "{0} / {1}", _hiddenPage + 1, lastPage + 1),
                    new Rect(1043, 585, 110, 34), 19, _muted, false, TextAlignment.Center);
                ButtonShape(">", new Rect(1155, 585, 41, 34), false, _hiddenPage < lastPage,
                    () => ChangeHiddenPage(1), 21, "Next hidden games page");
            }
            ButtonShape("Back", new Rect(479, 686, 342, 63), true, true, Back, 28);
        }

        private void ChangeHiddenPage(int offset)
        {
            _hiddenPage += offset;
            _message = null;
            Draw(redrawBody: false);
            MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
        }

        private void ButtonShape(string name, Rect bounds, bool primary, bool enabled, Action action,
            int size = 32, string automationName = null, Geometry icon = null, double labelInset = 0)
        {
            Brush fill = enabled && primary ? _accent : _shell;
            Brush edge = enabled && primary ? _accent : _muted;
            Polygon(bounds, fill, edge, Slant(bounds.Width, bounds.Height, 17));
            Brush ink = !enabled ? _muted : primary ? Brushes.Black : _ink;
            var title = new Rect(bounds.X + labelInset, bounds.Y, bounds.Width - labelInset * 2, bounds.Height);
            if (icon != null)
            {
                Glyph(icon, new Rect(bounds.X + 22, bounds.Y + (bounds.Height - 19) / 2, 19, 19),
                    ink, icon == Brand.Play);
                title = new Rect(bounds.X + 45, bounds.Y, bounds.Width - 58, bounds.Height);
            }
            Label(name.ToUpperInvariant(), title, size, ink, true, TextAlignment.Center);
            if (enabled) Hit(bounds, automationName ?? name, action, Slant(bounds.Width, bounds.Height, 17));
        }

        private void Badge(string label, Rect bounds)
        {
            Polygon(bounds, _shell, _muted, Slant(bounds.Width, bounds.Height, 12));
            Label(label, bounds, 24, _muted, true, TextAlignment.Center);
        }

        private Button Hit(Rect bounds, string title, Action action, Point[] outline)
        {
            Geometry geometry = GeometryFor(outline);
            var hover = new System.Windows.Shapes.Path
            {
                Data = geometry, Fill = Brushes.White, Opacity = 0, IsHitTestVisible = false
            };
            var focus = new System.Windows.Shapes.Path
            {
                Data = geometry, Stroke = _ink, StrokeThickness = 5,
                Opacity = 0, IsHitTestVisible = false
            };
            var surface = new Canvas { Clip = geometry, IsHitTestVisible = false };
            surface.Children.Add(hover);
            surface.Children.Add(focus);
            var button = new Button
            {
                Template = HitTemplate, Content = surface, Clip = geometry, Cursor = Cursors.Hand,
                FocusVisualStyle = null, HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Stretch
            };
            AutomationProperties.SetName(button, title);

            Action refreshFocus = () =>
            {
                double opacity = button.IsKeyboardFocused && !button.IsMouseOver ? 0.9 : 0;
                Animate(focus, OpacityProperty, focus.Opacity, opacity, 115);
            };
            button.MouseEnter += delegate
            {
                Animate(hover, OpacityProperty, hover.Opacity, 0.105, 115);
                refreshFocus();
            };
            button.MouseLeave += delegate
            {
                Animate(hover, OpacityProperty, hover.Opacity, 0, 130);
                refreshFocus();
            };
            button.GotKeyboardFocus += delegate { refreshFocus(); };
            button.LostKeyboardFocus += delegate { refreshFocus(); };
            button.PreviewMouseLeftButtonDown += delegate
            {
                Animate(hover, OpacityProperty, hover.Opacity, 0.23, 65);
            };
            button.PreviewMouseLeftButtonUp += delegate
            {
                Animate(hover, OpacityProperty, hover.Opacity, button.IsMouseOver ? 0.105 : 0, 125);
            };
            button.Click += delegate { action(); };
            Place(button, bounds);
            return button;
        }

        private static ControlTemplate CreateHitTemplate()
        {
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.BackgroundProperty, Brushes.Transparent);
            var content = new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetValue(ContentPresenter.ContentSourceProperty, "Content");
            border.AppendChild(content);
            return new ControlTemplate(typeof(Button)) { VisualTree = border };
        }

        private TextBlock Label(string text, Rect bounds, double size, Brush ink, bool display,
            TextAlignment alignment = TextAlignment.Left, TextWrapping wrapping = TextWrapping.NoWrap)
        {
            var block = new TextBlock
            {
                Text = text, FontFamily = display ? DisplayFont : BodyFont, FontSize = size,
                FontWeight = display ? FontWeights.Black : FontWeights.SemiBold,
                FontStyle = FontStyles.Normal,
                Foreground = ink, TextAlignment = alignment, TextWrapping = wrapping,
                TextTrimming = TextTrimming.CharacterEllipsis, IsHitTestVisible = false,
                VerticalAlignment = VerticalAlignment.Center
            };
            if (display)
            {
                block.Margin = new Thickness(5, 0, 5, 0);
                block.LayoutTransform = new ScaleTransform(.85, 1);
                block.RenderTransform = new SkewTransform(-11, 0);
                block.RenderTransformOrigin = new Point(.5, .5);
            }
            var container = new Border { Child = block, IsHitTestVisible = false, ClipToBounds = true };
            Place(container, bounds);
            return block;
        }

        private void FittedTitle(string title, Rect bounds, int largest, int smallest,
            VerticalAlignment alignment = VerticalAlignment.Center)
        {
            var text = Label(title, bounds, largest, _ink, true, TextAlignment.Left, TextWrapping.Wrap);
            text.Margin = new Thickness(18, 0, 12, 0);
            text.VerticalAlignment = alignment;
            text.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
            for (int size = largest; size >= smallest; size -= 2)
            {
                text.FontSize = size;
                text.LineHeight = size * 1.13;
                text.Measure(new Size(bounds.Width - 17, Double.PositiveInfinity));
                if (text.DesiredSize.Height <= bounds.Height - 5) break;
            }
        }

        private void Place(FrameworkElement element, Rect bounds)
        {
            element.Width = bounds.Width;
            element.Height = bounds.Height;
            Canvas.SetLeft(element, bounds.X);
            Canvas.SetTop(element, bounds.Y);
            _layer.Children.Add(element);
        }

        private void Box(Rect bounds, Brush fill, Brush stroke = null)
        {
            Place(new Rectangle { Fill = fill, Stroke = stroke, StrokeThickness = 1 }, bounds);
        }

        private Polygon Polygon(Rect bounds, Brush fill, Brush stroke, Point[] points)
        {
            var polygon = new Polygon { Fill = fill, Stroke = stroke, StrokeThickness = 1.5,
                Points = new PointCollection(points), Stretch = Stretch.None };
            Place(polygon, bounds);
            return polygon;
        }

        private Polyline Line(IEnumerable<Point> points, Brush stroke, double thickness)
        {
            var line = new Polyline { Points = new PointCollection(points), Stroke = stroke,
                StrokeThickness = thickness, StrokeLineJoin = PenLineJoin.Miter, IsHitTestVisible = false };
            _layer.Children.Add(line);
            return line;
        }

        private void Photo(BitmapSource source, Rect bounds, Geometry clip = null, bool fade = false)
        {
            var image = new Image { Source = source, Stretch = Stretch.UniformToFill, Clip = clip, IsHitTestVisible = false };
            if (clip == null) image.Clip = new RectangleGeometry(new Rect(0, 0, bounds.Width, bounds.Height));
            Place(image, bounds);
            if (fade) Reveal(image, 0.44, 0, 160);
        }

        private static Point[] Slant(double width, double height, double cut)
        {
            return new[] { new Point(cut, 0), new Point(width, 0),
                new Point(width - cut, height), new Point(0, height) };
        }

        private static Point[] Chamfer(double width, double height, double cut)
        {
            return new[] { new Point(cut, 0), new Point(width - cut, 0), new Point(width, cut),
                new Point(width, height - cut), new Point(width - cut, height), new Point(cut, height),
                new Point(0, height - cut), new Point(0, cut) };
        }

        private static Geometry GeometryFor(Point[] points)
        {
            var figure = new PathFigure { StartPoint = points[0], IsClosed = true, IsFilled = true };
            for (int i = 1; i < points.Length; i++) figure.Segments.Add(new LineSegment(points[i], true));
            var geometry = new PathGeometry(new[] { figure });
            geometry.Freeze();
            return geometry;
        }

        private static Brush Gradient(Color solid, double fullAt, double clearAt, bool vertical)
        {
            var stops = new GradientStopCollection
            {
                new GradientStop(solid, fullAt), new GradientStop(Color.FromArgb(0, solid.R, solid.G, solid.B), clearAt)
            };
            var brush = new LinearGradientBrush(stops, new Point(0, 0), vertical ? new Point(0, 1) : new Point(1, 0));
            brush.Freeze();
            return brush;
        }

        private static Brush ColorBrush(string hex)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            brush.Freeze();
            return brush;
        }
    }
}
