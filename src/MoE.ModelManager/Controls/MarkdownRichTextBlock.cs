using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;

namespace MoE.ModelManager.Controls;

/// <summary>
/// Renders CommonMark output as native, selectable WinUI text.
/// </summary>
public sealed class MarkdownRichTextBlock : UserControl
{
    /// <summary>
    /// Identifies the <see cref="Markdown"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty MarkdownProperty = DependencyProperty.Register(
        nameof(Markdown),
        typeof(string),
        typeof(MarkdownRichTextBlock),
        new PropertyMetadata(string.Empty, OnRenderPropertyChanged));

    /// <summary>
    /// Identifies the <see cref="RenderMarkdown"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty RenderMarkdownProperty = DependencyProperty.Register(
        nameof(RenderMarkdown),
        typeof(bool),
        typeof(MarkdownRichTextBlock),
        new PropertyMetadata(true, OnRenderPropertyChanged));

    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    private readonly RichTextBlock _richTextBlock = new()
    {
        IsTextSelectionEnabled = true,
        TextWrapping = TextWrapping.Wrap,
    };

    private DispatcherTimer? _renderTimer;
    private bool _renderPending;

    /// <summary>
    /// Initializes a new instance of the <see cref="MarkdownRichTextBlock"/> class.
    /// </summary>
    public MarkdownRichTextBlock()
    {
        Content = _richTextBlock;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    /// <summary>
    /// Gets or sets the source text.
    /// </summary>
    public string Markdown
    {
        get => (string)GetValue(MarkdownProperty);
        set => SetValue(MarkdownProperty, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether Markdown syntax is rendered.
    /// </summary>
    public bool RenderMarkdown
    {
        get => (bool)GetValue(RenderMarkdownProperty);
        set => SetValue(RenderMarkdownProperty, value);
    }

    private static void OnRenderPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        ((MarkdownRichTextBlock)sender).ScheduleRender();
    }

    private static void AddInlines(ContainerInline container, InlineCollection destination)
    {
        for (Markdig.Syntax.Inlines.Inline? current = container.FirstChild; current is not null; current = current.NextSibling)
        {
            switch (current)
            {
                case LiteralInline literal:
                    destination.Add(new Run { Text = literal.Content.ToString() });
                    break;
                case CodeInline code:
                    destination.Add(new Run
                    {
                        Text = code.Content,
                        FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                    });
                    break;
                case EmphasisInline emphasis:
                    Span emphasisSpan = new();
                    if (emphasis.DelimiterCount >= 2)
                    {
                        emphasisSpan.FontWeight = FontWeights.Bold;
                    }
                    else
                    {
                        emphasisSpan.FontStyle = Windows.UI.Text.FontStyle.Italic;
                    }

                    AddInlines(emphasis, emphasisSpan.Inlines);
                    destination.Add(emphasisSpan);
                    break;
                case LinkInline link:
                    Span linkSpan = new()
                    {
                        TextDecorations = Windows.UI.Text.TextDecorations.Underline,
                    };
                    AddInlines(link, linkSpan.Inlines);
                    if (!string.IsNullOrWhiteSpace(link.Url))
                    {
                        linkSpan.Inlines.Add(new Run { Text = $" ({link.Url})" });
                    }

                    destination.Add(linkSpan);
                    break;
                case LineBreakInline:
                    destination.Add(new LineBreak());
                    break;
                case ContainerInline nested:
                    AddInlines(nested, destination);
                    break;
                default:
                    destination.Add(new Run { Text = current.ToString() });
                    break;
            }
        }
    }

    private static string ReadSource(Markdig.Syntax.Block block, string source)
    {
        int start = block.Span.Start;
        int end = block.Span.End;
        return start >= 0 && end >= start && end < source.Length
            ? source[start..(end + 1)]
            : block.ToString() ?? string.Empty;
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        ScheduleRender();
    }

    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        _renderTimer?.Stop();
    }

    private void ScheduleRender()
    {
        _renderPending = true;
        if (!IsLoaded)
        {
            return;
        }

        _renderTimer ??= CreateRenderTimer();
        if (!_renderTimer.IsEnabled)
        {
            _renderTimer.Start();
        }
    }

    private DispatcherTimer CreateRenderTimer()
    {
        DispatcherTimer timer = new()
        {
            Interval = TimeSpan.FromMilliseconds(80),
        };
        timer.Tick += OnRenderTimerTick;
        return timer;
    }

    private void OnRenderTimerTick(object? sender, object args)
    {
        if (!_renderPending)
        {
            _renderTimer?.Stop();
            return;
        }

        _renderPending = false;
        RenderNow();
    }

    private void RenderNow()
    {
        _richTextBlock.Blocks.Clear();
        string source = Markdown ?? string.Empty;
        if (!RenderMarkdown)
        {
            AddPlainParagraph(source);
            return;
        }

        MarkdownDocument document = Markdig.Markdown.Parse(source, Pipeline);
        RenderBlocks(document, source, string.Empty);
        if (_richTextBlock.Blocks.Count == 0)
        {
            AddPlainParagraph(source);
        }
    }

    private void RenderBlocks(ContainerBlock container, string source, string prefix)
    {
        int orderedIndex = 1;
        foreach (Markdig.Syntax.Block block in container)
        {
            switch (block)
            {
                case HeadingBlock heading:
                    double headingSize = heading.Level switch
                    {
                        1 => 24,
                        2 => 21,
                        3 => 18,
                        _ => 16,
                    };
                    AddInlineParagraph(
                        heading.Inline,
                        prefix,
                        headingSize,
                        true);
                    break;
                case ParagraphBlock paragraph:
                    AddInlineParagraph(paragraph.Inline, prefix, FontSize, false);
                    break;
                case FencedCodeBlock fencedCode:
                    AddCodeParagraph(fencedCode.Lines.ToString(), prefix);
                    break;
                case CodeBlock code:
                    AddCodeParagraph(code.Lines.ToString(), prefix);
                    break;
                case QuoteBlock quote:
                    RenderBlocks(quote, source, prefix + "│ ");
                    break;
                case ListBlock list:
                    foreach (ListItemBlock item in list.OfType<ListItemBlock>())
                    {
                        string marker = list.IsOrdered ? $"{orderedIndex++}. " : "• ";
                        RenderBlocks(item, source, prefix + marker);
                    }

                    break;
                case ThematicBreakBlock:
                    AddPlainParagraph(prefix + "────────────────────────");
                    break;
                default:
                    AddCodeParagraph(ReadSource(block, source), prefix);
                    break;
            }
        }
    }

    private void AddPlainParagraph(string text)
    {
        Paragraph paragraph = new();
        paragraph.Inlines.Add(new Run { Text = text });
        _richTextBlock.Blocks.Add(paragraph);
    }

    private void AddInlineParagraph(ContainerInline? inline, string prefix, double fontSize, bool useSemibold)
    {
        Paragraph paragraph = new()
        {
            FontSize = fontSize,
            FontWeight = useSemibold ? FontWeights.SemiBold : FontWeights.Normal,
        };
        if (prefix.Length > 0)
        {
            paragraph.Inlines.Add(new Run { Text = prefix });
        }

        if (inline is not null)
        {
            AddInlines(inline, paragraph.Inlines);
        }

        _richTextBlock.Blocks.Add(paragraph);
    }

    private void AddCodeParagraph(string code, string prefix)
    {
        Paragraph paragraph = new()
        {
            FontFamily = new FontFamily("Cascadia Mono, Consolas"),
            FontSize = Math.Max(12, FontSize - 1),
        };
        paragraph.Inlines.Add(new Run { Text = prefix + code.TrimEnd() });
        _richTextBlock.Blocks.Add(paragraph);
    }
}
