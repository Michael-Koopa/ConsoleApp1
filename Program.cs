using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace fuck {
    class Program {
        private static Config? _config;
        private static Dictionary<string, Process> _keepAliveProcesses = new();
        private static int mp2_found_hasAlreadyPrinted = -1;
        private static int mp1_found_hasAlreadyPrinted = -1;
        private static readonly string ConfigPath = "config.json";
        private static bool _programsStarted = false;

        static void Main(string[] args) {
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

            monitorMain();
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

        private static void monitorMain() {
            while (true) {
                if (_config == null) {
                    Console.WriteLine("fix your config fucker. (a)");
                    return;
                }

                Process? mainProcess = getProcessByName(Path.GetFileNameWithoutExtension(_config.mainProgram));
                Process? mainProcess2 = getProcessByName(Path.GetFileNameWithoutExtension(_config.mainProgram2));

                if (mainProcess?.Id != mp1_found_hasAlreadyPrinted && mainProcess != null) {
                    Console.WriteLine($"found {_config.mainProgram} pid {mainProcess.Id}");
                    mp1_found_hasAlreadyPrinted = mainProcess.Id;
                }

                if (mainProcess2?.Id != mp2_found_hasAlreadyPrinted && mainProcess2 != null) {
                    Console.WriteLine($"found {_config.mainProgram2} pid {mainProcess2.Id}");
                    mp2_found_hasAlreadyPrinted = mainProcess2.Id;
                }

                if (!_programsStarted && shouldStartPrograms(mainProcess, mainProcess2)) {
                    runPrograms(_config.onStart);
                    runAllKeepAlives();
                    _programsStarted = true;
                }


                if (_programsStarted && shouldExitPrograms(mainProcess, mainProcess2)) {
                    killPrograms(_config.killOnExit);
                    Console.WriteLine();
                    Console.WriteLine($"exiting since ({_config.mainProgram} {_config.exitGate} {_config.mainProgram2}) has exited.");
                    Console.ReadKey();
                    break;
                }

                if (_programsStarted) {
                    monitorAllKeepAlive();
                }

                Thread.Sleep(_config.delay);
            }
        }
        private static Process? getProcessByName(string processName) {
            return Process.GetProcessesByName(processName).FirstOrDefault();
        }
        private static void killPrograms(IEnumerable<string>? programs) {
            if (programs == null) return;

            foreach (var program in programs) {
                try {
                    var processName = Path.GetFileNameWithoutExtension(program);

                    var processes = Process.GetProcessesByName(processName);

                    foreach (var process in processes) {
                        process.Kill();
                        process.WaitForExit();
                        Console.WriteLine($"killed {processName} pid {process.Id}");
                    }
                } catch (Exception ex) {
                    Console.WriteLine($"failed to kill {program}: {ex.Message}");
                }
            }
        }

        private static void runPrograms(IEnumerable<string>? programs) {
            if (programs == null) return;

            foreach (var program in programs) {
                try {
                    Process.Start(program);
                } catch (Exception ex) {
                    Console.WriteLine($"failed to start {program}: {ex.Message}");
                }
            }
        }

        private static void runAllKeepAlives() {
            if (_config == null) return;

            var keepAlivePrograms = _config.keepAlive.ToList();

            foreach (var program in keepAlivePrograms) {
                if (_keepAliveProcesses.ContainsKey(program)) continue;

                runKeepAlive(program);
            }
        }

        private static void runKeepAlive(string program) {
            try {
                var process = Process.Start(program);
                if (process == null) return;

                _keepAliveProcesses[program] = process;
                Console.WriteLine($"started keep-alive: {program}");
            } catch (Exception ex) {
                Console.WriteLine($"failed to start keep-alive {program}: {ex.Message}");
            }
        }

        private static void monitorAllKeepAlive() {
            if (_config == null) return;

            var keepAlivePrograms = _config.keepAlive.ToList();

            foreach (var program in keepAlivePrograms) {
                if (!_keepAliveProcesses.ContainsKey(program)) { runKeepAlive(program); continue; }

                var process = _keepAliveProcesses[program];
                process.Refresh();
                if (!process.HasExited) return;

                try {
                    if (process.TotalProcessorTime.TotalSeconds < 0.1) {
                        var newOSC = getProcessByName("VRCOSC");
                        if (newOSC != null) {
                            Console.WriteLine("found new vrcosc process");
                            _keepAliveProcesses[program] = newOSC;
                        }
                    } else {
                        Console.WriteLine($"keep-alive {program} exited. restarting.");
                        _keepAliveProcesses.Remove(program);
                        runKeepAlive(program);
                    }
                } catch (InvalidOperationException) {
                    runKeepAlive(program);
                }
            }
        }
    }

}
