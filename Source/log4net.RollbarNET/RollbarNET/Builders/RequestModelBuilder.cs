// Licensed under the Apache License Version 2.0
// See https://github.com/mroach/RollbarSharp/raw/e2e3d4a42b8c73a92826739c75ca0c98eed0ba48/LICENSE.txt
// Copied from https://github.com/mroach/RollbarSharp as of commit 7d5551393cae7fc43eeec2f37db32326fc5b2bed

using System;
using System.Collections.Generic;
using System.Linq;

namespace RollbarNET.Builders {
	public static class RequestModelBuilder {
		/// <summary>
		/// Finds dictionary keys in the <see cref="scrubParams"/> list and replaces their values
		/// with asterisks. Key comparison is case insensitive.
		/// </summary>
		/// <param name="dict"></param>
		/// <param name="scrubParams"></param>
		/// <returns></returns>
		private static IDictionary<string, string> Scrub(IDictionary<string, string> dict, string[] scrubParams) {
			if (dict == null || !dict.Any())
				return dict;

			if (scrubParams == null || !scrubParams.Any())
				return dict;

			var itemsToUpdate = dict.Keys
				.Where(k => scrubParams.Contains(k, StringComparer.InvariantCultureIgnoreCase))
				.ToArray();

			if (itemsToUpdate.Any()) {
				foreach (var key in itemsToUpdate) {
					var len = dict[key] == null ? 8 : dict[key].Length;
					dict[key] = new string('*', len);
				}
			}

			return dict;
		}
	}
}
