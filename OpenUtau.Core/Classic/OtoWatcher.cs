using System;
using System.IO;
using OpenUtau.Core;
using Serilog;

namespace OpenUtau.Classic {
    class OtoWatcher : IDisposable {
        public bool Paused { get; set; }

        private ClassicSinger singer;
        private FileSystemWatcher watcher;

        public OtoWatcher(ClassicSinger singer, string path) {
            this.singer = singer;
            // iOS does not support FileSystemWatcher; oto.ini hot-reload is unavailable there.
            if (OS.IsIOS()) {
                Log.Warning("FileSystemWatcher is not supported on iOS, oto.ini hot-reload disabled");
                return;
            }
            watcher = new FileSystemWatcher(path);
            watcher.Changed += OnFileChanged;
            watcher.Created += OnFileChanged;
            watcher.Deleted += OnFileChanged;
            watcher.Renamed += OnFileChanged;
            watcher.Error += OnError;
            watcher.Filter = "oto.ini";
            watcher.IncludeSubdirectories = true;
            watcher.EnableRaisingEvents = true;
        }

        private void OnFileChanged(object sender, FileSystemEventArgs e) {
            if (Paused) {
                return;
            }
            Log.Information($"File \"{e.FullPath}\" {e.ChangeType}");
            SingerManager.Inst.ScheduleReload(singer);
        }

        private void OnError(object sender, ErrorEventArgs e) {
            Log.Error($"Watcher error {e}");
        }

        public void Dispose() {
            watcher?.Dispose();
        }
    }
}
