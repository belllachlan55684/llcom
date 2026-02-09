using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using llcom.Tools;

namespace llcom.View.Controls
{
    public partial class AnsiTextBlock : UserControl
    {
        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register(nameof(Text), typeof(string), typeof(AnsiTextBlock),
                new PropertyMetadata("", OnTextChanged));

        public static readonly DependencyProperty DefaultForegroundProperty =
            DependencyProperty.Register(nameof(DefaultForeground), typeof(Brush), typeof(AnsiTextBlock),
                new PropertyMetadata(Brushes.Lime, OnDefaultForegroundChanged));

        public static readonly DependencyProperty EnableAnsiColorProperty =
            DependencyProperty.Register(nameof(EnableAnsiColor), typeof(bool), typeof(AnsiTextBlock),
                new PropertyMetadata(true, OnEnableAnsiColorChanged));

        public static readonly DependencyProperty ContentFontSizeProperty =
            DependencyProperty.Register(nameof(ContentFontSize), typeof(double), typeof(AnsiTextBlock),
                new PropertyMetadata(12.0, OnContentFontSizeChanged));

        public static readonly DependencyProperty ContentFontFamilyProperty =
            DependencyProperty.Register(nameof(ContentFontFamily), typeof(FontFamily), typeof(AnsiTextBlock),
                new PropertyMetadata(new FontFamily("Consolas,Microsoft YaHei,微软雅黑"), OnContentFontFamilyChanged));

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public Brush DefaultForeground
        {
            get => (Brush)GetValue(DefaultForegroundProperty);
            set => SetValue(DefaultForegroundProperty, value);
        }

        public bool EnableAnsiColor
        {
            get => (bool)GetValue(EnableAnsiColorProperty);
            set => SetValue(EnableAnsiColorProperty, value);
        }

        public double ContentFontSize
        {
            get => (double)GetValue(ContentFontSizeProperty);
            set => SetValue(ContentFontSizeProperty, value);
        }

        public FontFamily ContentFontFamily
        {
            get => (FontFamily)GetValue(ContentFontFamilyProperty);
            set => SetValue(ContentFontFamilyProperty, value);
        }

        public AnsiTextBlock()
        {
            InitializeComponent();
        }

        private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((AnsiTextBlock)d).UpdateContent();
        }

        private static void OnDefaultForegroundChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((AnsiTextBlock)d).UpdateContent();
        }

        private static void OnEnableAnsiColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((AnsiTextBlock)d).UpdateContent();
        }

        private static void OnContentFontSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ctrl = (AnsiTextBlock)d;
            if (ctrl.InnerTextBlock != null)
                ctrl.InnerTextBlock.FontSize = (double)e.NewValue;
        }

        private static void OnContentFontFamilyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ctrl = (AnsiTextBlock)d;
            if (ctrl.InnerTextBlock != null && e.NewValue is FontFamily ff)
                ctrl.InnerTextBlock.FontFamily = ff;
        }

        private void UpdateContent()
        {
            if (InnerTextBlock == null)
                return;

            InnerTextBlock.Inlines.Clear();
            var text = Text ?? "";
            var defaultBrush = DefaultForeground ?? Brushes.Lime;

            if (string.IsNullOrEmpty(text))
                return;

            SolidColorBrush frozenDefault = GetFrozenBrush(defaultBrush);

            if (!EnableAnsiColor)
            {
                if (!string.IsNullOrEmpty(text))
                {
                    InnerTextBlock.Inlines.Add(new Run(text) { Foreground = frozenDefault });
                }
                return;
            }

            var segments = AnsiParser.Parse(text);
            foreach (var seg in segments)
            {
                if (string.IsNullOrEmpty(seg.Text))
                    continue;
                var brush = seg.Color.HasValue
                    ? GetFrozenBrushFromColor(seg.Color.Value)
                    : frozenDefault;
                InnerTextBlock.Inlines.Add(new Run(seg.Text) { Foreground = brush });
            }
        }

        private static SolidColorBrush GetFrozenBrush(Brush brush)
        {
            var color = (brush as SolidColorBrush)?.Color ?? Colors.Lime;
            return GetFrozenBrushFromColor(color);
        }

        private static SolidColorBrush GetFrozenBrushFromColor(Color color)
        {
            var b = new SolidColorBrush(color);
            b.Freeze();
            return b;
        }

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);
            if (InnerTextBlock != null)
            {
                InnerTextBlock.FontSize = ContentFontSize;
                InnerTextBlock.FontFamily = ContentFontFamily;
            }
            UpdateContent();
        }
    }
}
