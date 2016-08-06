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
using System.Reflection;
using log4net;
using log4net.Config;
using Nini.Config;

namespace Anaximander {
	class Application {
		/// <summary>
		/// Text Console Logger
		/// </summary>
		private static readonly ILog LOG = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

		public static void Main(string[] args) {
			// First line, hook the appdomain to the crash reporter
			AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(CurrentDomain_UnhandledException);

			// Add the arguments supplied when running the application to the configuration
			var configSource = new ArgvConfigSource(args);

			// Configure Log4Net
			configSource.AddSwitch("Startup", "logconfig");
			string logConfigFile = configSource.Configs["Startup"].GetString("logconfig", String.Empty);
			if (String.IsNullOrEmpty(logConfigFile)) {
				XmlConfigurator.Configure();
				LOG.Info("[MAIN]: Configured log4net using ./Anaximander.exe.config as the default.");
			}
			else {
				XmlConfigurator.Configure(new System.IO.FileInfo(logConfigFile));
				LOG.Info($"[MAIN]: Configured log4net using \"{logConfigFile}\" as configuration file.");
			}


			LOG.Info("TODO");

			while (true) {
			}
		}

		private static bool isHandlingException = false;

		/// <summary>
		/// Global exception handler -- all unhandlet exceptions end up here :)
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e) {
			if (isHandlingException) {
				return;
			}

			isHandlingException = true;
			// TODO: Add config option to allow users to turn off error reporting
			// TODO: Post error report (disabled for now)

			string msg = String.Empty;
			
			var ex = (Exception)e.ExceptionObject;
			if (ex.InnerException != null) {
				msg = $"InnerException: {ex.InnerException}\n";
			}


			LOG.Error($"[APPLICATION]: APPLICATION EXCEPTION DETECTED: {e}\n" +
				"\n" +
				$"Exception: {e.ExceptionObject}\n" +
				msg +
				$"\nApplication is terminating: {e.IsTerminating}\n"
			);
			/*
			if (m_saveCrashDumps) {
				// Log exception to disk
				try {
					if (!Directory.Exists(m_crashDir)) {
						Directory.CreateDirectory(m_crashDir);
					}
					string log = Util.GetUniqueFilename(ex.GetType() + ".txt");
					using (StreamWriter m_crashLog = new StreamWriter(Path.Combine(m_crashDir, log))) {
						m_crashLog.WriteLine(msg);
					}

					File.Copy("Halcyon.ini", Path.Combine(m_crashDir, log + "_Halcyon.ini"), true);
				}
				catch (Exception e2) {
					m_log.ErrorFormat("[CRASH LOGGER CRASHED]: {0}", e2);
				}
			}
*/
			isHandlingException = false;
		}
	}
}
