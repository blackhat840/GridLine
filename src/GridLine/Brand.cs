using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace GridLine
{
    internal static class Brand
    {
        internal const string MarkPath = "M9,5 H31 L27,10 H12 L6,22 H20 L23,17 H14 L17,12 H31 L23,27 H0 Z";
        internal static readonly Geometry Mark = Shape(MarkPath);
        // Custom RIDLINE outlines use the same forward slant and cuts as the G.
        private const string WordmarkPath =
            "M34,5 L51,5 L53.4,9 L51.4,14 L45.8,18 L47.2,27 L40.7,27 L39.4,19 L33.9,19 L30.7,27 L25.2,27 Z M37.5,10 L46.5,10 L47.1,11 L46.3,13 L44.9,14 L35.9,14 Z " +
            "M59,5 L64.5,5 L55.7,27 L50.2,27 Z " +
            "M68,5 L85,5 L88,10 L83.2,22 L76.2,27 L59.2,27 Z M71.5,10 L80.5,10 L81.7,12 L78.5,20 L75.7,22 L66.7,22 Z " +
            "M94,5 L99.5,5 L92.7,22 L106.2,22 L102.7,27 L85.2,27 Z " +
            "M116,5 L121.5,5 L112.7,27 L107.2,27 Z " +
            "M125,5 L130.5,5 L135.6,18.5 L141,5 L146.5,5 L137.7,27 L132.2,27 L127.1,13.5 L121.7,27 L116.2,27 Z " +
            "M150,5 L170,5 L166.5,10 L153.5,10 L152.1,13.5 L164.1,13.5 L160.6,18.5 L150.1,18.5 L148.7,22 L163.2,22 L159.7,27 L141.2,27 Z";
        internal static readonly Geometry Wordmark = Shape(WordmarkPath);
        internal static readonly Geometry Star = Shape(
            "M12,2 L15,8.6 L22,9.4 L16.7,14.3 L18.2,21.4 L12,17.8 L5.8,21.4 L7.3,14.3 L2,9.4 L9,8.6 Z");
        internal static readonly Geometry Play = Shape("M6,3 L20,12 L6,21 Z");
        internal static readonly Geometry Library = Shape("M3,3 H10 V10 H3 Z M14,3 H21 V10 H14 Z M3,14 H10 V21 H3 Z M14,14 H21 V21 H14 Z");
        internal static readonly Geometry Wheel = Shape("M12,2 A10,10 0 1 1 11.99,2 Z M2.6,10 H7 L10,12.5 V21 M21.4,10 H17 L14,12.5 V21 M10,12.5 H14");
        internal static readonly Geometry External = Shape("M13,3 H21 V11 M21,3 L10,14 M10,5 H4 V20 H19 V14");
        internal static readonly Geometry Download = Shape("M12,3 V15 M7,10 L12,15 L17,10 M4,16 V21 H20 V16");
        internal static readonly Geometry Gallery = Shape("M3,4 H21 V20 H3 Z M3,17 L9,11 L13,15 L16,12 L21,17 M15,8 H17");
        internal static readonly Geometry Link = Shape("M9,7 L12,4 A5,5 0 0 1 19,11 L16,14 M15,17 L12,20 A5,5 0 0 1 5,13 L8,10 M8,16 L16,8");

        private static Geometry Shape(string data)
        {
            Geometry shape = Geometry.Parse(data);
            shape.Freeze();
            return shape;
        }

        internal static DrawingImage Lockup(Brush accent, Brush ink)
        {
            var drawing = new DrawingGroup();
            drawing.Children.Add(new GeometryDrawing(accent, null, Mark));
            drawing.Children.Add(new GeometryDrawing(ink, null, Wordmark));
            var wordmark = new DrawingImage(drawing);
            wordmark.Freeze();
            return wordmark;
        }

        public static ImageSource WindowIcon()
        {
            var plate = new RectangleGeometry(new Rect(0, 0, 64, 64), 11, 11);
            var mark = Mark.Clone();
            mark.Transform = new MatrixTransform(1.48, 0, 0, 1.48, 9, 8);
            var drawing = new DrawingGroup();
            drawing.Children.Add(new GeometryDrawing(new SolidColorBrush(Color.FromRgb(11, 16, 21)), null, plate));
            drawing.Children.Add(new GeometryDrawing(new SolidColorBrush(Color.FromRgb(206, 255, 38)), null, mark));
            var icon = new DrawingImage(drawing);
            icon.Freeze();
            return icon;
        }
    }

    internal sealed partial class LauncherWindow
    {
        private void Glyph(Geometry geometry, Rect bounds, Brush brush, bool filled = false)
        {
            Place(new Path
            {
                Data = geometry, Stroke = filled ? null : brush, Fill = filled ? brush : null,
                StrokeThickness = 1.8, StrokeLineJoin = PenLineJoin.Round, StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round, Stretch = Stretch.Uniform, IsHitTestVisible = false
            }, bounds);
        }

        private void DrawBrand()
        {
            var image = new Image { Source = Brand.Lockup(_accent, _ink), Stretch = Stretch.Uniform, IsHitTestVisible = false };
            AutomationProperties.SetName(image, "GridLine Sim Launcher");
            Place(image, new Rect(40, 19, 270, 35));
            Label("S I M   L A U N C H E R", new Rect(41, 55, 245, 20), 13, _muted, false);
        }
    }
}
