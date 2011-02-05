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
	class WebdavTests
	{
#if DEFAULT
		public const String TESTSERVER = "http://localhost";
		public const String TESTUSER = "wampp";
		public const String TESTPASSWORD = "xampp";
		/// <summary>
		/// the "basepath" on the server. this would result to http://localhost/webdav/unittest
		/// </summary>
		public const String TESTPATH = "webdav/unittest";
		/// <summary>
		/// test timeout 5 seconds
		/// </summary>
		public const int TIMEOUT = 5000;
#else
		public const String TESTSERVER = "http://localhost";
		public const String TESTUSER = "wampp";
		public const String TESTPASSWORD = "xampp";
		public const String TESTPATH = "webdav/test";
#endif

		private WebDAVClient getClient()
		{
			WebDAVClient wdc = new WebDAVClient();
			wdc.Server = TESTSERVER;
			wdc.User = TESTUSER;
			wdc.Pass = TESTPASSWORD;
			wdc.BasePath = TESTPATH;
			return wdc;
		}

		private const string TESTFILE = "testDummyX.bin";

		private void createTestFile()
		{
			File.Create(TESTFILE).Close();

		}

		private void deleteTestFile()
		{
			File.Delete(TESTFILE);
		}

		private AutoResetEvent autoReset;
		private Object result;
		private int status;

		[SetUp]
		public void setup()
		{
			autoReset = new AutoResetEvent(false);
			result = null;
			status = -1;
		}

		[Test]
		public void listTest()
		{
			WebDAVClient wdc = getClient();
			wdc.ListComplete += new ListCompleteDel(wdc_ListComplete);
			wdc.List();
			autoReset.WaitOne(TIMEOUT);
			Assert.AreNotEqual(status, -1, "wrong status");
			Assert.IsNotNull(result, "result object was null");
		}

		[Test]
		public void testCreateDir()
		{
			String dir = "testcreatedir";
			WebDAVClient wdc = getClient();
			wdc.CreateDirComplete += new CreateDirCompleteDel(wdc_CreateDirComplete);
			wdc.ListComplete += new ListCompleteDel(wdc_ListComplete);
			wdc.DeleteComplete += new DeleteCompleteDel(wdc_DeleteComplete);
			wdc.CreateDir(dir);
			autoReset.WaitOne(TIMEOUT);
			Assert.AreNotEqual(status, -1, "wrong status - create");

			autoReset.Reset();
			wdc.List();
			autoReset.WaitOne(TIMEOUT);
			Assert.AreNotEqual(status, -1, "wrong status - list");
			List<String> listing = (List<string>) result;			
			Assert.IsTrue(listing.Contains(dir + "/")); // directories have the / appended to the name

			// now delete again
			autoReset.Reset();
			wdc.Delete(dir + "/");
			autoReset.WaitOne(TIMEOUT);
			Assert.AreNotEqual(status, -1, "wrong status - delete");
		}

		[Test]
		public void testUpload()
		{
			WebDAVClient wdc = getClient();
			wdc.UploadComplete += new UploadCompleteDel(wdc_UploadComplete);
			wdc.ListComplete += new ListCompleteDel(wdc_ListComplete);
			wdc.DeleteComplete += new DeleteCompleteDel(wdc_DeleteComplete);
			
			createTestFile();
			try
			{
				wdc.Upload(TESTFILE, TESTFILE);
				autoReset.WaitOne(TIMEOUT);
				Assert.AreNotEqual(status, -1, "wrong status - upload");

				autoReset.Reset();
				wdc.List();
				autoReset.WaitOne(TIMEOUT);
				Assert.AreNotEqual(status, -1, "wrong status - list");
				List<String> listing = (List<string>)result;
				Assert.IsTrue(listing.Contains(TESTFILE));

				// now delete again
				autoReset.Reset();
				wdc.Delete(TESTFILE);
				autoReset.WaitOne(TIMEOUT);
				Assert.AreNotEqual(status, -1, "wrong status - delete");
			}
			finally
			{
				deleteTestFile();
			}

		
		}

		void wdc_UploadComplete(int statusCode, object state)
		{
			status = statusCode;
		}

		void wdc_DeleteComplete(int statusCode)
		{
			status = statusCode;
			autoReset.Set();
		}

		void wdc_CreateDirComplete(int statusCode)
		{
			status = statusCode;
			autoReset.Set();
		}

		void wdc_ListComplete(List<string> files, int statusCode)
		{
			result = files;
			status = statusCode;
			autoReset.Set();
		}

	}
}
