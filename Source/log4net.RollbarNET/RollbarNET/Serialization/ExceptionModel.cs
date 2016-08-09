// Licensed under the Apache License Version 2.0
// See https://github.com/mroach/RollbarSharp/raw/e2e3d4a42b8c73a92826739c75ca0c98eed0ba48/LICENSE.txt
// Copied from https://github.com/mroach/RollbarSharp as of commit 7d5551393cae7fc43eeec2f37db32326fc5b2bed

using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace RollbarNET.Serialization {
	/// <summary>
	/// Represents the details of the exception, but not the backtrace.
	/// </summary>
	[JsonObject(MemberSerialization.OptIn)]
	public class ExceptionModel {
		/// <summary>
		/// The class name of the exception (or some other string describing the error class)
		/// </summary>
		/// <example>ArgumentException</example>
		[JsonProperty("class")]
		public string Class { get; set; }

		/// <summary>
		/// The exception message (should not be prefixed with the class name)
		/// </summary>
		[JsonProperty("message")]
		public string Message { get; set; }

		/// <summary>
		/// Copy of the <see cref="Exception.Data"/> dictionary from the original
		/// <see cref="Exception"/> that was thrown.
		/// </summary>
		public IDictionary<object, object> Data { get; set; }

		public ExceptionModel(string exceptionClass, string message) {
			Class = exceptionClass;
			Message = message;
		}
	}
}