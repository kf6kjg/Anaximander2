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
using System.IO;
using DataReader;
using System.Collections;
using System.Collections.Generic;

namespace Anaximander {
	class Application {
		private static readonly ILog LOG = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

		private static readonly string ExecutableDirectory = Path.GetDirectoryName(Assembly.GetEntryAssembly().CodeBase.Replace("file:/", String.Empty));

		private static readonly string DEFAULT_INI_FILE = "Anaximander.ini";

		public static void Main(string[] args) {
			// First line, hook the appdomain to the crash reporter
			// Analysis disable once RedundantDelegateCreation // The "new" is required.
			AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(CurrentDomain_UnhandledException);

			var watch = System.Diagnostics.Stopwatch.StartNew();

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
				XmlConfigurator.Configure(new FileInfo(logConfigFile));
				LOG.Info($"[MAIN]: Configured log4net using \"{logConfigFile}\" as configuration file.");
			}

			// Configure nIni aliases and localles
			System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("en-US", true);

			configSource.Alias.AddAlias("On", true);
			configSource.Alias.AddAlias("Off", false);
			configSource.Alias.AddAlias("True", true);
			configSource.Alias.AddAlias("False", false);
			configSource.Alias.AddAlias("Yes", true);
			configSource.Alias.AddAlias("No", false);

			configSource.AddSwitch("Startup", "inifile");

			// Read in the ini file
			ReadConfigurationFromINI(configSource);

			watch.Stop();
			LOG.Info($"[MAIN] Read configuration in {watch.ElapsedMilliseconds} ms.");
			watch.Restart();

			// Load the RDB map
			var rdb_map = new RDBMap(configSource);

			watch.Stop();
			LOG.Info($"[MAIN] Loaded region DB in {watch.ElapsedMilliseconds} ms for a total of {rdb_map.GetRegionCount()} regions, resulting in an average of {(float)watch.ElapsedMilliseconds / rdb_map.GetRegionCount()} ms / region.");
			watch.Restart();

			/* Issues to watch for:
			 * Region delete - The DBA will need to actually remove the estate record to cause a map tile delete.
			 *  - (Done) This implies that the RDBMap needs to check its list of regions against the DB and remove all that aren't in the DB.
			 * Region move - The list of images in the filesystem will need to be compared with the data structure and any images that are not in the data structure will need to be culled.
			 *  - Unless the images are actually stored in the filesystem with the regionID as the filename and a conversion table is used (softlinks, redirects, etc).
			 * Tile image read during write - The web server could attempt to read a file while the file is being written.
			 *  - Possible solution: write to a random filename then try { mv rndname to finalname with overwrite } catch { try again later for a max of N times }
			 *    This should provide as much atomicity as possible, and allow anything that's blocking access to be bypassed via time delay. Needs to just fail under exceptions that indicate always-fail conditions.
			 */


			var writer = new TileImageWriter(configSource);
			var tileGen = new TileGenerator(configSource);

			// Generate & replace ocean tile
			writer.WriteOceanTile(tileGen.GenerateOceanTile());

			// Remove all tiles that do not have a corresponding entry in the map.

			// Generate region tiles - all existing are nearly guaranteed to be out of date.  Could do a file timestamp check.
			// Generate zoom level tiles


			RestApi.RestAPI.StartHost(UpdateRegionDelegate, MapRulesDelegate, CheckAPIKeyDelegate, useSSL:false);


			while (true) {
			}
		}

		private static RestApi.RulesModel MapRulesDelegate(string uuid = null) { // TODO
			return new RestApi.RulesModel();
		}

		private static void UpdateRegionDelegate(string uuid, RestApi.ChangeInfo changeData) { // TODO
			
		}

		private static bool CheckAPIKeyDelegate(string apiKey, string uuid) { // TODO
			return true;
		}

		private static void ReadConfigurationFromINI(IConfigSource configSource) {
			IConfig startupConfig = configSource.Configs["Startup"];
			string iniFileName = startupConfig.GetString("inifile", DEFAULT_INI_FILE);

			bool found_at_given_path = false;

			try {
				LOG.Info($"[MAIN] Attempting to read configuration file {Path.GetFullPath(iniFileName)}");
				startupConfig.ConfigSource.Merge(new IniConfigSource(iniFileName));
				LOG.Info($"[MAIN] Success reading configuration file.");
				found_at_given_path = true;
			}
			catch {
				LOG.Warn($"[MAIN] Failure reading configuration file at {Path.GetFullPath(iniFileName)}");
			}

			if (!found_at_given_path) {
				// Combine with true path to binary and try again.
				iniFileName = Path.Combine(ExecutableDirectory, iniFileName);

				try {
					LOG.Info($"[MAIN] Attempting to read configuration file from installation path {Path.GetFullPath(iniFileName)}");
					startupConfig.ConfigSource.Merge(new IniConfigSource(iniFileName));
					LOG.Info($"[MAIN] Success reading configuration file.");
				}
				catch {
					LOG.Fatal($"[MAIN] Failure reading configuration file at {Path.GetFullPath(iniFileName)}");
					throw;
				}
			}
		}

		private static bool isHandlingException = false;

		/// <summary>
		/// Global exception handler -- all unhandled exceptions end up here :)
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e) {
			if (isHandlingException) {
				return;
			}

			try {
				isHandlingException = true;

				string msg = String.Empty;

				var ex = (Exception)e.ExceptionObject;
				if (ex.InnerException != null) {
					msg = $"InnerException: {ex.InnerException}\n";
				}

				msg = $"[APPLICATION]: APPLICATION EXCEPTION DETECTED: {e}\n" +
					"\n" +
					$"Exception: {e.ExceptionObject}\n" +
					msg +
					$"\nApplication is terminating: {e.IsTerminating}\n";

				LOG.Fatal(msg);

				if (e.IsTerminating) {
					// Since we are crashing, there's no way that log4net.RollbarNET will be able to send the message to Rollbar directly.
					// So have a separate program go do that work while this one finishes dying.

					var raw_msg =  System.Text.Encoding.Default.GetBytes(msg);

					var err_reporter = new System.Diagnostics.Process();
					err_reporter.EnableRaisingEvents = false;
					err_reporter.StartInfo.FileName = Path.Combine(ExecutableDirectory, "RollbarCrashReporter.exe");
					err_reporter.StartInfo.WorkingDirectory = ExecutableDirectory;
					err_reporter.StartInfo.Arguments = raw_msg.Length.ToString(); // Let it know ahead of time how many characters are expected.
					err_reporter.StartInfo.RedirectStandardInput = true;
					err_reporter.StartInfo.RedirectStandardOutput = false;
					err_reporter.StartInfo.RedirectStandardError = false;
					err_reporter.StartInfo.UseShellExecute = false;
					if (err_reporter.Start()) {
						err_reporter.StandardInput.BaseStream.Write(raw_msg, 0, raw_msg.Length);
					}
				}
			}
			catch (Exception ex) {
				LOG.Error("[MAIN] Exception launching CrashReporter.", ex);
			}
			finally {
				isHandlingException = false;

				if (e.IsTerminating) {
					// Preempt to not show a pile of puke if console was disabled.
					Environment.Exit(1);
				}
			}
		}
	}
}
