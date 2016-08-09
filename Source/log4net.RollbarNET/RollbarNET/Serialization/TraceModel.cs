// Licensed under the Apache License Version 2.0
// See https://github.com/mroach/RollbarSharp/raw/e2e3d4a42b8c73a92826739c75ca0c98eed0ba48/LICENSE.txt
// Copied from https://github.com/mroach/RollbarSharp as of commit 7d5551393cae7fc43eeec2f37db32326fc5b2bed

using Newtonsoft.Json;

namespace RollbarNET.Serialization {
	/// <summary>
	/// Container for the exception detils as well as the backtrace frames
	/// </summary>
	[JsonObject(MemberSerialization.OptIn)]
	public class TraceModel {
		/// <summary>
		/// Description of the exception itself. Exception class and exception message.
		/// </summary>
		[JsonProperty("exception")]
		public ExceptionModel Exception { get; set; }

		/// <summary>
		/// Stack trace
		/// </summary>
		[JsonProperty("frames")]
		public FrameModel[] Frames { get; set; }

		public TraceModel(ExceptionModel ex, FrameModel[] frames) {
			Exception = ex;
			Frames = frames;
		}
	}
}