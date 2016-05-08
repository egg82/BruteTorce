using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using Util;

namespace Internal {
	class TorRunner {
		//vars
		private int _id = -1;
		private ushort controlPort = 0;
		private ushort socksPort = 0;
		private string controlPassword = null;
		private string _torDirectory = null;

		private Process _proc = null;
		private bool _started = false;

		public DataReceivedEventHandler onOutputData = null;
		public DataReceivedEventHandler onErrorData = null;

		public EventHandler onStartError = null;
		public EventHandler onStarted = null;
		public EventHandler onStopped = null;

		//constructor
		public TorRunner(int id, string torDirectory, ushort controlPort, ushort socksPort, string controlPassword) {
			if (id < 0) {
				throw new Exception("id cannot be less than 0.");
			}
			if (torDirectory == "" || torDirectory == null) {
				throw new Exception("torDirectory cannot be blank or null.");
			}
			if (!exists(torDirectory) || !isDirectory(torDirectory)) {
				throw new Exception("torDirectory must exist and be a valid directory.");
			}
			if (!exists(torDirectory + Path.DirectorySeparatorChar + "Tor" + Path.DirectorySeparatorChar + "tor.exe") || isDirectory(torDirectory + Path.DirectorySeparatorChar + "Tor" + Path.DirectorySeparatorChar + "tor.exe")) {
				throw new Exception("Tor/tor.exe must exist and cannot be a directory.");
			}

			_id = id;
			_torDirectory = torDirectory;
			this.controlPort = controlPort;
			this.socksPort = socksPort;
			this.controlPassword = controlPassword;
		}
		~TorRunner() {
			stop();
		}

		//public
		public void start() {
			if (_started) {
				if (onStarted != null) {
					onStarted.Invoke(this, EventArgs.Empty);
				}

				return;
			}

			load();
		}
		public void stop() {
			if (!_started) {
				if (onStopped != null) {
					onStopped.Invoke(this, EventArgs.Empty);
				}

				return;
			}

			unload();
			
			_started = false;
		}

		public void sendCommand(string command) {
			if (!_started) {
				return;
			}

			TcpClient client = null;

			try {
				client = new TcpClient("localhost", controlPort);
			} catch (Exception ex) {
#if DEBUG
				Console.WriteLine("[Err] [TorRunner #" + _id + "]: " + ex.Message);
#endif
				return;
			}
			
			client.SendTimeout = 20000;
			client.ReceiveTimeout = 20000;

			string response = null;
			
			response = request(client, "AUTHENTICATE \"" + controlPassword + "\"\r\n" + command);

#if DEBUG
			Console.WriteLine("[Info] [TorRunner #" + _id + "]: " + response);
#endif

			client.Close();
		}

		public bool started {
			get {
				return _started;
			}
		}

		//private
		private bool exists(string path) {
			return (File.Exists(path) || Directory.Exists(path)) ? true : false;
		}
		private bool isDirectory(string path) {
			FileAttributes attr = File.GetAttributes(path);
			return ((attr & FileAttributes.Directory) == FileAttributes.Directory) ? true : false;
		}

		private void load() {
			_proc = Process.Start(new ProcessStartInfo {
				FileName = _torDirectory + Path.DirectorySeparatorChar + "Tor" + Path.DirectorySeparatorChar + "tor.exe",
				Arguments = "--RunAsDaemon 1 --CookieAuthentication 0 --HashedControlPassword \"" + TorHasher.hashPassword(controlPassword) + "\" --ControlPort " + controlPort + " --PidFile tor" + _id + ".pid --SocksPort " + socksPort + " --DataDirectory Data/Tor" + _id + " --GeoIPFile Data/Tor/geoip --GeoIPv6File Data/Tor/geoip6",
				WorkingDirectory = _torDirectory,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
				CreateNoWindow = true,
				WindowStyle = ProcessWindowStyle.Hidden
			});

			if (_proc == null) {
				if (onStartError != null) {
					onStartError.Invoke(this, EventArgs.Empty);
				}
				throw new Exception("[Err] [TorRunner #" + _id + "]: Unable to start Tor.");
			}

			bool error = false;

			_proc.OutputDataReceived += new DataReceivedEventHandler((s, e) => {
				if (onOutputData != null) {
					onOutputData.Invoke(this, e);
				}
				if (e.Data != null && (e.Data as string).ToLower().IndexOf("opening control listener on") > -1) {
					Thread.Sleep(500);
					if (!error) {
						_started = true;
						if (onStarted != null) {
							onStarted.Invoke(this, EventArgs.Empty);
						}
					}
				}
			});
			_proc.ErrorDataReceived += new DataReceivedEventHandler((s, e) => {
				if (onErrorData != null) {
					onErrorData.Invoke(this, e);
				}
				error = true;
				if (onStartError != null) {
					onStartError.Invoke(this, EventArgs.Empty);
				}
				stop();
			});

			_proc.BeginOutputReadLine();
			_proc.BeginErrorReadLine();
		}
		private void unload() {
			if (_proc == null) {
				if (onStopped != null) {
					onStopped.Invoke(this, EventArgs.Empty);
				}

				return;
			}

			new Thread(() => {
				if (!_proc.HasExited) {
					if (_proc.CloseMainWindow()) {
						_proc.WaitForExit(2000);
					}
				}

				if (!_proc.HasExited) {
					_proc.Kill();
					_proc.WaitForExit(1000);
				}

				_proc = null;

				if (onStopped != null) {
					onStopped.Invoke(this, EventArgs.Empty);
				}
			}).Start();
		}

		private string request(TcpClient client, string message) {
			string totalResponse = "";

			try {
				using (StreamWriter writer = new StreamWriter(client.GetStream())) {
					using (StreamReader reader = new StreamReader(client.GetStream())) {
						writer.Write(message);
						writer.Flush();

						totalResponse += reader.ReadLine() + "\r\n";
						totalResponse += reader.ReadLine() + "\r\n";
					}
				}
			} catch (Exception ex) {
#if DEBUG
				Console.WriteLine("[Err] [TorRunner #" + _id + "]: " + ex.Message);
#endif
			}

			return totalResponse;
		}
	}
}
