// Licensed under the Apache License Version 2.0
// See https://github.com/mroach/RollbarSharp/raw/e2e3d4a42b8c73a92826739c75ca0c98eed0ba48/LICENSE.txt
// Copied from https://github.com/mroach/RollbarSharp as of commit 7d5551393cae7fc43eeec2f37db32326fc5b2bed

using System;
using System.Collections.Generic;
using RollbarNET.Serialization;

namespace RollbarNET.Builders {
	public static class TraceChainModelBuilder {
		/// <summary>
		/// Converts an <see cref="Exception"/> ands his InnerExceptions to
		/// <see cref="TraceModel"/>'s.
		/// </summary>
		/// <param name="exception"></param>
		/// <returns></returns>
		public static IEnumerable<TraceModel> CreateFromException(Exception exception) {
			var traces = new List<TraceModel>();
			var innerEx = exception;

			while (innerEx != null) {
				var exceptionModel = ExceptionModelBuilder.CreateFromException(innerEx);
				var frames = FrameModelBuilder.CreateFramesFromException(innerEx);

				traces.Insert(0, new TraceModel(exceptionModel, frames));

				innerEx = innerEx.InnerException;
			}

			return traces;
		}
	}
}