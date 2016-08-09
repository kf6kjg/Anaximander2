// Licensed under the Apache License Version 2.0
// See https://github.com/mroach/RollbarSharp/raw/e2e3d4a42b8c73a92826739c75ca0c98eed0ba48/LICENSE.txt
// Copied from https://github.com/mroach/RollbarSharp as of commit 18aba14ab710825c611a3b1ec7a24c7785d296d5

namespace RollbarNET {
	/// <summary>
	/// Event args fired when the response is received from the Rollbar endpoint
	/// </summary>
	public class RequestCompletedEventArgs {
		public Result Result { get; set; }

		public RequestCompletedEventArgs(Result result) {
			Result = result;
		}
	}
}