// Licensed under the Apache License Version 2.0
// See https://github.com/mroach/RollbarSharp/raw/e2e3d4a42b8c73a92826739c75ca0c98eed0ba48/LICENSE.txt
// Copied from https://github.com/mroach/RollbarSharp as of commit 7d5551393cae7fc43eeec2f37db32326fc5b2bed

using System.Collections.Generic;
using Newtonsoft.Json;

namespace RollbarNET.Serialization {
	/// <summary>
	/// Model for a text only report. The message ends up as the 'body' key
	/// in the message model and all custom data fields are merged in.
	/// </summary>
	/// <example>
	/// {
	///   message: {
	///     body: "my message text,
	///     custom_field: "my custom data"
	///   }
	/// }
	/// </example>
	[JsonObject(MemberSerialization.OptIn)]
	public class MessageBodyModel : BodyModel {
		public string Message { get; set; }

		public IDictionary<string, object> CustomData { get; set; }

		[JsonProperty("message")]
		internal IDictionary<string, object> Serialized {
			get {
				var result = new Dictionary<string, object>(CustomData);
				result["body"] = Message;
				return result;
			}
		}

		public MessageBodyModel(string message, IDictionary<string, object> customData = null) {
			Message = message;
			CustomData = customData ?? new Dictionary<string, object>();
		}
	}
}