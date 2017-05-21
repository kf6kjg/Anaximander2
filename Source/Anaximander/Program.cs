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
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Chattel;
using DataReader;
using log4net;
using log4net.Config;
using Nini.Config;

namespace Anaximander {
	class Application {
		private static readonly ILog LOG = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

		private static readonly string EXECUTABLE_DIRECTORY = Path.GetDirectoryName(Assembly.GetEntryAssembly().CodeBase.Replace("file:/", String.Empty));

		private static readonly string DEFAULT_INI_FILE = "Anaximander.ini";

		private static readonly string COMPILED_BY = "?mono?"; // Replaced during automatic packaging.

		private static IConfigSource _configSource;

		private static RDBMap _rdbMap;

		private static TileGenerator _tileGenerator;

		private static TileImageWriter _tileWriter;

		public static int Main(string[] args) {
			// First line, hook the appdomain to the crash reporter
			// Analysis disable once RedundantDelegateCreation // The "new" is required.
			AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(CurrentDomain_UnhandledException);

			var watch = System.Diagnostics.Stopwatch.StartNew();

			// Add the arguments supplied when running the application to the configuration
			var configSource = new ArgvConfigSource(args);
			_configSource = configSource;

			// Commandline switches
			configSource.AddSwitch("Startup", "inifile");
			configSource.AddSwitch("Startup", "logconfig");
			configSource.AddSwitch("Startup", "MaxParallism", "p");
			configSource.AddSwitch("Startup", "ServerMode");

			var startupConfig = _configSource.Configs["Startup"];

			// Configure Log4Net
			var logConfigFile = startupConfig.GetString("logconfig", String.Empty);
			if (string.IsNullOrEmpty(logConfigFile)) {
				XmlConfigurator.Configure();
				LogBootMessage();
				LOG.Info("[MAIN] Configured log4net using ./Anaximander.exe.config as the default.");
			}
			else {
				XmlConfigurator.Configure(new FileInfo(logConfigFile));
				LogBootMessage();
				LOG.Info($"[MAIN] Configured log4net using \"{logConfigFile}\" as configuration file.");
			}

			// Configure nIni aliases and localles
			Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("en-US", true);

			configSource.Alias.AddAlias("On", true);
			configSource.Alias.AddAlias("Off", false);
			configSource.Alias.AddAlias("True", true);
			configSource.Alias.AddAlias("False", false);
			configSource.Alias.AddAlias("Yes", true);
			configSource.Alias.AddAlias("No", false);

			// Read in the ini file
			ReadConfigurationFromINI(configSource);

			LOG.Info($"[MAIN] Configured for max degree of parallelism of {startupConfig.GetInt("MaxParallism", Constants.MaxDegreeParallism)}");

			Texture.Initialize(new ChattelReader(configSource));

			watch.Stop();
			LOG.Info($"[MAIN] Read configuration in {watch.ElapsedMilliseconds} ms.");
			watch.Restart();

			// Load the RDB map
			try {
				var rdb_map = new RDBMap(configSource);
				_rdbMap = rdb_map;
			}
			catch (DatabaseException e) {
				LOG.Error($"[MAIN] Unable to continue without database connection. Aborting.", e);

				return 1;
			}

			watch.Stop();
			LOG.Info($"[MAIN] Loaded region DB in {watch.ElapsedMilliseconds} ms for a total of {_rdbMap.GetRegionCount()} regions, resulting in an average of {(float)watch.ElapsedMilliseconds / _rdbMap.GetRegionCount()} ms / region.");
			watch.Restart();

			/* Issues to watch for:
			 * Region delete - The DBA will need to actually remove the estate record to cause a map tile delete.
			 * TODO: Tile image read during write - The web server could attempt to read a file while the file is being written.
			 *  - Possible solution: write to a random filename then try { mv rndname to finalname with overwrite } catch { try again later for a max of N times }
			 *    This should provide as much atomicity as possible, and allow anything that's blocking access to be bypassed via time delay. Needs to just fail under exceptions that indicate always-fail conditions.
			 */

			{
				LOG.Debug("Initializing writer and generator.");
				_tileWriter = new TileImageWriter(configSource);
				_tileGenerator = new TileGenerator(configSource);

				LOG.Debug("Writing ocean tile.");
				// Generate & replace ocean tile
				using (var ocean_tile = _tileGenerator.GenerateOceanTile()) {
					_tileWriter.WriteOceanTile(ocean_tile.Bitmap);
				}

				LOG.Debug("Generating a full batch of region tiles.");
				// Generate region tiles - all existing are nearly guaranteed to be out of date.
				var options = new ParallelOptions { MaxDegreeOfParallelism = startupConfig.GetInt("MaxParallism", Constants.MaxDegreeParallism) }; // -1 means full parallel.  1 means non-parallel.
				Parallel.ForEach(_rdbMap.GetRegionUUIDsAsStrings(), options, (region_id) => {
					var oldPriority = Thread.CurrentThread.Priority;
					Thread.CurrentThread.Priority = ThreadPriority.BelowNormal;

					//foreach(var region_id in rdb_map.GetRegionUUIDsAsStrings()) {
					UpdateRegionTile(region_id);

					Thread.CurrentThread.Priority = oldPriority;
				});

				watch.Stop();
				LOG.Info($"[MAIN] Created full res map tiles in {watch.ElapsedMilliseconds} ms all regions with known locations, resulting in an average of {(float)watch.ElapsedMilliseconds / _rdbMap.GetRegionCount()} ms / region.");
				watch.Restart();


				// Generate zoom level tiles.
				// Just quickly build the tile tree so that lookups of the super tiles can be done.

				var superGen = new SuperTileGenerator(configSource, _rdbMap);

				superGen.PreloadTileTrees(_rdbMap.GetRegionUUIDsAsStrings());

				watch.Stop();
				LOG.Info($"[MAIN] Preloaded tile tree in {watch.ElapsedMilliseconds} ms.");
				watch.Restart();


				// Remove all tiles that do not have a corresponding entry in the map.
				_tileWriter.RemoveDeadTiles(_rdbMap, superGen.AllNodesById);

				watch.Stop();
				LOG.Info($"[MAIN] Removed all old tiles in {watch.ElapsedMilliseconds} ms.");
				watch.Restart();


				// Actually generate the zoom level tiles.
				superGen.GeneratePreloadedTree();

				watch.Stop();
				LOG.Info($"[MAIN] Created all super tiles in {watch.ElapsedMilliseconds} ms.");
				//watch.Restart();
			}

			// Activate server process
			if (startupConfig.GetBoolean("ServerMode", Constants.KeepRunningDefault)) {
				var server_config = configSource.Configs["Server"];

				var domain = server_config?.GetString("UseSSL", Constants.ServerDomain) ?? Constants.ServerDomain;
				var port = (uint)(server_config?.GetInt("UseSSL", Constants.ServerPort) ?? Constants.ServerPort);
				var useSSL = server_config?.GetBoolean("UseSSL", Constants.ServerUseSSL) ?? Constants.ServerUseSSL;

				var protocol = useSSL ? "https" : "http";
				LOG.Info($"[MAIN] Activating server on '{protocol}://{domain}:{port}', listening for region updates.");

				RestApi.RestAPI.StartHost(
					UpdateRegionDelegate,
					MapRulesDelegate,
					CheckAPIKeyDelegate,
					domain,
					port,
					useSSL
				);

				while (true) {
					// Just spin.
					// TODO: swap out for a wait handle based approach. See http://stackoverflow.com/a/12367882/7363787
				}
			}

			// I don't care what's still connected or keeping things running, it's time to die!
			Environment.Exit(0);
			return 0;
		}

		private static RestApi.RulesModel MapRulesDelegate(string uuid = null) {
			var server_config = _configSource.Configs["Server"];

			var domain = server_config?.GetString("Domain", Constants.ServerDomain) ?? Constants.ServerDomain;
			var port = (uint)(server_config?.GetInt("Port", Constants.ServerPort) ?? Constants.ServerPort);
			var useSSL = server_config?.GetBoolean("UseSSL", Constants.ServerUseSSL) ?? Constants.ServerUseSSL;

			var protocol = useSSL ? "https" : "http";
			var rules = new RestApi.RulesModel {
				Info = new RestApi.GeneralRulesModel {
					PushNotifyUri = new Uri($"{protocol}://{domain}:{port}"),
					PushNotifyEvents = new List<RestApi.PushNotifyOn> {
						RestApi.PushNotifyOn.ValidatedPrimDBUpdate,
						RestApi.PushNotifyOn.TerrainUpdate,
					},
				},
			};

			// TODO: hook up the filters.  Takes config entries.

			return rules;
		}

		private static void UpdateRegionDelegate(string uuid, RestApi.ChangeInfo changeData) {
			var watch = System.Diagnostics.Stopwatch.StartNew();
			var redraw = false;

			if (_rdbMap.GetRegionByUUID(uuid) != null) {
				foreach (var change in changeData.Changes) {
					switch (change) {
						case RestApi.ChangeCategory.RegionStart:
							// Get all new data.
							redraw = _rdbMap.CreateRegion(uuid);
						break;
						case RestApi.ChangeCategory.RegionStop:
							// RegionStop just means redraw but no need to update source data.
							redraw = true;
						break;
						case RestApi.ChangeCategory.TerrainElevation:
						case RestApi.ChangeCategory.TerrainTexture:
							_rdbMap.UpdateRegionTerrainData(uuid);
							redraw = true;
						break;
						case RestApi.ChangeCategory.Prim:
							_rdbMap.UpdateRegionPrimData(uuid);
							redraw = true;
						break;
					}
				}
			}
			else { // New region, maybe.
				redraw = _rdbMap.CreateRegion(uuid);
			}

			if (redraw) {
				UpdateRegionTile(uuid);

				var superGen = new SuperTileGenerator(_configSource, _rdbMap);

				// Only update that portion of the tree that's affected by the change.
				superGen.PreloadTileTrees(new string[] { uuid });
				superGen.GeneratePreloadedTree();

				// Time for cleanup: make sure that we only have what we need.
				superGen.PreloadTileTrees(_rdbMap.GetRegionUUIDsAsStrings());
				_tileWriter.RemoveDeadTiles(_rdbMap, superGen.AllNodesById);

				watch.Stop();
				LOG.Info($"[UpdateRegionDelegate] Rebuilt region id '{uuid}', parent tree, and did filesystem cleanup in {watch.ElapsedMilliseconds} ms.");
			}
			else {
				watch.Stop();
				LOG.Info($"[UpdateRegionDelegate] Got request to rebuild region id '{uuid}', but there was nothing to do.");
			}
		}

		private static bool CheckAPIKeyDelegate(string apiKey, string uuid) {
			return _configSource.Configs["Server"]
				?.GetString("APIKeys", string.Empty)
				.Split(',')
				.Select(key => key.Trim())
				.Contains(apiKey)
				?? true;
		}

		private static void UpdateRegionTile(string region_id) {
			var defaultTiles = _configSource.Configs["DefaultTiles"];
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

			var region = _rdbMap.GetRegionByUUID(region_id);

			if (region.IsListedAsOnline()) {
				// Assume that during bootup the tile is out of date and rebuild everything.

				if (crashedTechnique == RegionErrorDisplayTechnique.IGNORE || region.IsCurrentlyAccessable()) {
					LOG.Info($"Generating a full region tile for {region_id}.");
					using (var tile_image = _tileGenerator.RenderRegionTile(region)) {
						_tileWriter.WriteTile((int)region.locationX, (int)region.locationY, 1, region_id, tile_image.Bitmap);
					}
				}
				else {
					if (crashedTechnique == RegionErrorDisplayTechnique.IMAGE) {
						LOG.Info($"Generating a crashed-style imaged based region tile for {region_id} as the DB reports it as online, but the region itself is not responding.");
						var filename = defaultTiles?.GetString("CrashedRegionImage", Constants.CrashedRegionImage) ?? Constants.CrashedRegionImage;

						_tileWriter.WriteTile((int)region.locationX, (int)region.locationY, 1, region_id, filename);
					}
					else if (crashedTechnique == RegionErrorDisplayTechnique.COLOR) {
						LOG.Info($"Generating a crashed-style color based region tile for {region_id} as the DB reports it as online, but the region itself is not responding.");
						var colorR = defaultTiles?.GetInt("CrashedRegionRed", Constants.CrashedRegionColor.R) ?? Constants.CrashedRegionColor.R;
						var colorG = defaultTiles?.GetInt("CrashedRegionGreen", Constants.CrashedRegionColor.G) ?? Constants.CrashedRegionColor.G;
						var colorB = defaultTiles?.GetInt("CrashedRegionBlue", Constants.CrashedRegionColor.B) ?? Constants.CrashedRegionColor.B;

						using (var tile_image = _tileGenerator.GenerateConstantColorTile(Color.FromArgb(colorR, colorG, colorB))) {
							_tileWriter.WriteTile((int)region.locationX, (int)region.locationY, 1, region_id, tile_image.Bitmap);
						}
					}
					else {
						LOG.Debug($"No render of crashed regions enabled. {region_id} is reported by the DB as online, but the region itself is not responding.");
					}
				}
			}
			else if (offlineTechnique != RegionErrorDisplayTechnique.IGNORE) {
				LOG.Debug($"Region {region_id} was reported by the DB to be offline.");
				// Go looking for the backup technique to find the coordinates of a region that has gone offline.
				var folderinfo = _configSource.Configs["Folders"];
				var tilepath = folderinfo?.GetString("MapTilePath", Constants.MapTilePath) ?? Constants.MapTilePath;

				var coords = string.Empty;
				try {
					coords = File.ReadAllText(Path.Combine(tilepath, Constants.ReverseLookupPath, region_id));
				}
				catch (SystemException) { // All IO errors just mean skippage.
					LOG.Info($"Offline region {region_id} has not been seen before so the coordinates cannot be found and no tile will be rendered.");
				}

				if (!string.IsNullOrWhiteSpace(coords)) { // Backup technique has succeeded, do as specified in config.
					var coordsList = coords.Split(',').Select(coord => int.Parse(coord)).ToArray();

					_rdbMap.UpdateRegionLocation(region_id, coordsList[0], coordsList[1]);

					if (offlineTechnique == RegionErrorDisplayTechnique.IMAGE) {
						LOG.Info($"Generating an offline-style imaged based region tile for {region_id} as the DB reports it as offline.");
						var filename = defaultTiles?.GetString("OfflineRegionImage", Constants.OfflineRegionImage) ?? Constants.OfflineRegionImage;

						_tileWriter.WriteTile((int)region.locationX, (int)region.locationY, 1, region_id, filename);
					}
					else if (offlineTechnique == RegionErrorDisplayTechnique.COLOR) {
						LOG.Info($"Generating an offline-style color based region tile for {region_id} as the DB reports it as offline.");
						var colorR = defaultTiles?.GetInt("OfflineRegionRed", Constants.OfflineRegionColor.R) ?? Constants.OfflineRegionColor.R;
						var colorG = defaultTiles?.GetInt("OfflineRegionGreen", Constants.OfflineRegionColor.G) ?? Constants.OfflineRegionColor.G;
						var colorB = defaultTiles?.GetInt("OfflineRegionBlue", Constants.OfflineRegionColor.B) ?? Constants.OfflineRegionColor.B;

						using (var tile_image = _tileGenerator.GenerateConstantColorTile(Color.FromArgb(colorR, colorG, colorB))) {
							_tileWriter.WriteTile((int)region.locationX, (int)region.locationY, 1, region_id, tile_image.Bitmap);
						}
					}
				}
			}
			else {
				LOG.Debug($"No render of offline regions enabled. {region_id} is reported by the DB as offline.");
			}
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
				iniFileName = Path.Combine(EXECUTABLE_DIRECTORY, iniFileName);

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

		private static void LogBootMessage() {
			LOG.Info("* * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * *");
			LOG.Info($"Anaximander2 v{Assembly.GetExecutingAssembly().GetName().Version.ToString()} {COMPILED_BY}");
			var bitdepth = Environment.Is64BitOperatingSystem ? "64bit" : "unknown or 32bit";
			LOG.Info($"OS: {Environment.OSVersion.VersionString} {bitdepth}");
			LOG.Info($"Commandline: {Environment.CommandLine}");
			LOG.Info($"CWD: {Environment.CurrentDirectory}");
			LOG.Info($"Machine: {Environment.MachineName}");
			LOG.Info($"Processors: {Environment.ProcessorCount}");
			LOG.Info($"User: {Environment.UserDomainName}/{Environment.UserName}");
			var isMono = Type.GetType("Mono.Runtime") != null;
			LOG.Info("Interactive shell: " + (Environment.UserInteractive ? "yes" : isMono ? "indeterminate" : "no"));
			LOG.Info("* * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * *");
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
					err_reporter.StartInfo.FileName = Path.Combine(EXECUTABLE_DIRECTORY, "RollbarCrashReporter.exe");
					err_reporter.StartInfo.WorkingDirectory = EXECUTABLE_DIRECTORY;
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
