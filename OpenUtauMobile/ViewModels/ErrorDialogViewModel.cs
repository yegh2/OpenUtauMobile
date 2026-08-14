using System;
using System.Reactive;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using OpenUtau.Core;
using OpenUtauMobile.Helpers;
using OpenUtauMobile.Services;
using ReactiveUI;

namespace OpenUtauMobile.ViewModels;

/// <summary>
/// 全局错误弹窗的 ViewModel。
/// 由 <see cref="OpenUtau.Core.ErrorMessageNotification"/> 的内容构造。
/// </summary>
public class ErrorDialogViewModel : PopupViewModelBase
{
    public string Title { get; }
    public string Message { get; }
    public string Detail { get; }
    public bool HasDetail => !string.IsNullOrWhiteSpace(Detail);

    public ReactiveCommand<Unit, Unit> CloseCommand { get; }

    /// <summary>
    /// 复制错误内容到剪贴板（含摘要与详情）。
    /// </summary>
    public ReactiveCommand<Unit, Unit> CopyCommand { get; }

    public ErrorDialogViewModel(ErrorMessageNotification notification)
    {
        // 提取友好摘要
        if (notification.e is MessageCustomizableException mce)
        {
            Title = L.S("ErrorDialog.Title");
            Message = string.IsNullOrWhiteSpace(mce.Message)
                ? mce.SubstanceException.Message
                : mce.Message;
            Detail = mce.SubstanceException.ToString();
        }
        else if (notification.e != null)
        {
            Title = L.S("ErrorDialog.Title");
            Message = string.IsNullOrWhiteSpace(notification.message)
                ? notification.e.Message
                : notification.message;
            Detail = notification.e.ToString();
        }
        else
        {
            Title = L.S("ErrorDialog.Title");
            Message = string.IsNullOrWhiteSpace(notification.message)
                ? L.S("ErrorDialog.UnknownError")
                : notification.message;
            Detail = string.Empty;
        }

        CloseCommand = ReactiveCommand.Create(RequestBack);
        CopyCommand = ReactiveCommand.CreateFromTask(CopyAsync);
    }

    public override void RequestBack()
    {
        RaiseClose(null);
    }

    /// <summary>
    /// 将标题、摘要与详情拼成文本复制到剪贴板。
    /// </summary>
    private async Task CopyAsync()
    {
        string text = Title + "\n" + Message;
        if (!string.IsNullOrWhiteSpace(Detail))
        {
            text += "\n\n" + Detail;
        }

        TopLevel? topLevel = AppService.GetTopLevel();
        IClipboard? clipboard = topLevel?.Clipboard;
        if (clipboard == null)
        {
            return;
        }

        await clipboard.SetTextAsync(text);
        ToastService.Enqueue(L.S("Common.Copied"));
    }
}
