using System;

namespace Standalone_AD25
{
    public class CanvasZoomPanHelper
    {
        private readonly System.Windows.Controls.Canvas _canvas;
        private System.Windows.Point _lastDragPoint;
        private bool _isDragging;

        private readonly System.Windows.Media.ScaleTransform _scaleTransform =
            new System.Windows.Media.ScaleTransform();

        private readonly System.Windows.Media.TranslateTransform _translateTransform =
            new System.Windows.Media.TranslateTransform();

        private readonly System.Windows.Media.TransformGroup _transformGroup =
            new System.Windows.Media.TransformGroup();

        public CanvasZoomPanHelper(System.Windows.Controls.Canvas canvas)
        {
            _canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));

            _transformGroup.Children.Add(_scaleTransform);
            _transformGroup.Children.Add(_translateTransform);
            _canvas.RenderTransform = _transformGroup;

            _canvas.Background = System.Windows.Media.Brushes.Transparent;
            _canvas.Focusable = true;

            _canvas.Loaded += (_, __) => AttachEvents();
        }

        // -------------------------------------------------
        // EVENT ATTACHMENT
        // -------------------------------------------------

        private void AttachEvents()
        {
            var scrollViewer = FindParent<System.Windows.Controls.ScrollViewer>(_canvas);
            if (scrollViewer == null)
                throw new InvalidOperationException("Canvas must be inside a ScrollViewer.");

            scrollViewer.PreviewMouseWheel += Canvas_MouseWheel;
            scrollViewer.PreviewMouseLeftButtonDown += Canvas_MouseLeftButtonDown;
            scrollViewer.PreviewMouseLeftButtonUp += Canvas_MouseLeftButtonUp;
            scrollViewer.PreviewMouseMove += Canvas_MouseMove;
            scrollViewer.PreviewMouseRightButtonDown += Canvas_MouseRightButtonDown;
        }

        private T? FindParent<T>(System.Windows.DependencyObject child)
            where T : System.Windows.DependencyObject
        {
            System.Windows.DependencyObject? parent =
                System.Windows.Media.VisualTreeHelper.GetParent(child);

            while (parent != null && parent is not T)
                parent = System.Windows.Media.VisualTreeHelper.GetParent(parent);

            return parent as T;
        }

        // -------------------------------------------------
        // FIT TO CONTENT
        // -------------------------------------------------

        public void FitToBoundingBox()
        {
            if (_canvas.Children.Count == 0)
                return;

            var scrollViewer = FindParent<System.Windows.Controls.ScrollViewer>(_canvas);
            if (scrollViewer == null)
                return;

            System.Windows.Rect contentBounds = CalculateCanvasBounds();
            if (contentBounds.IsEmpty ||
                contentBounds.Width <= 0 ||
                contentBounds.Height <= 0)
                return;

            double viewportWidth = scrollViewer.ViewportWidth;
            double viewportHeight = scrollViewer.ViewportHeight;

            if (viewportWidth <= 0 || viewportHeight <= 0)
                return;

            double scale = Math.Min(
                viewportWidth / contentBounds.Width,
                viewportHeight / contentBounds.Height
            );

            double centerOffsetX =
                (viewportWidth - contentBounds.Width * scale) / 2;

            double centerOffsetY =
                (viewportHeight - contentBounds.Height * scale) / 2;

            _scaleTransform.ScaleX = scale;
            _scaleTransform.ScaleY = scale;

            _translateTransform.X =
                -contentBounds.Left * scale + centerOffsetX;

            _translateTransform.Y =
                -contentBounds.Top * scale + centerOffsetY;
        }

        private System.Windows.Rect CalculateCanvasBounds()
        {
            System.Windows.Rect bounds = System.Windows.Rect.Empty;

            foreach (System.Windows.UIElement child in _canvas.Children)
            {
                if (child is System.Windows.FrameworkElement fe)
                {
                    double left = System.Windows.Controls.Canvas.GetLeft(fe);
                    double top = System.Windows.Controls.Canvas.GetTop(fe);

                    if (double.IsNaN(left)) left = 0;
                    if (double.IsNaN(top)) top = 0;

                    var childRect = new System.Windows.Rect(
                        left,
                        top,
                        fe.ActualWidth,
                        fe.ActualHeight
                    );

                    bounds.Union(childRect);
                }
            }

            return bounds;
        }

        // -------------------------------------------------
        // ZOOM / PAN (WPF ONLY)
        // -------------------------------------------------

        private void Canvas_MouseWheel(
            object sender,
            System.Windows.Input.MouseWheelEventArgs e)
        {
            System.Windows.Point mousePos = e.GetPosition(_canvas);

            double zoomDelta = e.Delta > 0 ? 1.1 : 1.0 / 1.1;
            double newScale = _scaleTransform.ScaleX * zoomDelta;

            if (newScale < 0.01 || newScale > 100)
                return;

            _scaleTransform.ScaleX = newScale;
            _scaleTransform.ScaleY = newScale;

            _translateTransform.X =
                (1 - zoomDelta) * mousePos.X + _translateTransform.X;

            _translateTransform.Y =
                (1 - zoomDelta) * mousePos.Y + _translateTransform.Y;

            e.Handled = true;
        }

        private void Canvas_MouseLeftButtonDown(
            object sender,
            System.Windows.Input.MouseButtonEventArgs e)
        {
            _lastDragPoint = e.GetPosition(sender as System.Windows.IInputElement);
            _isDragging = true;

            if (sender is System.Windows.UIElement element)
                element.CaptureMouse();

            System.Windows.Input.Mouse.OverrideCursor =
                System.Windows.Input.Cursors.SizeAll;
        }

        private void Canvas_MouseLeftButtonUp(
            object sender,
            System.Windows.Input.MouseButtonEventArgs e)
        {
            _isDragging = false;

            if (sender is System.Windows.UIElement element)
                element.ReleaseMouseCapture();

            System.Windows.Input.Mouse.OverrideCursor = null;
        }

        private void Canvas_MouseMove(
            object sender,
            System.Windows.Input.MouseEventArgs e)
        {
            if (!_isDragging)
                return;

            if (sender is System.Windows.IInputElement element)
            {
                System.Windows.Point currentPos = e.GetPosition(element);
                System.Windows.Vector delta = currentPos - _lastDragPoint;
                _lastDragPoint = currentPos;

                _translateTransform.X += delta.X;
                _translateTransform.Y += delta.Y;
            }
        }

        private void Canvas_MouseRightButtonDown(
            object sender,
            System.Windows.Input.MouseButtonEventArgs e)
        {
            FitToBoundingBox();
        }
    }
}
