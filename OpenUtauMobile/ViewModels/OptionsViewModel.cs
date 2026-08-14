using System;
using System.IO;
using System.Reactive;
using System.Threading.Tasks;
using OpenUtau;
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
    public ReactiveCommand<Unit, Unit> OpenLogFolderCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenAboutCommand { get; }

    public OptionsViewModel(MainViewModel navigator) : base(navigator)
    {
        BackCommand = ReactiveCommand.Create(OnBack);
        OpenSettingsCommand = ReactiveCommand.Create(OnOpenSettings);
        OpenDependencyManagerCommand = ReactiveCommand.Create(OnOpenDependencyManager);
        OpenHelpCommand = ReactiveCommand.Create(OnOpenHelp);
        OpenLogFolderCommand = ReactiveCommand.CreateFromTask(OnOpenLogFolderAsync);
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

    /// <summary>
    /// 打开日志文件夹：桌面平台用系统文件管理器，移动平台用注入的 <see cref="ServiceHub.OpenFolder"/>。
    /// </summary>
    private static async Task OnOpenLogFolderAsync()
    {
        try
        {
            string logDir = PathManager.Inst.LogsPath;
            if (!Directory.Exists(logDir))
            {
                ToastService.Enqueue(L.S("Options.Toast.LogFolderNotExist"));
                return;
            }

            if (ServiceHub.OpenFolder != null)
            {
                bool ok = await ServiceHub.OpenFolder(logDir);
                if (!ok)
                {
                    ToastService.Enqueue(L.S("Options.Toast.OpenLogFolderFailed"));
                }
            }
            else
            {
                // 桌面等平台：系统文件管理器打开
                OS.OpenFolder(logDir);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "打开日志文件夹失败");
            ToastService.Enqueue(L.S("Options.Toast.OpenLogFolderFailed"));
        }
    }

    private void OnOpenAbout()
    {
        Navigator.Navigate(new AboutViewModel(Navigator));
    }
}