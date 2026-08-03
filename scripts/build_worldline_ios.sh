#!/bin/bash
# Build worldline (OpenUtau C++ resampler) as a static library for iOS arm64.
# Produces: $OUT_DIR/libworldline.a
#
# Dependencies fetched from upstream repos (same revisions as openutau/OpenUtau
# WORKSPACE.bazel), with the same patches applied.
set -euo pipefail

SDK=$(xcrun --sdk iphoneos --show-sdk-path)
CLANG=$(xcrun --sdk iphoneos --find clang)
CLANGXX=$(xcrun --sdk iphoneos --find clang++)
MIN_IOS=13.0

ARCH=arm64
CFLAGS="-arch $ARCH -isysroot $SDK -miphoneos-version-min=$MIN_IOS -O2 -fPIC"
CXXFLAGS="$CFLAGS -std=c++17 -fvisibility=hidden -Wno-unused-function"

WORK=/tmp/worldline-ios
OUT_DIR=${OUT_DIR:-$WORK/out}
rm -rf "$WORK"
mkdir -p "$WORK/src" "$WORK/obj" "$OUT_DIR"

echo "== downloading dependencies =="
# WORLD vocoder (mmorise/World)
curl -sL -o /tmp/world.zip https://github.com/mmorise/World/archive/f8dd5fb289db6a7f7f704497752bf32b258f9151.zip
unzip -q /tmp/world.zip -d "$WORK/deps"
WORLD_ROOT="$WORK/deps/World-f8dd5fb289db6a7f7f704497752bf32b258f9151"
WORLD_SRC="$WORLD_ROOT/src"
WORLD_TOOLS="$WORLD_ROOT/tools"

# spline (ttk592/spline)
curl -sL -o /tmp/spline.zip https://github.com/ttk592/spline/archive/5894beaf91e9adbfdbe5c6c9a1c60770e380e8e8.zip
unzip -q /tmp/spline.zip -d "$WORK/deps"
SPLINE_SRC="$WORK/deps/spline-5894beaf91e9adbfdbe5c6c9a1c60770e380e8e8/src"

# libpyin (Sleepwalking/libpyin)
curl -sL -o /tmp/pyin.zip https://github.com/Sleepwalking/libpyin/archive/b38135390b335c3e8cea6ef35cf5093789b36dac.zip
unzip -q /tmp/pyin.zip -d "$WORK/deps"
PYIN_SRC="$WORK/deps/libpyin-b38135390b335c3e8cea6ef35cf5093789b36dac"

# libgvps (Sleepwalking/libgvps)
curl -sL -o /tmp/gvps.zip https://github.com/Sleepwalking/libgvps/archive/2f1b4106d72f8f8138dc447bf0123820c0772cbd.zip
unzip -q /tmp/gvps.zip -d "$WORK/deps"
GVPS_SRC="$WORK/deps/libgvps-2f1b4106d72f8f8138dc447bf0123820c0772cbd"

# libnpy (llohse/libnpy, header-only)
curl -sL -o /tmp/npy.zip https://github.com/llohse/libnpy/archive/refs/tags/v1.0.1.zip
unzip -q /tmp/npy.zip -d "$WORK/deps"
NPY_INC="$WORK/deps/libnpy-1.0.1/include"

echo "== applying upstream patches =="
# Patches come from openutau/OpenUtau cpp/third_party (fetched by the workflow)
PATCH_DIR=${PATCH_DIR:-/tmp/patches}
if [ -f "$PATCH_DIR/world.patch" ]; then
  (cd "$WORK/deps/World-f8dd5fb289db6a7f7f704497752bf32b258f9151" && patch -p1 < "$PATCH_DIR/world.patch")
fi
if [ -f "$PATCH_DIR/libpyin.patch" ]; then
  (cd "$PYIN_SRC" && patch -p1 < "$PATCH_DIR/libpyin.patch")
fi
if [ -f "$PATCH_DIR/spline.patch" ]; then
  (cd "$WORK/deps/spline-5894beaf91e9adbfdbe5c6c9a1c60770e380e8e8" && patch -p1 < "$PATCH_DIR/spline.patch")
fi

# libpyin includes <libgvps/gvps.h>; expose gvps root under libgvps/ name
ln -sfn "$GVPS_SRC" "$WORK/deps/libgvps"

echo "== preparing worldline sources =="
# worldline sources come from the checked-out OpenUtauMobile repo sibling 'worldline-src'
# (we copy them in the workflow before calling this script)
WORLDLINE_SRC=${WORLDLINE_SRC:-/tmp/worldline-src}
if [ ! -d "$WORLDLINE_SRC" ]; then
  echo "ERROR: worldline sources not found at $WORLDLINE_SRC"
  exit 1
fi
cp -R "$WORLDLINE_SRC" "$WORK/src/worldline"

# Patch classic_args.cpp: replace absl::SimpleAtoi/SimpleAtod with std equivalents
# (avoids cross-compiling the whole abseil-cpp dependency)
ABS_PATCH="$WORK/absl_compat.h"
cat > "$ABS_PATCH" <<'EOF'
#pragma once
#include <cstdlib>
#include <string_view>
namespace absl {
inline bool SimpleAtoi(std::string_view s, int* out) {
  if (s.empty()) return false;
  std::string tmp(s);
  char* end = nullptr;
  long v = std::strtol(tmp.c_str(), &end, 10);
  if (end == tmp.c_str() || *end != '\0') return false;
  *out = (int)v;
  return true;
}
inline bool SimpleAtod(std::string_view s, double* out) {
  if (s.empty()) return false;
  std::string tmp(s);
  char* end = nullptr;
  double v = std::strtod(tmp.c_str(), &end);
  if (end == tmp.c_str() || *end != '\0') return false;
  *out = v;
  return true;
}
}
EOF
sed -i '' 's|#include "absl/strings/numbers.h"|#include "absl_compat.h"|' "$WORK/src/worldline/classic/classic_args.cpp"

echo "== compiling =="
OBJS=()

compile_cpp() { # src
  local src="$1"; local name
  name=$(echo "$src" | md5 -q | cut -c1-10)
  "$CLANGXX" $CXXFLAGS -DFP_TYPE=double \
    -I"$WORK/src" \
    -I"$WORLD_SRC" \
    -I"$WORLD_TOOLS" \
    -I"$SPLINE_SRC" \
    -I"$PYIN_SRC" \
    -I"$WORK/deps" \
    -I"$NPY_INC" \
    -I"$WORK" \
    -c "$src" -o "$WORK/obj/$name.o"
  OBJS+=("$WORK/obj/$name.o")
}

compile_c() { # src, extra defines
  local src="$1"; local defs="$2"; local name
  name=$(echo "$src" | md5 -q | cut -c1-10)
  "$CLANG" $CFLAGS $defs \
    -I"$WORLD_SRC" \
    -I"$PYIN_SRC" \
    -I"$WORK/deps" \
    -c "$src" -o "$WORK/obj/$name.o"
  OBJS+=("$WORK/obj/$name.o")
}

# worldline core
for f in "$WORK/src/worldline"/worldline.cpp "$WORK/src/worldline"/phrase_synth.cpp \
         "$WORK/src/worldline"/classic/*.cpp "$WORK/src/worldline"/common/*.cpp \
         "$WORK/src/worldline"/f0/*.cpp "$WORK/src/worldline"/model/*.cpp \
         "$WORK/src/worldline"/platinum/*.cpp; do
  case "$f" in
    *_test.cpp|*worldline_main.cpp|*audio_debug.cpp|*audio_output.cpp) continue ;;
  esac
  compile_cpp "$f"
done

# WORLD vocoder
for f in "$WORLD_SRC"/*.cpp "$WORLD_TOOLS"/audioio.cpp; do
  compile_cpp "$f"
done

# spline (header-only, nothing to compile)

# libpyin + libgvps (C)
for f in "$PYIN_SRC"/*.c; do
  compile_c "$f" "-DFP_TYPE=double"
done
for f in "$GVPS_SRC"/*.c; do
  compile_c "$f" "-DFP_TYPE=double"
done

echo "== archiving =="
ar rcs "$OUT_DIR/libworldline.a" "${OBJS[@]}"
echo "== done =="
ls -lh "$OUT_DIR/libworldline.a"
