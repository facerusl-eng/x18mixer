namespace WpfMixer.ViewModels;

public sealed class MixerPageViewModel
{
    public MixerViewModel Root { get; }
    public MixerPageViewModel(MixerViewModel root) => Root = root;
}

public sealed class RoutingPageViewModel
{
    public MixerViewModel Root { get; }
    public RoutingPageViewModel(MixerViewModel root) => Root = root;
}

public sealed class KeyboardMappingPageViewModel
{
    public MixerViewModel Root { get; }
    public KeyboardMappingPageViewModel(MixerViewModel root) => Root = root;
}

public sealed class SettingsPageViewModel
{
    public MixerViewModel Root { get; }
    public SettingsPageViewModel(MixerViewModel root) => Root = root;
}
