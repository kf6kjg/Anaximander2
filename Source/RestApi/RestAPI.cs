// RestAPI.cs
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
using System.Collections;
using System.Reflection;
using log4net;
using Nancy;

namespace RestApi {
	public class RestAPI : NancyModule {
		private static readonly ILog LOG = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

		public delegate void UpdateRegionDelegate(string uuid);
		public delegate IEnumerable GetMapRulesDelegate(string uuid = null);

		private static UpdateRegionDelegate _updateRegionDelegate;
		private static GetMapRulesDelegate _getMapRulesDelegate;
		private static Nancy.Hosting.Self.NancyHost host;

		public static void StartHost(UpdateRegionDelegate update, GetMapRulesDelegate rules, string domain = "localhost", uint port = 6473, bool useSSL = true) {
			_updateRegionDelegate = update;
			_getMapRulesDelegate = rules;

			var protocol = useSSL ? "https" : "http";

			host = new Nancy.Hosting.Self.NancyHost(new Uri($"{protocol}://{domain}:{port}"));
			host.Start();
		}

		public RestAPI() {
			LOG.Debug("[REST_API] Starting up.");

			Get["/test"] = _ => {
				LOG.Debug("[REST_API] test called.");

				return (Response) "OK";
			};
		}
	}
}
