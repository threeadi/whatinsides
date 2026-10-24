using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WhatInsides.Controls;

public partial class SpeedGraphControl : UserControl
{
    private readonly List<double> _dataPoints = new();
    private const int MaxPoints = 26;

    public static readonly DependencyProperty ThemeColorProperty =
        DependencyProperty.Register(nameof(ThemeColor), typeof(Color), typeof(SpeedGraphControl),
            new PropertyMetadata(Color.FromRgb(0, 120, 212), OnColorChanged));

    public Color ThemeColor
    {
        get => (Color)GetValue(ThemeColorProperty);
        set => SetValue(ThemeColorProperty, value);
    }

    public SpeedGraphControl()
    {
        InitializeComponent();
        InitializeDefaultPoints();
    }

    private static void OnColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SpeedGraphControl ctrl && e.NewValue is Color c)
        {
            ctrl.UpdateBrushes(c);
        }
    }

    public void SetScale(string max, string mid, string min)
    {
        TxtYMax.Text = max;
        TxtYMid.Text = mid;
        TxtYMin.Text = min;
    }

    private void UpdateBrushes(Color c)
    {
        LineChart.Stroke = new SolidColorBrush(c);

        var grad = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(0, 1)
        };
        grad.GradientStops.Add(new GradientStop(Color.FromArgb(140, c.R, c.G, c.B), 0.0));
        grad.GradientStops.Add(new GradientStop(Color.FromArgb(10, c.R, c.G, c.B), 1.0));
        AreaPolygon.Fill = grad;
    }

    private void InitializeDefaultPoints()
    {
        UpdateBrushes(ThemeColor);
        var rand = new Random();
        for (int i = 0; i < MaxPoints; i++)
        {
            _dataPoints.Add(rand.Next(5, 65));
        }
    }

    public void AddPoint(double value)
    {
        if (_dataPoints.Count >= MaxPoints)
        {
            _dataPoints.RemoveAt(0);
        }
        _dataPoints.Add(value);
        Redraw();
    }

    private void ChartCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        Redraw();
    }

    public void Redraw()
    {
        double width = ChartCanvas.ActualWidth;
        double height = ChartCanvas.ActualHeight;

        if (width <= 0 || height <= 0 || _dataPoints.Count < 2) return;

        // Position grid lines
        LineGridTop.X1 = 0; LineGridTop.X2 = width; LineGridTop.Y1 = 8; LineGridTop.Y2 = 8;
        LineGridMid.X1 = 0; LineGridMid.X2 = width; LineGridMid.Y1 = height / 2; LineGridMid.Y2 = height / 2;
        LineGridBottom.X1 = 0; LineGridBottom.X2 = width; LineGridBottom.Y1 = height - 4; LineGridBottom.Y2 = height - 4;

        var points = new PointCollection();
        var polyPoints = new PointCollection();

        double stepX = width / (MaxPoints - 1);
        polyPoints.Add(new Point(0, height));

        for (int i = 0; i < _dataPoints.Count; i++)
        {
            double x = i * stepX;
            // Map 0-100 to canvas height
            double norm = Math.Clamp(_dataPoints[i] / 100.0, 0.0, 1.0);
            double y = height - (norm * (height - 16)) - 4;

            points.Add(new Point(x, y));
            polyPoints.Add(new Point(x, y));
        }

        // Close polygon to bottom right and bottom left
        polyPoints.Add(new Point((_dataPoints.Count - 1) * stepX, height));
        polyPoints.Add(new Point(0, height));

        LineChart.Points = points;
        AreaPolygon.Points = polyPoints;
    }
}
