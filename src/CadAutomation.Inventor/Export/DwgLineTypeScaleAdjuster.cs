using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace CadAutomation.Inventor.Export
{
    /// <summary>
    /// Inventor DataIO, bend layer çizgi tipini DWG'ye yazar fakat global çizgi tipi ölçeği
    /// için bir API parametresi sunmaz. AutoCAD Core Console ile tüm batch'i tek oturumda açıp
    /// LTSCALE değerini ayarlar; böylece yaklaşık 1 mm'lik kesikler büyük parçada düz görünmez.
    /// </summary>
    internal static class DwgLineTypeScaleAdjuster
    {
        private const int TimeoutMilliseconds = 300000;
        private const int FileReadyTimeoutMilliseconds = 10000;

        public static void Apply(IReadOnlyCollection<string> dwgFiles, double lineTypeScale)
        {
            if (dwgFiles == null) throw new ArgumentNullException(nameof(dwgFiles));
            if (lineTypeScale <= 0) throw new ArgumentOutOfRangeException(nameof(lineTypeScale));

            var files = dwgFiles
                .Where(File.Exists)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (files.Count == 0) return;

            var coreConsole = FindAutoCadCoreConsole();
            var tempFolder = Path.Combine(Path.GetTempPath(), "CadAutomation", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempFolder);

            try
            {
                foreach (var file in files)
                {
                    WaitUntilWritable(file, FileReadyTimeoutMilliseconds);
                }

                var scriptPath = Path.Combine(tempFolder, "apply-ltscale.scr");
                var resultPath = Path.Combine(tempFolder, "apply-ltscale.result");
                // GetShortPathName yalnızca mevcut dosyalarda güvenilir sonuç verir.
                File.WriteAllText(resultPath, string.Empty, Encoding.ASCII);
                File.WriteAllText(scriptPath, BuildScript(files, lineTypeScale, resultPath), Encoding.ASCII);
                var shortScriptPathWithoutExtension = Path.Combine(
                    Path.GetDirectoryName(ToShortPath(scriptPath)) ?? tempFolder,
                    Path.GetFileNameWithoutExtension(ToShortPath(scriptPath)));

                var startInfo = new ProcessStartInfo
                {
                    FileName = coreConsole,
                    // Core Console /s değerine otomatik olarak .scr ekliyor. Bu nedenle Türkçe
                    // karakter içermeyen 8.3 tam yolu uzantısız veriyoruz; aksi halde NAME.SCR.scr
                    // arayıp komut dosyasını sessizce atlayabiliyor.
                    Arguments = $"/i \"{ToShortPath(files[0])}\" /s \"{shortScriptPathWithoutExtension}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    // Kullanıcı adındaki Türkçe karakterler Core Console'un çalışma klasörünü
                    // bozabildiği için mevcut klasörün 8.3 yolunu kullanıyoruz.
                    WorkingDirectory = ToShortPath(tempFolder),
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };

                using (var process = Process.Start(startInfo))
                {
                    if (process == null)
                        throw new InvalidOperationException("AutoCAD Core Console başlatılamadı.");

                    var standardOutput = new StringBuilder();
                    var standardError = new StringBuilder();
                    process.OutputDataReceived += (_, eventArgs) =>
                    {
                        if (eventArgs.Data != null) standardOutput.AppendLine(eventArgs.Data);
                    };
                    process.ErrorDataReceived += (_, eventArgs) =>
                    {
                        if (eventArgs.Data != null) standardError.AppendLine(eventArgs.Data);
                    };
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    if (!process.WaitForExit(TimeoutMilliseconds))
                    {
                        process.Kill();
                        throw new TimeoutException("DWG çizgi ölçeği işlemi zaman aşımına uğradı.");
                    }

                    process.WaitForExit();

                    if (process.ExitCode != 0)
                        throw new InvalidOperationException(
                            $"AutoCAD Core Console hata kodu: {process.ExitCode}. " +
                            BuildDiagnosticMessage(standardOutput, standardError));

                    VerifyResult(resultPath, files.Count, lineTypeScale, standardOutput, standardError);
                }
            }
            finally
            {
                try { Directory.Delete(tempFolder, recursive: true); }
                catch (Exception) { }
            }
        }

        private static string BuildScript(IReadOnlyList<string> files, double lineTypeScale, string resultPath)
        {
            var scale = lineTypeScale.ToString("0.########", CultureInfo.InvariantCulture);
            var script = new StringBuilder();
            var escapedResultPath = EscapeLispString(ToShortPath(resultPath));

            script.AppendLine($"(setq ca-result (open \"{escapedResultPath}\" \"w\"))");

            for (var index = 0; index < files.Count; index++)
            {
                if (index > 0)
                {
                    script.AppendLine("_.OPEN");
                    script.AppendLine($"\"{EscapeScriptPath(ToShortPath(files[index]))}\"");
                }

                script.AppendLine($"(setvar \"LTSCALE\" {scale})");
                script.AppendLine("(setvar \"CELTSCALE\" 1.0)");
                script.AppendLine("(setvar \"MSLTSCALE\" 0)");
                script.AppendLine("(setvar \"PSLTSCALE\" 0)");
                script.AppendLine("_.QSAVE");
                script.AppendLine("(write-line (strcat (getvar \"DWGNAME\") \"|\" (rtos (getvar \"LTSCALE\") 2 8)) ca-result)");
            }

            script.AppendLine("(close ca-result)");
            script.AppendLine("_.QUIT");
            return script.ToString();
        }

        private static void WaitUntilWritable(string filePath, int timeoutMilliseconds)
        {
            var stopwatch = Stopwatch.StartNew();
            Exception? lastException = null;

            while (stopwatch.ElapsedMilliseconds < timeoutMilliseconds)
            {
                try
                {
                    using (new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    {
                        return;
                    }
                }
                catch (IOException ex)
                {
                    lastException = ex;
                    Thread.Sleep(200);
                }
                catch (UnauthorizedAccessException ex)
                {
                    lastException = ex;
                    Thread.Sleep(200);
                }
            }

            throw new IOException($"DWG dosyası kullanıma hazır değil: {filePath}", lastException);
        }

        private static void VerifyResult(
            string resultPath,
            int expectedFileCount,
            double expectedScale,
            StringBuilder standardOutput,
            StringBuilder standardError)
        {
            if (!File.Exists(resultPath))
            {
                throw new InvalidOperationException(
                    "AutoCAD çizgi ölçeği komut dosyasını çalıştırmadı. " +
                    BuildDiagnosticMessage(standardOutput, standardError));
            }

            var lines = File.ReadAllLines(resultPath)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToArray();
            if (lines.Length != expectedFileCount)
            {
                throw new InvalidOperationException(
                    $"Çizgi ölçeği yalnızca {lines.Length}/{expectedFileCount} DWG dosyasına uygulandı. " +
                    BuildDiagnosticMessage(standardOutput, standardError));
            }

            foreach (var line in lines)
            {
                var separator = line.LastIndexOf('|');
                double actualScale;
                if (separator < 0 ||
                    !double.TryParse(
                        line.Substring(separator + 1),
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out actualScale) ||
                    Math.Abs(actualScale - expectedScale) > 0.0000001)
                {
                    throw new InvalidOperationException(
                        "AutoCAD çizgi ölçeği doğrulaması başarısız: " + line + ". " +
                        BuildDiagnosticMessage(standardOutput, standardError));
                }
            }
        }

        private static string BuildDiagnosticMessage(StringBuilder standardOutput, StringBuilder standardError)
        {
            var combined = (standardError + Environment.NewLine + standardOutput)
                .Replace("\0", string.Empty)
                .Trim();
            if (combined.Length > 1000) combined = combined.Substring(combined.Length - 1000);
            return string.IsNullOrWhiteSpace(combined) ? "AutoCAD ayrıntılı çıktı üretmedi." : combined;
        }

        private static string FindAutoCadCoreConsole()
        {
            var autodeskFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Autodesk");
            if (!Directory.Exists(autodeskFolder))
                throw new FileNotFoundException("Autodesk program klasörü bulunamadı.");

            var executable = Directory.GetDirectories(autodeskFolder, "AutoCAD *", SearchOption.TopDirectoryOnly)
                .OrderByDescending(path => path, StringComparer.OrdinalIgnoreCase)
                .Select(path => Path.Combine(path, "accoreconsole.exe"))
                .FirstOrDefault(File.Exists);

            if (executable == null)
                throw new FileNotFoundException("AutoCAD Core Console (accoreconsole.exe) bulunamadı.");

            return executable;
        }

        private static string EscapeScriptPath(string path) => path.Replace('\\', '/').Replace("\"", "\"\"");

        private static string EscapeLispString(string path) =>
            path.Replace("\\", "/").Replace("\"", "\\\"");

        private static string ToShortPath(string path)
        {
            var buffer = new StringBuilder(1024);
            var length = GetShortPathName(path, buffer, buffer.Capacity);
            return length > 0 && length < buffer.Capacity ? buffer.ToString() : path;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int GetShortPathName(string longPath, StringBuilder shortPath, int bufferLength);
    }
}
