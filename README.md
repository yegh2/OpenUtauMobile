# OpenUtau Mobile (iOS)

[English](README.md) | [简体中文](README_zh.md)

## Special Thanks

- [OpenUtau](https://github.com/stakira/OpenUtau)
- [vocoder712/OpenUtauMobile](https://github.com/vocoder712/OpenUtauMobile) (upstream)

## What is OpenUtau Mobile?

OpenUtau Mobile is an open-source, free singing synthesis software for mobile devices.

This repository is an **iOS-focused fork** of [vocoder712/OpenUtauMobile](https://github.com/vocoder712/OpenUtauMobile). It is an editor based on the [OpenUtau Core](https://github.com/stakira/OpenUtau/tree/master/OpenUtau.Core) with some patches applied. It fully supports OpenUtau USTX project files.

The second-generation rewrite is based on:

- Avalonia 12.1
- .NET 10
- MVVM architecture (ReactiveUI)

## Compatibility

### Platforms

- iOS (built as an unsigned IPA, requires self-signing)

### Singer Types

- DiffSinger
- UTAU
- Vogen

Other untested types are not guaranteed to work correctly.

## Quick Start

1. Download the unsigned IPA from the latest [Actions](https://github.com/yegh2/OpenUtauMobile/actions/workflows/build-unsigned-ipa.yml) run (artifact `OpenUtauMobile-unsigned-ipa`), then sign and install it on your iPhone.
2. Download a voicebank. You can usually find download links on the [DiffSinger Custom Voicebank Share Page](https://docs.qq.com/sheet/DQXNDY0pPaEpOc3JN?tab=BB08J2) or the [UTAU wiki](https://utau.fandom.com/). Voicebanks are usually packaged in ZIP format.
3. Open the software → Tap the `Singer` button on the home page → Tap `+` → Select the voicebank package (ZIP) downloaded in the previous step, and follow the instructions to install.
4. Return to the home page, tap `New` to enter the editor, and start creating!

You can also use the `Open` button on the home page to directly find and open OpenUtau USTX project files.

> [!WARNING]
> The software is still in heavy development and may be unstable. Due to framework limitations, memory usage can be high — **remember to save often**. If the app crashes, you can find a recovery file ending in `.autosave.ustx` in the same directory as your project file.

## Building & Contributing

If you want to help improve this project:

- If you find a bug, have a feature request, or have a suggestion for the UI/UX, feel free to report it in [Issues](https://github.com/yegh2/OpenUtauMobile/issues) or discuss suggestions in [Discussions](https://github.com/yegh2/OpenUtauMobile/discussions).

- **Contributing Code:** Clone this repository locally, then open `OpenUtauMobile.sln` in the project root with Visual Studio / JetBrains Rider. It is recommended to create a new branch for your changes. Once completed, submit a Pull Request to the `dev` branch.

## iOS Build Guide

Due to Apple's policy restrictions, the iOS version cannot be distributed as a pre-built package and must be signed by yourself.

### Getting the IPA from CI (recommended)

1. Push to the `dev` branch — the [Build Unsigned IPA](https://github.com/yegh2/OpenUtauMobile/actions/workflows/build-unsigned-ipa.yml) workflow runs automatically on macOS.
2. When the run finishes, download the `OpenUtauMobile-unsigned-ipa` artifact and extract the `.ipa` inside.
3. Sign and install the IPA with your own certificate (free Apple ID works for personal devices).

### Building locally on macOS

#### Requirements

- macOS (required)
- .NET 10 SDK
- Xcode 15 or later
- Apple Developer Account (free account works for personal devices)
- iOS workload

#### Setting Up the Development Environment

```bash
# Install .NET 10 SDK (if not already installed)
brew install dotnet-sdk

# Install the iOS workload
dotnet workload install ios
```

#### Build Steps

1. **Clone the repository**
   ```bash
   git clone https://github.com/yegh2/OpenUtauMobile.git
   cd OpenUtauMobile
   git checkout dev
   ```

2. **Restore dependencies (iOS only)**
   ```bash
   dotnet restore OpenUtauMobile.iOS/OpenUtauMobile.iOS.csproj -p:TargetFramework=net10.0-ios
   ```

3. **Publish the IPA**
   ```bash
   dotnet publish OpenUtauMobile.iOS/OpenUtauMobile.iOS.csproj \
       -f net10.0-ios \
       -c Debug \
       -p:EnableCodeSigning=false \
       -p:RuntimeIdentifier=ios-arm64
   ```

   The unsigned `.app` is produced in the output directory; package it into an IPA with:
   ```bash
   mkdir -p Payload && cp -R <output>/*.app Payload/ && zip -r OpenUtauMobile-unsigned.ipa Payload
   ```

### Installing on iPhone

1. **Connect your iPhone to Mac via USB**

2. **Find your device ID**
   ```bash
   xcrun devicectl list devices
   ```

3. **Install the app**
   ```bash
   xcrun devicectl device install app \
       --device <your-device-id> \
       <path-to-your-signed.ipa>
   ```

4. **Trust the developer certificate**

   After the first installation, go to your iPhone:
   **Settings → General → VPN & Device Management**, find the developer certificate and tap Trust.

### FAQ

**Q: The build takes too long, what can I do?**

A: Debug mode builds typically take 5-15 minutes. Release builds (with AOT enabled) may take 20-40 minutes. It's recommended to use Debug mode for daily development.

**Q: What if the certificate expires?**

A: Free developer certificates are valid for 7 days. After expiration, you need to re-sign and reinstall. A paid developer account ($99/year) provides certificates valid for 1 year.

**Q: Can I build without a Mac?**

A: No. Apple requires iOS apps to be built on macOS using the Xcode toolchain.

## Reporting Issues

When reporting bugs, please provide:

- Device model
- iOS version
- App version
- Reproduction steps
- Screenshots or screen recordings
- Logs if available

## License

This project is licensed under the [Apache 2.0](./LICENSE) license.

This is NOT the official OpenUtau application and must not impersonate the official OpenUtau.
