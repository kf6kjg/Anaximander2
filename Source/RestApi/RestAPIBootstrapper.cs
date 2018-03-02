// RestAPIBootstrapper.cs
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
using System.Reflection;
using System.Text;
using log4net;
using Nancy;
using Nancy.ErrorHandling;

namespace RestApi {
	// Automagically called by the default bootstrapper.
	public class RestAPIBootstrapper : DefaultNancyBootstrapper {
		private static readonly ILog LOG = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

		protected override void ApplicationStartup(Nancy.TinyIoc.TinyIoCContainer container, Nancy.Bootstrapper.IPipelines pipelines) {
			base.ApplicationStartup(container, pipelines);
			Nancy.Json.JsonSettings.PrimitiveConverters.Add(new JsonConvertEnum());

			pipelines.OnError += (context, exception) => {
				LOG.Error($"Unhandled error from '{context.Request.UserHostAddress}' on '{context.Request.Url}': {exception.Message}", exception);

				var response = new Response();
				response.StatusCode = HttpStatusCode.InternalServerError;
				response.ContentType = "application/json";
				response.Contents = (obj) => {
					var output = Encoding.UTF8.GetBytes("{\"error\": \"Server error.  This error has been sent to the developers.\"}");
					obj.Write(output, 0, output.Length);
				};

				return response;
			};

#if DEBUG
			StaticConfiguration.DisableErrorTraces = false;
#endif
		}
	}
}