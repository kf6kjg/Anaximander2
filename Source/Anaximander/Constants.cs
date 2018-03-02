// Constants.cs
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

namespace Anaximander {
	public static class Constants {
		// Please keep constants sorted by category and name.

		// Operations
		public const bool KeepRunningDefault = false;

		// Startup
		public const int MaxDegreeParallism = -1;

		// Server
		public const bool ServerUseSSL = false;
		public const string ServerDomain = "localhost";
		public const int ServerPort = 6473;

		// Folders
		public const string MapTilePath = "./maptiles";
		public const string RawImagePath = "raw"; // relative to MapTilePath
		public const string ReverseLookupPath = "by_uuid"; // relative to MapTilePath

		// MapTileInfo
		public const string RenderTechnique = "obbrenderer";
		public const ImageFormats ImageFormat = ImageFormats.JPEG;

		public static readonly Color BeachColor = Color.FromArgb(0, 255, 255);
		public static readonly Color OceanColor = Color.FromArgb(0, 0, 255);

		public const string OceanOverlay = "";

		public const string OceanTileName = "ocean";

		public const int PixelScale = 256;

		public const string TileNameFormat = "map-{Z}-{X}-{Y}-objects";

		public const string WaterOverlay = "";

		// TileZooming
		public const int HighestZoomLevel = 8;

		// DefaultTiles
		public const RegionErrorDisplayTechnique OfflineRegion = RegionErrorDisplayTechnique.IGNORE;
		public static readonly Color OfflineRegionColor = Color.FromArgb(0, 0, 0);
		public const string OfflineRegionImage = "./images/offline.jpg";

		public const RegionErrorDisplayTechnique CrashedRegion = RegionErrorDisplayTechnique.IGNORE;
		public static readonly Color CrashedRegionColor = Color.FromArgb(0, 0, 0);
		public const string CrashedRegionImage = "./images/crashed.jpg";
	}

	public enum ImageFormats {
		JPEG,
		PNG,
	}

	public enum RegionErrorDisplayTechnique {
		IGNORE,
		IMAGE,
		COLOR,
	}
}
