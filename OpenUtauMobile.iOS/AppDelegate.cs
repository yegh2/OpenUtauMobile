using System;
using System.Text;
using System.Threading.Tasks;
using Foundation;
using Avalonia;
using Avalonia.Media;
using Avalonia.iOS;
using ReactiveUI.Avalonia;
using OpenUtau.Core;
using OpenUtauMobile.Services;
using Serilog;
using UIKit;
using OpenUtau.Audio;


namespace OpenUtauMobile.iOS;

// The UIApplicationDelegate for the application. This class is responsible for launching the 
// User Interface of the application, as well as listening (and optionally responding) to 
// application events from iOS.
[Register("AppDelegate")]
#pragma warning disable CA1711 // Identifiers should not have incorrect suffix
public partial class AppDelegate : AvaloniaAppDelegate<App>
#pragma warning restore CA1711 // Identifiers should not have incorrect suffix
{
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); // 注册编码提供程序以支持更多编码格式
        InitLogging();
        ServiceHub.InitAudioOutput = InitAudioOutput; // iOS: AVAudioEngine 音频后端
        ServiceHub.TryGetPlatformAccentFallback = TryGetPlatformAccentFallback;
        // iOS: 系统文件选择器（security-scoped URL）的沙盒导入/保存授权
        ServiceHub.ImportFileToSandbox = Storage.IOSFileAccess.ImportFileToSandbox;
        ServiceHub.ImportFolderToSandbox = Storage.IOSFileAccess.ImportFolderToSandbox;
        ServiceHub.PrepareSaveDestination = Storage.IOSFileAccess.PrepareSavePath;
        ServiceHub.ReleaseSaveDestination = Storage.IOSFileAccess.ReleaseSavePath;
        // iOS: 原生 UIDocumentPicker（Avalonia iOS storage provider 不稳定）
        ServiceHub.PickFileAsync = Storage.IOSDocumentPicker.PickFileAsync;
        ServiceHub.PickFolderAsync = Storage.IOSDocumentPicker.PickFolderAsync;
        ServiceHub.SaveFileAsync = Storage.IOSDocumentPicker.SaveFileAsync;
        // iOS: 系统分享面板（导出日志等）
        ServiceHub.ShareFile = ShareFileAsync;
        return base.CustomizeAppBuilder(builder)
            .UseReactiveUI(_ =>
            {
            });
    }

    /// <summary>分享单个文件：弹出 iOS 系统分享面板。</summary>
    private static Task<bool> ShareFileAsync(string path)
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        DispatchQueue.MainQueue.DispatchAsync(() =>
        {
            try
            {
                UIViewController? root = GetRootViewController();
                if (root == null)
                {
                    Log.Warning("ShareFile: 找不到 RootViewController");
                    tcs.TrySetResult(false);
                    return;
                }

                NSUrl url = NSUrl.FromFilename(path);
                var activity = new UIActivityViewController(new NSObject[] { url }, null);

                // iPad 需要 popover 锚点，否则崩溃
                UIPopoverPresentationController? popover = activity.PopoverPresentationController;
                if (popover != null)
                {
                    popover.SourceView = root.View;
                    popover.SourceRect = new CGRect(root.View.Bounds.GetMidX(), root.View.Bounds.GetMidY(), 0, 0);
                    popover.PermittedArrowDirections = UIPopoverArrowDirection.Any;
                }

                activity.CompletionWithItemsHandler = (_, _, _, _) => tcs.TrySetResult(true);
                root.PresentViewController(activity, true, null);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "ShareFile: 打开分享面板失败 {Path}", path);
                tcs.TrySetResult(false);
            }
        });
        return tcs.Task;
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

    private static void InitAudioOutput()
    {
        string pref = OpenUtau.Core.Util.Preferences.Default.AudioBackend;
        Log.Information("初始化音频输出，偏好后端: {Backend}", string.IsNullOrEmpty(pref) ? "Auto" : pref);
        try
        {
            PlaybackManager.Inst.AudioOutput = new Audio.IOSAudioOutput();
            Log.Information("使用 AVAudioEngine 音频后端");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "IOSAudioOutput 初始化失败，回退到 Dummy");
            PlaybackManager.Inst.AudioOutput = new DummyAudioOutput();
        }
    }

    private static void InitLogging()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Debug()
            .WriteTo.Logger(lc => lc
                .MinimumLevel.Information() // 
                .WriteTo.File(PathManager.Inst.LogFilePath, rollingInterval: RollingInterval.Day, encoding: Encoding.UTF8)) // 写入日志文件
            .CreateLogger();
        AppDomain.CurrentDomain.UnhandledException += (_, args) => {
            Log.Error((Exception)args.ExceptionObject, "未经处理的异常！"); // 未处理异常
            DocManager.Inst.ExecuteCmd(new ErrorMessageNotification((Exception)args.ExceptionObject));
        };
        TaskScheduler.UnobservedTaskException += (_, args) => {
            Log.Error(args.Exception, "未观察到的 Task 异常！"); // 未观察到的 Task 异常
            DocManager.Inst.ExecuteCmd(new ErrorMessageNotification(args.Exception));
            args.SetObserved();
        };
        Log.Information("==========开始记录日志==========");
    }

    private static (bool success, Color color, string source) TryGetPlatformAccentFallback()
    {
        UIColor accent = UIColor.SystemBlue;
        accent.GetRGBA(out nfloat r, out nfloat g, out nfloat b, out _);
        Color color = Color.FromRgb(
            (byte)Math.Clamp((int)(r * 255), 0, 255),
            (byte)Math.Clamp((int)(g * 255), 0, 255),
            (byte)Math.Clamp((int)(b * 255), 0, 255));
        return (true, color, "iOS.UIKit.SystemBlue");
    }
}
