using System.Text.RegularExpressions;

namespace Util {
	class HostValidator {
		//structs
		public struct HostValidatorArgs {
			public string hostname;
			public ushort port;
		}

		//vars
		private static Regex httpRegex = new Regex(@"Host: ?(.*?)\r?\n", RegexOptions.Multiline);
		private static Regex httpPortRegex = new Regex(@"(?:get|post|put|delete|head|options|GET|POST|PUT|DELETE|HEAD|OPTIONS) (?:https?:\/\/.*?):([0-9]{1,4}|[1-5][0-9]{4}|6[0-4][0-9]{3}|65[0-4][0-9]{2}|655[0-2][0-9]|6553[0-5])\/?(?: |\r?\n)", RegexOptions.Multiline);

		//constructor
		public HostValidator() {

		}

		//public
		public static HostValidatorArgs getHost(string input) {
			if (httpRegex.IsMatch(input)) {
				return new HostValidatorArgs {
					hostname = httpRegex.Match(input).Groups[1].Value,
					port = (httpPortRegex.IsMatch(input)) ? ushort.Parse(httpPortRegex.Match(input).Groups[1].Value) : (ushort) 80
				};
			}

			return new HostValidatorArgs {
				hostname = null,
				port = 0
			};
		}

		//private

	}
}
