using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using XrayUI.Helpers;
using XrayUI.Models;

namespace XrayUI.Services
{
    public partial class XrayService
    {
        // Public so helper cores (e.g. RealLatencyProbeService's throwaway speed-test core) can
        // launch the same engine without duplicating the path.
        public static readonly string ExePath = Path.Combine(
            AppContext.BaseDirectory, "Assets", "engine", "xray.exe");

        public static readonly string RulesDir = Path.Combine(
            AppContext.BaseDirectory, "Assets", "rules");

        private static readonly string ConfigPath = Path.Combine(
            AppPaths.DataDir, "xray_config.json");

        private const int LogBufferMax = 500;

        // Upper bound on waiting for the readiness line of a freshly launched core (see
        // XrayReadySignal). Normally Ready lands in tens of ms; this cap only bites when
        // the core stays silent, where it reproduces the old fixed-delay behavior.
        private static readonly TimeSpan StartupReadyCap = TimeSpan.FromSeconds(3);

        private Process? _process;
        private readonly Lock _processStateLock = new();
        private string _sessionId = string.Empty;

        // Kernel-level safety net: xray.exe is assigned to a kill-on-close Job Object, so if the
        // UI process dies any way (taskkill /F, AV crash, OOM) the kernel kills xray.exe too.
        // Created lazily on first start and reused across Start/Stop cycles.
        private JobObjectGuard? _jobGuard;

        private StringBuilder _startupLog = new();
        private bool _collectStartupLog;
        private readonly Lock _startupLogLock = new();

        // Fixed-size ring buffer: O(1) append + oldest-drop, no array shifting.
        private readonly string[] _logBuffer = new string[LogBufferMax];
        private int _logHead;    // index of the next write slot
        private int _logCount;   // number of valid entries (<= LogBufferMax)
        private readonly Lock _bufferLock = new();

        public ProxyTelemetry Telemetry { get; } = new();
        public XrayStatsService Stats { get; } = new();
        public int StatsApiPort { get; private set; }

        public bool IsRunning => _process is { HasExited: false };

        public string LastError { get; private set; } = string.Empty;

        /// <summary>
        /// Route-test API of the running core, or null when nothing is running or the config
        /// exposes none. Set before RunningChanged(true) fires and cleared before (false) does.
        /// </summary>
        public RouteTestEndpoint? RouteTest { get; private set; }

        public event EventHandler<string>? LogReceived;

        public event EventHandler<bool>? RunningChanged;

        public IReadOnlyList<string> GetLogBuffer()
        {
            lock (_bufferLock)
            {
                if (_logCount == 0)
                {
                    return Array.Empty<string>();
                }

                var snapshot = new string[_logCount];
                if (_logCount < LogBufferMax)
                {
                    // Not yet wrapped — data is contiguous in [0, _logCount)
                    Array.Copy(_logBuffer, 0, snapshot, 0, _logCount);
                }
                else
                {
                    // Wrapped — oldest at _logHead, newest at _logHead-1
                    int tailCount = LogBufferMax - _logHead;
                    Array.Copy(_logBuffer, _logHead, snapshot, 0, tailCount);
                    Array.Copy(_logBuffer, 0, snapshot, tailCount, _logHead);
                }
                return snapshot;
            }
        }

        public void ClearLogBuffer()
        {
            lock (_bufferLock)
            {
                Array.Clear(_logBuffer, 0, _logBuffer.Length);
                _logHead = 0;
                _logCount = 0;
            }
        }

        private void AppendLog(string line)
        {
            lock (_bufferLock)
            {
                _logBuffer[_logHead] = line;
                _logHead = (_logHead + 1) % LogBufferMax;
                if (_logCount < LogBufferMax)
                {
                    _logCount++;
                }
            }

            Telemetry.Observe(line);
            LogReceived?.Invoke(this, line);
        }

        private void BeginStartupLogCapture()
        {
            lock (_startupLogLock)
            {
                _startupLog = new StringBuilder();
                _collectStartupLog = true;
            }
        }

        private void AppendStartupLog(string line)
        {
            lock (_startupLogLock)
            {
                if (!_collectStartupLog)
                {
                    return;
                }

                _startupLog.AppendLine(line);
            }
        }

        private string StopStartupLogCaptureAndRead()
        {
            lock (_startupLogLock)
            {
                _collectStartupLog = false;
                var text = _startupLog.Length > 0
                    ? _startupLog.ToString().Trim()
                    : string.Empty;
                _startupLog = new StringBuilder();
                return text;
            }
        }

        private void StopStartupLogCapture()
        {
            lock (_startupLogLock)
            {
                _collectStartupLog = false;
                _startupLog = new StringBuilder();
            }
        }

        public async Task<bool> StartAsync(string configJson, int statsApiPort = 0)
        {
            if (_process is not null)
            {
                // Restart path (e.g. ReapplyRoutingAsync): skip the DNS flush — the new xray
                // session is about to repopulate the resolver cache anyway, and flushing
                // adds avoidable latency to every routing/DNS/proxy-mode toggle.
                await StopCoreAsync();
            }

            RouteTest = null;
            LastError = string.Empty;
            Telemetry.Reset();
            Stats.Reset();
            StatsApiPort = statsApiPort;
            _sessionId = Guid.NewGuid().ToString("N");
            DiagnosticLogService.Write("core", $"session={_sessionId} start requested");

            if (!File.Exists(ExePath))
            {
                LastError = Loc.Format("Xray_ExeNotFound", ExePath);
                AppendLog(Loc.Format("XrayLog_Error", LastError));
                return false;
            }

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
                await File.WriteAllTextAsync(ConfigPath, configJson);

                var psi = new ProcessStartInfo
                {
                    FileName = ExePath,
                    Arguments = $"run -config \"{ConfigPath}\"",
                    WorkingDirectory = Path.GetDirectoryName(ExePath)!,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                psi.EnvironmentVariables["XRAY_LOCATION_ASSET"] = RulesDir;

                var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
                _process = process;
                var readySignal = XrayReadySignal.Attach(process);

                process.OutputDataReceived += (_, e) =>
                {
                    if (e.Data is null) return;
                    AppendStartupLog(e.Data);
                    AppendLog(e.Data);
                };

                process.ErrorDataReceived += (_, e) =>
                {
                    if (e.Data is null) return;
                    AppendStartupLog(e.Data);
                    AppendLog(e.Data);
                };

                process.Exited += OnProcessExited;

                BeginStartupLogCapture();
                process.Start();
                AttachToJobObject(process);
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                AppendLog(Loc.Format("XrayLog_Started", ExePath));
                AppendLog(Loc.Format("XrayLog_Config", ConfigPath));
                DiagnosticLogService.Write("core", $"session={_sessionId} started");

                // Ready typically lands well under 100ms; Exited surfaces bad configs / port
                // clashes / TUN elevation failures immediately. A quiet, living process is not
                // considered ready: verify that this configuration's mixed listener accepts a
                // loopback TCP connection before exposing it to the system proxy.
                var readyStopwatch = Stopwatch.StartNew();
                var outcome = await readySignal.WaitAsync(StartupReadyCap);
                readyStopwatch.Stop();

                // Diagnostic for "occasionally slow start": logs how long the readiness wait
                // actually took and how it resolved.
                var readyLabel = outcome switch
                {
                    XrayReadySignal.Outcome.Ready => "Ready",
                    XrayReadySignal.Outcome.Exited => "Exited",
                    _ => "TimedOut"
                };
                AppendLog($"[XrayUI] core readiness: {readyLabel} in {readyStopwatch.ElapsedMilliseconds} ms");
                DiagnosticLogService.Write("core", $"session={_sessionId} readiness={readyLabel} elapsedMs={readyStopwatch.ElapsedMilliseconds}");

                if (outcome == XrayReadySignal.Outcome.Exited || process.HasExited)
                {
                    // Process output is delivered through asynchronous DataReceived
                    // callbacks. WaitForExit() (unlike only checking HasExited) also
                    // drains the redirected stdout/stderr pipes before we snapshot the
                    // startup log; otherwise users only see the generic exit code.
                    try { process.WaitForExit(); } catch { }
                    var startupLog = StopStartupLogCaptureAndRead();
                    LastError = startupLog.Length > 0
                        ? startupLog
                        : Loc.Format("Xray_ExitedImmediately", process.ExitCode);
                    AppendLog(Loc.Format("XrayLog_StartFailed", LastError));
                    DiagnosticLogService.Write("core", $"session={_sessionId} startup failed exitCode={process.ExitCode}");
                    await StopCoreAsync();
                    return false;
                }

                if (outcome == XrayReadySignal.Outcome.TimedOut
                    && !await VerifyMixedInboundReadyAsync(configJson))
                {
                    LastError = "Xray 核心未在限定时间内监听 mixed 入站。";
                    AppendLog(Loc.Format("XrayLog_StartFailed", LastError));
                    DiagnosticLogService.Write("core", $"session={_sessionId} readiness listener check failed");
                    await StopCoreAsync();
                    return false;
                }

                StopStartupLogCapture();
                var routeTest = RouteTestProtocol.ReadEndpointFromConfig(configJson);
                lock (_processStateLock)
                {
                    // Serialize publication with the exit callback. A core that exited during
                    // the readiness wait must not leave a stale endpoint or a late true event.
                    if (!ReferenceEquals(process, _process) || process.HasExited)
                    {
                        LastError = "Xray core exited before startup completed.";
                        return false;
                    }
                    RouteTest = routeTest;
                    RunningChanged?.Invoke(this, true);
                }
                return true;
            }
            catch (Exception ex)
            {
                StopStartupLogCapture();
                LastError = ex.Message;
                AppendLog(Loc.Format("XrayLog_Exception", ex.Message));
                await StopCoreAsync();
                return false;
            }
        }

        public async Task StopAsync()
        {
            await StopCoreAsync();
            FlushSystemDnsCache();
        }

        /// <summary>
        /// Best-effort recovery for a core that outlived the UI process which launched it.
        /// Only a process that both owns the requested local TCP listener and resolves to this
        /// portable application's bundled xray.exe is eligible. Any uncertainty leaves the
        /// process untouched and lets the caller retain its normal port-conflict prompt.
        /// </summary>
        public async Task<bool> TryRecoverOrphanedCoreOnPortAsync(int port)
        {
            var listenerProcessIds = PortHelper.GetTcpListenerProcessIds(port);
            if (listenerProcessIds.Count == 0)
            {
                return false;
            }

            var recoveredAny = false;
            foreach (var processId in listenerProcessIds)
            {
                if (_process is { Id: var trackedProcessId } && trackedProcessId == processId)
                {
                    await StopCoreAsync();
                    recoveredAny = true;
                    continue;
                }

                Process? process = null;
                try
                {
                    process = Process.GetProcessById(processId);
                    var executablePath = process.MainModule?.FileName;
                    if (!PortHelper.PathsAreEqual(executablePath, ExePath))
                    {
                        continue;
                    }

                    process.Kill(entireProcessTree: true);
                    var exitTask = process.WaitForExitAsync();
                    if (await Task.WhenAny(exitTask, Task.Delay(TimeSpan.FromSeconds(2))) == exitTask)
                    {
                        recoveredAny = true;
                        AppendLog($"[XrayUI] recovered orphaned core PID {processId} on local port {port}");
                    }
                }
                catch (Exception ex) when (
                    ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
                {
                    AppendLog($"[XrayUI] could not recover listener PID {processId}: {ex.Message}");
                }
                finally
                {
                    process?.Dispose();
                }
            }

            return recoveredAny;
        }

        /// <summary>
        /// Kills the xray process and tears down state, without flushing the OS DNS cache.
        /// Used by StartAsync on the restart path so reapply doesn't pay DNS flush latency
        /// on every routing/DNS/proxy-mode toggle. No-op if not running.
        /// </summary>
        private async Task StopCoreAsync()
        {
            Process? process;
            lock (_processStateLock)
            {
                process = _process;
                _process = null;
                RouteTest = null;
            }
            if (process is null)
            {
                return;
            }

            process.Exited -= OnProcessExited;

            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }

                var exited = process.WaitForExitAsync();
                await Task.WhenAny(exited, Task.Delay(TimeSpan.FromSeconds(3)));
            }
            catch
            {
            }
            finally
            {
                process.Dispose();
            }

            AppendLog(L.XrayLog_Stopped);
            DiagnosticLogService.Write("core", $"session={_sessionId} stopped");
            RunningChanged?.Invoke(this, false);
        }

        private static async Task<bool> VerifyMixedInboundReadyAsync(string configJson)
        {
            try
            {
                var root = JsonNode.Parse(configJson) as JsonObject;
                var inbound = root?["inbounds"]?.AsArray().FirstOrDefault(node =>
                    string.Equals(node?["tag"]?.GetValue<string>(), XrayConfigConstants.MixedInboundTag, StringComparison.Ordinal));
                if (inbound?["port"]?.GetValue<int>() is not int port || port is < 1 or > 65535)
                    return false;

                using var client = new TcpClient();
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(1));
                await client.ConnectAsync("127.0.0.1", port, timeout.Token);
                return client.Connected;
            }
            catch (Exception ex) when (ex is SocketException or OperationCanceledException or JsonException)
            {
                return false;
            }
        }

        /// <summary>
        /// Best-effort DNS resolver cache flush to clear any cached fake IPs (198.18.0.0/15) that
        /// might linger in the Windows resolver cache after a FakeDNS-enabled run. Harmless
        /// when FakeDNS was not used. Runs unconditionally on every stop to keep XrayService
        /// stateless w.r.t. last-run config.
        /// </summary>
        private void FlushSystemDnsCache()
        {
            try
            {
                if (!DnsFlushResolverCache())
                {
                    Debug.WriteLine($"[DNS] DnsFlushResolverCache failed: {Marshal.GetLastWin32Error()}");
                }
            }
            catch (Exception ex)
            {
                // best-effort, never block stop on this
                Debug.WriteLine($"[DNS] DnsFlushResolverCache exception: {ex.Message}");
            }
        }

        [DllImport("dnsapi.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DnsFlushResolverCache();

        public void StopForShutdown()
        {
            Process? process;
            lock (_processStateLock)
            {
                process = _process;
                _process = null;
                RouteTest = null;
            }
            if (process is null)
            {
                return;
            }

            process.Exited -= OnProcessExited;

            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(500);
                }
            }
            catch
            {
            }
            finally
            {
                process.Dispose();
            }

            AppendLog(L.XrayLog_Shutdown);
            RunningChanged?.Invoke(this, false);
        }

        private void OnProcessExited(object? sender, EventArgs e)
        {
            // Stop/reapply may already have installed a newer process by the time Windows
            // dispatches this event. An old exit must never turn the newer session off.
            lock (_processStateLock)
            {
                if (!ReferenceEquals(sender, _process)) return;
                RouteTest = null;
                AppendLog(L.XrayLog_ProcessExited);
                var exitCode = sender is Process process && process.HasExited ? process.ExitCode : -1;
                DiagnosticLogService.Write("core", $"session={_sessionId} unexpected exitCode={exitCode}");
                RunningChanged?.Invoke(this, false);
            }
        }

        // ─────────── Job Object: orphan-xray safety net ───────────
        // The kill-on-close Job Object interop lives in Helpers/JobObjectGuard (shared with the
        // real-delay speed-test core). The guard is created lazily and reused across Start/Stop
        // cycles; each launched process is assigned to it.

        private void AttachToJobObject(Process process)
        {
            try
            {
                _jobGuard ??= JobObjectGuard.Create();
                if (_jobGuard is null)
                {
                    AppendLog(Loc.Format("XrayLog_JobCreateFailed", Marshal.GetLastWin32Error()));
                    return;
                }

                if (!_jobGuard.TryAssign(process))
                {
                    AppendLog(Loc.Format("XrayLog_JobAssignFailed", Marshal.GetLastWin32Error()));
                }
            }
            catch (Exception ex)
            {
                AppendLog(Loc.Format("XrayLog_JobBindException", ex.Message));
            }
        }
    }
}
