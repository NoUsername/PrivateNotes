using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.IO;

namespace Tomboy.PrivateNotes
{
	/// <summary>
	/// encapsulates wedav calls with a sync-server
	/// </summary>
	public class WebDAVInterface
	{
		/// <summary>
		/// webdav implementation
		/// </summary>
		private WebDAVClient client;

		/// <summary>
		/// event that is used to handle the asynchronous webdav calls in a synchronous way
		/// </summary>
		AutoResetEvent autoReset;

		/// <summary>
		/// the result of the last dirlisting
		/// </summary>
		List<String> dirListing;

		/// <summary>
		/// the last statusCode
		/// </summary>
		int lastStatusCode;

		/// <summary>
		/// 
		/// </summary>
		/// <param name="server">path to the server, like "http://privatenotes.dyndns-server.com"</param>
		/// <param name="path">path, starting fromt he server root-url, like "/webdav2/username/"</param>
		/// <param name="user">username for login</param>
		/// <param name="pass">pw for login</param>
		/// <param name="checkSSlCertificates">if true, an ssl certificate (if https is used) will be checked,
		/// if false, the ssl check will be ommited (has to be used for self-signed certificates etc)</param>
		public WebDAVInterface(String server, String path, String user, String pass, bool checkSSlCertificates)
		{
			//Logger.Debug("creating server " + server + " path " + path + " user " + user + " pass " + pass);
			client = new WebDAVClient();
			client.Server = server;
			client.BasePath = path;
			client.User = user;
			client.Pass = pass;
			client.SSLCertVerification = checkSSlCertificates;
			autoReset = new AutoResetEvent(false);
			autoReset.Reset();

			client.ListComplete += list;
			client.DownloadComplete += download;
			client.UploadComplete += upload;
			client.DeleteComplete += delete;
		}

		/// <summary>
		/// checks if a lock-file exists
		/// </summary>
		/// <returns></returns>
		public bool CheckForLockFile()
		{
			Logger.Debug("checking for lock file");
			client.List();
			Logger.Debug("starting wait");
			autoReset.WaitOne();
			Logger.Debug("waiting is over :), check if ok");
			CheckException("error while checking for lockfile");
			Logger.Debug("checking for lockfile was ok");
			return dirListing.Contains("lock");
		}

		/// <summary>
		/// downlaods the lockfile
		/// </summary>
		/// <param name="toFile">save the downloaded file there</param>
		/// <returns></returns>
		public bool DownloadLockFile(String toFile)
		{
			client.Download("lock", toFile);
			autoReset.WaitOne();
			CheckException("error while downloading lockfile");
			Console.WriteLine("STATUS for lockfile download=" + lastStatusCode);
			// TODO return true/false depending on statuscode
			return true;
		}

		/// <summary>
		/// removes the lock-file from the server
		/// </summary>
		/// <returns></returns>
		public bool RemoveLock()
		{
			client.Delete("lock");
			autoReset.WaitOne();
			CheckException("error while removing lock");
			Console.WriteLine("STATUS for delete=" + lastStatusCode);
			// TODO return true/false depending on statuscode
			return true;
		}

		/// <summary>
		/// removes the given file from the server
		/// </summary>
		/// <param name="name"></param>
		/// <returns></returns>
		public bool RemoveFile(String name)
		{
			client.Delete(name);
			autoReset.WaitOne();
			CheckException("error while removing file " + name);
			Console.WriteLine("STATUS for delete=" + lastStatusCode);
			// TODO return true/false depending on statuscode
			return true;
		}

		/// <summary>
		/// upload a file
		/// </summary>
		/// <param name="path"></param>
		/// <returns></returns>
		public bool UploadFile(String path)
		{
			client.Upload(path, Path.GetFileName(path));
			autoReset.WaitOne();
			CheckException("error while uploading file " + path);
			Console.WriteLine("STATUS for upload=" + lastStatusCode);
			// TODO return true/false depending on statuscode
			return true;
		}

		/// <summary>
		/// downloads all notes found at this path
		/// </summary>
		/// <param name="toPath"></param>
		/// <returns></returns>
		public bool DownloadNotes(String toPath)
		{
			// fetch list of all files to download
			client.List();
			autoReset.WaitOne();
			foreach (String file in dirListing) {
				if (!file.EndsWith("/"))
				{
					// not for dirs
					client.Download(file, Path.Combine(toPath, file));
					autoReset.WaitOne();
					CheckException("exception while downloading note " + file);
					// TODO check statuscode
					Console.WriteLine("downloaded with code " + lastStatusCode);
				}
			}
			// TODO error checking
			return true;
		}

		/// <summary>
		/// uploads all notes found at this path
		/// </summary>
		/// <param name="fromPath"></param>
		/// <returns></returns>
		public bool UploadNotes(String fromPath)
		{
			string[] files = Directory.GetFiles(fromPath);
			// upload all those files
			
			foreach (String file in files)
			{
				if (!file.EndsWith("/")
					&& !file.EndsWith("lock"))
				{
					// not for dirs and don't upload lockfile again
					String toPath = Path.GetFileName(file);
					client.Upload(file, toPath);
					autoReset.WaitOne();
					CheckException("exception while uploading note " + file);
					// TODO check statuscode
					Console.WriteLine("uploaded with code " + lastStatusCode);
				}
			}
			// TODO error checking
			return true;
		}

		// callback methods from asynchronous webdav-result-methods
		#region CALLBACK METHODS

		private void list(List<String> files, int statusCode)
		{
			dirListing = files;
			lastStatusCode = statusCode;
			autoReset.Set();
		}

		private void download(int statusCode)
		{
			lastStatusCode = statusCode;
			autoReset.Set();
		}

		private void upload(int statusCode, object state)
		{
			lastStatusCode = statusCode;
			autoReset.Set();
		}

		private void delete(int statusCode)
		{
			lastStatusCode = statusCode;
			autoReset.Set();
		}
		#endregion

		/// <summary>
		/// checks if an exception has occured in the asynchronous webdav calls, if so
		/// this exception is wrapped as the innterexception of a new one that will get
		/// thrown by this function, with the message set to the string passed to it
		/// </summary>
		/// <param name="msgOnError">error-message of the exception that will be thrown (if there was one)</param>
		private void CheckException(String msgOnError)
		{
			if (lastStatusCode == WebDAVClient.EXCEPTION_RESPONSE_CODE
					&& client.LastException != null)
			{
				Exception e = client.LastException;
				client.ResetException();
				throw new WebDavException(msgOnError, e);
			}
		}

	}

	/// <summary>
	/// class for webdav service exceptions
	/// </summary>
	class WebDavException : Exception
	{
		public WebDavException(String msg) : base(msg)
		{

		}

		public WebDavException(String msg, Exception innerException)
			: base(msg, innerException)
		{

		}

	}

}
