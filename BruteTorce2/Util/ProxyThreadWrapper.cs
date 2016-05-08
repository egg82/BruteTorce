using BruteTorce2;
using Internal;
using Internal.Events;
using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.IO;
using System.Net.Sockets;
using System.Text;

namespace Util {
	class ProxyThreadWrapper {
		//vars
		public EventHandler onReady = null;
		public EventHandler onStarted = null;
		public EventHandler onStopped = null;

		private ProxyThread thread = null;
		private ushort maxThreadRequests = Program.maxThreadRequests;
		
		private ushort requestCount = 0;

		private Socket client = null;
		private byte[] buffer = new byte[1024];

		//constructor
		public ProxyThreadWrapper(int id) {
			thread = new ProxyThread(id, (ushort) (Program.baseControlPort + id), (ushort) (Program.baseSocksPort + id), Program.controlPassword);

			thread.onStarted += new EventHandler(onThreadStarted);
			thread.onStopped += new EventHandler(onThreadStopped);
			thread.onDisconnected += new EventHandler(onThreadDisconnected);
			thread.onReady += new EventHandler(onThreadReady);
			thread.onData += new EventHandler<ProxyThreadDataEventArgs>(onThreadData);
		}
		~ProxyThreadWrapper() {
			stop();
		}

		//public
		public void start() {
			thread.start();
		}
		public void stop() {
			if (!thread.connected && client != null) {
				killSocket();
			}
			thread.stop();
		}

		public void setClient(Socket s) {
			if (client != null) {
				killSocket();
			}
			if (s == null || !s.Connected) {
				return;
			}

			try {
				s.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, new AsyncCallback(onReceive), null);
			} catch (Exception ex) {
				Console.WriteLine("[Err] [ProxyThreadWrapper #" + thread.id + "]: " + ex.Message);
				try {
					s.Close();
				} catch (Exception ex2) {
#if DEBUG
					Console.WriteLine("[Warn] [ProxyThreadWrapper #" + thread.id + "]: " + ex2.Message);
#endif
				}
				return;
			}

			client = s;
		}

		public int id {
			get {
				return thread.id;
			}
		}

		public bool started {
			get {
				return thread.started;
			}
		}
		public bool ready {
			get {
				return (thread.ready && client == null) ? true : false;
			}
		}
		public bool connected {
			get {
				return thread.connected;
			}
		}

		//private
		private void onThreadStarted(object sender, EventArgs e) {
			Console.WriteLine("[Info] [ProxyThreadWrapper #" + thread.id + "]: Started.");

			if (onStarted != null) {
				onStarted.Invoke(this, EventArgs.Empty);
			}
		}
		private void onThreadStopped(object sender, EventArgs e) {
			Console.WriteLine("[Info] [ProxyThreadWrapper #" + thread.id + "]: Stopped.");

			if (onStopped != null) {
				onStopped.Invoke(this, EventArgs.Empty);
			}
		}
		private void onThreadReady(object sender, EventArgs e) {
			Console.WriteLine("[Info] [ProxyThreadWrapper #" + thread.id + "]: Circuit built.");
			
			requestCount = 0;
			if (client != null) {
				/*if (client != null) {
					get(context);
				} else {
					if (onReady != null) {
						onReady.Invoke(this, EventArgs.Empty);
					}
				}*/
			} else {
				if (onReady != null) {
					onReady.Invoke(this, EventArgs.Empty);
				}
			}
		}
		private void onThreadDisconnected(object sender, EventArgs e) {
			killSocket();
		}
		private void onThreadData(object sender, ProxyThreadDataEventArgs e) {
			if (client == null) {
				thread.disconnect();
				return;
			}

			try {
				client.Send(e.bytes);
			} catch (Exception ex) {
#if DEBUG
				Console.WriteLine("[Err] [ProxyThreadWrapper #" + thread.id + "]: " + ex.Message);
#endif
				killSocket();
				return;
			}

			Console.WriteLine("[Info] [ProxyThreadWrapper #" + thread.id + "]: Socket data received.");

			requestCount++;
			if (requestCount >= maxThreadRequests) {
				thread.rebuildCircuit();
			} else {
				if (onReady != null) {
					onReady.Invoke(this, EventArgs.Empty);
				}
			}
		}

		private void onReceive(IAsyncResult e) {
			int bytes = 0;

			if (client == null) {
#if DEBUG
				Console.WriteLine("[Info] [ProxyThreadWrapper #" + thread.id + "]: Socket closed.");
#endif
				return;
			}

			try {
				bytes = client.EndReceive(e);
			} catch (Exception ex) {
#if DEBUG
				Console.WriteLine("[Info] [ProxyThreadWrapper #" + thread.id + "]: Socket closed. Reason: \"" + ex.Message + "\"");
#endif

				killSocket();
				return;
			}

			if (bytes > 0) {
				if (!thread.connected) {
					HostValidator.HostValidatorArgs args = HostValidator.getHost(Encoding.UTF8.GetString(buffer, 0, bytes));
					if (args.hostname != null && args.port != 0) {
						thread.connect(args.hostname, args.port);
					} else {
						killSocket();
#if DEBUG
						Console.WriteLine("[Err] [ProxyThreadWrapper #" + thread.id + "]: Socket closed. Reason: invalid host. Data: " + toLiteral(Encoding.UTF8.GetString(buffer, 0, bytes)));
#endif
						return;
					}
				}
				
				if (thread.send(buffer, 0, bytes, SocketFlags.None) == -1) {
					killSocket();
#if DEBUG
					Console.WriteLine("[Err] [ProxyThreadWrapper #" + thread.id + "]: Socket closed.");
#endif
					return;
				}

				Console.WriteLine("[Info] [ProxyThreadWrapper #" + thread.id + "]: Socket data sent.");

				try {
					client.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, new AsyncCallback(onReceive), null);
				} catch (Exception ex) {
#if DEBUG
					Console.WriteLine("[Info] [ProxyThreadWrapper #" + thread.id + "]: Socket closed. Reason: \"" + ex.Message + "\"");
#endif
					killSocket();
					return;
				}
			} else {
				killSocket();
#if DEBUG
				Console.WriteLine("[Info] [ProxyThreadWrapper #" + thread.id + "]: Socket closed gracefully.");
#endif
			}
		}

		private string toLiteral(string input) {
			using (var writer = new StringWriter()) {
				using (var provider = CodeDomProvider.CreateProvider("CSharp")) {
					provider.GenerateCodeFromExpression(new CodePrimitiveExpression(input), writer, new CodeGeneratorOptions { IndentString = "\t" });
					var literal = writer.ToString();
					literal = literal.Replace(string.Format("\" +{0}\t\"", Environment.NewLine), "");
					return literal;
				}
			}
		}

		private void killSocket() {
			requestCount++;
			if (requestCount >= maxThreadRequests) {
				thread.rebuildCircuit();
			} else {
				if (onReady != null) {
					onReady.Invoke(this, EventArgs.Empty);
				}
			}

			if (thread.connected) {
				thread.disconnect();
			} else {
				try {
					client.Close();
					client = null;
				} catch (Exception ex) {
#if DEBUG
					Console.WriteLine("[Info] [ProxyThreadWrapper #" + thread.id + "]: " + ex.Message);
#endif
				}
			}
		}
	}
}
