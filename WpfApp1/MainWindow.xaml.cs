using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Xceed.Wpf.Toolkit;
using System.Drawing;

namespace WpfApp1
{
    public partial class MainWindow : Window
    {
        private const int object_radius = 3;
        private const int over_dist_squared = object_radius * object_radius;

        Point currPoint = new Point();
        Color selectedColor = new Color();
        private Line SelectedLine;

        public bool IsVector = false;
        private bool MovingStartEndPoint = false;

        private double OffsetX, OffsetY;

        const int width = 240;
        const int height = 240;
        WriteableBitmap wbitmap = new WriteableBitmap(
       width, height, 96, 96, PixelFormats.Bgra32, null);
        byte[,,] pixels = new byte[height, width, 4];

        private double TrashWidth, TrashHeight;
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            TrashWidth = 3;
            TrashHeight = 3;
            
            paintSurface.Background = Brushes.White;
        }

        public double FindDistanceToPointSquared(Point mouse_pt, Point pt1, Point pt2, out Point closest)
        {
            double dx = pt2.X - pt1.X;
            double dy = pt2.Y - pt1.Y;

            if((dx==0) && (dy == 0))
            {
                closest = pt1;
                dx = mouse_pt.X - pt1.X;
                dy = mouse_pt.Y - pt1.Y;
                return Math.Sqrt(dx * dx + dy * dy);
            }

            double t = ((mouse_pt.X - pt1.X) * dx + (mouse_pt.Y - pt1.Y) * dy) / (dx * dx + dy * dy);

            if (t < 0)
            {
                closest = new Point(pt1.X, pt1.Y);
                dx = mouse_pt.X - pt1.X;
                dy = mouse_pt.Y - pt1.Y;
            }
            else if (t > 1)
            {
                closest = new Point(pt2.X, pt2.Y);
                dx = mouse_pt.X - pt2.X;
                dy = mouse_pt.Y - pt2.Y;
            }
            else
            {
                closest = new Point(pt1.X + t * dx, pt1.Y + t * dy);
                dx = mouse_pt.X - closest.X;
                dy = mouse_pt.Y - closest.Y;
            }

            return Math.Sqrt(dx * dx + dy * dy);
        }

        public MainWindow()
        {
            InitializeComponent();
            slider_key.Value = 100;
            updatergb();
        }

        private void show_color()
        {
            int red = int.Parse(label_red.Content.ToString());
            int green = int.Parse(label_green.Content.ToString());
            int blue = int.Parse(label_blue.Content.ToString());

            curr_color.Background = new SolidColorBrush(Color.FromRgb((byte)red, (byte)green, (byte)blue));
        }

        public void updatecmyk()
        {
            double rc = double.Parse(label_red.Content.ToString()) / 255;
            double gc = double.Parse(label_green.Content.ToString()) / 255;
            double bc = double.Parse(label_blue.Content.ToString()) / 255;

            double max = rc;
            if (gc > max) max = gc;
            if (bc > max) max = bc;

            double k = 1 - max;

            double c = (1 - rc - k) / (1 - k) * 100;
            double m = (1 - gc - k) / (1 - k) * 100;
            double y = (1 - bc - k) / (1 - k) * 100;


            slider_cyan.Value = (int)c;
            slider_magenta.Value = (int)m;
            slider_yellow.Value = (int)y;
            slider_key.Value = (int)(k * 100);

            updatergb();

        }

        public void updatergb()
        {
            double cyan = double.Parse(label_cyan.Content.ToString());
            double magenta = double.Parse(label_magenta.Content.ToString());
            double yellow = double.Parse(label_yellow.Content.ToString());
            double key = double.Parse(label_key.Content.ToString());

            double red = 255 * (1 - cyan / 100) * (1 - (key / 100));
            double green = 255 * (1 - magenta / 100) * (1 - key / 100);
            double blue = 255 * (1 - yellow / 100) * (1 - key / 100);

            slider_red.Value = (int)red;
            slider_green.Value = (int)green;
            slider_blue.Value = (int)blue;

            curr_color.Background = new SolidColorBrush(Color.FromRgb((byte)red, (byte)green, (byte)blue));
        }

        private bool MouseIsOverLine(Point mouse_pt, out Line hit_line)
        {
            foreach(object obj in paintSurface.Children)
            {
                if(obj is Line)
                {
                    Line line = obj as Line;

                    Point closest;
                    Point pt1 = new Point(line.X1, line.Y1);
                    Point pt2 = new Point(line.X2, line.Y2);
                    if(FindDistanceToPointSquared(mouse_pt, pt1, pt2, out closest) < over_dist_squared)
                    {
                        hit_line = line;
                        return true;
                    }
                }
            }

            hit_line = null;
            return false;
        }

        private bool MouseIsOverEndpoint(Point mouse_pt, out Line hit_line, out bool start_endpoint)
        {
            foreach(object obj in paintSurface.Children)
            {
                if(obj is Line)
                {
                    Line line = obj as Line;
                    Point point = new Point(line.X1, line.Y1);
                    if(FindDistanceToPointSquared(mouse_pt, point, point, out point) < over_dist_squared)
                    {
                        hit_line = line;
                        start_endpoint = true;
                        return true;
                    }

                    point = new Point(line.X2, line.Y2);
                    if (FindDistanceToPointSquared(mouse_pt, point, point, out point)
                        < over_dist_squared)
                    {
                        hit_line = line;
                        start_endpoint = false;
                        return true;
                    }
                }
            }

            hit_line = null;
            start_endpoint = false;
            return false;
        }

        private void Canvas_MouseMove_NotDown(object sender, MouseEventArgs e)
        {
            Cursor new_cursor = Cursors.Cross;

            Point location = e.GetPosition(this);
            if (MouseIsOverEndpoint(location, out SelectedLine, out MovingStartEndPoint))
                new_cursor = Cursors.Arrow;
            else if (MouseIsOverLine(location, out SelectedLine))
                new_cursor = Cursors.Hand;
            if (paintSurface.Cursor != new_cursor)
                paintSurface.Cursor = new_cursor;
        }

        private void Canvas_MouseDown_1(object sender, MouseButtonEventArgs e)
        {
            if (IsVector == false)
            {
                if (e.ButtonState == MouseButtonState.Pressed)
                    currPoint = e.GetPosition(this);
            }

            else
            {
                Point location = e.MouseDevice.GetPosition(this);
                if(MouseIsOverEndpoint(location, out SelectedLine, out MovingStartEndPoint))
                {
                    paintSurface.MouseMove -= Canvas_MouseMove_NotDown;
                    paintSurface.MouseMove += Canvas_MouseMove_MovingEndPoint;
                    paintSurface.MouseUp += Canvas_MouseUp_MovingEndPoint;

                    Point hit_point;
                    if (MovingStartEndPoint)
                        hit_point = new Point(SelectedLine.X1, SelectedLine.Y1);
                    else
                        hit_point = new Point(SelectedLine.X2, SelectedLine.Y2);
                    OffsetX = hit_point.X - location.X;
                    OffsetY = hit_point.Y - location.Y;
                }

                else if (MouseIsOverLine(location, out SelectedLine))
                {
                    paintSurface.MouseMove -= Canvas_MouseMove_NotDown;
                    paintSurface.MouseMove += Canvas_MouseMove_MovingSegment;
                    paintSurface.MouseUp += Canvas_MouseUp_MovingSegment;
                    OffsetX = SelectedLine.X1 - location.X;
                    OffsetY = SelectedLine.Y1 - location.Y;
                }

                else
                {
                    paintSurface.MouseMove -= Canvas_MouseMove_NotDown;
                    paintSurface.MouseMove += Canvas_MouseMove_Drawing;
                    paintSurface.MouseUp += Canvas_MouseUp_Drawing;

                    SelectedLine = new Line();
                    SelectedLine.Stroke = Brushes.Red;
                    SelectedLine.X1 = location.X;
                    SelectedLine.Y1 = location.Y;
                    SelectedLine.X2 = location.X;
                    SelectedLine.Y2 = location.Y;
                    paintSurface.Children.Add(SelectedLine);
                }
            }
        }

        private void Canvas_MouseMove_Drawing(object sender, MouseEventArgs e)
        {
            Point location = e.GetPosition(this);
            SelectedLine.X2 = location.X;
            SelectedLine.Y2 = location.Y;
        }

        private void Canvas_MouseUp_Drawing(object sender, MouseEventArgs e)
        {
            SelectedLine.Stroke = Brushes.Black;

            paintSurface.MouseMove -= Canvas_MouseMove_Drawing;
            paintSurface.MouseMove += Canvas_MouseMove_NotDown;
            paintSurface.MouseUp -= Canvas_MouseUp_Drawing;

            if((SelectedLine.X1==SelectedLine.X2) && (SelectedLine.Y1 == SelectedLine.Y2))
            {
                paintSurface.Children.Remove(SelectedLine);
            }
        }

        private void Canvas_MouseMove_MovingEndPoint(object sender, MouseEventArgs e)
        {
            Point location = e.MouseDevice.GetPosition(this);
            if (MovingStartEndPoint)
            {
                SelectedLine.X1 = location.X + OffsetX;
                SelectedLine.Y1 = location.Y + OffsetY;
            }
            else
            {
                SelectedLine.X2 = location.X + OffsetX;
                SelectedLine.Y2 = location.Y + OffsetY;
            }
        }

        private void Canvas_MouseUp_MovingEndPoint(object sender, MouseEventArgs e)
        {
            paintSurface.MouseMove += Canvas_MouseMove_NotDown;
            paintSurface.MouseMove -= Canvas_MouseMove_MovingEndPoint;
            paintSurface.MouseUp -= Canvas_MouseUp_MovingEndPoint;
        }

        private void Canvas_MouseMove_MovingSegment(object sender, MouseEventArgs e)
        {
            Point location = e.GetPosition(this);
            double new_x1 = location.X + OffsetX;
            double new_y1 = location.Y + OffsetY;

            double dx = new_x1 - SelectedLine.X1;
            double dy = new_y1 - SelectedLine.Y1;

            SelectedLine.X1 = new_x1;
            SelectedLine.Y1 = new_y1;
            SelectedLine.X2 += dx;
            SelectedLine.Y2 += dy;
        }

        private void Canvas_MouseUp_MovingSegment(object sender, MouseEventArgs e)
        {
            paintSurface.MouseMove += Canvas_MouseMove_NotDown;
            paintSurface.MouseMove -= Canvas_MouseMove_MovingSegment;
            paintSurface.MouseUp -= Canvas_MouseUp_MovingSegment;

            //paintSurface.Children.Add(SelectedLine);

            //Point location = e.GetPosition(this);
            //if((location.X>=0) && (location.X < TrashWidth) && (location.Y >= 0) && (location.Y < TrashHeight))
            //{
            //    if(System.Windows.MessageBox.Show("Delete this segment?", "Delete Segment?", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            //    {
            //        paintSurface.Children.Remove(SelectedLine);
            //    }
            //}
        }

        private void Canvas_MouseMove_1(object sender, MouseEventArgs e)
        {
            if (IsVector == false)
            {
                Line line = new Line();

                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    int red = int.Parse(label_red.Content.ToString());
                    int green = int.Parse(label_green.Content.ToString());
                    int blue = int.Parse(label_blue.Content.ToString());
                    SolidColorBrush colorBrush = new SolidColorBrush(Color.FromRgb((byte)red, (byte)green, (byte)blue));
                    line.Stroke = colorBrush;

                    //to dla color pickera
                    //if (ColorPicker1.SelectedColor.HasValue)
                    //    colorBrush.Color = selectedColor;
                    //jesli wybrany jest jakis kolor to koloruj, nie to daj czarny
                    //if (selectedColor.ToString() != "#00000000")
                    //{
                    //    line.Stroke = colorBrush;
                    //}
                    //else line.Stroke = SystemColors.WindowFrameBrush;

                    line.X1 = currPoint.X;
                    line.Y1 = currPoint.Y;

                    line.X2 = e.GetPosition(this).X;
                    line.Y2 = e.GetPosition(this).Y;
                    currPoint = e.GetPosition(this);

                    paintSurface.Children.Add(line);
                }
            }

            else
            {
                //NewPt2 = new Point(e.GetPosition(this).X, e.GetPosition(this).Y);
                //paintSurface.InvalidateVisual();
            }
        }

        private void Canvas_Clear(object sender, RoutedEventArgs e)
        {
            paintSurface.Children.Clear();
        }

        private void Canvas_Vector(object sender, RoutedEventArgs e)
        {
            IsVector = true;
        }

        private void Canvas_Draw(object sender, RoutedEventArgs e)
        {
            IsVector = false;
        }

        private void ColorPicker1_SelectedColorChanged(object sender, RoutedEventArgs e)
        {
            if (ColorPicker1.SelectedColor.HasValue)
            {
                selectedColor = ColorPicker1.SelectedColor.Value;
            }
        }

        private void slider_cyan_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            label_cyan.Content = slider_cyan.Value;
            updatergb();
        }

        private void slider_magenta_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            label_magenta.Content = slider_magenta.Value;
            updatergb();
        }

        private void slider_yellow_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            label_yellow.Content = slider_yellow.Value;
            updatergb();
        }

        private void slider_key_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            label_key.Content = slider_key.Value;
            updatergb();
        }

        private void slider_red_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            label_red.Content = slider_red.Value;
            show_color();
        }

        private void slider_green_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            label_green.Content = slider_green.Value;
            show_color();
        }

        private void slider_blue_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            label_blue.Content = slider_blue.Value;
            show_color();
        }

        private void button_to_CMYK_Click(object sender, RoutedEventArgs e)
        {
            updatecmyk();
        }

        public WriteableBitmap SaveAsWriteableBitmap(Canvas paintSurface)
        {
            if (paintSurface == null) return null;

            // Save current canvas transform
            Transform transform = paintSurface.LayoutTransform;
            // reset current transform (in case it is scaled or rotated)
            paintSurface.LayoutTransform = null;

            // Get the size of canvas
            Size size = new Size(paintSurface.ActualWidth, paintSurface.ActualHeight);
            // Measure and arrange the surface
            // VERY IMPORTANT
            paintSurface.Measure(size);
            paintSurface.Arrange(new Rect(size));

            // Create a render bitmap and push the surface to it
            RenderTargetBitmap renderBitmap = new RenderTargetBitmap(
              (int)size.Width,
              (int)size.Height,
              96d,
              96d,
              PixelFormats.Pbgra32);
            renderBitmap.Render(paintSurface);


            //Restore previously saved layout
            paintSurface.LayoutTransform = transform;

            //create and return a new WriteableBitmap using the RenderTargetBitmap
            return new WriteableBitmap(renderBitmap);
        }

        //private void Window_Loaded(object sender, RoutedEventArgs e)
        //{
        //    const int width = Int32.Parse(paintSurface.Width);
        //    const int height = 240;

        //    WriteableBitmap wbitmap = SaveAsWriteableBitmap(paintSurface);
        //        //new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
        //    byte[,,] pixels = new byte[height, width, 4];

        //    // Clear to black.
        //    for (int row = 0; row < height; row++)
        //    {
        //        for (int col = 0; col < width; col++)
        //        {
        //            for (int i = 0; i < 3; i++)
        //                pixels[row, col, i] = 0;
        //            pixels[row, col, 3] = 255;
        //        }
        //    }

        //    // Blue.
        //    for (int row = 0; row < 80; row++)
        //    {
        //        for (int col = 0; col <= row; col++)
        //        {
        //            pixels[row, col, 0] = 255;
        //        }
        //    }

        //    // Green.
        //    for (int row = 80; row < 160; row++)
        //    {
        //        for (int col = 0; col < 80; col++)
        //        {
        //            pixels[row, col, 1] = 255;
        //        }
        //    }

        //    // Red.
        //    for (int row = 160; row < 240; row++)
        //    {
        //        for (int col = 0; col < 80; col++)
        //        {
        //            pixels[row, col, 2] = 255;
        //        }
        //    }

        //    // Copy the data into a one-dimensional array.
        //    byte[] pixels1d = new byte[height * width * 4];
        //    int index = 0;
        //    for (int row = 0; row < height; row++)
        //    {
        //        for (int col = 0; col < width; col++)
        //        {
        //            for (int i = 0; i < 4; i++)
        //                pixels1d[index++] = pixels[row, col, i];
        //        }
        //    }

        //    // Update writeable bitmap with the colorArray to the image.
        //    Int32Rect rect = new Int32Rect(0, 0, width, height);
        //    int stride = 4 * width;
        //    wbitmap.WritePixels(rect, pixels1d, stride, 0);

        //    // Create an Image to display the bitmap.
        //    Image image = new Image();
        //    image.Stretch = Stretch.None;
        //    image.Margin = new Thickness(0);

        //    grdMain.Children.Add(image);

        //    //Set the Image source.
        //    image.Source = wbitmap;
        //}
    }
}
