// Licensed under the Apache License Version 2.0
// See https://github.com/mroach/RollbarSharp/raw/e2e3d4a42b8c73a92826739c75ca0c98eed0ba48/LICENSE.txt
// Copied from https://github.com/mroach/RollbarSharp as of commit 7d5551393cae7fc43eeec2f37db32326fc5b2bed

using RollbarNET.Serialization;
using System;

namespace RollbarNET.Builders {
	public static class PersonModelBuilder {
		/// <summary>
		/// Find just the username from environment.
		/// Sets both the ID and Username to this username since ID is required.
		/// Email address won't be set.
		/// </summary>
		/// <returns></returns>
		public static PersonModel CreateFromEnvironment() {
			//Make the user-id as unique but reproducible as possible (a SID would be even better, but that might be a security risk)
			var id = string.Format(@"{0}\{1}", Environment.MachineName, Environment.UserName);

			if (id.Length > 40) {
				id = id.Substring(0, 40);
			}

			return new PersonModel {
				Id = id,
				Username = Environment.UserName
			};
		}
	}
}
