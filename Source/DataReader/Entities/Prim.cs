// Prim.cs
//
// Author:
//       Ricky Curtice <ricky@rwcproductions.com>
//
// Copyright (c) 2017 Richard Curtice
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
	public class Prim {

		public int ObjectFlags { get { return _primData.ObjectFlags; } }

		public int State { get { return _primData.State; } }

		public double PositionX  { get { return _primData.PositionX; } }

		public double PositionY { get { return _primData.PositionY; } }

		public double PositionZ { get { return _primData.PositionZ; } }

		public double GroupPositionX { get { return _primData.GroupPositionX; } }

		public double GroupPositionY { get { return _primData.GroupPositionY; } }

		public double GroupPositionZ { get { return _primData.GroupPositionZ; } }

		public double RotationX { get { return _primData.RotationX; } }

		public double RotationY { get { return _primData.RotationY; } }

		public double RotationZ { get { return _primData.RotationZ; } }

		public double RotationW { get { return _primData.RotationW; } }

		public double ScaleX { get { return _primData.ScaleX; } }

		public double ScaleY { get { return _primData.ScaleY; } }

		public double ScaleZ { get { return _primData.ScaleZ; } }

		public double RootRotationX { get { return _primData.RootRotationX; } }

		public double RootRotationY { get { return _primData.RootRotationY; } }

		public double RootRotationZ { get { return _primData.RootRotationZ; } }

		public double RootRotationW { get { return _primData.RootRotationW; } }

		public byte[] Texture { get { return _primData.Texture; } }


		#region Private Properties

		private RegionPrimData _primData;

		#endregion

		#region Constructors

		public Prim(ref RegionPrimData data) {
			_primData = data;
		}

		#endregion
	}
}

