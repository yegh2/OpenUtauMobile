using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using OpenUtauMobile.ViewModels;

namespace OpenUtauMobile.Controls;

/// <summary>
/// 钢琴卷帘批处理工具栏。
/// 左侧为全选与升降八度快捷按钮，右侧为歌词/音符/重置三个下拉菜单。
/// </summary>
public partial class PianoRollBatchEditBar : UserControl
{
    public PianoRollBatchEditBar()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 点击下拉按钮时在按钮下方打开对应的批处理菜单。
    /// </summary>
    private void OnDropdownClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
        {
            return;
        }

        bool wasOpen = MenuPopup.IsOpen;
        MenuPopup.IsOpen = false;
        if (wasOpen && MenuPopup.PlacementTarget == button)
        {
            return;
        }

        if (button.Tag is not IReadOnlyList<MenuActionItem> actions)
        {
            return;
        }

        BuildMenuItems(actions);
        MenuPopup.PlacementTarget = button;
        MenuPopup.IsOpen = true;
    }

    /// <summary>
    /// 根据菜单条目列表构建菜单按钮。
    /// </summary>
    private void BuildMenuItems(IReadOnlyList<MenuActionItem> actions)
    {
        MenuPanel.Children.Clear();

        foreach (MenuActionItem item in actions)
        {
            Button menuButton = new()
            {
                Content = item.Header,
                Command = item.Command,
            };
            menuButton.Classes.Add("MenuItemBtn");
            if (item.IsDanger)
            {
                menuButton.Classes.Add("DangerBtn");
            }

            menuButton.Click += OnMenuItemClick;
            MenuPanel.Children.Add(menuButton);
        }
    }

    /// <summary>
    /// 点击菜单项后关闭下拉弹层。
    /// </summary>
    private void OnMenuItemClick(object? sender, RoutedEventArgs e)
    {
        MenuPopup.IsOpen = false;
    }
}