using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Foundation;
using OpenUtau.Core;
using Serilog;

namespace OpenUtauMobile.iOS.Storage;

/// <summary>
/// iOS 沙盒外的文件访问辅助。
/// 系统文件选择器（UIDocumentPicker）返回的是 security-scoped URL：
/// - 打开文件/文件夹：拷贝进沙盒（Documents/OpenUtau/Imported），保证后续可读且跨会话可用；
/// - 保存文件：开启 security-scoped 授权后返回原路径，写入完成后调用 <see cref="ReleaseSavePath"/> 释放。
/// </summary>
public static class IOSFileAccess
{
    private static readonly ConcurrentDictionary<string, NSUrl> ActiveSaveScopes = new();

    private static string? ContainerRoot { get; } = GetContainerRoot();

    private static string? GetContainerRoot()
    {
        try
        {
            // 沙盒容器根 = Documents 的上级目录
            NSUrl documents = NSFileManager.DefaultManager.GetUrl(
                NSSearchPathDirectory.DocumentDirectory,
                NSSearchPathDomain.User, null, true, out NSError? error);
            if (error != null || documents?.Path is not { } docPath)
            {
                return null;
            }
            return Path.GetDirectoryName(docPath);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "IOSFileAccess: 获取容器路径失败");
            return null;
        }
    }

    private static bool IsInsideContainer(string path)
    {
        return ContainerRoot != null
               && path.StartsWith(ContainerRoot, StringComparison.Ordinal);
    }

    /// <summary>
    /// 等待文件/目录真正落盘。LiveContainer 的 Fix File Picker 在回调返回后
    /// 才把选中文件异步复制到 File Provider Storage，立刻读会扑空（文件不存在）。
    /// </summary>
    private static bool WaitForPath(string path, bool isDirectory, int timeoutMs = 5000)
    {
        Func<bool> exists = isDirectory
            ? () => Directory.Exists(path)
            : () => File.Exists(path);
        if (exists())
        {
            return true;
        }
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            Thread.Sleep(100);
            if (exists())
            {
                Log.Information("IOSFileAccess: 等待 {Kind} 落盘完成 ({Elapsed}ms): {Path}",
                    isDirectory ? "目录" : "文件", sw.ElapsedMilliseconds, path);
                return true;
            }
        }
        Log.Warning("IOSFileAccess: 等待 {Kind} 落盘超时 ({Timeout}ms): {Path}",
            isDirectory ? "目录" : "文件", timeoutMs, path);
        return false;
    }

    private static NSUrl? StartScope(string path)
    {
        try
        {
            NSUrl url = NSUrl.FromFilename(path);
            return url.StartAccessingSecurityScopedResource() ? url : null;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "IOSFileAccess: StartAccessingSecurityScopedResource 失败 {Path}", path);
            return null;
        }
    }

    private static void StopScope(NSUrl? url)
    {
        try
        {
            url?.StopAccessingSecurityScopedResource();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "IOSFileAccess: StopAccessingSecurityScopedResource 失败");
        }
    }

    /// <summary>
    /// 把系统选择器选中的文件拷贝进沙盒，返回沙盒内路径。
    /// 已在沙盒内的路径原样返回。
    /// </summary>
    public static string ImportFileToSandbox(string externalPath)
    {
        try
        {
            if (string.IsNullOrEmpty(externalPath))
            {
                return string.Empty;
            }
            // Fix File Picker 异步复制，先等文件落盘再判存在
            if (!WaitForPath(externalPath, isDirectory: false))
            {
                Log.Warning("IOSFileAccess: 文件不存在 {Path}", externalPath);
                return string.Empty;
            }
            if (IsInsideContainer(externalPath))
            {
                return externalPath; // 已在沙盒内，无需拷贝
            }

            NSUrl? scope = StartScope(externalPath);
            try
            {
                string importDir = Path.Combine(PathManager.Inst.DataPath, "Imported");
                Directory.CreateDirectory(importDir);
                string dest = Path.Combine(importDir, $"{DateTime.Now:yyyyMMdd_HHmmss_fff}_{Path.GetFileName(externalPath)}");
                File.Copy(externalPath, dest, overwrite: true);
                Log.Information("IOSFileAccess: 已导入文件 {Src} -> {Dest}", externalPath, dest);
                return dest;
            }
            finally
            {
                StopScope(scope);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "IOSFileAccess: 导入文件失败 {Path}", externalPath);
            return string.Empty;
        }
    }

    /// <summary>
    /// 把系统选择器选中的文件夹（如音源目录）整体拷贝进沙盒，返回沙盒内路径。
    /// 已在沙盒内的路径原样返回。
    /// </summary>
    public static string ImportFolderToSandbox(string externalPath)
    {
        try
        {
            if (string.IsNullOrEmpty(externalPath))
            {
                return string.Empty;
            }
            // Fix File Picker 异步复制，先等目录落盘再判存在
            if (!WaitForPath(externalPath, isDirectory: true))
            {
                Log.Warning("IOSFileAccess: 目录不存在 {Path}", externalPath);
                return string.Empty;
            }
            if (IsInsideContainer(externalPath))
            {
                return externalPath;
            }

            NSUrl? scope = StartScope(externalPath);
            try
            {
                string importDir = Path.Combine(PathManager.Inst.DataPath, "Imported");
                Directory.CreateDirectory(importDir);
                string dest = Path.Combine(importDir, $"{DateTime.Now:yyyyMMdd_HHmmss_fff}_{Path.GetFileName(externalPath)}");
                if (NSFileManager.DefaultManager.Copy(
                        NSUrl.FromFilename(externalPath), NSUrl.FromFilename(dest), out NSError? error))
                {
                    Log.Information("IOSFileAccess: 已导入目录 {Src} -> {Dest}", externalPath, dest);
                    return dest;
                }
                Log.Error("IOSFileAccess: 拷贝目录失败 {Path}: {Error}",
                    externalPath, error?.LocalizedDescription ?? "unknown");
                return string.Empty;
            }
            finally
            {
                StopScope(scope);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "IOSFileAccess: 导入目录失败 {Path}", externalPath);
            return string.Empty;
        }
    }

    /// <summary>
    /// 为保存目标路径开启 security-scoped 授权，返回可写入的路径。
    /// 写入完成后必须调用 <see cref="ReleaseSavePath"/> 释放。
    /// </summary>
    public static string PrepareSavePath(string externalPath)
    {
        try
        {
            if (string.IsNullOrEmpty(externalPath))
            {
                return string.Empty;
            }
            if (IsInsideContainer(externalPath))
            {
                return externalPath; // 沙盒内路径可直接写
            }

            NSUrl? scope = StartScope(externalPath);
            if (scope == null)
            {
                Log.Warning("IOSFileAccess: 保存路径授权失败 {Path}", externalPath);
                return string.Empty;
            }
            ActiveSaveScopes[externalPath] = scope;
            Log.Information("IOSFileAccess: 保存路径已授权 {Path}", externalPath);
            return externalPath;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "IOSFileAccess: PrepareSavePath 失败 {Path}", externalPath);
            return string.Empty;
        }
    }

    /// <summary>
    /// 释放保存路径的 security-scoped 授权。
    /// </summary>
    public static void ReleaseSavePath(string path)
    {
        if (string.IsNullOrEmpty(path)) return;
        if (ActiveSaveScopes.TryRemove(path, out NSUrl? scope))
        {
            StopScope(scope);
            Log.Information("IOSFileAccess: 保存路径授权已释放 {Path}", path);
        }
    }
}
