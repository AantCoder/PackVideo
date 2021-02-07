using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace PackVideo
{
    class VideoConverter
    {
        public string FfmpegArgumentsResize = "-filter:v scale=\"iw/2:ih/2\" -vcodec h264 -crf 22 -acodec aac";
        public string FfmpegArguments = "-vcodec h264 -crf 22 -acodec aac";
        public string OutFileExtension = ".avi";
        public string TempWorkFile = "TempFile.avi";

        public string Status = null;
        public string Log = null;
        public string ProgressText = null;
        public int ProgressValue;
        public Action<bool> UpdateStatus;

        private readonly string ConsoleConverter;

        private bool DeleteSource = false;
        private bool AutoResize = false;
        private long SizeAllSource;
        private List<VideoFile> Files = null;
        private int IndexWork = 0;
        private DateTime StartWork;
        private DateTime StartWorkFile;
        private int MinutesLeft;
        private int StatusLastIndexWork = 0;
        private int StatusPrecent = 0;
        private string StatusTime = null;
        private StreamReader WorkOutput = null;
        private StreamReader WorkError = null;
        private Process WorkProcess = null;

        public VideoConverter()
        {
            ConsoleConverter = Path.Combine(Path.GetDirectoryName(Environment.GetCommandLineArgs()[0]), "ffmpeg.exe");
        }

        public bool GetFiles(string folder)
        {
            var files = Directory.GetFiles(folder, "*.mp4", SearchOption.AllDirectories);
            Files = files
                .Where(fn => !File.Exists(Path.Combine(Path.GetDirectoryName(fn), Path.GetFileNameWithoutExtension(fn) + OutFileExtension)))
                .Select(fn => new VideoFile()
                {
                    FileNameSource = fn,
                    SizeSource = new FileInfo(fn).Length,
                    FileNameFine = Path.Combine(Path.GetDirectoryName(fn), Path.GetFileNameWithoutExtension(fn) + OutFileExtension)
                })
                .ToList();

            if (Files.Count == 0)
            {
                Status = "Файлы для сжатия не найдены";
                Log = "";
                return false;
            }
            SizeAllSource = Files.Sum(vf => vf.SizeSource);
            Status = $"Всего файлов {Files.Count} размером {SizeAllSource / 1024 / 1024} Mb";
            Log = Status + Environment.NewLine
                + "Файлы к сжатию:"
                + Files.Aggregate("", (r, i) => r + Environment.NewLine + i.FileNameSource);
            return true;
        }

        public void Start(bool deleteSource, bool autoResize)
        {
            if (!File.Exists(ConsoleConverter))
            {
                Status = "Не найден файл " + ConsoleConverter;
                return;
            }

            DeleteSource = deleteSource;
            AutoResize = autoResize;
            StartWork = DateTime.Now;

            var th = new Thread(StreamReadErrorDo);
            th.IsBackground = true;
            th.Start();

            th = new Thread(StreamReadOutputDo);
            th.IsBackground = true;
            th.Start();

            th = new Thread(WorkDo);
            th.IsBackground = true;
            th.Start();
        }

        private void WorkDo()
        {
            try
            {
                while (IndexWork < Files.Count)
                {
                    SetStatus(true);
                    var vf = Files[IndexWork];

                    var vi = GetVideoInfo(vf.FileNameSource);

                    if (vi.Width > 0 && vi.Height > 0)
                    {
                        string arguments = AutoResize
                            && (vi.Width > 1100 || vi.Height > 1100)
                            ? FfmpegArgumentsResize
                            : FfmpegArguments;

                        var workFile = Path.Combine(Path.GetDirectoryName(vf.FileNameSource), TempWorkFile);
                        if (File.Exists(workFile)) File.Delete(workFile);

                        StartWorkFile = DateTime.Now;

                        var psi = new ProcessStartInfo()
                        {
                            FileName = ConsoleConverter,
                            Arguments = $"-y -i \"{vf.FileNameSource}\" " + arguments + $" \"{workFile}\" ",
                            WindowStyle = ProcessWindowStyle.Hidden,
                            CreateNoWindow = true,
                            RedirectStandardError = true,
                            RedirectStandardOutput = true,
                            UseShellExecute = false
                        };
                        WorkProcess = Process.Start(psi);
                        WorkProcess.ErrorDataReceived += RemoteErrorDataReceived;
                        WorkOutput = WorkProcess.StandardOutput;
                        WorkError = WorkProcess.StandardError;
                        WorkProcess.WaitForExit();
                        WorkProcess = null;

                        StartWorkFile = DateTime.MinValue;

                        var rfi = new FileInfo(workFile);
                        if (!rfi.Exists)
                        {
                            Status = "Ошибка: нет файла результата";
                            AddLog(Status);
                            SetStatus(false, true);
                            return;
                        }
                        vf.SizeFine = rfi.Length;
                        if (File.Exists(vf.FileNameFine)) File.Delete(vf.FileNameFine);

                        File.Move(workFile, vf.FileNameFine);

                        if (DeleteSource && File.Exists(vf.FileNameSource)) File.Delete(vf.FileNameSource);
                    }
                    else
                        vf.SizeFine = vf.SizeSource;

                    IndexWork++;
                }
                var sizeFine = Files.Sum(vf => vf.SizeFine) / 1024 / 1024;
                Status = $"Успешно завершено {Files.Count} файлов. Теперь видео весит {sizeFine} Mb, уменьшилось на {SizeAllSource / 1024 / 1024 - sizeFine} Mb ";
                AddLog(Status);
                SetStatus(false);
            }
            catch (Exception e)
            {
                Status = "Ошибка " + e.Message;
                AddLog(Status);
                SetStatus(false, true);
            }
        }

        private void StreamReadOutputDo()
        {
            try
            {
                string read = "";
                while (true)
                {
                    if (WorkOutput != null && !WorkOutput.EndOfStream)
                    {
                        var c = WorkOutput.Read();

                        if (c != 13 && c != 10)
                            read += (char)c;
                        else if (read.Length > 0)
                        {
                            AddLog(read);
                            SetStatus(true);
                            read = "";
                        }
                    }
                    else if (read.Length > 0)
                    {
                        AddLog(read);
                        SetStatus(true);
                        read = "";
                    }
                }
            }
            catch (Exception e)
            {
                Status = "Ошибка чтения Output: " + e.Message;
                AddLog(Status);
                SetStatus(false, true);
            }
        }

        private void StreamReadErrorDo()
        {
            try
            {
                string read = "";
                while (true)
                {
                    if (WorkError != null && !WorkError.EndOfStream)
                    {
                        var c = WorkError.Read();

                        if (c != 13 && c != 10)
                            read += (char)c;
                        else if (read.Length > 0)
                        {
                            AddLog(read);
                            SetStatus(true);
                            read = "";
                        }
                    }
                    else if (read.Length > 0)
                    {
                        AddLog(read);
                        SetStatus(true);
                        read = "";
                    }
                }
            }
            catch (Exception e)
            {
                Status = "Ошибка чтения Error: " + e.Message;
                AddLog(Status);
                SetStatus(false, true);
            }
        }

        private void SetStatus(bool progress, bool showError = false)
        {
            if (progress && Files.Count > 0 && IndexWork >= 0 && IndexWork < Files.Count)
            {
                if (StatusLastIndexWork != IndexWork)
                {
                    StatusLastIndexWork = IndexWork;

                    var sizeDone = Files.Where(vf => vf.SizeFine > 0).Sum(vf => vf.SizeSource);
                    var pr = (double)sizeDone / (double)SizeAllSource;
                    var startWorkFile = StartWorkFile != DateTime.MinValue ? StartWorkFile : DateTime.Now;
                    var m = (startWorkFile - StartWork).TotalSeconds;
                    MinutesLeft = (int)(m / pr - m);

                    StatusPrecent = (int)(100 * pr);
                }
                var time = MinutesLeft;
                if (StartWorkFile != DateTime.MinValue)
                {
                    var mFile = (DateTime.Now - StartWorkFile).TotalSeconds;
                    time -= (int)mFile;
                }
                if (time >= 0) StatusTime = $"Осталось " + (time / 60 / 60).ToString("00") + ":" + (time / 60 % 60).ToString("00") + ":" + (time % 60).ToString("00") + " ";

                Status = StatusTime + $"Обрабатывается файл {IndexWork + 1} из {Files.Count}: {Files[IndexWork].FileNameSource} {Files[IndexWork].SizeSource} Mb ";
                ProgressText = $"{IndexWork + 1} из {Files.Count} " + StatusTime;
            }
            ProgressValue = IndexWork == Files.Count ? 100 : StatusPrecent;
            UpdateStatus(showError);
        }

        protected void RemoteErrorDataReceived(Object sender, DataReceivedEventArgs e)
        {
            if (String.IsNullOrEmpty(e.Data)) return;
            Status = "Ошибка обработки: " + e.Data;
            AddLog(Status);
            SetStatus(false);
        }

        private void AddLog(string msg)
        {
            lock (this)
            {
                var log = msg + Environment.NewLine + Log;
                if (log.Length > 10000) Log = log.Substring(0, 10000);
                else Log = log;
            }
        }

        public void Stop()
        {
            try
            {
                if (WorkProcess != null) WorkProcess.Kill();
            }
            catch
            { }
        }

        public class VideoInfo
        {
            public int Width;
            public int Height;
        }

        public VideoInfo GetVideoInfo(string fileName)
        {
            var vi = new VideoInfo();
            try
            {
                var psi = new ProcessStartInfo()
                {
                    FileName = ConsoleConverter,
                    Arguments = $" -i \"{fileName}\"",
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false
                };
                var workProcess = Process.Start(psi);
                var workOutput = workProcess.StandardOutput;
                var workError = workProcess.StandardError;
                workProcess.WaitForExit();
                workError.BaseStream.Flush();
                var msg = workError.ReadToEnd();

                var streams = msg.Split(new string[] { "Stream #" }, StringSplitOptions.None);
                int i;
                for (i = 1; i < streams.Length; i++)
                {
                    int p = streams[i].IndexOf(" Video: ");
                    if (p >= 0) break;
                }
                if (i == streams.Length) return vi;
                var stream = streams[i];

                for (i = 2; i < stream.Length - 1; i++)
                {
                    if (char.IsDigit(stream[i - 2])
                        && char.IsDigit(stream[i - 1])
                        && stream[i] == 'x'
                        && char.IsDigit(stream[i + 1])
                        ) break;
                }
                if (i == stream.Length - 1) return vi;
                var posX = i;

                var posB = stream.Substring(0, posX).LastIndexOf(' ');
                var posE = stream.IndexOf(' ', posX);
                var posE2 = stream.IndexOf(',', posX);
                if (posE2 >= 0 && posE2 < posE) posE = posE2;

                var width = int.Parse(stream.Substring(posB + 1, posX - posB - 1));
                var height = int.Parse(stream.Substring(posX + 1, posE - posX - 1));

                vi.Width = width;
                vi.Height = height;
            }
            catch
            { }
            return vi;
        }

    }
}
