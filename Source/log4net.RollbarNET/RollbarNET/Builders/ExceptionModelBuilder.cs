// Licensed under the Apache License Version 2.0
// See https://github.com/mroach/RollbarSharp/raw/e2e3d4a42b8c73a92826739c75ca0c98eed0ba48/LICENSE.txt
// Copied from https://github.com/mroach/RollbarSharp as of commit 7d5551393cae7fc43eeec2f37db32326fc5b2bed

using System;
using System.Linq;
using RollbarNET.Serialization;

namespace RollbarNET.Builders {
	public static class ExceptionModelBuilder {
		/// <summary>
		/// Converts an exception to an <see cref="ExceptionModel"/>.
		/// </summary>
		public static ExceptionModel CreateFromException(Exception ex) {
			var m = new ExceptionModel(ex.GetType().Name, ex.Message);
            
			if (ex.Data.Count > 0)
				m.Data = ex.Data.Keys.Cast<object>().ToDictionary(k => k, k => ex.Data[k]);
            
			return m;
		}
	}
}
