using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Util {
	/*
	 * Modified from https://github.com/joelverhagen/TorSharp/blob/master/TorSharp/Tools/Tor/TorPasswordHasher.cs
	*/

	class TorHasher {
		public static string hashPassword(string password) {
			Random random = new Random();

			byte[] salt = new byte[8];
			random.NextBytes(salt);

			var c = 96;
			var s2KSpecifier = salt.Concat(new[] { (byte) c }).ToArray();

			var EXPBIAS = 6;
			var count = (16 + (c & 15)) << ((c >> 4) + EXPBIAS);

			byte[] hash;
			using (var d = SHA1.Create()) {
				var tmp = s2KSpecifier
					   .Take(8)
					   .Concat(Encoding.ASCII.GetBytes(password))
					   .ToArray();

				var secretLen = tmp.Length;
				while (count != 0) {
					if (count > secretLen) {
						d.TransformBlock(tmp, 0, tmp.Length, null, -1);
						count -= secretLen;
					} else {
						d.TransformBlock(tmp, 0, count, null, -1);
						count = 0;
					}
				}

				d.TransformFinalBlock(new byte[0], 0, 0);
				hash = d.Hash;
			}

			var s2KSpecifierHex = BytesToHex(s2KSpecifier);
			var hashHex = BytesToHex(hash);

			return $"16:{s2KSpecifierHex}{hashHex}";
		}

		private static string BytesToHex(byte[] bytes) {
			return BitConverter
				.ToString(bytes, 0, bytes.Length)
				.Replace("-", string.Empty)
				.ToUpper();
		}
	}
}
