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
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using DataReader;
using log4net;
using log4net.Config;
using Nini.Config;

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
			var logConfigFile = configSource.Configs["Startup"].GetString("logconfig", String.Empty);
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
			configSource.AddSwitch("Startup", "ServerMode");

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

			// Remove all tiles that do not have a corresponding entry in the map.
			writer.RemoveDeadTiles(rdb_map);

			// Generate & replace ocean tile
			using (var ocean_tile = tileGen.GenerateOceanTile()) {
				writer.WriteOceanTile(ocean_tile);
			}

			var defaultTiles = configSource.Configs["DefaultTiles"];
			var techniqueConfig = defaultTiles?.GetString("OfflineRegion", Constants.OfflineRegion.ToString()) ?? Constants.OfflineRegion.ToString();
			RegionErrorDisplayTechnique offlineTechnique;
			if (!Enum.TryParse(techniqueConfig.ToUpperInvariant(), out offlineTechnique)) {
				LOG.Error($"Invalid offline region technique '{techniqueConfig}' in configuration.");
			}
			techniqueConfig = defaultTiles?.GetString("CrashedRegion", Constants.CrashedRegion.ToString()) ?? Constants.CrashedRegion.ToString();
			RegionErrorDisplayTechnique crashedTechnique;
			if (!Enum.TryParse(techniqueConfig.ToUpperInvariant(), out crashedTechnique)) {
				LOG.Error($"Invalid crashed region technique '{techniqueConfig}' in configuration.");
			}

			// Generate region tiles - all existing are nearly guaranteed to be out of date.
#if DEBUG
			var options = new ParallelOptions { MaxDegreeOfParallelism = -1 }; // -1 means full parallel.  1 means non-parallel.

			Parallel.ForEach(rdb_map.GetRegionUUIDsAsStrings(), options, (region_id) => {
#else
			Parallel.ForEach(rdb_map.GetRegionUUIDsAsStrings(), (region_id) => {
#endif
			//foreach(var region_id in rdb_map.GetRegionUUIDsAsStrings()) {
				var region = rdb_map.GetRegionByUUID(region_id);

				if (region.locationX != null) {
					// Assume that during bootup the tile is out of date and rebuild everything.

					if (crashedTechnique == RegionErrorDisplayTechnique.IGNORE || region.isRegionCurrentlyUp) {
						using (var tile_image = tileGen.RenderRegionTile(region)) {
							writer.WriteTile((int)region.locationX, (int)region.locationY, 1, region_id, tile_image);
						}
					}
					else {
						if (crashedTechnique == RegionErrorDisplayTechnique.IMAGE) {
							var filename = defaultTiles?.GetString("CrashedRegionImage", Constants.CrashedRegionImage) ?? Constants.CrashedRegionImage;

							writer.WriteTile((int)region.locationX, (int)region.locationY, 1, region_id, filename);
						}
						else if (crashedTechnique == RegionErrorDisplayTechnique.COLOR) {
							var colorR = defaultTiles?.GetInt("CrashedRegionRed", Constants.CrashedRegionColor.R) ?? Constants.CrashedRegionColor.R;
							var colorG = defaultTiles?.GetInt("CrashedRegionGreen", Constants.CrashedRegionColor.G) ?? Constants.CrashedRegionColor.G;
							var colorB = defaultTiles?.GetInt("CrashedRegionBlue", Constants.CrashedRegionColor.B) ?? Constants.CrashedRegionColor.B;

							using (var tile_image = tileGen.GenerateConstantColorTile(Color.FromArgb(colorR, colorG, colorB))) {
								writer.WriteTile((int)region.locationX, (int)region.locationY, 1, region_id, tile_image);
							}
						}
					}
				}
				else if (offlineTechnique != RegionErrorDisplayTechnique.IGNORE) {
					// Go looking for the backup technique to find the coordinates of a region that has gone offline.
					var folderinfo = configSource.Configs["Folders"];
					var tilepath = folderinfo?.GetString("MapTilePath", Constants.MapTilePath) ?? Constants.MapTilePath;

					var coords = string.Empty;
					try {
						coords = File.ReadAllText(Path.Combine(tilepath, Constants.ReverseLookupPath, region_id));
					}
					catch (SystemException) { // All IO errors just mean skippage.
					}

					if (!string.IsNullOrWhiteSpace(coords)) { // Backup technique has succeeded, do as specified in config.
						var coordsList = coords.Split(',').Select(coord => int.Parse(coord)).ToArray();

						if (offlineTechnique == RegionErrorDisplayTechnique.IMAGE) {
							var filename = defaultTiles?.GetString("OfflineRegionImage", Constants.OfflineRegionImage) ?? Constants.OfflineRegionImage;

							writer.WriteTile(coordsList[0], coordsList[1], 1, region_id, filename);
						}
						else if (offlineTechnique == RegionErrorDisplayTechnique.COLOR) {
							var colorR = defaultTiles?.GetInt("OfflineRegionRed", Constants.OfflineRegionColor.R) ?? Constants.OfflineRegionColor.R;
							var colorG = defaultTiles?.GetInt("OfflineRegionGreen", Constants.OfflineRegionColor.G) ?? Constants.OfflineRegionColor.G;
							var colorB = defaultTiles?.GetInt("OfflineRegionBlue", Constants.OfflineRegionColor.B) ?? Constants.OfflineRegionColor.B;

							using (var tile_image = tileGen.GenerateConstantColorTile(Color.FromArgb(colorR, colorG, colorB))) {
								writer.WriteTile(coordsList[0], coordsList[1], 1, region_id, tile_image);
							}
						}
					}
				}
			});

			watch.Stop();
			LOG.Info($"[MAIN] Created full res map tiles in {watch.ElapsedMilliseconds} ms all regions with known locations, resulting in an average of {(float)watch.ElapsedMilliseconds / rdb_map.GetRegionCount()} ms / region.");
			watch.Restart();

			// TODO: Generate zoom level tiles


			// Activate server process
			if (configSource.Configs["Startup"].GetBoolean("ServerMode", Constants.KeepRunningDefault)) {
				LOG.Info("Activating server, listening for region updates.");
				RestApi.RestAPI.StartHost(UpdateRegionDelegate, MapRulesDelegate, CheckAPIKeyDelegate, useSSL:false); // TODO: make SSL an option.  Not really needed since servers all should be on a private network, but...

				while (true) {
					// Just spin.
				}
			}
		}

		private static RestApi.RulesModel MapRulesDelegate(string uuid = null) { // TODO: MapRulesDelegate
			return new RestApi.RulesModel();
		}

		private static void UpdateRegionDelegate(string uuid, RestApi.ChangeInfo changeData) { // TODO: UpdateRegionDelegate
			
		}

		private static bool CheckAPIKeyDelegate(string apiKey, string uuid) { // TODO: CheckAPIKeyDelegate
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
