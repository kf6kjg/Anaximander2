// AnaximanderAppender.cs
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
using log4net.Appender;
using log4net.Core;

namespace Console {
	public class AnaximanderAppender : AnsiColorTerminalAppender {
		private ConsoleBase _console;

		public ConsoleBase Console {
			get { return _console; }
			set { _console = value; }
		}

		override protected void Append(LoggingEvent loggingEvent) {
			if (_console != null) {
				_console.LockOutput();
			}

			string loggingMessage = RenderLoggingEvent(loggingEvent);

			try {
				if (_console != null) {
					string level = "normal";

					if (loggingEvent.Level == Level.Error) {
						level = "error";
					}
					else if (loggingEvent.Level == Level.Warn) {
						level = "warn";
					}

					_console.Output(loggingMessage, level);
				}
				else {
					if (!loggingMessage.EndsWith("\n", StringComparison.Ordinal)) {
						System.Console.WriteLine(loggingMessage);
					}
					else {
						System.Console.Write(loggingMessage);
					}
				}
			}
			catch (Exception e) {
				System.Console.WriteLine($"Couldn't write out log message: {e}");
			}
			finally {
				if (_console != null) {
					_console.UnlockOutput();
				}
			}
		}
	}
}

