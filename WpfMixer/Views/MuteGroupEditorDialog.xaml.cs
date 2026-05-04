using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using WpfMixer.Models;

namespace WpfMixer.Views;

public partial class MuteGroupEditorDialog : Window
{
    private readonly MuteGroup _group;

    public MuteGroupEditorDialog(MuteGroup group, ObservableCollection<Channel> allChannels)
    {
        InitializeComponent();
        _group = group;
        NameBox.Text = group.Name;

        var items = new List<ChannelCheckItem>();
        foreach (var ch in allChannels)
            items.Add(new ChannelCheckItem(ch, group.ChannelIds.Contains(ch.Id)));
        ChannelList.ItemsSource = items;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        _group.Name = NameBox.Text;
        _group.ChannelIds.Clear();
        foreach (var item in (List<ChannelCheckItem>)ChannelList.ItemsSource)
            if (item.IsInGroup) _group.ChannelIds.Add(item.Channel.Id);
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
        => DialogResult = false;
}

public class ChannelCheckItem : ObservableObject
{
    public Channel Channel { get; }

    private bool _isInGroup;
    public bool IsInGroup
    {
        get => _isInGroup;
        set => SetProperty(ref _isInGroup, value);
    }

    public ChannelCheckItem(Channel channel, bool isInGroup)
    {
        Channel = channel;
        _isInGroup = isInGroup;
    }
}
