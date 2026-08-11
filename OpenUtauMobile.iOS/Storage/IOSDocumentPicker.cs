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
    public static Task<string> SaveFileAsync(string title, string extension, string defaultFileName)
    {
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

    [SupportedOSPlatform("ios14.0")]
    private static UTType[] ToUttTypes(string[] filters)
    {
        if (filters == null || filters.Length == 0)
        {
            return new[] { UTTypes.Data };
        }
        UTType[] types = filters
            .Select(f => f.TrimStart('*', '.'))
            .Where(ext => ext.Length > 0)
            .Select(UTType.CreateFromExtension)
            .Where(t => t != null)
            .Cast<UTType>()
            .ToArray();
        return types.Length > 0 ? types : new[] { UTTypes.Data };
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
