// Licensed under the Apache License Version 2.0
// See https://github.com/mroach/RollbarSharp/raw/e2e3d4a42b8c73a92826739c75ca0c98eed0ba48/LICENSE.txt
// Copied from https://github.com/mroach/RollbarSharp as of commit 7d5551393cae7fc43eeec2f37db32326fc5b2bed

using Newtonsoft.Json;

namespace RollbarNET.Serialization {
	/// <summary>
	/// Object describing the user affected by the item. 'id' is required.
	/// </summary>
	[JsonObject(MemberSerialization.OptIn)]
	public class PersonModel {
		/// <summary>
		/// User ID of the affected user. A string up to 40 characters. Required.
		/// </summary>
		[JsonProperty("id")]
		public string Id { get; set; }

		/// <summary>
		/// Affected user's username. A string up to 255 characters. Optional.
		/// </summary>
		[JsonProperty("username")]
		public string Username { get; set; }

		/// <summary>
		/// Affected user's email address. A string up to 255 characters. Optional.
		/// </summary>
		[JsonProperty("email")]
		public string Email { get; set; }
	}
}