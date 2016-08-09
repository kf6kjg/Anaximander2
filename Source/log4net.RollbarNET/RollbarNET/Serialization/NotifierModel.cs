// Licensed under the Apache License Version 2.0
// See https://github.com/mroach/RollbarSharp/raw/e2e3d4a42b8c73a92826739c75ca0c98eed0ba48/LICENSE.txt
// Copied from https://github.com/mroach/RollbarSharp as of commit 7d5551393cae7fc43eeec2f37db32326fc5b2bed

using Newtonsoft.Json;

namespace RollbarNET.Serialization {
	/// <summary>
	/// Describes the notifier library/code that reported this item i.e. this .NET binding
	/// </summary>
	[JsonObject(MemberSerialization.OptIn)]
	public class NotifierModel {
		/// <summary>
		/// Name of the notifier. RollbarSharp in this case.
		/// </summary>
		[JsonProperty("name")]
		public string Name { get; set; }

		/// <summary>
		/// Version of the notifier. Defaults to the version from the assembly info
		/// </summary>
		[JsonProperty("version")]
		public string Version { get; set; }

		public NotifierModel() {
		}

		public NotifierModel(string name, string version) {
			Name = name;
			Version = version;
		}
	}
}