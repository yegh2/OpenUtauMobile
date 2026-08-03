# OpenUtau Mobile（iOS 分支）

[English](README.md) | [简体中文](README_zh.md)

OpenUtau Mobile 是一款面向移动端的开源免费歌声合成软件，基于 [OpenUtau 内核](https://github.com/stakira/OpenUtau/tree/master/OpenUtau.Core)，完全支持 OpenUtau 的 USTX 工程文件以及常见声库格式（UTAU / DiffSinger / Vogen）。

本分支专注于 **iOS 平台**。

## Fork 声明

本仓库是 [vocoder712/OpenUtauMobile](https://github.com/vocoder712/OpenUtauMobile) 的 **fork**，上游项目基于 [OpenUtau](https://github.com/stakira/OpenUtau)（MIT 协议）开发。

项目的一般功能、使用说明、声库安装方法及已知限制，请参阅[上游项目 README](https://github.com/vocoder712/OpenUtauMobile#readme)。

## 本分支的改动

- **iOS：worldline 合成器** — 将 worldline C++ 合成器（含 WORLD / libpyin / libgvps / spline 依赖）交叉编译为 iOS arm64 静态库并链接进应用。此前 iOS 版本完全没有可用的合成器（`DllNotFoundException: worldline`）。
- **iOS：音频输出** — 使用 AVAudioEngine 实现了真正的音频后端（此前 iOS 无音频输出）。
- **iOS：稳定性修复** — 修复 iOS 不支持的 `FileSystemWatcher`、Preferences 中 `Process.MainModule` 崩溃、FilePicker 中多余花括号导致的编译错误。
- **GitHub Actions 自动构建** — 新增 `build-unsigned-ipa.yml` 工作流，自动构建未签名 IPA（包含 worldline 交叉编译步骤）。

## iOS 构建

项目目标框架为 `net10.0-ios`。可通过 GitHub Actions 工作流自动构建未签名 IPA：

1. 打开本仓库的 **Actions** 标签页。
2. 选择 **Build Unsigned IPA (OpenUtauMobile)** → **Run workflow**。
3. 下载 `OpenUtauMobile-unsigned-ipa` 产物。

该 IPA 未签名，需要自行签名后安装到设备（例如个人开发者证书、AltStore 等）。

## 开源许可

本项目基于 [Apache License 2.0](./LICENSE.txt) 开源。

本项目**不是**官方 OpenUtau 应用，不得冒充官方 OpenUtau。第三方声明见 [NOTICE](./NOTICE)。

## 特别感谢

- [OpenUtau](https://github.com/stakira/OpenUtau) — 原始桌面应用
- [OpenUtauMobile (vocoder712)](https://github.com/vocoder712/OpenUtauMobile) — 上游移动端项目
