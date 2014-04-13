using System;
using System.Net;
using System.Text;
using KeePassLib.Serialization;
using System.IO;

namespace OpenSSHSync
{
	class SftpWebRequest : WebRequest
	{
		private readonly Uri uri;

		public SftpWebRequest (Uri uri)
		{
			this.Headers = new WebHeaderCollection ();

			this.uri = uri;

			this.Timeout = -1;
		}

		public override Uri RequestUri { get { return uri; } }

		public override ICredentials Credentials { get; set; }

		public override string Method { get; set; }

		public override WebHeaderCollection Headers { get; set; }

		public override int Timeout { get; set; }

		private Stream inputStream = null;

		public override Stream GetRequestStream ()
		{
			if (inputStream == null)
				inputStream = new RequestStream (this);

			return inputStream;
		}

		private WebResponse response = null;

		public override WebResponse GetResponse ()
		{
			if (response == null) {
				if (string.IsNullOrEmpty (Method)) {
					if (inputStream != null && inputStream.CanRead) {
						Method = WebRequestMethods.Ftp.UploadFile;
					} else {
						Method = WebRequestMethods.Ftp.DownloadFile; // default
					}
				}

				response = new SshCommand (this);
			}

			return response;
		}

		~SftpWebRequest()
		{
			if (inputStream != null) {
				inputStream.Dispose ();
			}
		}

		public override string ToString ()
		{
			return string.Format (@"[SftpWebRequest({0:d}): URI= {1}, Method={2})", GetHashCode(), RequestUri, Method );
		}
	}
}

