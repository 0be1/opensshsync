using System;
using KeePass.Plugins;
using System.Net;
using System.Diagnostics;
using KeePassLib.Utility;

namespace OpenSSHSync
{
	public sealed class OpenSSHSyncExt : Plugin
	{
		internal const string SSH = "ssh";

		class SftpRequestCreator : IWebRequestCreate
		{
			public WebRequest Create (Uri uri)
			{
				return new SftpWebRequest (uri);
			}
		}

		#pragma warning disable 414
		private IPluginHost pluginHost;
		#pragma warning restore 414

		private bool CheckSshCommand ()
		{
			try {
				var process = Process.Start (new ProcessStartInfo {
					FileName = SSH,
					Arguments = "-V",
					UseShellExecute = false,
					CreateNoWindow = true,
					RedirectStandardError = true,
				});
				#pragma warning disable 219
				var version = process.StandardError.ReadLine ();
				#pragma warning restore 219

				return process.ExitCode != 255;
			} catch {
				MessageService.ShowWarning ("command '{0}' not found in PATH", SSH);

				return false;
			}
		}

		public override bool Initialize (IPluginHost host)
		{
			this.pluginHost = host;

			if (CheckSshCommand ()) {
				WebRequest.RegisterPrefix ("sftp", new SftpRequestCreator ());

				return true;
			}

			return false;
		}
	}
}

