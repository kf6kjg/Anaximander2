// Licensed under the Apache License Version 2.0
// See https://github.com/mroach/RollbarSharp/raw/e2e3d4a42b8c73a92826739c75ca0c98eed0ba48/LICENSE.txt
// Copied from https://github.com/mroach/RollbarSharp as of commit 7d5551393cae7fc43eeec2f37db32326fc5b2bed

using Newtonsoft.Json;

namespace RollbarNET.Serialization {
	/// <summary>
	/// Wrapper for the whole payload. This is the access token as one item
	/// and the whole notice request as another
	/// </summary>
	[JsonObject(MemberSerialization.OptIn)]
	public class PayloadModel {
		/// <summary>
		/// Access token
		/// </summary>
		[JsonProperty("access_token")]
		public string AccessToken { get; set; }

		/// <summary>
		/// Body of the request
		/// </summary>
		[JsonProperty("data")]
		public DataModel Data { get; set; }

		public PayloadModel(string accessToken, DataModel data) {
			AccessToken = accessToken;
			Data = data;
		}
	}
}

