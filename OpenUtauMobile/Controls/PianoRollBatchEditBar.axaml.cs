using Avalonia.Controls;
using Avalonia.Interactivity;

namespace OpenUtauMobile.Controls;

/// <summary>
/// 钢琴卷帘批处理工具栏。
/// 左侧为全选与升降八度快捷按钮，右侧为歌词/音符/重置三个批处理下拉菜单。
/// </summary>
public partial class PianoRollBatchEditBar : UserControl
{
    public PianoRollBatchEditBar()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 点击下拉按钮时在按钮旁打开对应的批处理菜单。
    /// </summary>
    private void OnMenuButtonClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { ContextMenu: { } menu } button)
        {
            menu.Open(button);
        }
    }
}