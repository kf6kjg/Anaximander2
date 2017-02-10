// Program.cs
//
// Author:
//       Ricky Curtice <ricky@rwcproductions.com>
//
// Copyright (c) 2016 Richard Curtice
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
using System;
using System.Text;
using System.Reflection;
using log4net;
using log4net.Config;

namespace RollbarCrashReporter {
	class Application {
		private static readonly ILog LOG = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

		private static readonly string COMPILED_BY = "?mono?"; // Replaced during automatic packaging.

		public static void Main(string[] args) {
			XmlConfigurator.Configure();

			if (args.Length < 1) {
				LOG.Fatal($"[CRASH_REPORTER] Missing number of characters to read from stdin.\n\n{GenerateBootMessage()}");
				Console.WriteLine("Missing number of characters to read from stdin.");
				Environment.Exit(1);
			}

			var char_count = Int32.Parse(args[0]);

			var raw_input = new byte[char_count];

			Console.OpenStandardInput().Read(raw_input, 0, char_count);

			string msg = System.Text.Encoding.UTF8.GetString(raw_input);

			Console.WriteLine("[CRASH_REPORTER] Logging inbound message.");
			LOG.Fatal(msg);
		}

		private static string GenerateBootMessage() {
			var output = new StringBuilder();
			output.AppendLine("* * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * *");
			output.AppendLine($"Anaximander2 v{Assembly.GetExecutingAssembly().GetName().Version.ToString()} {COMPILED_BY}");
			var bitdepth = Environment.Is64BitOperatingSystem ? "64bit" : "unknown or 32bit";
			output.AppendLine($"OS: {Environment.OSVersion.VersionString} {bitdepth}");
			output.AppendLine($"Commandline: {Environment.CommandLine}");
			output.AppendLine($"CWD: {Environment.CurrentDirectory}");
			output.AppendLine($"Machine: {Environment.MachineName}");
			output.AppendLine($"Processors: {Environment.ProcessorCount}");
			output.AppendLine($"User: {Environment.UserDomainName}/{Environment.UserName}");
			var isMono = Type.GetType("Mono.Runtime") != null;
			output.AppendLine("Interactive shell: " + (Environment.UserInteractive ? "yes" : isMono ? "indeterminate" : "no"));
			output.AppendLine("* * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * *");

			return output.ToString();
		}
	}
}
