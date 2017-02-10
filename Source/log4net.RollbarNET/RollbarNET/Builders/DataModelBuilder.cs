// Licensed under the Apache License Version 2.0
// See https://github.com/mroach/RollbarSharp/raw/e2e3d4a42b8c73a92826739c75ca0c98eed0ba48/LICENSE.txt
// Copied from https://github.com/mroach/RollbarSharp as of commit 7d5551393cae7fc43eeec2f37db32326fc5b2bed

using System;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using RollbarNET.Serialization;

namespace RollbarNET.Builders {
	public class DataModelBuilder {
		public Configuration Configuration { get; protected set; }

		public DataModelBuilder()
			: this(Configuration.CreateFromAppConfig()) {
		}

		public DataModelBuilder(Configuration configuration) {
			Configuration = configuration;
		}

		public DataModel CreateExceptionNotice(Exception ex, string message = null, string level = "error") {
			var body = BodyModelBuilder.CreateExceptionBody(ex);
			var model = Create(level, body);

			//merge exception data dictionaries to list of keyValues pairs
			var keyValuePairs = body.TraceChain.Where(tm => tm.Exception.Data != null).SelectMany(tm => tm.Exception.Data);

			foreach (var keyValue in keyValuePairs) {
				//the keys in keyValuePairs aren't necessarily unique, so don't add but overwrite
				model.Custom[keyValue.Key.ToString()] = keyValue.Value;
			}

			model.Title = message;

			return model;
		}

		public DataModel CreateMessageNotice(string message, string level = "info", IDictionary<string, object> customData = null) {
			return Create(level, BodyModelBuilder.CreateMessageBody(message, customData));
		}

		/// <summary>
		/// Create the best stub of a request that we can using the message level and body
		/// </summary>
		/// <param name="level"></param>
		/// <param name="body"></param>
		/// <returns></returns>
		protected DataModel Create(string level, BodyModel body) {
			var model = new DataModel(level, body);

			model.CodeVersion = System.Reflection.Assembly.GetEntryAssembly().GetName().Version.ToString();
			model.Environment = Configuration.Environment;
			model.Platform = Configuration.Platform;
			model.Language = Configuration.Language;
			model.Framework = Configuration.Framework;

			model.Timestamp = (ulong)Now();

			model.Notifier = NotifierModelBuilder.CreateFromAssemblyInfo();

			model.Request = new RequestModel();
			model.Server = new ServerModel();

			//Obtain person information on non-hosted environment only
			model.Person = PersonModelBuilder.CreateFromEnvironment();

			model.Server.GitSha = Configuration.GitSha;
			model.Server.Machine = Environment.MachineName;
			#if __MonoCS__
			model.Custom.Add("Compiler", "Mono");
			#else
			model.Custom.Add("Compiler", "VS");
			#endif
			model.Custom.Add("Commandline", Environment.CommandLine);
			model.Custom.Add("CWD", Environment.CurrentDirectory);
			model.Custom.Add("ProcessorCount", Environment.ProcessorCount);

			return model;
		}

		/// <summary>
		/// Current UTC date time as a UNIX timestamp
		/// </summary>
		/// <returns></returns>
		private static double Now() {
			var epoch = new DateTime(1970, 1, 1);
			return (DateTime.UtcNow - epoch).TotalSeconds;
		}

		public static string FingerprintHash(params object[] fields) {
			return FingerprintHash(string.Join(",", fields));
		}

		/// <summary>
		/// To make sure fingerprints are the correct length and don't
		/// contain any problematic characters, SHA1 the fingerprint.
		/// </summary>
		/// <param name="data"></param>
		/// <returns></returns>
		public static string FingerprintHash(string data) {
			using (var sha = new SHA1Managed()) {
				var bytes = Encoding.UTF8.GetBytes(data);
				var hash = sha.ComputeHash(bytes);
				return BitConverter.ToString(hash).Replace("-", string.Empty);
			}
		}
	}
}
