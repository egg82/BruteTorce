using System;
using System.Net.Sockets;

namespace Util.Events {
	class HttpProxyEventArgs : EventArgs {
		//vars
		private Socket _socket = null;

		//constructor
		public HttpProxyEventArgs(Socket socket) {
			_socket = socket;
		}

		//public
		public Socket socket {
			get {
				return _socket;
			}
		}

		//private

	}
}
