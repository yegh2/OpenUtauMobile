using System.Windows.Input;
using IconPacks.Avalonia.PhosphorIcons;

namespace OpenUtauMobile.ViewModels;

/// <summary>
/// 批处理下拉菜单中的一个文本条目。
/// 与 ContextActionItem（图标+ToolTip）不同，该模型用于显示文字菜单。
/// </summary>
public class MenuActionItem
{
    /// <summary>菜单项显示文本（已本地化）。</summary>
    public string Header { get; init; } = string.Empty;

    /// <summary>菜单项可选图标。</summary>
    public PackIconPhosphorIconsKind? Icon { get; init; }

    /// <summary>菜单项点击时执行的命令。</summary>
    public ICommand Command { get; init; } = null!;

    /// <summary>是否为危险操作（重置类），true 时显示红色样式。</summary>
    public bool IsDanger { get; init; }
}