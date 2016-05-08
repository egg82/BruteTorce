using System;
using System.Net;
using System.Net.Sockets;
using Util.Events;

namespace Util {
	class HttpProxy {
		//vars
		public EventHandler<HttpProxyEventArgs> onConnected = null;

		private TcpListener server = null;

		//constructor
		public HttpProxy(ushort listenPort) {
			server = new TcpListener(IPAddress.Loopback, listenPort);
		}

		//public
		public void start() {
			if (server.Server.IsBound) {
				return;
			}

			server.Start();
			server.BeginAcceptSocket(new AsyncCallback(onAcceptSocket), null);
		}
		public void stop() {
			if (!server.Server.IsBound) {
				return;
			}

			server.Stop();
		}

		public bool listening {
			get {
				return server.Server.IsBound;
			}
		}

		//private
		private void onAcceptSocket(IAsyncResult e) {
			Socket s = null;

			try {
				s = server.EndAcceptSocket(e);
			} catch (Exception ex) {
#if DEBUG
				Console.WriteLine("[Listener]: " + ex.Message);
#endif
				return;
			}
			
			if (s != null) {
				if (onConnected != null) {
					onConnected.Invoke(this, new HttpProxyEventArgs(s));
				}
#if DEBUG
				Console.WriteLine("[Listener]: Socket opened.");
#endif
			}

			server.BeginAcceptSocket(new AsyncCallback(onAcceptSocket), null);
		}
	}
}
