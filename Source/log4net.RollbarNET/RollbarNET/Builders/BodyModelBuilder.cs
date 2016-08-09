// Licensed under the Apache License Version 2.0
// See https://github.com/mroach/RollbarSharp/raw/e2e3d4a42b8c73a92826739c75ca0c98eed0ba48/LICENSE.txt
// Copied from https://github.com/mroach/RollbarSharp as of commit 7d5551393cae7fc43eeec2f37db32326fc5b2bed

using System;
using System.Collections.Generic;
using RollbarNET.Serialization;

namespace RollbarNET.Builders {
	/// <summary>
	/// Builder for the 'body' of the request.
	/// This will be either an exception with details
	/// or a plain text message with optional fields
	/// </summary>
	public static class BodyModelBuilder {
		public static ExceptionBodyModel CreateExceptionBody(Exception exception) {
			var traces = TraceChainModelBuilder.CreateFromException(exception);
			return new ExceptionBodyModel(traces);
		}

		public static MessageBodyModel CreateMessageBody(string message, IDictionary<string, object> customData) {
			return new MessageBodyModel(message, customData);
		}
	}
}
