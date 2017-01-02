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
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using log4net;
using Nini.Config;

namespace Anaximander {
	public class TileImageWriter {
		private static readonly ILog LOG = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

		private readonly ImageFormats _imageFormat;
		private readonly DirectoryInfo _tileFolder;
		private readonly DirectoryInfo _reverseLookupFolder = null;
		private readonly string _tileNameFormat;
		private readonly string _oceanTileName;

		public TileImageWriter(IConfigSource config) {
			var tileinfo = config.Configs["MapTileInfo"];

			var format = tileinfo?.GetString("ImageFormat", Constants.ImageFormat.ToString()) ?? Constants.ImageFormat.ToString();
			if (!Enum.TryParse<ImageFormats>(format, out _imageFormat)) {
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
				throw e;
			}

			try {
				_reverseLookupFolder = Directory.CreateDirectory(Path.Combine(_tileFolder.FullName, Constants.ReverseLookupPath));
			}
#pragma warning disable RECS0022 // A catch clause that catches System.Exception and has an empty body
			catch { // Don't care if this fails.
#pragma warning restore RECS0022 // A catch clause that catches System.Exception and has an empty body
			}
		}

		public DateTimeOffset? GetTileModDate(int locationX, int locationY, int locationZ) {
			try {
				return File.GetLastWriteTimeUtc(Path.Combine(_tileFolder.FullName, PrepareTileFilename(locationX, locationY, locationZ)));
			}
			catch (FileNotFoundException) {
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
		public void WriteTile(int locationX, int locationY, int locationZ, string regionId, DirectBitmap bitmap) {
			if (locationZ == 1 && _reverseLookupFolder != null) { // Only if a region.
				try { // Store the reverse lookup file.
					File.WriteAllText(Path.Combine(_reverseLookupFolder.FullName, regionId), $"{locationX},{locationY}");
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
		public void WriteTile(int locationX, int locationY, int locationZ, string regionId, string file) {
			if (locationZ == 1 && _reverseLookupFolder != null) { // Only if a region.
				try { // Store the reverse lookup file.
					File.WriteAllText(Path.Combine(_reverseLookupFolder.FullName, regionId), $"{locationX},{locationY}");
				}
				// Analysis disable once EmptyGeneralCatchClause
#pragma warning disable RECS0022 // A catch clause that catches System.Exception and has an empty body
				catch { // I don't care if this fails for some reason.
#pragma warning restore RECS0022 // A catch clause that catches System.Exception and has an empty body
				}
			}
			File.Copy(file, Path.Combine(_tileFolder.FullName, PrepareTileFilename(locationX, locationY, locationZ)));
		}

		/// <summary>
		/// Writes the tile to disk named as the ocean tile, and in the config-file specified folder.
		/// </summary>
		/// <param name="bitmap">Bitmap of the region.</param>
		public void WriteOceanTile(DirectBitmap bitmap) {
			WriteTile(PrepareTileFilename(_oceanTileName), bitmap);
		}

		/// <summary>
		/// Writes the tile to disk in the config-file specified folder.  Prefer the use of the more specific overloads if they fit.
		/// </summary>
		/// <param name="filename">Filename.</param>
		/// <param name="bitmap">Bitmap.</param>
		private void WriteTile(string filename, DirectBitmap bitmap) {
			ImageFormat format = ImageFormat.Jpeg;
			switch (_imageFormat) {
				case ImageFormats.JPEG:
					format = ImageFormat.Jpeg;
				break;
				case ImageFormats.PNG:
					format = ImageFormat.Png;
				break;
			}

			try {
				bitmap.Bitmap.Save(Path.Combine(_tileFolder.FullName, filename), format);
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
		public void RemoveDeadTiles(DataReader.RDBMap rdbMap) {
			LOG.Info("Checking for base region tiles that need to be removed.");

			var files = Directory.EnumerateFiles(_tileFolder.FullName);

			var order = string.Join(string.Empty, _tileNameFormat.Split('{')
				.Where(str => str.Contains("}"))
				.Select(str => str[0])
			);
			var regex = new Regex("/" + PrepareTileFilename(Regex.Replace(_tileNameFormat, "{[XYZ]}", "([0-9]+)")) + "$");

			var counter = 0;

			#if DEBUG
			var options = new ParallelOptions { MaxDegreeOfParallelism = 1 }; // -1 means full parallel.  1 means non-parallel.

			Parallel.ForEach(files, options, (filename) => {
			#else
			Parallel.ForEach(files, (filename) => {
			#endif
				var match = regex.Match(filename);

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
				if (z == 0) {
					var regionExists = false;

					try {
						rdbMap.GetRegionByLocation(x, y);
						regionExists = true;
					}
					catch(KeyNotFoundException) {
					}

					if (!regionExists) {
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
			});

			// TODO: check the super tiles for posible removals.  Not a high likelyhood on a growing grid, but will happen in all other cases.

			if (counter > 0) {
				LOG.Info($"Deleted {counter} region tiles for removed regions.");
			}
		}

	}
}

