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
using System.Linq;
using System.Reflection;
using log4net;
using Nancy;
using Nancy.ModelBinding;

namespace RestApi {
	public class RestAPI : NancyModule {
		private static readonly ILog LOG = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

		public delegate void UpdateRegionDelegate(string uuid, ChangeInfo changeData);
		public delegate RulesModel GetMapRulesDelegate(string uuid = null);
		public delegate bool CheckAPIKeyDelegate(string apiKey, string uuid);

		private static UpdateRegionDelegate _updateRegionDelegate;
		private static GetMapRulesDelegate _getMapRulesDelegate;
		private static CheckAPIKeyDelegate _checkAPIKeyDelegate;
		private static Nancy.Hosting.Self.NancyHost host;

		private static readonly BindingConfig bindingConfig = new BindingConfig();

		public static void StartHost(UpdateRegionDelegate update, GetMapRulesDelegate rules, CheckAPIKeyDelegate keychecker, string domain = "localhost", uint port = 6473, bool useSSL = true) {
			_updateRegionDelegate = update;
			_getMapRulesDelegate = rules;
			_checkAPIKeyDelegate = keychecker;

			var protocol = useSSL ? "https" : "http";

			bindingConfig.BodyOnly = true;

			host = new Nancy.Hosting.Self.NancyHost(new Uri($"{protocol}://{domain}:{port}"));
			host.Start();
		}

		public static void StopHost() {
			host.Stop();
		}

		public RestAPI() {
			Get["/test"] = _ => {
				LOG.Debug($"[REST_API] test called by IP '{Request.UserHostAddress}'.");

				return (Response) "OK";
			};

			Post["/updateregion/{uuid}"] = parameters => {
				//this.RequiresHttps();
				LOG.Debug($"[REST_API] Update region called by IP '{Request.UserHostAddress}' for region id {parameters.uuid}.");

				if (_updateRegionDelegate == null || _checkAPIKeyDelegate == null) {
					return (Response) HttpStatusCode.BadGateway;
				}

				// Read the API key and verify.
				var authtoken = Request.Headers["Authorization"]?.FirstOrDefault();
				if (!_checkAPIKeyDelegate.EndInvoke(_checkAPIKeyDelegate.BeginInvoke(authtoken, parameters.uuid, null, null))) {
					LOG.Info($"[REST_API] UpdateRegion: By IP '{Request.UserHostAddress}', API key '{authtoken}' failed verification for region ID '{parameters.uuid}'");
					return (Response) HttpStatusCode.Forbidden;
				}

				// Read in the change data object and pass on to delegate.
				var changeData = this.Bind<ChangeInfo>(bindingConfig);
				_updateRegionDelegate.EndInvoke(_updateRegionDelegate.BeginInvoke(parameters.uuid, changeData, null, null));

				return (Response) HttpStatusCode.NoContent;
			};

			Get["/maprules/{uuid?}"] = parameters => {
				if (parameters["uuid"]?.Value == null) {
					LOG.Debug($"[REST_API] Map rules called by IP '{Request.UserHostAddress}'.");
				}
				else {
					LOG.Debug($"[REST_API] Map rules called by IP '{Request.UserHostAddress}' for region id {parameters.uuid}.");
				}

				if (_getMapRulesDelegate == null) {
					return (Response) HttpStatusCode.BadGateway;
				}

				var result = _getMapRulesDelegate.BeginInvoke(parameters["uuid"].Value, null, null);

				return _getMapRulesDelegate.EndInvoke(result);
			};
		}
	}
}
