using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
namespace GameBoost.UI.Services;

public static class ResolutionConfirmation
{
    public static Task<bool> AskAsync()
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(15);
        var text = new TextBlock { Margin = new Thickness(16), FontSize = 17 };
        var keep = new Button { Content = "Manter resolução", Margin = new Thickness(10) };
        var revert = new Button { Content = "Reverter", IsCancel = true, Margin = new Thickness(10) };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal };
        buttons.Children.Add(keep); buttons.Children.Add(revert);
        var content = new StackPanel(); content.Children.Add(text); content.Children.Add(buttons);
        var dialog = new Window { Title = "Confirmar resolução", Content = content, SizeToContent = SizeToContent.WidthAndHeight, MinWidth = 420, WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = Application.Current.MainWindow, ResizeMode = ResizeMode.NoResize, Topmost = true };
        keep.Click += (_, _) => dialog.DialogResult = true;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        void Tick()
        {
            var remaining = (int)Math.Ceiling((deadline - DateTimeOffset.UtcNow).TotalSeconds);
            text.Text = $"Manter esta resolução? Reversão automática em {Math.Max(0, remaining)} s.";
            if (remaining <= 0) dialog.DialogResult = false;
        }
        timer.Tick += (_, _) => Tick();
        dialog.Loaded += (_, _) => { Tick(); timer.Start(); };
        dialog.Closed += (_, _) => timer.Stop();
        return Task.FromResult(dialog.ShowDialog() == true);
    }
}
