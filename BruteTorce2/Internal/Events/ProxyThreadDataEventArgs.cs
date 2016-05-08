using System;

namespace Internal.Events {
	class ProxyThreadDataEventArgs : EventArgs {
		//vars
		private byte[] _bytes = null;

		//constructor
		public ProxyThreadDataEventArgs(byte[] bytes) {
			_bytes = bytes;
		}

		//public
		public byte[] bytes {
			get {
				return _bytes;
			}
		}

		//private

	}
}
