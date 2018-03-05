// TileImageWriter.cs
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
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Nini.Config;

namespace Anaximander {
	public class TileImageWriter {
		private static readonly log4net.ILog LOG = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

		private readonly ImageFormats _imageFormat;
		private readonly DirectoryInfo _tileFolder;
		private readonly DirectoryInfo _reverseLookupFolder;
		private readonly DirectoryInfo _rawImageFolder;
		private readonly string _tileNameFormat;
		private readonly string _oceanTileName;

		private readonly ParallelOptions PARALLELISM_OPTIONS;

		public TileImageWriter(IConfigSource config) {
			var tileinfo = config.Configs["MapTileInfo"];

			var format = tileinfo?.GetString("ImageFormat", Constants.ImageFormat.ToString()) ?? Constants.ImageFormat.ToString();
			if (!Enum.TryParse(format, out _imageFormat)) {
				LOG.Error($"Invalid image format '{format}' in configuration.");
			}

			_tileNameFormat = tileinfo?.GetString("TileNameFormat", Constants.TileNameFormat) ?? Constants.TileNameFormat;

			_oceanTileName = tileinfo?.GetString("OceanTileName", Constants.OceanTileName) ?? Constants.OceanTileName;

			var folderinfo = config.Configs["Folders"];

			var tilepath = folderinfo?.GetString("MapTilePath", Constants.MapTilePath) ?? Constants.MapTilePath;
			try {
				_tileFolder = Directory.CreateDirectory(tilepath);
			}
			catch (Exception e) {
				LOG.Fatal($"Error creating folder '{tilepath}': {e}");
				throw;
			}

			try {
				_reverseLookupFolder = Directory.CreateDirectory(Path.Combine(_tileFolder.FullName, Constants.ReverseLookupPath));
			}
#pragma warning disable RECS0022 // A catch clause that catches System.Exception and has an empty body
			catch { // Don't care if this fails.
#pragma warning restore RECS0022 // A catch clause that catches System.Exception and has an empty body
			}

			var startupConfig = config.Configs["Startup"];

			if (startupConfig.GetBoolean("ServerMode", Constants.KeepRunningDefault)) {
				try {
					_rawImageFolder = Directory.CreateDirectory(Path.Combine(_tileFolder.FullName, Constants.RawImagePath));
				}
#pragma warning disable RECS0022 // A catch clause that catches System.Exception and has an empty body
				catch { // Don't care if this fails.
#pragma warning restore RECS0022 // A catch clause that catches System.Exception and has an empty body
				}
			}

			PARALLELISM_OPTIONS = new ParallelOptions { MaxDegreeOfParallelism = startupConfig.GetInt("MaxParallism", Constants.MaxDegreeParallism) }; // -1 means full parallel.  1 means non-parallel.
		}

		public DateTimeOffset? GetTileModDate(int locationX, int locationY, int locationZ) {
			try {
				return File.GetLastWriteTimeUtc(Path.Combine(_tileFolder.FullName, PrepareTileFilename(locationX, locationY, locationZ)));
			}
			catch (FileNotFoundException) {
				// Don't care.
			}

			return null;
		}

		/// <summary>
		/// Writes the tile to disk with the formatting specified in the INI file, and in the config-file specified folder.
		/// Also writes a file with the specified UUID into a "by_uuid" folder under the config-file specified folder for later recall of previous coordinates.
		/// </summary>
		/// <param name="locationX">Region location x.</param>
		/// <param name="locationY">Region location y.</param>
		/// <param name="locationZ">Region location z.</param>
		/// <param name="regionId">Region UUID.</param>
		/// <param name="bitmap">Bitmap of the region.</param>
		public void WriteTile(int locationX, int locationY, int locationZ, Guid regionId, Bitmap bitmap) {
			if (locationZ == 1 && _reverseLookupFolder != null && Guid.Empty != regionId) { // Only if a region.
				LOG.Debug($"Writing reverse lookup file for {regionId}.");
				try { // Store the reverse lookup file.
					File.WriteAllText(Path.Combine(_reverseLookupFolder.FullName, regionId.ToString()), $"{locationX},{locationY}");
				}
				// Analysis disable once EmptyGeneralCatchClause
#pragma warning disable RECS0022 // A catch clause that catches System.Exception and has an empty body
				catch { // I don't care if this fails for some reason.
#pragma warning restore RECS0022 // A catch clause that catches System.Exception and has an empty body
				}
			}
			WriteTile(PrepareTileFilename(locationX, locationY, locationZ), bitmap);
		}

		/// <summary>
		/// Copies the specified tile with the formatting specified in the INI file, and in the config-file specified folder.
		/// Also writes a file with the specified UUID into a "by_uuid" folder under the config-file specified folder for later recall of previous coordinates.
		/// </summary>
		/// <param name="locationX">Region location x.</param>
		/// <param name="locationY">Region location y.</param>
		/// <param name="locationZ">Region location z.</param>
		/// <param name="regionId">Region UUID.</param>
		/// <param name="file">File to be copied.</param>
		public void WriteTile(int locationX, int locationY, int locationZ, Guid regionId, string file) {
			if (locationZ == 1 && _reverseLookupFolder != null && Guid.Empty != regionId) { // Only if a region.
				LOG.Debug($"Writing reverse lookup file for {regionId}, copying from {file}.");
				try { // Store the reverse lookup file.
					File.WriteAllText(Path.Combine(_reverseLookupFolder.FullName, regionId.ToString()), $"{locationX},{locationY}");
				}
				// Analysis disable once EmptyGeneralCatchClause
#pragma warning disable RECS0022 // A catch clause that catches System.Exception and has an empty body
				catch { // I don't care if this fails for some reason.
#pragma warning restore RECS0022 // A catch clause that catches System.Exception and has an empty body
				}
			}
			LOG.Debug($"Writing tile for {regionId}, copying from {file}.");
			File.Copy(file, Path.Combine(_tileFolder.FullName, PrepareTileFilename(locationX, locationY, locationZ)));
		}

		/// <summary>
		/// Writes an uncompressed raw format in a "raw" folder under the config-file specified folder for later recall during super tile regeneration.
		/// </summary>
		/// <param name="locationX">Region location x.</param>
		/// <param name="locationY">Region location y.</param>
		/// <param name="locationZ">Region location z.</param>
		/// <param name="bitmap">Bitmap of the region.</param>
		public void WriteRawTile(int locationX, int locationY, int locationZ, Bitmap bitmap) {
			var filename = $"{TileTreeNode.MakeId(locationX, locationY, locationZ)}.tiff";
			LOG.Debug($"Writing raw image file {filename} for later use.");
			try {
				bitmap.Save(Path.Combine(_rawImageFolder.FullName, filename), ImageFormat.Tiff);
			}
			catch (Exception e) {
				LOG.Error($"Error writing map image tile to disk: {e}");
			}
		}

		/// <summary>
		/// Writes the tile to disk named as the ocean tile, and in the config-file specified folder.
		/// </summary>
		/// <param name="bitmap">Bitmap of the region.</param>
		public void WriteOceanTile(Bitmap bitmap) {
			WriteTile(PrepareTileFilename(_oceanTileName), bitmap);
		}

		/// <summary>
		/// Writes the tile to disk in the config-file specified folder.  Prefer the use of the more specific overloads if they fit.
		/// </summary>
		/// <param name="filename">Filename.</param>
		/// <param name="bitmap">Bitmap.</param>
		private void WriteTile(string filename, Bitmap bitmap) {
			var format = ImageFormat.Jpeg;
			if (_imageFormat == ImageFormats.JPEG) {
				format = ImageFormat.Jpeg;
			}
			else if (_imageFormat == ImageFormats.PNG) {
				format = ImageFormat.Png;
			}

			LOG.Debug($"Writing image file {filename}.");
			try {
				bitmap.Save(Path.Combine(_tileFolder.FullName, filename), format);
			}
			catch (Exception e) {
				LOG.Error($"Error writing map image tile to disk: {e}");
			}
		}

		private string PrepareTileFilename(int locationX, int locationY, int locationZ) {
			return PrepareTileFilename(_tileNameFormat.Replace("{X}", locationX.ToString()).Replace("{Y}", locationY.ToString()).Replace("{Z}", locationZ.ToString()));
		}

		private string PrepareTileFilename(string filename) {
			string extension = string.Empty;
			switch (_imageFormat) {
				case ImageFormats.JPEG:
					extension = ".jpg";
					break;
				case ImageFormats.PNG:
					extension = ".png";
					break;
			}

			return filename + extension;
		}

		// This really doesn't belong here, but where should it be?
		public void RemoveDeadTiles(DataReader.RDBMap rdbMap, IEnumerable<string> superTiles) {
			LOG.Info("Checking for base region tiles that need to be removed.");

			var files = Directory.EnumerateFiles(_tileFolder.FullName);

			var order = string.Join(string.Empty, _tileNameFormat.Split('{')
				.Where(str => str.Contains("}"))
				.Select(str => str[0])
			);
			var region_tile_regex = new Regex("/" + PrepareTileFilename(Regex.Replace(_tileNameFormat, "{[XYZ]}", "([0-9]+)")) + "$");

			var counter = 0;

			Parallel.ForEach(files, PARALLELISM_OPTIONS, (filename) => {
				var oldPriority = Thread.CurrentThread.Priority;
				Thread.CurrentThread.Priority = ThreadPriority.BelowNormal;

				var match = region_tile_regex.Match(filename);

				if (!match.Success) {
					return;
				}

				int x, y, z;
				switch (order) {
					case "XYZ":
						x = int.Parse(match.Groups[1].Value);
						y = int.Parse(match.Groups[2].Value);
						z = int.Parse(match.Groups[3].Value);
						break;
					case "XZY":
						x = int.Parse(match.Groups[1].Value);
						z = int.Parse(match.Groups[2].Value);
						y = int.Parse(match.Groups[3].Value);
						break;
					case "YXZ":
						y = int.Parse(match.Groups[1].Value);
						x = int.Parse(match.Groups[2].Value);
						z = int.Parse(match.Groups[3].Value);
						break;
					case "YZX":
						y = int.Parse(match.Groups[1].Value);
						z = int.Parse(match.Groups[2].Value);
						x = int.Parse(match.Groups[3].Value);
						break;
					case "ZXY":
						z = int.Parse(match.Groups[1].Value);
						x = int.Parse(match.Groups[2].Value);
						y = int.Parse(match.Groups[3].Value);
						break;
					//case "ZYX":
					default:
						z = int.Parse(match.Groups[1].Value);
						y = int.Parse(match.Groups[2].Value);
						x = int.Parse(match.Groups[3].Value);
						break;
				}

				// Delete all region tiles for regions that have been explicitly removed from the DB.  For the new guy: this does not remove regions that are simply just offline.
				if (z == 1) {
					DataReader.Region region = null;

					try {
						region = rdbMap.GetRegionByLocation(x, y);
					}
					catch (KeyNotFoundException) {
						// Don't care
					}

					if (region == null) {
						// Remove the region tile.
						try {
							File.Delete(filename);
							counter++;
						}
						catch (IOException) {
							// File was in use.  Skip for now.
							LOG.Warn($"Attempted removal of {filename} failed as file was in-use.");
						}
					}
				}
				else {
					// Check the super tiles for posible removals.  Not a high likelyhood on a growing grid, but will happen in all other cases.
					var key = TileTreeNode.MakeId(x, y, z);
					if (!superTiles.Contains(key)) {
						try {
							File.Delete(filename);
							counter++;
						}
						catch (IOException) {
							// File was in use.  Skip for now.
							LOG.Warn($"Attempted removal of {filename} failed as file was in-use.");
						}
					}
				}

				Thread.CurrentThread.Priority = oldPriority;
			});

			if (counter > 0) {
				LOG.Info($"Deleted {counter} region tiles for removed regions and consequent super tiles.");
			}


			// Go clean up the uuid reverse lookup folder.
			if (_reverseLookupFolder != null) {
				counter = 0;

				var uuid_regex = new Regex("/[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$");
				files = Directory.EnumerateFiles(_reverseLookupFolder.FullName);
				Parallel.ForEach(files, PARALLELISM_OPTIONS, (filename) => {
					var match = uuid_regex.Match(filename);

					if (!match.Success) {
						return;
					}

					// Delete all uuid lookup files for regions that have been explicitly removed from the DB.  For the new guy: this does not remove regions that are simply just offline.
					var uuid = Guid.Parse(match.Value.Substring(1));
					if (!rdbMap.GetRegionUUIDs().Contains(uuid)) {
						// Remove the file.
						try {
							File.Delete(filename);
							counter++;
						}
						catch (IOException) {
							// File was in use.  Skip for now.
							LOG.Warn($"Attempted removal of {filename} failed as file was in-use.");
						}
					}
				});

				if (counter > 0) {
					LOG.Info($"Deleted {counter} uuid lookup files for removed regions.");
				}
			}

			// Go clean up the raw image folder.
			if (_rawImageFolder != null) {
				counter = 0;

				var raw_image_regex = new Regex("/[^/]+.tiff$");
				files = Directory.EnumerateFiles(_rawImageFolder.FullName);
				Parallel.ForEach(files, PARALLELISM_OPTIONS, (filename) => {
					var match = raw_image_regex.Match(filename);

					if (!match.Success) {
						return;
					}

					// Delete all uuid lookup files for regions that have been explicitly removed from the DB.  For the new guy: this does not remove regions that are simply just offline.
					var key = match.Value.Substring(1, match.Value.Length - 6);
					if (!superTiles.Contains(key)) {
						// Remove the file.
						try {
							File.Delete(filename);
							counter++;
						}
						catch (IOException) {
							// File was in use.  Skip for now.
							LOG.Warn($"Attempted removal of {filename} failed as file was in-use.");
						}
					}
				});

				if (counter > 0) {
					LOG.Info($"Deleted {counter} raw image files for removed super tiles.");
				}
			}
		}

		[System.Obsolete("These load methods should be in their own 'reader' class")]
		public Bitmap LoadTile(int locationX, int locationY, int locationZ) {
			var image_path = Path.Combine(_tileFolder.FullName, PrepareTileFilename(locationX, locationY, locationZ));

			try {
				return new Bitmap(Image.FromFile(image_path));
			}
			catch (Exception e) {
				LOG.Error($"Error reading map image tile from disk: {e}");
			}

			return null;
		}

		[System.Obsolete("These load methods should be in their own 'reader' class")]
		public Bitmap LoadRawTile(int locationX, int locationY, int locationZ) {
			var image_path = Path.Combine(_rawImageFolder.FullName, $"{TileTreeNode.MakeId(locationX, locationY, locationZ)}.tiff");

			try {
				return new Bitmap(Image.FromFile(image_path));
			}
			catch (Exception e) {
				LOG.Warn($"Error reading raw image tile from disk: {e}");
			}

			return null;
		}
	}
}

