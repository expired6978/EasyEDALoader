using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Standalone
{
    using System;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;
    using System.Windows.Media;
    using System.Windows.Shapes;

    public class CanvasZoomPanHelper
    {
        private readonly Canvas _canvas;
        private Point _lastDragPoint;
        private bool _isDragging;

        private readonly ScaleTransform _scaleTransform = new ScaleTransform();
        private readonly TranslateTransform _translateTransform = new TranslateTransform();
        private readonly TransformGroup _transformGroup = new TransformGroup();

        public CanvasZoomPanHelper(Canvas canvas)
        {
            _canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));

            _transformGroup.Children.Add(_scaleTransform);
            _transformGroup.Children.Add(_translateTransform);

            _canvas.RenderTransform = _transformGroup;

            _canvas.Background = Brushes.Transparent;
            _canvas.Focusable = true;
            _canvas.Focus();

            AttachEvents();
        }

        private T? FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject? parent = VisualTreeHelper.GetParent(child);
            while (parent != null && parent is not T)
                parent = VisualTreeHelper.GetParent(parent);
            return parent as T;
        }

        private void AttachEvents()
        {
            var scrollViewer = _canvas.Parent as ScrollViewer;
            if (scrollViewer == null)
                throw new InvalidOperationException("Canvas must be inside a ScrollViewer.");

            scrollViewer.PreviewMouseWheel += Canvas_MouseWheel;
            scrollViewer.PreviewMouseLeftButtonDown += Canvas_MouseLeftButtonDown;
            scrollViewer.PreviewMouseLeftButtonUp += Canvas_MouseLeftButtonUp;
            scrollViewer.PreviewMouseMove += Canvas_MouseMove;
            scrollViewer.PreviewMouseRightButtonDown += Canvas_MouseRightButtonDown;
        }

        public void FitToBoundingBox()
        {
            if (_canvas == null || _canvas.Children.Count == 0)
                return;

            var scrollViewer = FindParent<ScrollViewer>(_canvas);
            if (scrollViewer == null)
                return;

            // 1. Measure bounds of canvas content
            Rect contentBounds = CalculateCanvasBounds();
            if (contentBounds.IsEmpty || contentBounds.Width == 0 || contentBounds.Height == 0)
                return;

            // 2. Get visible size (viewport)
            double viewportWidth = scrollViewer.ViewportWidth;
            double viewportHeight = scrollViewer.ViewportHeight;

            if (viewportWidth <= 0 || viewportHeight <= 0)
                return;

            // 3. Compute uniform scale (fit to visible size)
            double scale = Math.Min(viewportWidth / contentBounds.Width, viewportHeight / contentBounds.Height);

            // 4. Center the content in the viewport
            double centerOffsetX = (viewportWidth - contentBounds.Width * scale) / 2;
            double centerOffsetY = (viewportHeight - contentBounds.Height * scale) / 2;

            // 5. Translate so that contentBounds.Left/Top maps to origin
            double translateX = -contentBounds.Left * scale + centerOffsetX;
            double translateY = -contentBounds.Top * scale + centerOffsetY;

            // 6. Apply transforms
            _scaleTransform.ScaleX = scale;
            _scaleTransform.ScaleY = scale;
            _translateTransform.X = translateX;
            _translateTransform.Y = translateY;
        }

        private Rect CalculateCanvasBounds()
        {
            Rect bounds = Rect.Empty;

            foreach (UIElement child in _canvas.Children)
            {
                if (child is FrameworkElement fe)
                {
                    double left = Canvas.GetLeft(fe);
                    double top = Canvas.GetTop(fe);

                    // Default to 0 if not set
                    if (double.IsNaN(left)) left = 0;
                    if (double.IsNaN(top)) top = 0;

                    Rect childRect = new Rect(left, top, fe.ActualWidth, fe.ActualHeight);
                    bounds.Union(childRect);
                }
            }

            return bounds;
        }

        private void Canvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            Point mousePos = e.GetPosition(_canvas);

            double zoomDelta = e.Delta > 0 ? 1.1 : 1.0 / 1.1;
            double newScale = _scaleTransform.ScaleX * zoomDelta;

            // Limit zoom levels
            if (newScale < 0.01 || newScale > 100) return;

            // Update scale
            _scaleTransform.ScaleX = newScale;
            _scaleTransform.ScaleY = newScale;

            // Adjust translation to zoom around mouse
            _translateTransform.X = (1 - zoomDelta) * mousePos.X + _translateTransform.X;
            _translateTransform.Y = (1 - zoomDelta) * mousePos.Y + _translateTransform.Y;

            e.Handled = true;
        }

        private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _lastDragPoint = e.GetPosition(sender as IInputElement);
            _isDragging = true;

            if (sender is UIElement element)
                element.CaptureMouse();

            Mouse.OverrideCursor = Cursors.SizeAll;
        }

        private void Canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isDragging = false;

            if (sender is UIElement element)
                element.ReleaseMouseCapture();

            Mouse.OverrideCursor = null;
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDragging) return;

            if (sender is IInputElement element)
            {
                Point currentPos = e.GetPosition(element);
                Vector delta = currentPos - _lastDragPoint;
                _lastDragPoint = currentPos;

                _translateTransform.X += delta.X;
                _translateTransform.Y += delta.Y;
            }
        }
        private void Canvas_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            FitToBoundingBox();
        }
    }

}
