using System;
using System.IO;
using System.Reactive;
using OpenUtau.Core;
using OpenUtauMobile.Helpers;
using OpenUtauMobile.Services;
using ReactiveUI;
using Serilog;

namespace OpenUtauMobile.ViewModels;

public class OptionsViewModel : NavigateViewModelBase
{
    public ReactiveCommand<Unit, Unit> BackCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenSettingsCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenDependencyManagerCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenHelpCommand { get; }
    public ReactiveCommand<Unit, Unit> ExportLogCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenAboutCommand { get; }

    public OptionsViewModel(MainViewModel navigator) : base(navigator)
    {
        BackCommand = ReactiveCommand.Create(OnBack);
        OpenSettingsCommand = ReactiveCommand.Create(OnOpenSettings);
        OpenDependencyManagerCommand = ReactiveCommand.Create(OnOpenDependencyManager);
        OpenHelpCommand = ReactiveCommand.Create(OnOpenHelp);
        ExportLogCommand = ReactiveCommand.Create(OnExportLog);
        OpenAboutCommand = ReactiveCommand.Create(OnOpenAbout);
    }

    private void OnBack()
    {
        Navigator.NavigateBack(this);
    }

    private void OnOpenSettings()
    {
        Navigator.Navigate(new SettingsViewModel(Navigator));
    }

    private void OnOpenDependencyManager()
    {
        Navigator.Navigate(new DependencyManagerViewModel(Navigator));
    }

    private void OnOpenHelp()
    {
        // TODO: Navigator.Navigate(new HelpViewModel(Navigator));
        ToastService.Enqueue(L.S("Options.Toast.HelpNotImpl"));
    }

    private async void OnExportLog()
    {
        try
        {
            string logPath = PathManager.Inst.LogFilePath;
            if (!File.Exists(logPath))
            {
                ToastService.Enqueue("日志文件不存在（首次启动后生成）");
                return;
            }

            if (ServiceHub.ShareFile != null)
            {
                // iOS：弹出系统分享面板（存文件 / 隔空投送 / 发微信等）
                bool ok = await ServiceHub.ShareFile(logPath);
                if (!ok)
                {
                    ToastService.Enqueue("分享失败");
                }
            }
            else
            {
                // 桌面等平台：直接提示日志位置
                ToastService.Enqueue($"日志路径：{logPath}");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "导出日志失败");
            ToastService.Enqueue($"导出日志失败：{ex.Message}");
        }
    }

    private void OnOpenAbout()
    {
        Navigator.Navigate(new AboutViewModel(Navigator));
    }
}