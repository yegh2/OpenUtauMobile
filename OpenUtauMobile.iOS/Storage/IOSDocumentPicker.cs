using System;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using CoreFoundation;
using Foundation;
using Serilog;
using UIKit;
using UniformTypeIdentifiers;

namespace OpenUtauMobile.iOS.Storage;

/// <summary>
/// 原生系统文件选择器（UIDocumentPickerViewController）。
/// 不依赖 Avalonia 的 iOS storage provider（其 RootViewController 获取在部分环境下
/// 会静默失败导致选择器无反应）。
/// 返回的是沙盒外的 security-scoped URL 路径，调用方需配合 IOSFileAccess 做
/// 沙盒导入或保存授权。
/// </summary>
public static class IOSDocumentPicker
{
    // 强引用持有 delegate，防止被 GC 回收导致回调丢失（“选了没反应”的经典原因）
    private static UIDocumentPickerDelegate? _activeDelegate;

    /// <summary>选择单个文件。取消或失败返回空字符串。</summary>
    [SupportedOSPlatform("ios14.0")]
    public static Task<string> PickFileAsync(string title, string[] filters)
    {
        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        DispatchQueue.MainQueue.DispatchAsync(() =>
        {
            try
            {
                if (!OperatingSystem.IsIOSVersionAtLeast(14))
                {
                    Log.Warning("IOSDocumentPicker: 系统文件选择器需要 iOS 14+");
                    tcs.TrySetResult(string.Empty);
                    return;
                }

                UTType[] allowed = ToUttTypes(filters);
                var picker = new UIDocumentPickerViewController(allowed, false);
                picker.Title = title;
                if (OperatingSystem.IsIOSVersionAtLeast(11))
                {
                    picker.AllowsMultipleSelection = false;
                }

                _activeDelegate = new PickDelegate(urls =>
                {
                    _activeDelegate = null;
                    if (urls == null || urls.Length == 0)
                    {
                        Log.Information("IOSDocumentPicker: 文件选择已取消");
                        tcs.TrySetResult(string.Empty);
                    }
                    else
                    {
                        Log.Information("IOSDocumentPicker: 选中文件 {Path}", urls[0].Path ?? "<no path>");
                        tcs.TrySetResult(urls[0].Path ?? string.Empty);
                    }
                });
                picker.Delegate = _activeDelegate;
                Present(picker);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "IOSDocumentPicker: 打开文件选择器失败");
                tcs.TrySetResult(string.Empty);
            }
        });
        return tcs.Task;
    }

    /// <summary>选择文件夹。取消或失败返回空字符串。</summary>
    [SupportedOSPlatform("ios14.0")]
    public static Task<string> PickFolderAsync(string title)
    {
        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        DispatchQueue.MainQueue.DispatchAsync(() =>
        {
            try
            {
                if (!OperatingSystem.IsIOSVersionAtLeast(14))
                {
                    Log.Warning("IOSDocumentPicker: 系统文件夹选择器需要 iOS 14+");
                    tcs.TrySetResult(string.Empty);
                    return;
                }

                var picker = new UIDocumentPickerViewController(new[] { UTTypes.Folder }, false);
                picker.Title = title;
                if (OperatingSystem.IsIOSVersionAtLeast(11))
                {
                    picker.AllowsMultipleSelection = false;
                }

                _activeDelegate = new PickDelegate(urls =>
                {
                    _activeDelegate = null;
                    if (urls == null || urls.Length == 0)
                    {
                        Log.Information("IOSDocumentPicker: 文件夹选择已取消");
                        tcs.TrySetResult(string.Empty);
                    }
                    else
                    {
                        Log.Information("IOSDocumentPicker: 选中文件夹 {Path}", urls[0].Path ?? "<no path>");
                        tcs.TrySetResult(urls[0].Path ?? string.Empty);
                    }
                });
                picker.Delegate = _activeDelegate;
                Present(picker);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "IOSDocumentPicker: 打开文件夹选择器失败");
                tcs.TrySetResult(string.Empty);
            }
        });
        return tcs.Task;
    }

    /// <summary>
    /// 系统保存对话框：导出模式（用户选位置，OS 负责拷贝）。
    /// 返回用户选择的目标路径（沙盒外），调用方需配合 IOSFileAccess.PrepareSavePath 授权后写入。
    /// 取消或失败返回空字符串。
    /// </summary>
    [SupportedOSPlatform("ios14.0")]
    public static Task<string> SaveFileAsync(string title, string extension, string defaultFileName)    {
        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        DispatchQueue.MainQueue.DispatchAsync(() =>
        {
            NSUrl? tempDir = null;
            try
            {
                if (!OperatingSystem.IsIOSVersionAtLeast(14))
                {
                    Log.Warning("IOSDocumentPicker: 系统保存对话框需要 iOS 14+");
                    tcs.TrySetResult(string.Empty);
                    return;
                }

                string ext = extension.StartsWith('.') ? extension : "." + extension;
                string fileName = string.IsNullOrEmpty(defaultFileName) ? "untitled" : defaultFileName;
                if (!fileName.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                {
                    fileName += ext;
                }

                // 先建一个临时空文件，再用导出模式让用户选位置
                tempDir = NSFileManager.DefaultManager.GetTemporaryDirectory().Append(Guid.NewGuid().ToString(), true);
                if (!NSFileManager.DefaultManager.CreateDirectory(tempDir, true, null, out _))
                {
                    Log.Error("IOSDocumentPicker: 创建临时目录失败");
                    tcs.TrySetResult(string.Empty);
                    return;
                }
                NSUrl tempFile = tempDir.Append(fileName, false);
                NSData.FromBytes(0, 0).Save(tempFile, false);

                var picker = new UIDocumentPickerViewController(new[] { tempFile }, asCopy: true);
                picker.Title = title;

                _activeDelegate = new PickDelegate(urls =>
                {
                    _activeDelegate = null;
                    try
                    {
                        if (tempDir != null)
                        {
                            NSFileManager.DefaultManager.Remove(tempDir, out _);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "IOSDocumentPicker: 清理临时目录失败");
                    }
                    if (urls == null || urls.Length == 0)
                    {
                        tcs.TrySetResult(string.Empty);
                    }
                    else
                    {
                        tcs.TrySetResult(urls[0].Path ?? string.Empty);
                    }
                });
                picker.Delegate = _activeDelegate;
                Present(picker);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "IOSDocumentPicker: 打开保存对话框失败");
                if (tempDir != null)
                {
                    try { NSFileManager.DefaultManager.Remove(tempDir, out _); } catch { }
                }
                tcs.TrySetResult(string.Empty);
            }
        });
        return tcs.Task;
    }

    /// <summary>
    /// 打开指定文件夹：用 UIDocumentPickerViewController 定位到该目录，
    /// 让用户在"文件"浏览器中直接看到文件夹内容（如日志目录）。
    /// iOS 无法像桌面那样打开系统文件管理器并选中目录，这是最接近的替代方案：
    /// 通过 <see cref="UIDocumentPickerViewController.DirectoryUrl"/> 让选择器初始显示目标文件夹。
    /// 注意：DirectoryUrl 在部分 iOS 版本（如 14.x）存在已知问题可能被忽略，
    /// 此时会退化为文件浏览器根目录，但仍比跳转文件 App 更接近目标。
    /// </summary>
    /// <param name="path">要打开的文件夹绝对路径（沙盒内）。</param>
    /// <returns>是否成功弹出文件浏览器。</returns>
    [SupportedOSPlatform("ios14.0")]
    public static Task<bool> OpenFolderAsync(string path)
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        DispatchQueue.MainQueue.DispatchAsync(() =>
        {
            try
            {
                if (!OperatingSystem.IsIOSVersionAtLeast(14))
                {
                    Log.Warning("IOSDocumentPicker: 打开文件夹需要 iOS 14+");
                    tcs.TrySetResult(false);
                    return;
                }

                // 打开模式（允许任意文件/文件夹），配合 DirectoryUrl 定位到目标目录，
                // 用户可直接看到日志文件夹内容并进入浏览。
                var picker = new UIDocumentPickerViewController(new[] { UTTypes.Data }, false);
                picker.Title = "OpenUtau";
                if (OperatingSystem.IsIOSVersionAtLeast(11))
                {
                    picker.AllowsMultipleSelection = false;
                }

                // 让文件浏览器初始定位到目标文件夹（部分系统版本可能忽略）
                NSUrl folderUrl = NSUrl.FromFilename(path);
                bool isDir = false;
                if (NSFileManager.DefaultManager.FileExists(folderUrl.Path ?? string.Empty, ref isDir) && isDir)
                {
                    picker.DirectoryUrl = folderUrl;
                }

                // 仅浏览，无需处理选择结果；保持强引用防止 delegate 被回收
                _activeDelegate = new PickDelegate(urls =>
                {
                    _activeDelegate = null;
                    tcs.TrySetResult(true);
                });
                picker.Delegate = _activeDelegate;
                Present(picker);
                tcs.TrySetResult(true);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "IOSDocumentPicker: 打开文件夹失败 {Path}", path);
                tcs.TrySetResult(false);
            }
        });
        return tcs.Task;
    }

    [SupportedOSPlatform("ios14.0")]
    private static UTType[] ToUttTypes(string[] filters)
    {
        // 历史背景：早期认为“扩展名过滤导致文件置灰”而改为放行所有文件（public.data）。
        // 后定位真正原因是 LiveContainer 下 UIDocumentPicker 的 XPC 交互问题（Fix File Picker 解决），
        // 与过滤无关。但 Fix File Picker 开启后恢复精确过滤尚未验证，这里保持放行，避免回归。
        return new[] { UTTypes.Data };
    }

    private static void Present(UIViewController picker)
    {
        UIViewController? root = GetRootViewController();
        if (root == null)
        {
            throw new InvalidOperationException("IOSDocumentPicker: 找不到 RootViewController");
        }
        // 如果当前已有一个弹出的控制器，在其上 present（避免重复弹窗叠加）
        while (root.PresentedViewController != null)
        {
            root = root.PresentedViewController;
        }
        root.PresentViewController(picker, true, null);
    }

    private static UIViewController? GetRootViewController()
    {
        foreach (UIWindowScene scene in UIApplication.SharedApplication.ConnectedScenes)
        {
            foreach (UIWindow window in scene.Windows)
            {
                if (window.RootViewController != null)
                {
                    return window.RootViewController;
                }
            }
        }
        return null;
    }

    private sealed class PickDelegate : UIDocumentPickerDelegate
    {
        private readonly Action<NSUrl[]?> _handler;
        internal PickDelegate(Action<NSUrl[]?> handler) => _handler = handler;

        public override void WasCancelled(UIDocumentPickerViewController controller)
            => _handler(null);

        public override void DidPickDocument(UIDocumentPickerViewController controller, NSUrl[] urls)
            => _handler(urls);

        public override void DidPickDocument(UIDocumentPickerViewController controller, NSUrl url)
            => _handler(new[] { url });
    }
}
