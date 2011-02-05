using System;
using System.Collections.Generic;
using System.Text;
using Tomboy.PrivateNotes.Crypto;
using Tomboy.PrivateNotes;
using NUnit.Framework;

namespace TestProject
{

	[TestFixture]
	class TestClass
	{

		public static void Main()
		{
			new TestClass().TestDifferentPws();
		}

		[Test]
		public void TestDifferentPws()
		{
			String basePw = "basicPw";
			for (int i = 0; i < 10; i++)
			{
				testCrypt("D:\\test.note", "some test dataasdfö", Util.GetBytes(basePw));
				basePw = basePw + (char)('a' + i);
			}
		}

		public void testCrypt(String _file, String _data, byte[] _pw)
		{
			CryptoFormat cff = CryptoFormatProviderFactory.INSTANCE.GetCryptoFormat();

			byte[] salt;
			byte[] key = AESUtil.CalculateSaltedHash(_pw, out salt);

			if (cff.Version() == 0)
			{
				// if "defective" old version is used, the key is passed in directly
				key = _pw;
			}
			
			cff.WriteCompatibleFile(_file, Util.GetBytes(_data), key, salt);
			bool ok;
			byte[] decrypted = cff.DecryptFile(_file, _pw, out ok);
			Assert.NotNull(decrypted);
			Assert.IsTrue(_data.Equals(Util.FromBytes(decrypted)), "value doesn't match after decryption");
			Assert.IsTrue(ok);
		}

		[Test]
		public void TestDifferentDataLenghts()
		{
			CryptoFormat cff = CryptoFormatProviderFactory.INSTANCE.GetCryptoFormat();
			String file = @"D:\test.txt";
			byte[] pw = Util.GetBytes("password");
			String data = "this is my base test data ";
			for (int i = 0; i < 10; i++)
			{
				byte[] salt;
				byte[] key = AESUtil.CalculateSaltedHash(pw, out salt);

				if (cff.Version() == 0)
				{
					// if "defective" old version is used, the key is passed in directly
					key = pw;
				}

				cff.WriteCompatibleFile(file, Util.GetBytes(data), key, salt);
				bool ok;
				byte[] decrypted = cff.DecryptFile(file, pw, out ok);
				Assert.NotNull(decrypted);
				Assert.IsTrue(data.Equals(Util.FromBytes(decrypted)));
				Assert.IsTrue(ok);
				data = data + (char)('a' + i);
			}
		}

		[Test]
		public void TestCrpytoFiles()
		{
			CryptoFormat cff = CryptoFormatProviderFactory.INSTANCE.GetCryptoFormat();
			byte[] pw = Util.GetBytes("password");
			String file = @"D:\test.txt";

			byte[] salt;
			byte[] key = AESUtil.CalculateSaltedHash(pw, out salt);

			if (cff.Version() == 0)
			{
				// if "defective" old version is used, the key is passed in directly
				key = pw;
			}

			cff.WriteCompatibleFile(file, Util.GetBytes("Hello there..."), key, salt);

			bool ok;
			byte[] data = cff.DecryptFile(file, pw, out ok);
			Assert.IsTrue(ok);
		}
	}
}
