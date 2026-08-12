using System.Reactive;
using ReactiveUI;

namespace OpenUtauMobile.ViewModels;

/// <summary>
/// 通用确认弹窗 ViewModel。确认返回 true，取消/关闭返回 null。
/// </summary>
public class ConfirmPopupViewModel : PopupViewModelBase
{
    public string Title { get; }
    public string Message { get; }
    public string ConfirmText { get; }
    public string CancelText { get; }

    public ReactiveCommand<Unit, Unit> ConfirmCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelCommand { get; }

    public ConfirmPopupViewModel(string title, string message, string confirmText = "确定", string cancelText = "取消")
    {
        Title = title;
        Message = message;
        ConfirmText = confirmText;
        CancelText = cancelText;
        ConfirmCommand = ReactiveCommand.Create(() => RaiseClose(true));
        CancelCommand = ReactiveCommand.Create(() => RaiseClose(null));
    }
}
