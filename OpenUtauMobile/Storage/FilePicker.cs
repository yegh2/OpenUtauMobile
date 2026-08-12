using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using OpenUtauMobile.Controls;
using OpenUtauMobile.Services;
using OpenUtauMobile.ViewModels;
using Serilog;

namespace OpenUtauMobile.Storage;

/// <summary>
/// 完全封装的文件选择器
/// </summary>
public static class FilePicker
{
    // 内置 UI：Android / iOS 使用（iOS 放弃系统 UIDocumentPicker，
    // 因为 LiveContainer 下 Fix File Picker 依赖 TweakLoader 且复制机制不稳定）；
    // Windows DEBUG 下用内置 UI 方便调试。
    private static readonly bool UseInternalPicker = OperatingSystem.IsAndroid()
                                                     || OperatingSystem.IsIOS()
#if DEBUG
                                                     || OperatingSystem.IsWindows()
#endif
        ;
    /// <summary>
    /// 权限检查
    /// </summary>
    /// <returns></returns>
    private static bool CheckAndRequestStoragePermission()
    {
        if (OperatingSystem.IsIOS()) return true; // iOS sandbox needs no external storage permission

        IExternalStorageService? service = ServiceHub.ExternalStorageService;
        if (service == null) return false;
        if (!service.HasManageExternalStoragePermissionAsync())
        {
            service.RequestManageExternalStoragePermission();
            return false;
        }

        return true;
    }

    /// <summary>
    /// 使用内置文件选择器
    /// </summary>
    private static async Task<string> PickSingleFileInternalAsync(string title, string[] filters)
    {
        if (!CheckAndRequestStoragePermission()) return string.Empty;
        string? result = await RunOnUiThreadAsync(() =>
            PopupService.Show<string>(new FilePickerPopup(), new FilePickerPopupViewModel(title, filters)));
        return result ?? string.Empty;
    }

    /// <summary>
    /// 引导用户选取一个文件
    /// </summary>
    /// <param name="title">弹窗标题</param>
    /// <param name="filters">过滤器，形如["*.wav"]</param>
    /// <returns>选中的文件路径，如果取消或失败则返回 <see cref="string.Empty"/></returns>
    public static async Task<string> PickSingleFileAsync(string title, string[] filters)
    {
        // Android 需要特殊处理
        if (UseInternalPicker)
        {
            return await PickSingleFileInternalAsync(title, filters);
        }

        // iOS：走原生 UIDocumentPicker（Avalonia iOS storage provider 不稳定），
        // 选完拷贝进沙盒保证后续可读且跨会话可用。
        if (OperatingSystem.IsIOS() && ServiceHub.PickFileAsync != null)
        {
            string picked = await ServiceHub.PickFileAsync(title, filters);
            Log.Information("FilePicker: iOS 选择结果={Picked}", string.IsNullOrEmpty(picked) ? "<empty>" : picked);
            if (string.IsNullOrEmpty(picked)) return string.Empty;
            return ServiceHub.ImportFileToSandbox?.Invoke(picked) ?? picked;
        }
        if (OperatingSystem.IsIOS())
        {
            Log.Warning("FilePicker: iOS 但 ServiceHub.PickFileAsync 未接线，走 Avalonia provider");
        }

        IStorageProvider? storageProvider = StorageProviderFactory.GetStorageProvider();
        if (storageProvider is null || !storageProvider.CanOpen)
            return string.Empty;

        FilePickerFileType[]? fileTypes = filters.Length > 0
            ? [new FilePickerFileType("Files") { Patterns = filters }]
            : null;

        FilePickerOpenOptions options = new()
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = fileTypes
        };

        IReadOnlyList<IStorageFile> files = await storageProvider.OpenFilePickerAsync(options);
        if (files.Count == 0) return string.Empty;
        string? path = files[0].TryGetLocalPath();
        if (string.IsNullOrEmpty(path)) return string.Empty;
        return path;
    }

    /// <summary>
    /// 使用内置文件夹选择器（Android 使用自定义 UI）。
    /// 取消或失败时返回 <see cref="string.Empty"/>。
    /// </summary>
    private static async Task<string> PickFolderInternalAsync(string title)
    {
        if (!CheckAndRequestStoragePermission()) return string.Empty;
        string? result = await RunOnUiThreadAsync(() =>
            PopupService.Show<string>(new FilePickerPopup(), new FolderPickerViewModel(title)));
        return result ?? string.Empty;
    }

    /// <summary>
    /// 使用系统文件夹选择器选择一个目录，全平台统一入口。
    /// Android 回退至内置 UI。
    /// 取消或失败时返回 <see cref="string.Empty"/>。
    /// </summary>
    public static async Task<string> PickFolderAsync(string title)
    {
        if (UseInternalPicker)
            return await PickFolderInternalAsync(title);

        // iOS：原生文件夹选择器 + 整体拷进沙盒（如音源目录）。
        if (OperatingSystem.IsIOS() && ServiceHub.PickFolderAsync != null)
        {
            string picked = await ServiceHub.PickFolderAsync(title);
            Log.Information("FilePicker: iOS 文件夹结果={Picked}", string.IsNullOrEmpty(picked) ? "<empty>" : picked);
            if (string.IsNullOrEmpty(picked)) return string.Empty;
            return ServiceHub.ImportFolderToSandbox?.Invoke(picked) ?? picked;
        }

        IStorageProvider? storageProvider = StorageProviderFactory.GetStorageProvider();
        if (storageProvider is null || !storageProvider.CanPickFolder)
            return string.Empty;

        FolderPickerOpenOptions options = new()
        {
            Title = title,
            AllowMultiple = false,
        };

        IReadOnlyList<IStorageFolder> folders = await storageProvider.OpenFolderPickerAsync(options);
        if (folders.Count == 0) return string.Empty;
        string? path = folders[0].TryGetLocalPath();
        if (string.IsNullOrEmpty(path)) return string.Empty;
        return path;
    }

    /// <summary>
    /// 文件保存对话框，全平台统一入口。
    /// Android / Windows 使用内置 UI；其他平台使用系统 <see cref="IStorageProvider.SaveFilePickerAsync"/>。
    /// </summary>
    /// <param name="title">对话框标题</param>
    /// <param name="extension">强制后缀（含点或不含均可，如 ".ustx" 或 "ustx"）</param>
    /// <param name="defaultFileName">预填文件名（不含后缀）</param>
    /// <param name="initialPath">初始目录</param>
    /// <returns>完整目标路径；取消时返回 <see cref="string.Empty"/>。</returns>
    public static async Task<string> SaveFileAsync(
        string title, string extension,
        string defaultFileName = "", string initialPath = "")
    {
        if (UseInternalPicker)
        {
            if (!CheckAndRequestStoragePermission()) return string.Empty;
            string? result = await RunOnUiThreadAsync(() =>
                PopupService.Show<string?>(new FilePickerPopup(),
                    new FileSavePickerViewModel(title, extension, initialPath, defaultFileName)));
            return result ?? string.Empty;
        }

        // iOS：原生保存对话框（导出模式），返回路径后开启 security-scoped 授权。
        // 写入完成后调用 <see cref="ReleaseSaveAccess"/> 释放。
        if (OperatingSystem.IsIOS() && ServiceHub.SaveFileAsync != null)
        {
            string picked = await ServiceHub.SaveFileAsync(title, extension, defaultFileName);
            Log.Information("FilePicker: iOS 保存结果={Picked}", string.IsNullOrEmpty(picked) ? "<empty>" : picked);
            if (string.IsNullOrEmpty(picked)) return string.Empty;
            return ServiceHub.PrepareSaveDestination?.Invoke(picked) ?? picked;
        }

        // 其他平台：使用系统 SaveFilePicker
        IStorageProvider? storageProvider = StorageProviderFactory.GetStorageProvider();
        if (storageProvider is null || !storageProvider.CanSave) return string.Empty;

        string ext = extension.StartsWith('.') ? extension : "." + extension;
        IStorageFile? file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = defaultFileName,
            DefaultExtension = ext,
            FileTypeChoices = [new FilePickerFileType("Files") { Patterns = [$"*{ext}"] }],
        });
        if (file is null) return string.Empty;
        string? path = file.TryGetLocalPath();
        if (string.IsNullOrEmpty(path)) return string.Empty;
        return path;
    }

    /// <summary>
    /// 释放保存路径的访问授权（iOS）。导出完成后调用。
    /// </summary>
    public static void ReleaseSaveAccess(string path)
    {
        if (OperatingSystem.IsIOS() && !string.IsNullOrEmpty(path))
        {
            ServiceHub.ReleaseSaveDestination?.Invoke(path);
        }
    }

    private static async Task<T> RunOnUiThreadAsync<T>(Func<Task<T>> action)
    {
        if (Dispatcher.UIThread.CheckAccess())
            return await action();

        TaskCompletionSource<T> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Dispatcher.UIThread.Post(async void () =>
        {
            try
            {
                tcs.SetResult(await action());
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        }, DispatcherPriority.Send);

        return await tcs.Task;
    }
}
