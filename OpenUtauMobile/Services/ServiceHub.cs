using System;
using Avalonia.Media;
using OpenUtauMobile.Storage;
using OpenUtauMobile.Themes.OpenUtauMobile.Runtime;

namespace OpenUtauMobile.Services;

/// <summary>
/// 跨平台能力抽象层
/// </summary>
public static class ServiceHub
{
    public static Action? InitAudioOutput { get; set; }
    public static IExternalStorageService? ExternalStorageService { get; set; }
    public static ISystemAccentColorProvider? SystemAccentColorProvider { get; set; }
    public static Func<(bool success, Color color, string source)>? TryGetPlatformAccentFallback { get; set; }

    // iOS：系统文件选择器返回的路径在沙盒外（security-scoped），
    // 需要拷贝进沙盒或授权访问后才能被 OpenUtau 正常读写。
    /// <summary>把外部文件拷贝进沙盒，返回沙盒内路径（iOS）。</summary>
    public static Func<string, string>? ImportFileToSandbox { get; set; }
    /// <summary>把外部文件夹拷贝进沙盒，返回沙盒内路径（iOS）。</summary>
    public static Func<string, string>? ImportFolderToSandbox { get; set; }
    /// <summary>为保存目标路径开启访问授权，返回可写入的路径（iOS）。</summary>
    public static Func<string, string>? PrepareSaveDestination { get; set; }
    /// <summary>释放保存路径的访问授权（iOS）。</summary>
    public static Action<string>? ReleaseSaveDestination { get; set; }
}