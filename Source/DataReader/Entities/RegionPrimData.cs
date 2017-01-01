// RegionTerrainData.cs
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

namespace DataReader {
	public struct RegionPrimData {
		public int ObjectFlags { get; set; }

		public int State { get; set; }

		public double PositionX  { get; set; }

		public double PositionY { get; set; }

		public double PositionZ { get; set; }

		public double GroupPositionX { get; set; }

		public double GroupPositionY { get; set; }

		public double GroupPositionZ { get; set; }

		public double RotationX { get; set; }

		public double RotationY { get; set; }

		public double RotationZ { get; set; }

		public double RotationW { get; set; }

		public double ScaleX { get; set; }

		public double ScaleY { get; set; }

		public double ScaleZ { get; set; }

		public double RootRotationX { get; set; }

		public double RootRotationY { get; set; }

		public double RootRotationZ { get; set; }

		public double RootRotationW { get; set; }

		public byte[] Texture { get; set; }

	}
}


