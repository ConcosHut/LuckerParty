using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace LuckerParty.Launcher;

internal sealed partial class LauncherWindow
{
    private sealed record InstanceCardElements(Border Status, TextBlock CloseLabel, Button Show, Button Close);

    private Border BuildSidebar()
    {
        var rail = new Grid { RowDefinitions = new("Auto,*,Auto"), Margin = new Thickness(16, 30, 16, 22) };
        var header = new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 10,
            Margin = new Thickness(6, 0, 0, 18), VerticalAlignment = VerticalAlignment.Center
        };
        var heading = PartyRoom.Text("Running instances", 19); heading.FontWeight = FontWeight.ExtraBold;
        heading.VerticalAlignment = VerticalAlignment.Center; header.Children.Add(heading);
        _count.FontSize = 16; _count.FontWeight = FontWeight.Bold; _count.VerticalAlignment = VerticalAlignment.Center;
        header.Children.Add(new Border
        {
            Background = PartyRoom.Lilac, CornerRadius = new CornerRadius(20),
            MinWidth = 32, Padding = new Thickness(8, 4), Child = _count
        });
        Place(rail, header);
        _instances.Spacing = 14;
        Place(rail, new ScrollViewer { Content = _instances, Margin = new Thickness(0, 0, 0, 16) }, 1);
        var hint = PartyRoom.Text("Updates wait until all game windows close.", 13, PartyRoom.Muted);
        hint.Margin = new Thickness(6, 0); Place(rail, hint, 2);
        return new Border { Name = "InstanceSidebar", Width = 304, Background = PartyRoom.Sand, Child = rail };
    }

    private void RefreshInstances()
    {
        var games = _controller.Instances; _count.Text = games.Length.ToString();
        var ids = games.Select(game => game.Identity.Pid).ToHashSet();
        foreach (var id in _cards.Keys.Where(id => !ids.Contains(id)).ToArray())
        { _instances.Children.Remove(_cards[id].Card); _cards.Remove(id); }
        if (games.Length == 0)
        {
            if (_instances.Children.Count == 0) _instances.Children.Add(BuildEmptyInstances());
            return;
        }
        if (_cards.Count == 0) _instances.Children.Clear();
        foreach (var game in games)
        {
            if (!_cards.TryGetValue(game.Identity.Pid, out var card))
            {
                card = BuildInstanceCard(game); _cards[game.Identity.Pid] = card; _instances.Children.Add(card.Card);
            }
            var minutes = Math.Max(0, (int)(DateTime.UtcNow - game.StartedAt).TotalMinutes);
            card.Time.Text = game.CloseFailed ? "Couldn't close" : game.Closing ? "Closing…" :
                minutes == 0 ? "Running · just now" : $"Running · {minutes} min";
            card.Time.Foreground = game.CloseFailed ? PartyRoom.Coral : PartyRoom.Muted;
            if (card.Card.Tag is not InstanceCardElements elements) continue;
            elements.Status.Background = game.CloseFailed ? PartyRoom.Coral : game.Closing ? PartyRoom.Brush("#EAAB43") : PartyRoom.Green;
            ToolTip.SetTip(elements.Status, game.CloseFailed ? "The game did not exit. Force close is available." : game.Closing ? "Waiting for this game to close." : "This game is running.");
            elements.Show.IsEnabled = !game.Closing; elements.Close.IsEnabled = !game.Closing;
            elements.CloseLabel.Text = game.CloseFailed ? "Force close" : "Close";
            ToolTip.SetTip(elements.Close, game.CloseFailed ? "End only this game process after normal close failed." : "Close this game window.");
        }
    }

    private static Border BuildEmptyInstances()
    {
        var content = new StackPanel { Spacing = 12, HorizontalAlignment = HorizontalAlignment.Center };
        content.Children.Add(new CapsuleThumbnail(1) { Width = 68, Height = 90, HorizontalAlignment = HorizontalAlignment.Center });
        var title = PartyRoom.Text("Your party starts here", 18); title.FontWeight = FontWeight.ExtraBold;
        title.TextAlignment = TextAlignment.Center; content.Children.Add(title);
        var hint = PartyRoom.Text("Press Play to start a game. Your windows will appear here so you can show or close each one.", 14, PartyRoom.Muted);
        hint.TextAlignment = TextAlignment.Center; content.Children.Add(hint);
        return new Border
        {
            Background = PartyRoom.Card, BorderBrush = PartyRoom.Brush("#E6DCCD"), BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(18), Padding = new Thickness(18, 24), Child = content
        };
    }

    private (Border Card, TextBlock Time) BuildInstanceCard(GameInstance game)
    {
        var row = new Grid { ColumnDefinitions = new("64,*"), RowDefinitions = new("Auto,Auto") };
        Place(row, new CapsuleThumbnail(game.Number) { Width = 64, Height = 90, VerticalAlignment = VerticalAlignment.Center });
        var details = new StackPanel { Spacing = 7, Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        var title = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 7 };
        var dot = new Border { Width = 8, Height = 8, CornerRadius = new CornerRadius(4), Background = PartyRoom.Green, VerticalAlignment = VerticalAlignment.Center };
        title.Children.Add(dot);
        var label = PartyRoom.Text($"Instance {game.Number:00}", 18); label.FontWeight = FontWeight.ExtraBold;
        title.Children.Add(label); details.Children.Add(title);
        var time = PartyRoom.Text("Running · just now", 14, PartyRoom.Muted); details.Children.Add(time);
        var actions = new Grid { ColumnDefinitions = new("*,*"), ColumnSpacing = 8, Margin = new Thickness(0, 14, 0, 0) };
        var show = InstanceAction("Show", "eye", PartyRoom.Lilac);
        show.Click += async (_, _) => await _controller.ShowInstanceAsync(game.Identity);
        var close = InstanceAction("Close", "close", PartyRoom.Brush("#ECE9E6"));
        close.Click += async (_, _) => await _controller.CloseInstanceAsync(game.Identity,
            force: _controller.Instances.Any(instance => instance.Identity == game.Identity && instance.CloseFailed));
        AutomationProperties.SetName(show, $"Show instance {game.Number}"); AutomationProperties.SetName(close, $"Close instance {game.Number}");
        ToolTip.SetTip(show, "Bring this game window to the front.");
        Place(actions, show); Place(actions, close, column: 1);
        Place(row, details, column: 1);
        Place(row, actions, 1); Grid.SetColumnSpan(actions, 2);
        var card = new Border
        {
            Background = PartyRoom.Card, BorderBrush = PartyRoom.Brush("#E8DFD2"), BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(18), Padding = new Thickness(16), Child = row,
            BoxShadow = new BoxShadows(new BoxShadow { OffsetY = 2, Blur = 8, Color = Color.Parse("#0936291B") }),
            Tag = new InstanceCardElements(dot, ((StackPanel)close.Content!).Children.OfType<TextBlock>().Single(), show, close)
        };
        return (card, time);
    }

    private static Button InstanceAction(string label, string icon, IBrush background)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5, VerticalAlignment = VerticalAlignment.Center };
        content.Children.Add(new InstanceActionIcon(icon) { Width = 15, Height = 15, VerticalAlignment = VerticalAlignment.Center });
        var text = PartyRoom.Text(label, 14); text.FontWeight = FontWeight.Bold; content.Children.Add(text);
        var button = Button(content, background); button.Padding = new Thickness(8, 10); button.MinHeight = 44;
        button.HorizontalAlignment = HorizontalAlignment.Stretch;
        return button;
    }
}
