# OpenUtau Mobile（iOS）

[English](README.md) | [简体中文](README_zh.md)

## 特别感谢

- [OpenUtau](https://github.com/stakira/OpenUtau)
- [vocoder712/OpenUtauMobile](https://github.com/vocoder712/OpenUtauMobile)（上游项目）

## OpenUtau Mobile 是什么？

OpenUtau Mobile 是一个面向移动端的开源免费歌声合成软件。

本仓库是 [vocoder712/OpenUtauMobile](https://github.com/vocoder712/OpenUtauMobile) 的 **iOS 专向 fork**。它是一个基于 [OpenUtau 内核](https://github.com/stakira/OpenUtau/tree/master/OpenUtau.Core) 并进行了一些修补的编辑器，完全支持 OpenUtau 的 USTX 工程文件。

第二代重写基于：

- Avalonia 12.1
- .NET 10
- MVVM 架构（ReactiveUI）

## 兼容性

### 运行平台

- iOS（构建为未签名 IPA，需自行签名安装）

### 歌手类型

- DiffSinger
- UTAU
- Vogen

其他未测试的类型不能保证正常工作。

## 快速开始

1. 在 [Release](https://github.com/yegh2/OpenUtauMobile/releases) 页面下载未签名 IPA（资源名为 `OpenUtauMobile-unsigned.ipa`），自行签名安装到 iPhone。若尚未发布 Release，可从最新一次 [Actions](https://github.com/yegh2/OpenUtauMobile/actions/workflows/build-unsigned-ipa.yml) 构建的 artifact（`OpenUtauMobile-unsigned-ipa`）中获取。
2. 下载一个声库。通常你可以在 [DiffSinger 自制声库分享页面](https://docs.qq.com/sheet/DQXNDY0pPaEpOc3JN?tab=BB08J2) 或 [UTAU wiki](https://utau.fandom.com/) 找到下载地址。声库通常以 zip 格式打包。
3. 打开软件 → 点击首页的 `歌手` 按钮 → 点击右下角 `+` → 选择上一步下载的声库安装包，然后按照指引安装。
4. 返回首页，点击 `新建` 进入编辑器，开始你的创作吧！

你也可以在首页的 `打开` 直接找到并打开 OpenUtau 的 USTX 工程文件。

> [!WARNING]
> 软件仍在密集开发中，可能不稳定。受限于开发框架，内存占用可能较高，**记得随时保存**。如果崩溃，可以在工程文件同目录找到以 `.autosave.ustx` 结尾的文件恢复。

## 自行构建与贡献

如果你想让这个项目变得更好：

- 如果你发现了 BUG 或者有想实现的功能，或者对操作逻辑 / UI 有好的建议，欢迎在 [Issues](https://github.com/yegh2/OpenUtauMobile/issues) 提出 BUG，在 [Discussions](https://github.com/yegh2/OpenUtauMobile/discussions) 提建议。

- **贡献代码：** 克隆本仓库到本地后，使用 Visual Studio 或 JetBrains Rider 打开项目根目录的 `OpenUtauMobile.sln` 即可进入开发环境。建议新建分支操作。完成后向 `dev` 分支发起 Pull Request。

## iOS 构建指南

由于 Apple 的政策限制，iOS 版本无法直接发布安装包，需要自行签名。

### 从 Release 获取 IPA（推荐）

1. 推送到 `dev` 分支——[Build Unsigned IPA](https://github.com/yegh2/OpenUtauMobile/actions/workflows/build-unsigned-ipa.yml) 工作流会自动在 macOS 上构建。
2. 在 [Release](https://github.com/yegh2/OpenUtauMobile/releases) 页面下载 `OpenUtauMobile-unsigned.ipa` 资源。若尚未发布 Release，可从最新一次工作流运行的 artifact（`OpenUtauMobile-unsigned-ipa`）中获取。
3. 使用你自己的证书签名安装（免费 Apple ID 即可用于个人设备）。

### 在 macOS 上本地构建

#### 环境要求

- macOS（必须）
- .NET 10 SDK
- Xcode 15 或更高版本
- Apple 开发者账号（免费账号即可用于个人设备）
- iOS 工作负载

#### 安装开发环境

```bash
# 安装 .NET 10 SDK（如果尚未安装）
brew install dotnet-sdk

# 安装 iOS 工作负载
dotnet workload install ios
```

#### 构建步骤

1. **克隆项目**
   ```bash
   git clone https://github.com/yegh2/OpenUtauMobile.git
   cd OpenUtauMobile
   git checkout dev
   ```

2. **还原依赖（仅 iOS）**
   ```bash
   dotnet restore OpenUtauMobile.iOS/OpenUtauMobile.iOS.csproj -p:TargetFramework=net10.0-ios
   ```

3. **发布 IPA**
   ```bash
   dotnet publish OpenUtauMobile.iOS/OpenUtauMobile.iOS.csproj \
       -f net10.0-ios \
       -c Debug \
       -p:EnableCodeSigning=false \
       -p:RuntimeIdentifier=ios-arm64
   ```

   输出目录中会生成未签名的 `.app`，可打包为 IPA：

   ```bash
   mkdir -p Payload && cp -R <输出目录>/*.app Payload/ && zip -r OpenUtauMobile-unsigned.ipa Payload
   ```

### 安装到 iPhone

1. **使用 USB 连接 iPhone 到 Mac**

2. **查看设备 ID**
   ```bash
   xcrun devicectl list devices
   ```

3. **安装应用**
   ```bash
   xcrun devicectl device install app \
       --device <你的设备ID> \
       <已签名的.ipa路径>
   ```

4. **信任开发者证书**

   首次安装后，在 iPhone 上前往：
   **设置 → 通用 → VPN 与设备管理**，找到开发者证书并点击信任。

### 常见问题

**Q: 构建时间很长怎么办？**

A: Debug 模式构建通常需要 5-15 分钟。如果构建 Release 版本（启用 AOT），可能需要 20-40 分钟。建议日常开发使用 Debug 模式。

**Q: 证书过期了怎么办？**

A: 免费开发者证书有效期为 7 天，过期后需要重新签名安装。付费开发者账号（$99/年）证书有效期为 1 年。

**Q: 可以不用 Mac 构建吗？**

A: 不可以。Apple 要求 iOS 应用必须在 macOS 上使用 Xcode 工具链构建。

## 报告问题

报告 BUG 时，请提供：

- 设备型号
- iOS 版本
- 应用版本
- 复现步骤
- 截图或屏幕录制
- 日志（如有）

## 开源协议

本项目采用 [Apache 2.0](./LICENSE) 许可证开源。

不是官方 OpenUtau，不得冒充官方 OpenUtau。
