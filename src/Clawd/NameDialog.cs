using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;

namespace Clawd;

/// <summary>Small "name your crab" window.</summary>
class NameDialog : Window
{
    public NameDialog(string current, Action<string> onOk)
    {
        Title = "Имя краба";
        Width = 300; SizeToContent = SizeToContent.Height; CanResize = false;
        Topmost = true; WindowStartupLocation = WindowStartupLocation.CenterScreen;

        var box = new TextBox { Text = current, MaxLength = 16, Watermark = "Clawd" };
        void Ok() { onOk((box.Text ?? "").Trim()); Close(); }
        box.KeyDown += (_, e) => { if (e.Key == Key.Enter) Ok(); };

        var ok = new Button { Content = "Готово" };
        ok.Click += (_, _) => Ok();
        var cancel = new Button { Content = "Отмена" };
        cancel.Click += (_, _) => Close();

        Content = new StackPanel
        {
            Margin = new Thickness(16), Spacing = 10,
            Children =
            {
                new TextBlock { Text = "Как назовём краба?" },
                box,
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { ok, cancel } },
            }
        };
        Opened += (_, _) => { box.Focus(); box.SelectAll(); };
    }
}
