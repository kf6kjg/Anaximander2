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
		public static bool KeepRunningDefault = false;

		// Folders
		public static string MapTilePath = "./maptiles";

		// MapTileInfo
		public static ImageFormats ImageFormat = ImageFormats.JPEG;

		public static Color OceanColor = Color.FromArgb(0, 0, 255);
		public static Color BeachColor = Color.FromArgb(0, 255, 255);
		public static string OceanTileName = "ocean";

		public static int PixelScale = 256;

		public static string TileNameFormat = "map-{Z}-{X}-{Y}-objects";

		// TileZooming
		public static int HighestZoomLevel = 8;
	}

	public enum ImageFormats {
		JPEG,
		PNG
	}
}
