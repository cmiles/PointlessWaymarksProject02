using System;
using System.Windows;
using System.Windows.Controls;

namespace PwTrackTrimmer.Controls
{
    public class WrapPanel : Panel
    {
        public static readonly DependencyProperty OrientationProperty =
            DependencyProperty.Register(
                nameof(Orientation),
                typeof(Orientation),
                typeof(WrapPanel),
                new PropertyMetadata(Orientation.Horizontal, OnOrientationChanged));

        public Orientation Orientation
        {
            get => (Orientation)GetValue(OrientationProperty);
            set => SetValue(OrientationProperty, value);
        }

        private static void OnOrientationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is WrapPanel panel)
            {
                panel.InvalidateMeasure();
                panel.InvalidateArrange();
            }
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            var curLineSize = new Size();
            var panelSize = new Size();
            bool isHorizontal = Orientation == Orientation.Horizontal;

            Size childAvailableSize = new Size(
                isHorizontal ? double.PositiveInfinity : availableSize.Width,
                isHorizontal ? availableSize.Height : double.PositiveInfinity);

            foreach (UIElement child in Children)
            {
                if (child == null || child.Visibility == Visibility.Collapsed)
                    continue;

                child.Measure(childAvailableSize);
                Size sz = child.DesiredSize;

                if (isHorizontal)
                {
                    if (curLineSize.Width + sz.Width > availableSize.Width && curLineSize.Width > 0)
                    {
                        panelSize.Width = Math.Max(curLineSize.Width, panelSize.Width);
                        panelSize.Height += curLineSize.Height;
                        curLineSize = sz;
                    }
                    else
                    {
                        curLineSize.Width += sz.Width;
                        curLineSize.Height = Math.Max(curLineSize.Height, sz.Height);
                    }
                }
                else
                {
                    if (curLineSize.Height + sz.Height > availableSize.Height && curLineSize.Height > 0)
                    {
                        panelSize.Height = Math.Max(curLineSize.Height, panelSize.Height);
                        panelSize.Width += curLineSize.Width;
                        curLineSize = sz;
                    }
                    else
                    {
                        curLineSize.Height += sz.Height;
                        curLineSize.Width = Math.Max(curLineSize.Width, sz.Width);
                    }
                }
            }

            if (isHorizontal)
            {
                panelSize.Width = Math.Max(curLineSize.Width, panelSize.Width);
                panelSize.Height += curLineSize.Height;
            }
            else
            {
                panelSize.Height = Math.Max(curLineSize.Height, panelSize.Height);
                panelSize.Width += curLineSize.Width;
            }

            return panelSize;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            int firstInLine = 0;
            var curLineSize = new Size();
            double accumulatedV = 0.0;
            bool isHorizontal = Orientation == Orientation.Horizontal;

            for (int i = 0; i < Children.Count; i++)
            {
                UIElement child = Children[i];
                if (child == null || child.Visibility == Visibility.Collapsed)
                    continue;

                Size sz = child.DesiredSize;

                if (isHorizontal)
                {
                    if (curLineSize.Width + sz.Width > finalSize.Width && curLineSize.Width > 0)
                    {
                        ArrangeLine(accumulatedV, curLineSize.Height, firstInLine, i, isHorizontal);
                        accumulatedV += curLineSize.Height;
                        curLineSize = sz;
                        firstInLine = i;
                    }
                    else
                    {
                        curLineSize.Width += sz.Width;
                        curLineSize.Height = Math.Max(curLineSize.Height, sz.Height);
                    }
                }
                else
                {
                    if (curLineSize.Height + sz.Height > finalSize.Height && curLineSize.Height > 0)
                    {
                        ArrangeLine(accumulatedV, curLineSize.Width, firstInLine, i, isHorizontal);
                        accumulatedV += curLineSize.Width;
                        curLineSize = sz;
                        firstInLine = i;
                    }
                    else
                    {
                        curLineSize.Height += sz.Height;
                        curLineSize.Width = Math.Max(curLineSize.Width, sz.Width);
                    }
                }
            }

            if (firstInLine < Children.Count)
            {
                ArrangeLine(accumulatedV, isHorizontal ? curLineSize.Height : curLineSize.Width, firstInLine, Children.Count, isHorizontal);
            }

            return finalSize;
        }

        private void ArrangeLine(double v, double lineV, int start, int end, bool isHorizontal)
        {
            double u = 0.0;
            for (int i = start; i < end; i++)
            {
                UIElement child = Children[i];
                if (child == null || child.Visibility == Visibility.Collapsed)
                    continue;

                Size sz = child.DesiredSize;
                if (isHorizontal)
                {
                    child.Arrange(new Rect(u, v, sz.Width, lineV));
                    u += sz.Width;
                }
                else
                {
                    child.Arrange(new Rect(v, u, lineV, sz.Height));
                    u += sz.Height;
                }
            }
        }
    }
}
