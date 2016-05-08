using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading;
using Util;
using Util.Events;

namespace BruteTorce2 {
	class Program {
		//vars
		public static int numThreads = 20;
		public static ushort baseProxyPort = 40000;
		public static ushort baseControlPort = 50000;
		public static ushort baseSocksPort = 60000;
		public static string controlPassword = "test";
		public static ushort proxyPort = 30000;
		public static ushort maxThreadRequests = 5;

		private static List<Socket> buffer = new List<Socket>();
		private static List<ProxyThreadWrapper> threads = new List<ProxyThreadWrapper>();
		private static int numReady = 0;
		private static int startThread = 0;
		private static HttpProxy listener = null;

		private static bool running = true;
		private static bool firstrun = true;

		//constructor
		static void Main(string[] args) {
#if DEBUG
			Console.WriteLine("Debug mode. Extra output enabled.");
#endif
			Console.WriteLine("Killing Tor..");
			foreach (Process proc in Process.GetProcessesByName("tor")) {
				proc.Kill();
			}

			Console.WriteLine("Starting listener..");

			listener = new HttpProxy(proxyPort);
			listener.onConnected += new EventHandler<HttpProxyEventArgs>(onConnected);
			try {
				listener.start();
			} catch (Exception ex) {
				Console.WriteLine("Error starting listener: " + ex.Message);
				return;
			}

			Console.WriteLine("Starting threads..");

			for (int i = 0; i < numThreads; i++) {
				ProxyThreadWrapper thread = new ProxyThreadWrapper(i);

				thread.onStarted += new EventHandler(onThreadStarted);
				thread.onStopped += new EventHandler(onThreadStopped);
				thread.onReady += new EventHandler(onThreadReady);

				new Thread(() => {
					try {
						thread.start();
					} catch (Exception ex) {
						Console.WriteLine("Error starting thread: " + ex.Message);
					}
				}).Start();

				threads.Add(thread);
			}

			Console.CancelKeyPress += (o, e) => {
				e.Cancel = true;

				Console.WriteLine("Stopping listener..");
				listener.stop();

				Console.WriteLine("Stopping threads..");
				for (int i = 0; i < numThreads; i++) {
					threads[i].stop();
				}
			};

			while (running) { }
			Console.WriteLine("All threads stopped. It it safe to close the window now.");
			while (true) { }
		}

		//functions
		private static void onConnected(object sender, HttpProxyEventArgs e) {
			if (!e.socket.Connected) {
				try {
					e.socket.Shutdown(SocketShutdown.Both);
					e.socket.Close();
					e.socket.Dispose();
				} catch (Exception ex) {
#if DEBUG
					Console.WriteLine(ex.Message);
#endif
				}
				Console.WriteLine("Socket discarded.");
				return;
			}

			if (numReady <= 0) {
#if DEBUG
				Console.WriteLine("Socket sent to buffer.");
#endif
				buffer.Add(e.socket);
			} else {
				for (int i = startThread; i < numThreads; i++) {
					startThread++;
					if (threads[i].ready) {
						numReady--;
#if DEBUG
						Console.WriteLine("Socket sent to thread #" + i + ".");
#endif
						threads[i].setClient(e.socket);
						break;
					}
				}
				if (startThread == numThreads - 1) {
					startThread = 0;
				}
			}
		}

		private static void onThreadStarted(object sender, EventArgs e) {
			if (firstrun && numReady == numThreads - 1) {
				Console.WriteLine("All threads started.");
			}
		}
		private static void onThreadStopped(object sender, EventArgs e) {
			for (int i = 0; i < numThreads; i++) {
				if (threads[i].started) {
					return;
				}
			}
			running = false;
		}
		private static void onThreadReady(object sender, EventArgs e) {
			while (buffer.Count > 0 && !buffer[0].Connected) {
				try {
					buffer[0].Shutdown(SocketShutdown.Both);
					buffer[0].Close();
					buffer[0].Dispose();
				} catch (Exception ex) {
#if DEBUG
					Console.WriteLine(ex.Message);
#endif
				}
				Console.WriteLine("Socket discarded.");
				buffer.RemoveAt(0);
			}

			if (buffer.Count > 0) {
				(sender as ProxyThreadWrapper).setClient(buffer[0]);
				buffer.RemoveAt(0);
#if DEBUG
				Console.WriteLine("Thread #" + (sender as ProxyThreadWrapper).id + " pulled socket from buffer.");
#endif
			} else {
				numReady++;
			}
		}
	}
}
