using System.Diagnostics;
using System.Text;
using System.Text.Json;
using CliWrap;

namespace fuck {
    class Program {
        private static Config? _config;
        private static Dictionary<string, Command> _keepAliveCommands = new();
        private static Dictionary<string, CancellationTokenSource> _keepAliveCts = new();
        private static int mp2_found_hasAlreadyPrinted = -1;
        private static int mp1_found_hasAlreadyPrinted = -1;
        private static readonly string ConfigPath = "config.json";
        private static bool _programsStarted = false;

        static async Task Main(string[] args) {
            Console.OutputEncoding = Encoding.UTF8;
            Console.WriteLine();
            const string header =
                @" ███▄ ▄███▓  ▄████   ▄████  ▄▄▄       ▒█████   █     █░
▓██▒▀█▀ ██▒ ██▒ ▀█▒ ██▒ ▀█▒▒████▄    ▒██▒  ██▒▓█░ █ ░█░
▓██    ▓██░▒██░▄▄▄░▒██░▄▄▄░▒██  ▀█▄  ▒██░  ██▒▒█░ █ ░█ 
▒██    ▒██ ░▓█  ██▓░▓█  ██▓░██▄▄▄▄██ ▒██   ██░░█░ █ ░█ 
▒██▒   ░██▒░▒▓███▀▒░▒▓███▀▒ ▓█   ▓██▒░ ████▓▒░░░██▒██▓ 
░ ▒░   ░  ░ ░▒   ▒  ░▒   ▒  ▒▒   ▓▒█░░ ▒░▒░▒░ ░ ▓░▒ ▒  
░  ░      ░  ░   ░   ░   ░   ▒   ▒▒ ░  ░ ▒ ▒░   ▒ ░ ░  
░      ░   ░ ░   ░ ░ ░   ░   ░   ▒   ░ ░ ░ ▒    ░   ░  
	   ░         ░       ░       ░  ░    ░ ░      ░    ";

            Console.ForegroundColor = ConsoleColor.DarkRed;
            Console.WriteLine(header);
            Console.ResetColor();

            loadConfig();

            if (_config == null) {
                Console.WriteLine("fix your config fucker. its fucked");
                return;
            }

            await monitorMain();
        }

        private static void loadConfig() {
            try {
                if (!File.Exists(ConfigPath)) {
                    Console.WriteLine("Config file not found. Creating default config.json...");
                    createDefaultConfig();
                    return;
                }

                string json = File.ReadAllText(ConfigPath);
                _config = JsonSerializer.Deserialize<Config>(json, JsonContext.Default.Config);

                if (_config == null) {
                    Console.WriteLine("fix your program fucker... deserializer returned null.");
                }

                Console.WriteLine("ready");
                Console.WriteLine();
            } catch (Exception e) {
                Console.WriteLine($"error loading config: {e.Message}");
                Console.WriteLine("creating default config");
                createDefaultConfig();
            }
        }

        private static void createDefaultConfig() {
            _config = new Config
            {
                delay = 1000,
                mainProgram = "main_program_to_watch.exe",
                mainProgram2 = "secondmain_program_to_watch.exe",
                startGate = "AND",
                exitGate = "AND",
                onStart = new List<string> { "program_to_start.exe" },
                keepAlive = new List<string> { "program_to_keep_alive.exe" },
                killOnExit = new List<string> { "program_to_kill.exe" }
            };

            string jsonString = JsonSerializer.Serialize(_config, JsonContext.Default.Config);
            File.WriteAllText(ConfigPath, jsonString);

            Console.WriteLine("created default config.json. edit it");
            Console.WriteLine("press any key to exit...");
            Console.ReadKey();
            Environment.Exit(0);
        }

        private static Process? findProcess(string processName) {
            return Process.GetProcessesByName(Path.GetFileNameWithoutExtension(processName)).FirstOrDefault();
        }

        private static bool shouldStartPrograms(Process? mainProcess, Process? mainProcess2) {
            if (_config == null) return false;

            bool mainProcessRunning = mainProcess != null && !mainProcess.HasExited;
            bool mainProcess2Running = mainProcess2 != null && !mainProcess2.HasExited;

            return _config.startGate.ToUpper() switch {
                "AND" => mainProcessRunning && mainProcess2Running,
                "OR" => mainProcessRunning || mainProcess2Running,
                _ => false
            };
        }

        private static bool shouldExitPrograms(Process? mainProcess, Process? mainProcess2) {
            if (_config == null) return false;

            bool mainProcessClosed = mainProcess == null || mainProcess.HasExited;
            bool mainProcess2Closed = mainProcess2 == null || mainProcess2.HasExited;

            return _config.exitGate.ToUpper() switch {
                "AND" => mainProcessClosed && mainProcess2Closed,
                "OR" => mainProcessClosed || mainProcess2Closed,
                _ => false
            };
        }

        private static async Task monitorMain() {
            using var cts = new CancellationTokenSource();

            Process? mainProcess = null;
            Process? mainProcess2 = null;

            while (!cts.Token.IsCancellationRequested) {
                if (_config == null) {
                    Console.WriteLine("fix your config fucker. (a)");
                    return;
                }

                try {
                    mainProcess = findProcess(_config.mainProgram);
                    mainProcess2 = findProcess(_config.mainProgram2);

                    if (mainProcess?.Id != mp1_found_hasAlreadyPrinted && mainProcess != null) {
                        Console.WriteLine($"found {_config.mainProgram} pid {mainProcess.Id}");
                        mp1_found_hasAlreadyPrinted = mainProcess.Id;
                    }
                    
                    if (mainProcess2?.Id != mp2_found_hasAlreadyPrinted && mainProcess2 != null) {
                        Console.WriteLine($"found {_config.mainProgram2} pid {mainProcess2.Id}");
                        mp2_found_hasAlreadyPrinted = mainProcess2.Id;
                    }

                    if (!_programsStarted && shouldStartPrograms(mainProcess, mainProcess2)) {
                        await runPrograms(_config.onStart, cts.Token);
                        await runAllKeepAlives(cts.Token);
                        _programsStarted = true;
                    }

                    if (_programsStarted && shouldExitPrograms(mainProcess, mainProcess2)) {
                        await killPrograms(_config.killOnExit);
                        Console.WriteLine();
                        Console.WriteLine($"exiting since ({_config.mainProgram} {_config.exitGate} {_config.mainProgram2}) has exited.");
                        Console.ReadKey();
                        break;
                    }

                    if (_programsStarted) {
                        foreach (var program in _config.keepAlive) {
                            var processName = Path.GetFileNameWithoutExtension(program);
                            var process = Process.GetProcessesByName(processName).FirstOrDefault();

                            if (process == null || process.HasExited) {
                                Console.WriteLine($"keep-alive process {program} is not running anymore");
                                if (_keepAliveCommands.ContainsKey(program)) {
                                    _keepAliveCts[program].Cancel();
                                    _keepAliveCommands.Remove(program);
                                    _keepAliveCts.Remove(program);
                                }
                                await runKeepAlive(program, cts.Token);
                            }
                        }
                    }

                    await Task.Delay(_config.delay, cts.Token);
                } catch (OperationCanceledException) {
                    break;
                } catch (Exception ex) {
                    Console.WriteLine($"Error: {ex.Message}");
                    await Task.Delay(1000, cts.Token);
                }
            }
        }

        private static async Task killPrograms(IEnumerable<string>? programs) {
            if (programs == null) return;

            foreach (var program in programs) {
                try {
                    var processName = Path.GetFileNameWithoutExtension(program);
                    var processes = Process.GetProcessesByName(processName);

                    foreach (var process in processes) {
                        process.Kill();
                        await process.WaitForExitAsync();
                        Console.WriteLine($"killed {processName} pid {process.Id}");
                    }
                } catch (Exception ex) {
                    Console.WriteLine($"failed to kill {program}: {ex.Message}");
                }
            }
        }

        private static async Task runPrograms(IEnumerable<string>? programs, CancellationToken cancellationToken) {
            if (programs == null) return;

            foreach (var program in programs) {
                try {
                    await Cli.Wrap(program).WithValidation(CommandResultValidation.None).ExecuteAsync(cancellationToken);
                } catch (Exception ex) {
                    Console.WriteLine($"failed to start {program}: {ex.Message}");
                }
            }
        }

        private static async Task runAllKeepAlives(CancellationToken cancellationToken) {
            if (_config == null) return;

            foreach (var program in _config.keepAlive) {
                if (_keepAliveCommands.ContainsKey(program)) continue;
                await runKeepAlive(program, cancellationToken);
            }
        }

        private static async Task runKeepAlive(string program, CancellationToken cancellationToken) {
            try {
                var cts = new CancellationTokenSource();
                _keepAliveCts[program] = cts;

                var cmd = Cli.Wrap(program).WithValidation(CommandResultValidation.None);
                _keepAliveCommands[program] = cmd;

                var task = cmd.ExecuteAsync(cts.Token);
                Console.WriteLine($"started keep-alive: {program} pid {task.ProcessId}");

                _ = Task.Run(async () => {
                    try {
                        await task;
                    } catch (OperationCanceledException) { } catch (Exception ex) {
                        Console.WriteLine($"keep-alive {program} exited: {ex.Message}");
                        _keepAliveCommands.Remove(program);
                        _keepAliveCts.Remove(program);
                        if (!cancellationToken.IsCancellationRequested) {
                            await runKeepAlive(program, cancellationToken);
                        }
                    }
                }, cancellationToken);
            } catch (Exception ex) {
                Console.WriteLine($"failed to start keep-alive {program}: {ex.Message}");
            }
        }
    }
}