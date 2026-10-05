namespace LayoutOverlay;

public sealed class LayoutChangeEventArgs(uint layoutId) : EventArgs
{
    public uint LayoutId { get; } = layoutId;
}
