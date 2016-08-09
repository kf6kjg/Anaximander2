// Licensed under the Apache License Version 2.0
// See https://github.com/mroach/RollbarSharp/raw/e2e3d4a42b8c73a92826739c75ca0c98eed0ba48/LICENSE.txt
// Copied from https://github.com/mroach/RollbarSharp as of commit 7d5551393cae7fc43eeec2f37db32326fc5b2bed

using System.Collections.Generic;
using Newtonsoft.Json;

namespace RollbarNET.Serialization {
	/// <summary>
	/// Model used when reporting an exception rather than a message
	/// </summary>
	[JsonObject(MemberSerialization.OptIn)]
	public class ExceptionBodyModel : BodyModel {
		/// <summary>
		/// Exception trace. Includes exception class, message, and backtrace.
		/// </summary>
		[JsonProperty("trace_chain")]
		public IEnumerable<TraceModel> TraceChain { get; set; }

		public ExceptionBodyModel(IEnumerable<TraceModel> traceChain) {
			TraceChain = traceChain;
		}
	}
}