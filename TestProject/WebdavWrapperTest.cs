#define DEFAULT

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using NUnit.Framework;
using Tomboy.PrivateNotes;
using Tomboy.PrivateNotes.Crypto;


namespace TestProject
{
	/// <summary>
	/// INFO: Keep in mind that webdav tests NEED A WEBDAV SERVER TO SUCCESSFULLY RUN!
	/// 
	/// to add your own, add them in the #else section and remove the #define in the first line of this test
	/// or add another define
	/// 
	/// you should make sure that the webdav folder is empty before running the tests
	/// 
	/// </summary>
	[TestFixture]
	class WebdavWrapperTests
	{
#if DEFAULT
		public const String TESTSERVER = "http://localhost";
		public const String TESTUSER = "wampp";
		public const String TESTPASSWORD = "xampp";
		/// <summary>
		/// the "basepath" on the server. this would result to http://localhost/webdav/unittest
		/// </summary>
		public const String TESTPATH = "webdav/unittest";
#else
		public const String TESTSERVER = "http://privatenotes.dyndns-server.com/";
		public const String TESTUSER = "";
		public const String TESTPASSWORD = "";
		public const String TESTPATH = "webdav2/";
#endif

		private WebDAVInterface getClient()
		{
			WebDAVInterface wdc = new WebDAVInterface(TESTSERVER, TESTPATH, TESTUSER, TESTPASSWORD, false);
			return wdc;
		}

		private void createTestFile(String name)
		{
			File.Create(name).Close();

		}

		private void deleteTestFile(String name)
		{
			File.Delete(name);
		}

		[Test]
		public void testLockFile()
		{
			WebDAVInterface wdi = getClient();
			bool hasLockFile = wdi.CheckForLockFile();
			Assert.IsFalse(hasLockFile, "shouldn't have lock file");
			String fileName = "lock";
			createTestFile(fileName);
			try
			{
				bool success = wdi.UploadFile(fileName);
				Assert.IsTrue(success, "upload failed");
				hasLockFile = wdi.CheckForLockFile();
				Assert.IsTrue(hasLockFile, "uploading lock file error");
				success = wdi.RemoveLock();
				Assert.IsTrue(success, "remove lock error");
				hasLockFile = wdi.CheckForLockFile();
				Assert.IsFalse(hasLockFile, "shouldn't have lock file");
			}
			finally
			{
				deleteTestFile(fileName);
			}
		}

		[Test]
		public void testNotesUpAndDownload()
		{
			String[] notesNames = new string[]{"note1.note", "blub.note", "asdf.note"};
			WebDAVInterface wdi = getClient();
			String basepath = "./temp/";
			Directory.CreateDirectory(basepath);
			
			// create the note files)
			foreach (String fileName in notesNames)
				createTestFile(basepath + fileName);

			try
			{
				bool success = wdi.UploadNotes(basepath);
				Assert.IsTrue(success, "upload failed");

				// now delete all locally
				foreach (String fileName in notesNames)
					deleteTestFile(basepath + fileName);

				// now download them from remote
				success = wdi.DownloadNotes(basepath);
				Assert.IsTrue(success, "download failed");

				// delete them on the remote server to be clean for the next test
				foreach (String fileName in notesNames)
					wdi.RemoveFile(fileName);

				// check if all are there:
				foreach (String fileName in notesNames)
					Assert.IsTrue(File.Exists(basepath + fileName), "we should have this file!");
				
			}
			finally
			{
				// try again to delete notes
				foreach (String fileName in notesNames){
						try
						{
							deleteTestFile(basepath + fileName);
						}
						catch (Exception)
						{
							;// ignore
						}
				}

				Directory.Delete(basepath);
			}
		}


	}
}
