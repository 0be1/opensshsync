using System;
using System.IO;
using System.Net;

namespace OpenSSHSync
{
	abstract class RefStream<T> : MemoryStream where T : MarshalByRefObject
	{
		private readonly T refObj;
		private bool closed = false;

		internal RefStream (T req)
		{
			this.refObj = req;
		}

		protected abstract void OnClose ();

		protected T RefObj { get { return refObj; } }

		public override void Close ()
		{
			if (closed)
				return;

			closed = true;

			base.Seek (0, SeekOrigin.Begin);

			if (refObj != null)
				OnClose ();

			base.Close ();
		}

		public override bool CanWrite {
			get {
				return !closed;
			}
		}

		public override bool CanSeek {
			get {
				return !closed;
			}
		}
	}

	class RequestStream : RefStream<WebRequest> {

		public RequestStream (WebRequest req) : base (req)
		{}

		protected override void OnClose ()
		{
			RefObj.GetResponse ();
		}
	}

	// http://stackoverflow.com/a/4139427
	public static class StreamExtensions
	{
		public static void CopyTo (this Stream input, Stream output, int bufferSize = short.MaxValue)
		{
			if (!input.CanRead)
				throw new InvalidOperationException ("input must be open for reading");
			if (!output.CanWrite)
				throw new InvalidOperationException ("output must be open for writing");

			byte[][] buf = { new byte[bufferSize], new byte[bufferSize] };
			int[] bufl = { 0, 0 };
			int bufno = 0;
			IAsyncResult read = input.BeginRead (buf [bufno], 0, buf [bufno].Length, null, null);
			IAsyncResult write = null;

			while (true) {

				// wait for the read operation to complete
				read.AsyncWaitHandle.WaitOne (); 
				bufl [bufno] = input.EndRead (read);

				// if zero bytes read, the copy is complete
				if (bufl [bufno] == 0) {
					break;
				}

				// wait for the in-flight write operation, if one exists, to complete
				// the only time one won't exist is after the very first read operation completes
				if (write != null) {
					write.AsyncWaitHandle.WaitOne ();
					output.EndWrite (write);
				}

				// start the new write operation
				write = output.BeginWrite (buf [bufno], 0, bufl [bufno], null, null);

				// toggle the current, in-use buffer
				// and start the read operation on the new buffer.
				//
				// Changed to use XOR to toggle between 0 and 1.
				// A little speedier than using a ternary expression.
				bufno ^= 1; // bufno = ( bufno == 0 ? 1 : 0 ) ;
				read = input.BeginRead (buf [bufno], 0, buf [bufno].Length, null, null);

			}

			// wait for the final in-flight write operation, if one exists, to complete
			// the only time one won't exist is if the input stream is empty.
			if (write != null) {
				write.AsyncWaitHandle.WaitOne ();
				output.EndWrite (write);
			}

			if (output.CanSeek)
				output.Seek (0, SeekOrigin.Begin);

			output.Flush ();

			// return to the caller ;
			return;
		}
	}
}

