using System;
using System.Net;
using System.IO;
using System.Diagnostics;
using KeePassLib.Serialization;
using System.Text;
using KeePassLib.Utility;

namespace OpenSSHSync
{
	public class SshCommand : WebResponse
	{
		private readonly Process process;
		private readonly WebHeaderCollection headers;
		private readonly WebRequest request;

		public SshCommand (WebRequest request)
		{
			this.request = request;

			this.headers = new WebHeaderCollection ();

			process = Process.Start (new ProcessStartInfo {
				FileName = OpenSSHSyncExt.SSH,
				Arguments = "-q -o 'BatchMode=yes' -o 'PasswordAuthentication=no' " + GetSession (request) + " " + GetCommand (request),
				UseShellExecute = false,
				CreateNoWindow = true,
				RedirectStandardInput = true,
				RedirectStandardOutput = true
			});

			if (request.GetRequestStream () != null) {
				request.GetRequestStream ().CopyTo (process.StandardInput.BaseStream);
			}
		}

		public override WebHeaderCollection Headers { get { return headers; } }

		static string GetSession (WebRequest request)
		{
			StringBuilder retval = new StringBuilder ();

			NetworkCredential cred = (request.Credentials as NetworkCredential);

			if (cred != null) {

				if (!string.IsNullOrEmpty (cred.UserName) && "anonymous" != cred.UserName) {
					retval.AppendFormat ("'{0}'", cred.UserName);

					if (!string.IsNullOrEmpty (cred.Password))
						retval.AppendFormat (":'{0}'", cred.Password);

					retval.Append ('@');
				}
			}

			retval.Append (request.RequestUri.Host);

			return retval.ToString ();
		}

		static string  GetCommand (WebRequest request)
		{
			string path = request.RequestUri.AbsolutePath.Substring (1);

			switch (request.Method) {
			case IOConnection.WrmDeleteFile:
			case WebRequestMethods.Ftp.DeleteFile:

				return string.Format ("rm -f '{0}'", path);

			case WebRequestMethods.Ftp.AppendFile:

				return string.Format ("cat >> '{0}'", path);

			case WebRequestMethods.Ftp.DownloadFile:

				return string.Format ("cat '{0}'", path);

			case WebRequestMethods.Ftp.ListDirectory:

				return string.Format ("ls -1 '{0}'", path);

			case WebRequestMethods.Ftp.GetFileSize:

				return string.Format ("stat -c '%s' '{0}'", path);

			case WebRequestMethods.Ftp.MakeDirectory:

				return string.Format ("mkdir '{0}'", path);

			case IOConnection.WrmMoveFile:
				string fromPath = path;
				string toPath = request.Headers.Get (IOConnection.WrhMoveFileTo);

				if (string.IsNullOrEmpty (toPath))
					throw new InvalidOperationException ("empty destination path");

				try {
					toPath = new Uri (toPath).AbsolutePath.Substring (1);
				} catch (Exception ex) {
					throw new InvalidOperationException (string.Format ("path '{0}' is not absolute", toPath), ex);
				}

				if (string.IsNullOrEmpty (toPath))
					throw new InvalidOperationException ("empty destination path");

				return string.Format ("mv '{0}' '{1}'", fromPath, toPath);

			case WebRequestMethods.Http.Post:
			case WebRequestMethods.Http.Put:
			case WebRequestMethods.Ftp.UploadFile:

				return string.Format ("cat > '{0}'", path);
			
			default:
				MessageService.ShowWarning ("unsupported method '{0}'", request.Method);
				throw new InvalidOperationException (string.Format ("unsupported method '{0}'", request.Method));
			}
		}

		private Stream outputStream = null;

		public override Stream GetResponseStream ()
		{
			if (outputStream == null) {
				outputStream = new MemoryStream ();

				process.StandardOutput.BaseStream.CopyTo (outputStream);

				process.WaitForExit (request.Timeout);
			}

			return outputStream;
		}

		public override void Close ()
		{
			if (outputStream != null)
				outputStream.Close ();

			process.Close ();
		}

		public override string ToString ()
		{
			return string.Format ("[SshCommand({0:d}: Command={1} {2}]", GetHashCode (), process.StartInfo.FileName, process.StartInfo.Arguments);
		}
	}
}

