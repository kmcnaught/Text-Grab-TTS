using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Text_Grab.Interfaces;
using Text_Grab.Models;
using Text_Grab.Services;
using Text_Grab.Utilities;

namespace Text_Grab.Views;

public partial class SpeakParagraphOverlay : Window
{
    private List<ParagraphGroup> _paragraphs = new();
    private List<System.Windows.Shapes.Rectangle> _highlights = new();
    private System.Windows.Shapes.Rectangle? _hoveredRect = null;
    private double _ocrScale = 1.0;
    private DpiScale _dpi;
    private int _hoveredIndex = -1;

    private static readonly System.Windows.Media.Brush NormalFill =
        new SolidColorBrush(System.Windows.Media.Color.FromArgb(40, 255, 255, 255));
    private static readonly System.Windows.Media.Brush HoverFill =
        new SolidColorBrush(System.Windows.Media.Color.FromArgb(90, 100, 180, 255));
    private static readonly System.Windows.Media.Brush NormalStroke =
        new SolidColorBrush(System.Windows.Media.Color.FromArgb(120, 255, 255, 255));
    private static readonly System.Windows.Media.Brush HoverStroke =
        new SolidColorBrush(System.Windows.Media.Color.FromArgb(220, 100, 180, 255));

    public SpeakParagraphOverlay()
    {
        InitializeComponent();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        _dpi = VisualTreeHelper.GetDpi(this);

        // Capture the screen through the transparent window
        Bitmap screenBitmap = ImageMethods.GetWindowsBoundsBitmap(this);

        // Show it as background
        BackgroundImage.Source = ImageMethods.BitmapToImageSource(screenBitmap);

        HintText.Text = "Detecting paragraphs...";

        ILanguage language = LanguageUtilities.GetOCRLanguage();
        (IOcrLinesWords? ocrResult, double scale) =
            await OcrUtilities.GetOcrResultFromBitmapAsync(screenBitmap, language);

        screenBitmap.Dispose();

        if (ocrResult is null || ocrResult.Lines.Length == 0)
        {
            HintText.Text = "No text detected. Press Esc to close.";
            return;
        }

        _ocrScale = scale;
        _paragraphs = ParagraphDetector.GroupLinesIntoParagraphs(ocrResult.Lines);
        DrawParagraphHighlights();

        HintText.Text = "Click a paragraph to speak it  |  Esc to cancel";
    }

    private void DrawParagraphHighlights()
    {
        ParagraphCanvas.Children.Clear();
        _highlights.Clear();

        foreach (ParagraphGroup para in _paragraphs)
        {
            System.Windows.Rect canvasRect = OcrRectToCanvas(para.BoundingBox);

            System.Windows.Shapes.Rectangle rect = new()
            {
                Width = canvasRect.Width,
                Height = canvasRect.Height,
                Fill = NormalFill,
                Stroke = NormalStroke,
                StrokeThickness = 1.5,
                RadiusX = 3,
                RadiusY = 3,
            };

            System.Windows.Controls.Canvas.SetLeft(rect, canvasRect.X);
            System.Windows.Controls.Canvas.SetTop(rect, canvasRect.Y);
            ParagraphCanvas.Children.Add(rect);
            _highlights.Add(rect);
        }
    }

    private void ParagraphCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (_paragraphs.Count == 0)
            return;

        System.Windows.Point pos = e.GetPosition(ParagraphCanvas);
        int hitIndex = FindParagraphAtCanvasPoint(pos);

        if (hitIndex == _hoveredIndex)
            return;

        // Reset previous
        if (_hoveredIndex >= 0 && _hoveredIndex < _highlights.Count)
        {
            _highlights[_hoveredIndex].Fill = NormalFill;
            _highlights[_hoveredIndex].Stroke = NormalStroke;
        }

        _hoveredIndex = hitIndex;

        if (_hoveredIndex >= 0 && _hoveredIndex < _highlights.Count)
        {
            _highlights[_hoveredIndex].Fill = HoverFill;
            _highlights[_hoveredIndex].Stroke = HoverStroke;
        }
    }

    private void ParagraphCanvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_paragraphs.Count == 0)
            return;

        System.Windows.Point pos = e.GetPosition(ParagraphCanvas);
        int index = FindParagraphAtCanvasPoint(pos);

        if (index < 0)
            index = FindNearestParagraphBycentroid(pos);

        if (index < 0)
        {
            Close();
            return;
        }

        ILanguage language = LanguageUtilities.GetOCRLanguage();
        bool isSpaceJoining = language.IsSpaceJoining();
        string text = _paragraphs[index].GetText(isSpaceJoining);

        Close();

        if (!string.IsNullOrWhiteSpace(text))
            Singleton<TtsService>.Instance.Speak(text);
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
            Close();
    }

    // --- Coordinate helpers ---

    private System.Windows.Rect OcrRectToCanvas(Windows.Foundation.Rect ocrRect)
    {
        // OCR coords are in scaledBitmap space (physical pixels * _ocrScale)
        // Canvas coords are in WPF logical pixels
        double x = ocrRect.X / _ocrScale / _dpi.DpiScaleX;
        double y = ocrRect.Y / _ocrScale / _dpi.DpiScaleY;
        double w = ocrRect.Width / _ocrScale / _dpi.DpiScaleX;
        double h = ocrRect.Height / _ocrScale / _dpi.DpiScaleY;
        return new System.Windows.Rect(x, y, Math.Max(w, 1), Math.Max(h, 1));
    }

    private int FindParagraphAtCanvasPoint(System.Windows.Point pt)
    {
        for (int i = 0; i < _paragraphs.Count; i++)
        {
            System.Windows.Rect rect = OcrRectToCanvas(_paragraphs[i].BoundingBox);
            if (rect.Contains(pt))
                return i;
        }
        return -1;
    }

    private int FindNearestParagraphBycentroid(System.Windows.Point pt)
    {
        if (_paragraphs.Count == 0)
            return -1;

        int best = 0;
        double bestDist = double.MaxValue;

        for (int i = 0; i < _paragraphs.Count; i++)
        {
            System.Windows.Rect rect = OcrRectToCanvas(_paragraphs[i].BoundingBox);
            System.Windows.Point center = new(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);
            double dx = pt.X - center.X;
            double dy = pt.Y - center.Y;
            double dist = dx * dx + dy * dy;
            if (dist < bestDist)
            {
                bestDist = dist;
                best = i;
            }
        }

        return best;
    }
}
