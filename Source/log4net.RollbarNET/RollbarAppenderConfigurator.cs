// RollbarAppenderConfigurator.cs
//
// Copyright (c) 2014 Morten Teinum
// Copied from https://github.com/mteinum/log4net.Rollbar  as of commit 194b000152de2eac2bdd046f9ed7d3e94fe69f9c
// Further changes (c) 2016 Richard Curtice under the same license.
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
using log4net.Config;

namespace log4net_RollbarNET {
	public static class RollbarAppenderConfigurator {
		/// <summary>
		/// Initializes the log4net system using the <see cref="RollbarAppender"/>
		/// </summary>
		/// <param name="accessToken">the post_server_item key</param>
		/// <param name="configureAppender"></param>
		public static void Configure(string accessToken = null, Action<RollbarAppender> configureAppender = null) {
			var appender = new RollbarAppender { AccessToken = accessToken };

			if (configureAppender != null) {
				configureAppender(appender);
			}

			appender.ActivateOptions();

			BasicConfigurator.Configure(appender);
		}
	}
}

