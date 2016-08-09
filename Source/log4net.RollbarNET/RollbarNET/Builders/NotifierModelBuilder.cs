// Licensed under the Apache License Version 2.0
// See https://github.com/mroach/RollbarSharp/raw/e2e3d4a42b8c73a92826739c75ca0c98eed0ba48/LICENSE.txt
// Copied from https://github.com/mroach/RollbarSharp as of commit 7d5551393cae7fc43eeec2f37db32326fc5b2bed

using System.Reflection;
using RollbarNET.Serialization;

namespace RollbarNET.Builders {
	public static class NotifierModelBuilder {
		/// <summary>
		/// Creates a model representing this notifier binding itself.
		/// Will be reported as the assembly's name and currently compiled version.
		/// </summary>
		/// <returns></returns>
		public static NotifierModel CreateFromAssemblyInfo() {
			var ai = Assembly.GetAssembly(typeof(NotifierModel)).GetName();

			return new NotifierModel(ai.Name, ai.Version.ToString());
		}
	}
}
