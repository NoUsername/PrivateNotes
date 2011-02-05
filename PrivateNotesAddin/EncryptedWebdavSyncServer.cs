using System;
using System.Collections.Generic;
using System.Text;
using Tomboy.Sync;

namespace Tomboy.PrivateNotes
{
	/// <summary>
	/// extends the EncryptedFileSystemSyncServer and implements a webdav based server-sync
	/// on top of the local encrypted sync service
	/// </summary>
	public class EncryptedWebdavSyncServer : EncryptedFileSystemSyncServer
	{
		/// <summary>
		/// the object that is used to communicate via webdav
		/// </summary>
		private WebDAVInterface webdavserver;
		
		public EncryptedWebdavSyncServer(String _tempDir, byte[] _key, WebDAVInterface _webDav)
			: base(_tempDir, _key, _webDav)
		{

		}

		/// <summary>
		/// gets the path of the current revision dir
		/// </summary>
		/// <param name="rev"></param>
		/// <returns></returns>
		override internal string GetRevisionDirPath(int rev)
		{
			return serverPath;
		}

		/// <summary>
		/// called during the ctor of the base-class, here we fetch the data from the server to have the same 
		/// </summary>
		internal override void SetupWorkDirectory(object initParam)
		{
			Logger.Debug("basic stuff");
			base.SetupWorkDirectory(initParam);

			// because we only sync to the server and just copy the stuff everytime we
			// sync, we only need to use a temp-directory
			serverPath = cachePath;

			// refresh these variables because they would be wrong else
			lockPath = System.IO.Path.Combine(serverPath, "lock");
			manifestPath = System.IO.Path.Combine(serverPath, "manifest.xml");
			

			Logger.Debug("getting webdav interface");
			// this must be the webdav interface (not very nice way to do it, but did't know any other way)
			webdavserver = initParam as WebDAVInterface;

			Logger.Debug("deleting local files");
			// make our local sync dir empty
			Util.DelelteFilesInDirectory(serverPath);

			Logger.Debug("checking for remote lockfile");
			// fetch data from server
			if (webdavserver.CheckForLockFile())
			{
				Logger.Debug("downloading lockfile");
				webdavserver.DownloadLockFile(lockPath);
			}
			Logger.Debug("downloading notes");
			webdavserver.DownloadNotes(serverPath);
			Logger.Debug("workdir setup done");
		}

		/// <summary>
		/// executed when the manifest file gets created
		/// </summary>
		/// <param name="pathToManifestFile"></param>
		internal override void OnManifestFileCreated(String pathToManifestFile) {
			// upload the manifest file:
			webdavserver.UploadFile(pathToManifestFile);
		}

		/// <summary>
		/// executed when a file should get deleted from the server
		/// </summary>
		/// <param name="pathToNote"></param>
		internal override void OnDeleteFile(String pathToNote)
		{
			// only use the name of the file (the rest is the local path which is irrelevant for the server)
			webdavserver.RemoveFile(System.IO.Path.GetFileName(pathToNote));
		}

		/// <summary>
		/// executed when a file should get updated to the server
		/// </summary>
		/// <param name="pathToNote"></param>
		internal override void OnUploadFile(String pathToNote) {
			webdavserver.UploadFile(pathToNote);
		}

		/// <summary>
		/// executed when lockfile gets deleted
		/// </summary>
		/// <param name="path"></param>
		internal override void RemoveLockFile(string path)
		{
			base.RemoveLockFile(path);
			// on server
			try
			{
				if (webdavserver.CheckForLockFile())
				{
					webdavserver.RemoveLock();
				}
			}
			catch (Exception e)
			{
				Logger.Warn("Error deleting servers lock: {1}", e.Message);
			}
		}

	}
}
