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
using System.IO;
using System.Reflection;
using Nini.Config;
using log4net;
using System.Drawing.Imaging;

namespace Anaximander {
	public class TileImageWriter {
		private static readonly ILog LOG = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

		private readonly ImageFormats _imageFormat;
		private readonly DirectoryInfo _tileFolder;
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
		}

		/// <summary>
		/// Writes the tile to disk with the formatting specified in the INI file, and in the config-file specified folder.
		/// </summary>
		/// <param name="locationX">Region location x.</param>
		/// <param name="locationY">Region location y.</param>
		/// <param name="locationZ">Region location z.</param>
		/// <param name="bitmap">Bitmap of the region.</param>
		public void WriteTile(int locationX, int locationY, int locationZ, DirectBitmap bitmap) {
			WriteTile(_tileNameFormat.Replace("{X}", locationX.ToString()).Replace("{Y}", locationY.ToString()).Replace("{Z}", locationZ.ToString()), bitmap);
		}

		/// <summary>
		/// Writes the tile to disk named as the ocean tile, and in the config-file specified folder.
		/// </summary>
		/// <param name="bitmap">Bitmap of the region.</param>
		public void WriteOceanTile(DirectBitmap bitmap) {
			WriteTile(_oceanTileName, bitmap);
		}

		/// <summary>
		/// Writes the tile to disk in the config-file specified folder.  Prefer the use of the more specific overloads if they fit.
		/// </summary>
		/// <param name="filename">Filename.</param>
		/// <param name="bitmap">Bitmap.</param>
		private void WriteTile(string filename, DirectBitmap bitmap) {
			ImageFormat format = ImageFormat.Jpeg;
			string extension = string.Empty;
			switch (_imageFormat) {
				case ImageFormats.JPEG:
					format = ImageFormat.Jpeg;
					extension = ".jpg";
				break;
				case ImageFormats.PNG:
					format = ImageFormat.Png;
					extension = ".png";
				break;
			}

			try {
				bitmap.Bitmap.Save(Path.Combine(_tileFolder.FullName, filename + extension), format);
			}
			catch (Exception e) {
				LOG.Error($"Error writing map image tile to disk: {e}");
			}
		}
	}
}

