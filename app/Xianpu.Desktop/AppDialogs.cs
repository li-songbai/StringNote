using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace Xianpu.Desktop;

internal static class AppDialogs
{
    internal static readonly Color AccentColor = Color.FromRgb(47, 107, 72);       // #2F6B48
    internal static readonly Color AccentHoverColor = Color.FromRgb(36, 85, 56);   // #245538
    internal static readonly Color AccentLightColor = Color.FromRgb(234, 243, 237);// #EAF3ED
    internal static readonly Color DangerColor = Color.FromRgb(220, 38, 38);       // #DC2626
    internal static readonly Color DangerHoverColor = Color.FromRgb(185, 28, 28);  // #B91C1C
    internal static readonly Color ErrorBadgeColor = Color.FromRgb(185, 28, 28);   // #B91C1C

    internal static readonly SolidColorBrush AccentBrush = Freeze(new SolidColorBrush(AccentColor));
    internal static readonly SolidColorBrush AccentHoverBrush = Freeze(new SolidColorBrush(AccentHoverColor));
    internal static readonly SolidColorBrush AccentLightBrush = Freeze(new SolidColorBrush(AccentLightColor));
    internal static readonly SolidColorBrush DangerBrush = Freeze(new SolidColorBrush(DangerColor));
    internal static readonly SolidColorBrush DangerHoverBrush = Freeze(new SolidColorBrush(DangerHoverColor));
    internal static readonly SolidColorBrush CardBackgroundBrush = Freeze(new SolidColorBrush(Colors.White));
    internal static readonly SolidColorBrush CardBorderBrush = Freeze(new SolidColorBrush(Color.FromRgb(216, 226, 236)));     // #D8E2EC
    internal static readonly SolidColorBrush DividerBrush = Freeze(new SolidColorBrush(Color.FromRgb(238, 242, 246)));        // #EEF2F6
    internal static readonly SolidColorBrush SubtleSurfaceBrush = Freeze(new SolidColorBrush(Color.FromRgb(248, 250, 252)));  // #F8FAFC
    internal static readonly SolidColorBrush SubtleBorderBrush = Freeze(new SolidColorBrush(Color.FromRgb(226, 232, 240)));   // #E2E8F0
    internal static readonly SolidColorBrush InputBorderBrush = Freeze(new SolidColorBrush(Color.FromRgb(203, 213, 225)));    // #CBD5E1
    internal static readonly SolidColorBrush TitleBrush = Freeze(new SolidColorBrush(Color.FromRgb(30, 41, 59)));             // #1E293B
    internal static readonly SolidColorBrush BodyBrush = Freeze(new SolidColorBrush(Color.FromRgb(51, 65, 85)));              // #334155
    internal static readonly SolidColorBrush MutedBrush = Freeze(new SolidColorBrush(Color.FromRgb(100, 116, 139)));          // #64748B

    private static T Freeze<T>(T freezable) where T : Freezable
    {
        if (freezable.CanFreeze)
            freezable.Freeze();
        return freezable;
    }

    public static Window CreateCardWindow(
        Window? owner,
        string title,
        string? subtitle,
        double width,
        double? height,
        bool resizable,
        out Grid bodyHost,
        string badgeText = "笺",
        Color? badgeColor = null)
    {
        var window = new Window
        {
            Title = title,
            Width = width + 28,
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            ResizeMode = resizable ? ResizeMode.CanResizeWithGrip : ResizeMode.NoResize,
            ShowInTaskbar = false,
            FontFamily = owner?.FontFamily ?? new FontFamily("Microsoft YaHei UI"),
            Foreground = TitleBrush
        };

        if (owner is { IsVisible: true })
        {
            window.Owner = owner;
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }
        else
        {
            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        if (height.HasValue)
        {
            window.Height = height.Value + 28;
            if (resizable)
            {
                window.MinWidth = Math.Min(width + 28, 420);
                window.MinHeight = Math.Min(height.Value + 28, 280);
            }
        }
        else
        {
            window.SizeToContent = SizeToContent.Height;
        }

        var outerContainer = new Grid { Margin = new Thickness(14) };

        var card = new Border
        {
            CornerRadius = new CornerRadius(12),
            Background = CardBackgroundBrush,
            BorderBrush = CardBorderBrush,
            BorderThickness = new Thickness(1),
            Effect = new DropShadowEffect
            {
                BlurRadius = 22,
                ShadowDepth = 4,
                Opacity = 0.16,
                Color = Color.FromRgb(15, 23, 42)
            }
        };

        var cardLayout = new Grid();
        cardLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        cardLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        cardLayout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        // Draggable header
        var headerGrid = new Grid
        {
            Margin = new Thickness(18, 14, 14, 12),
            Background = Brushes.Transparent,
            Cursor = Cursors.Arrow
        };
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        headerGrid.MouseLeftButtonDown += (_, e) =>
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                window.DragMove();
        };

        var resolvedBadgeColor = badgeColor ?? AccentColor;
        var badge = new Border
        {
            Width = 24,
            Height = 24,
            CornerRadius = new CornerRadius(6),
            Background = Freeze(new SolidColorBrush(resolvedBadgeColor)),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0),
            Child = new TextBlock
            {
                Text = badgeText,
                Foreground = Brushes.White,
                FontSize = 12.5,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        Grid.SetColumn(badge, 0);
        headerGrid.Children.Add(badge);

        var titleStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        titleStack.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 14.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = TitleBrush,
            TextTrimming = TextTrimming.CharacterEllipsis
        });

        if (!string.IsNullOrWhiteSpace(subtitle))
        {
            titleStack.Children.Add(new TextBlock
            {
                Text = subtitle,
                FontSize = 11.5,
                Foreground = MutedBrush,
                Margin = new Thickness(0, 2, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis
            });
        }

        Grid.SetColumn(titleStack, 1);
        headerGrid.Children.Add(titleStack);

        var closeBtn = CreateCloseButton(window);
        Grid.SetColumn(closeBtn, 2);
        headerGrid.Children.Add(closeBtn);

        Grid.SetRow(headerGrid, 0);
        cardLayout.Children.Add(headerGrid);

        var separator = new Border
        {
            Height = 1,
            Background = DividerBrush
        };
        Grid.SetRow(separator, 1);
        cardLayout.Children.Add(separator);

        bodyHost = new Grid
        {
            Margin = new Thickness(18, 14, 18, 16)
        };
        Grid.SetRow(bodyHost, 2);
        cardLayout.Children.Add(bodyHost);

        card.Child = cardLayout;
        outerContainer.Children.Add(card);
        window.Content = outerContainer;

        return window;
    }

    public static void ShowInfo(Window? owner, string title, string message, string? actionLabel = null, Action? actionCallback = null)
    {
        var window = CreateCardWindow(owner, title, null, 420, null, false, out var bodyHost, badgeText: "✓", badgeColor: AccentColor);

        bodyHost.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        bodyHost.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var messageText = new TextBlock
        {
            Text = message,
            FontSize = 13,
            Foreground = BodyBrush,
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 20
        };
        Grid.SetRow(messageText, 0);
        bodyHost.Children.Add(messageText);

        var footer = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 18, 0, 0)
        };

        if (!string.IsNullOrWhiteSpace(actionLabel) && actionCallback is not null)
        {
            var actionBtn = CreateAccentOutlineButton(owner, actionLabel);
            actionBtn.Margin = new Thickness(0, 0, 8, 0);
            actionBtn.Click += (_, _) =>
            {
                try
                {
                    actionCallback();
                }
                catch
                {
                    // Ignore shell launch errors from action callback.
                }
            };
            footer.Children.Add(actionBtn);
        }

        var okBtn = CreatePrimaryButton(owner, "确定", minWidth: 84);
        okBtn.IsDefault = true;
        okBtn.IsCancel = true;
        okBtn.Click += (_, _) => CloseWithResult(window, true);
        footer.Children.Add(okBtn);

        Grid.SetRow(footer, 1);
        bodyHost.Children.Add(footer);

        window.ShowDialog();
    }

    public static void ShowError(Window? owner, string title, string message)
    {
        var window = CreateCardWindow(owner, title, null, 420, null, false, out var bodyHost, badgeText: "!", badgeColor: ErrorBadgeColor);

        bodyHost.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        bodyHost.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var msgContainer = new Border
        {
            CornerRadius = new CornerRadius(8),
            Background = Freeze(new SolidColorBrush(Color.FromRgb(254, 242, 242))),
            BorderBrush = Freeze(new SolidColorBrush(Color.FromRgb(254, 202, 202))),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(12, 10, 12, 10),
            Child = new TextBlock
            {
                Text = message,
                FontSize = 13,
                Foreground = Freeze(new SolidColorBrush(Color.FromRgb(127, 29, 29))),
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 20
            }
        };
        Grid.SetRow(msgContainer, 0);
        bodyHost.Children.Add(msgContainer);

        var footer = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 16, 0, 0)
        };
        var okBtn = CreateDangerButton(owner, "知道了", minWidth: 86);
        okBtn.IsDefault = true;
        okBtn.IsCancel = true;
        okBtn.Click += (_, _) => CloseWithResult(window, true);
        footer.Children.Add(okBtn);

        Grid.SetRow(footer, 1);
        bodyHost.Children.Add(footer);

        window.ShowDialog();
    }

    public static bool Confirm(Window? owner, string title, string message, string confirmText = "确定", bool isDanger = false)
    {
        var window = CreateCardWindow(
            owner,
            title,
            null,
            410,
            null,
            false,
            out var bodyHost,
            badgeText: isDanger ? "!" : "?",
            badgeColor: isDanger ? DangerColor : AccentColor);

        bodyHost.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        bodyHost.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var messageText = new TextBlock
        {
            Text = message,
            FontSize = 13,
            Foreground = BodyBrush,
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 20
        };
        Grid.SetRow(messageText, 0);
        bodyHost.Children.Add(messageText);

        var footer = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 18, 0, 0)
        };

        var cancelBtn = CreateSecondaryButton(owner, "取消", minWidth: 74);
        cancelBtn.IsCancel = true;
        cancelBtn.Margin = new Thickness(0, 0, 8, 0);
        cancelBtn.Click += (_, _) => CloseWithResult(window, false);

        var confirmBtn = isDanger
            ? CreateDangerButton(owner, confirmText, minWidth: 88)
            : CreatePrimaryButton(owner, confirmText, minWidth: 88);
        confirmBtn.IsDefault = true;
        confirmBtn.Click += (_, _) => CloseWithResult(window, true);

        footer.Children.Add(cancelBtn);
        footer.Children.Add(confirmBtn);

        Grid.SetRow(footer, 1);
        bodyHost.Children.Add(footer);

        return window.ShowDialog() == true;
    }

    public static string? PromptSingleLine(Window? owner, string title, string label, string initial = "")
    {
        var window = CreateCardWindow(owner, title, null, 400, null, false, out var bodyHost);

        bodyHost.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        bodyHost.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        bodyHost.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var caption = new TextBlock
        {
            Text = label,
            FontSize = 12.5,
            Foreground = MutedBrush,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8)
        };
        Grid.SetRow(caption, 0);
        bodyHost.Children.Add(caption);

        var input = new TextBox
        {
            Text = initial,
            FontSize = 13.5,
            Padding = new Thickness(10, 7, 10, 7),
            BorderBrush = InputBorderBrush,
            BorderThickness = new Thickness(1)
        };
        Grid.SetRow(input, 1);
        bodyHost.Children.Add(input);

        var footer = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 16, 0, 0)
        };

        var cancelBtn = CreateSecondaryButton(owner, "取消", minWidth: 74);
        cancelBtn.IsCancel = true;
        cancelBtn.Margin = new Thickness(0, 0, 8, 0);
        cancelBtn.Click += (_, _) => CloseWithResult(window, false);

        var okBtn = CreatePrimaryButton(owner, "确定", minWidth: 82);
        okBtn.IsDefault = true;
        okBtn.Click += (_, _) => CloseWithResult(window, true);

        footer.Children.Add(cancelBtn);
        footer.Children.Add(okBtn);
        Grid.SetRow(footer, 2);
        bodyHost.Children.Add(footer);

        window.Loaded += (_, _) =>
        {
            input.Focus();
            input.SelectAll();
        };

        return window.ShowDialog() == true ? input.Text : null;
    }

    public static string? PromptLyrics(Window? owner, string title, string label, string initial = "", double height = 400)
    {
        var window = CreateCardWindow(owner, title, null, 580, height, true, out var bodyHost);

        bodyHost.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        bodyHost.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        bodyHost.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var caption = new TextBlock
        {
            Text = label,
            FontSize = 12.5,
            Foreground = MutedBrush,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8)
        };
        Grid.SetRow(caption, 0);
        bodyHost.Children.Add(caption);

        var inputContainer = new Border
        {
            CornerRadius = new CornerRadius(8),
            BorderBrush = InputBorderBrush,
            BorderThickness = new Thickness(1),
            Background = Brushes.White,
            Padding = new Thickness(2)
        };

        var input = new TextBox
        {
            Text = initial,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalContentAlignment = VerticalAlignment.Top,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            FontSize = 14,
            Padding = new Thickness(10, 8, 10, 8),
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent
        };
        inputContainer.Child = input;
        Grid.SetRow(inputContainer, 1);
        bodyHost.Children.Add(inputContainer);

        var footer = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 14, 0, 0)
        };

        var cancelBtn = CreateSecondaryButton(owner, "取消", minWidth: 76);
        cancelBtn.IsCancel = true;
        cancelBtn.Margin = new Thickness(0, 0, 8, 0);
        cancelBtn.Click += (_, _) => CloseWithResult(window, false);

        var okBtn = CreatePrimaryButton(owner, "确定", minWidth: 86);
        okBtn.IsDefault = false;
        okBtn.Click += (_, _) => CloseWithResult(window, true);

        footer.Children.Add(cancelBtn);
        footer.Children.Add(okBtn);
        Grid.SetRow(footer, 2);
        bodyHost.Children.Add(footer);

        window.Loaded += (_, _) =>
        {
            input.Focus();
            input.CaretIndex = input.Text.Length;
        };

        return window.ShowDialog() == true ? input.Text : null;
    }

    internal static Button CreatePrimaryButton(Window? owner, string content, double minWidth = 82)
    {
        var btn = new Button
        {
            Content = content,
            MinWidth = minWidth,
            Padding = new Thickness(12, 6, 12, 6),
            FontSize = 12.5,
            FontWeight = FontWeights.SemiBold,
            Cursor = Cursors.Hand
        };
        if (owner?.TryFindResource("PrimaryButton") is Style primaryStyle)
        {
            btn.Style = primaryStyle;
        }
        else
        {
            btn.Background = AccentBrush;
            btn.Foreground = Brushes.White;
            btn.BorderBrush = AccentBrush;
        }
        return btn;
    }

    internal static Button CreateSecondaryButton(Window? owner, string content, double minWidth = 74)
    {
        var btn = new Button
        {
            Content = content,
            MinWidth = minWidth,
            Padding = new Thickness(12, 6, 12, 6),
            FontSize = 12.5,
            Cursor = Cursors.Hand
        };
        if (owner?.TryFindResource(typeof(Button)) is Style baseStyle)
        {
            btn.Style = baseStyle;
        }
        return btn;
    }

    internal static Button CreateAccentOutlineButton(Window? owner, string content, double minWidth = 96)
    {
        var btn = new Button
        {
            Content = content,
            MinWidth = minWidth,
            Padding = new Thickness(12, 6, 12, 6),
            FontSize = 12.5,
            FontWeight = FontWeights.SemiBold,
            Cursor = Cursors.Hand
        };
        if (owner?.TryFindResource("AccentOutlineButton") is Style outlineStyle)
        {
            btn.Style = outlineStyle;
        }
        else
        {
            btn.Background = AccentLightBrush;
            btn.Foreground = AccentHoverBrush;
            btn.BorderBrush = Freeze(new SolidColorBrush(Color.FromRgb(148, 191, 164)));
        }
        return btn;
    }

    internal static Button CreateDangerButton(Window? owner, string content, double minWidth = 84)
    {
        var btn = new Button
        {
            Content = content,
            MinWidth = minWidth,
            Padding = new Thickness(12, 6, 12, 6),
            FontSize = 12.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
            Background = DangerBrush,
            BorderBrush = DangerBrush,
            BorderThickness = new Thickness(1),
            Cursor = Cursors.Hand,
            Template = BuildRoundedButtonTemplate(DangerHoverBrush)
        };
        return btn;
    }

    internal static void CloseWithResult(Window window, bool result)
    {
        try
        {
            window.DialogResult = result;
        }
        catch (InvalidOperationException)
        {
            window.Close();
        }
    }

    private static Button CreateCloseButton(Window window)
    {
        var hoverBg = Freeze(new SolidColorBrush(Color.FromRgb(241, 245, 249))); // #F1F5F9
        var btn = new Button
        {
            Content = "✕",
            Width = 26,
            Height = 26,
            Padding = new Thickness(0),
            FontSize = 12,
            Foreground = MutedBrush,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            Cursor = Cursors.Hand,
            ToolTip = "关闭",
            Template = BuildRoundedButtonTemplate(hoverBg, cornerRadius: 6)
        };
        btn.Click += (_, _) => CloseWithResult(window, false);
        return btn;
    }

    private static ControlTemplate BuildRoundedButtonTemplate(Brush hoverBackground, double cornerRadius = 6)
    {
        var template = new ControlTemplate(typeof(Button));
        var chrome = new FrameworkElementFactory(typeof(Border), "Chrome");
        chrome.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
        chrome.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
        chrome.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
        chrome.SetValue(Border.CornerRadiusProperty, new CornerRadius(cornerRadius));
        chrome.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));

        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        chrome.AppendChild(presenter);

        template.VisualTree = chrome;

        var hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty, hoverBackground, "Chrome"));
        template.Triggers.Add(hoverTrigger);

        var pressedTrigger = new Trigger { Property = System.Windows.Controls.Primitives.ButtonBase.IsPressedProperty, Value = true };
        pressedTrigger.Setters.Add(new Setter(UIElement.OpacityProperty, 0.82, "Chrome"));
        template.Triggers.Add(pressedTrigger);

        return template;
    }
}
