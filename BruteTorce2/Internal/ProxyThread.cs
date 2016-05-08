using Internal.Events;
using Org.Mentalis.Network.ProxySocket;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Internal {
	class ProxyThread {
		//vars
		private int _id = -1;

		private ushort _socksPort = 0;
		private TorRunner runner = null;
		private ProxySocket _socket = null;
		private byte[] buffer = new byte[1024];

		private bool _ready = false;
		private bool _connected = false;
		private string lastHost = null;
		private ushort lastPort = 0;

		public EventHandler onStarted = null;
		public EventHandler onStopped = null;
		public EventHandler onDisconnected = null;
		public EventHandler onReady = null;
		public EventHandler<ProxyThreadDataEventArgs> onData = null;

		//constructor
		public ProxyThread(int id, ushort controlPort, ushort socksPort, string controlPassword) {
			if (id < 0) {
				throw new Exception("id cannot be less than 0.");
			}

			_id = id;
			_socksPort = socksPort;

			runner = new TorRunner(id, Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + "lib", controlPort, socksPort, controlPassword);
			startSocket();

			runner.onStarted += new EventHandler(onRunnerStarted);
			runner.onStopped += new EventHandler(onRunnerStopped);
			runner.onOutputData += new DataReceivedEventHandler(onRunnerOutput);
			runner.onErrorData += new DataReceivedEventHandler(onRunnerError);
		}
		~ProxyThread() {
			stop();
		}

		//public
		public void start() {
			if (runner.started) {
				return;
			}

			_ready = false;

			runner.start();
		}
		public void stop() {
			if (!runner.started) {
				return;
			}

			_ready = false;

			stopSocket();
			runner.stop();
		}

		public void rebuildCircuit() {
			if (!runner.started || !_ready) {
				return;
			}

			_ready = false;

			disconnect();
			rebuild();
		}

		public void connect(string host, ushort port) {
			if (_connected) {
				return;
			}

			lastHost = host;
			lastPort = port;

			try {
				_socket.Connect(host, port);
				_socket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, new AsyncCallback(onReceive), null);
			} catch (Exception ex) {
#if DEBUG
				Console.WriteLine("[Err] [ProxyThread #" + _id + "]: " + ex.Message);
#endif
				return;
			}

			_connected = true;
		}
		public void disconnect() {
			lastHost = null;
			lastPort = 0;

			stopSocket();
			startSocket();
			
			if (_connected) {
				_connected = false;
				if (onDisconnected != null) {
					onDisconnected.Invoke(this, EventArgs.Empty);
				}
			}
		}
		public int send(byte[] buffer, int offset, int size, SocketFlags socketFlags) {
			if (!_connected) {
				return -1;
			}

			try {
				return _socket.Send(buffer, offset, size, socketFlags);
			} catch (Exception ex) {
#if DEBUG
				Console.WriteLine("[Err] [ProxyThread #" + _id + "]: " + ex.Message);
#endif
				disconnect();
			}

			return -1;
		}

		public bool started {
			get {
				return runner.started;
			}
		}
		public bool ready {
			get {
				return _ready;
			}
		}
		public bool connected {
			get {
				return _connected;
			}
		}
		public int id {
			get {
				return _id;
			}
		}
		public string connectedHost {
			get {
				return lastHost;
			}
		}
		public ushort connectedPort {
			get {
				return lastPort;
			}
		}

		//private
		private void onRunnerStarted(object sender, EventArgs e) {
			if (onStarted != null) {
				onStarted.Invoke(this, e);
			}

			_ready = true;
			if (onReady != null) {
				onReady.Invoke(this, EventArgs.Empty);
			}
		}
		private void onRunnerStopped(object sender, EventArgs e) {
			_ready = false;
			if (onStopped != null) {
				onStopped.Invoke(this, e);
			}
		}
		private void onRunnerOutput(object sender, DataReceivedEventArgs e) {
#if DEBUG
			Console.WriteLine("[Info] [TorRunner #" + _id + "]: " + e.Data);
#endif
		}
		private void onRunnerError(object sender, DataReceivedEventArgs e) {
#if DEBUG
			Console.WriteLine("[Err] [TorRunner #" + _id + "]: " + e.Data);
#endif
			_ready = false;
		}

		private void rebuild() {
			new Thread(() => {
				runner.sendCommand("SIGNAL NEWNYM\r\n");
				Thread.Sleep(10000);
				
				/*stopSocket();
				startSocket();

				if (lastHost != null && lastPort != 0) {
					try {
						_socket.Connect(lastHost, lastPort);
						_socket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, new AsyncCallback(onReceive), null);
					} catch (Exception ex) {
#if DEBUG
						Console.WriteLine("[Err] [ProxyThread #" + _id + "]: " + ex.Message);
#endif
						disconnect();
						return;
					}
				}*/

				_ready = true;
				if (onReady != null) {
					onReady.Invoke(this, EventArgs.Empty);
				}
			}).Start();
		}

		private void onReceive(IAsyncResult e) {
			int bytes = 0;

			try {
				bytes = _socket.EndReceive(e);
			} catch (Exception ex) {
#if DEBUG
				Console.WriteLine("[Warn] [ProxyThread #" + _id + "]: Socket disconnected. Reason: \"" + ex.Message + "\"");
#endif
				disconnect();
				return;
			}

			if (bytes > 0) {
				if (onData != null) {
					byte[] tempBuffer = new byte[bytes];
					Buffer.BlockCopy(buffer, 0, tempBuffer, 0, bytes);
					onData.Invoke(this, new ProxyThreadDataEventArgs(tempBuffer));
				}

				try {
					_socket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, new AsyncCallback(onReceive), null);
				} catch (Exception ex) {
#if DEBUG
					Console.WriteLine("[Err] [ProxyThread #" + _id + "]: " + ex.Message);
#endif
					disconnect();
				}
			}
		}

		private void stopSocket() {
			try {
				_socket.Shutdown(SocketShutdown.Both);
				_socket.Close();
				_socket.Dispose();
			} catch (Exception ex) {
#if DEBUG
				Console.WriteLine("[Info] [ProxyThread #" + _id + "]: " + ex.Message);
#endif
			}
		}
		private void startSocket() {
			try {
				_socket = new ProxySocket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
				_socket.ProxyEndPoint = new IPEndPoint(IPAddress.Loopback, _socksPort);
				_socket.ProxyType = ProxyTypes.Socks5;
			} catch (Exception ex) {
#if DEBUG
				Console.WriteLine("[Err] [ProxyThread #" + _id + "]: \"" + ex.Message + "\" - Resrarting thread.");
#endif
				stop();
				start();
			}
		}
	}
}
