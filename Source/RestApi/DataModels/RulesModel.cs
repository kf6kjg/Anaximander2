// RulesModel.cs
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

namespace RestApi {
	public class RulesModel {
		public GeneralRulesModel Info { get; set; }

		// The below are used to set a special flag in the region that will be checked after the DB write if ValidatedDBUpdate is set.
		// If that check passes or AnyDBUpdate is set, then the call to Anax is sent.
		public uint? ObjectFlagMask { get; set; }
		public float? PartMinScale { get; set; }
		public float? PartMaxScale { get; set; }
		public OffRegionPrimHandling OffRegionPrims { get; set; } = OffRegionPrimHandling.All;
		public float? PartMinAltitude { get; set; }
		public float? PartMaxAltitude { get; set; }
		public AltitudeReference? PartAltitudeReference { get; set; }
		public IEnumerable<uint> PCodesToIgnore { get; set; }
}

	public class GeneralRulesModel {
		public Uri PushNotifyUri { get; set; }
		public IEnumerable<PushNotifyOn> PushNotifyEvents { get; set; }
	}

	public enum PushNotifyOn {
		AnyDBUpdate,
		ValidatedPrimDBUpdate,
		TerrainUpdate
	}

	public enum OffRegionPrimHandling {
		None,
		CenterInRegion,
		OBBCornerInRegion,
		All
	}

	public enum AltitudeReference {
		Region,
		Terrain,
		Water
	}
}

