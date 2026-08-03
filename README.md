# OpenUtau Mobile (iOS Fork)

[English](README.md) | [简体中文](README_zh.md)

OpenUtau Mobile is a free and open-source singing voice synthesis software for mobile devices, based on the [OpenUtau Core](https://github.com/stakira/OpenUtau/tree/master/OpenUtau.Core). It fully supports OpenUtau USTX project files and common voicebank formats (UTAU / DiffSinger / Vogen).

This fork focuses on the **iOS platform**.

## Fork Notice

This repository is a **fork of [vocoder712/OpenUtauMobile](https://github.com/vocoder712/OpenUtauMobile)**, which itself is derived from [OpenUtau](https://github.com/stakira/OpenUtau) (MIT licensed).

For general features, usage instructions, voicebank installation, and known limitations, please see the [upstream project's README](https://github.com/vocoder712/OpenUtauMobile#readme).

## Changes in this fork

- **iOS: worldline resampler** — cross-compiled the worldline C++ resampler (with WORLD / libpyin / libgvps / spline) as a static library for iOS arm64 and linked it into the app. Previously iOS builds had no working resampler at all (`DllNotFoundException: worldline`).
- **iOS: audio output** — implemented a real audio backend using AVAudioEngine (previously iOS had no audio output).
- **iOS: stability fixes** — `FileSystemWatcher` (unsupported on iOS), `Process.MainModule` crash in Preferences, and a stray-brace compile error in FilePicker.
- **GitHub Actions workflow** — automated build of an unsigned IPA (`build-unsigned-ipa.yml`), including the worldline cross-compilation step.

## iOS Build

The project targets `net10.0-ios`. You can build the unsigned IPA via the GitHub Actions workflow:

1. Go to the **Actions** tab of this repository.
2. Select **Build Unsigned IPA (OpenUtauMobile)** → **Run workflow**.
3. Download the `OpenUtauMobile-unsigned-ipa` artifact.

The IPA is unsigned — install it on a device with your own signing setup (e.g. personal developer certificate, AltStore, or similar).

## License

This project is licensed under the [Apache License 2.0](./LICENSE.txt).

This is **NOT** the official OpenUtau application and must not impersonate the official OpenUtau. See [NOTICE](./NOTICE) for third-party notices.

## Special Thanks

- [OpenUtau](https://github.com/stakira/OpenUtau) — the original desktop application
- [OpenUtauMobile (vocoder712)](https://github.com/vocoder712/OpenUtauMobile) — the upstream mobile project
