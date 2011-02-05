#define RANDOM_PADDING

using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using Tomboy.PrivateNotes;
using Tomboy.PrivateNotes.Crypto;


namespace TestProject
{
	[TestFixture]
	class UtilTests
	{
#if RANDOM_PADDING
		[Test]
		public void testRandomPadding()
		{
			// take at least 4 byytes, because @ 4 bytes if all are 0, lets say this is not random (of course it could happen, but well, nobody is perfect)
			for (int pad = 4; pad < 33; pad++)
			{
				for (int i = 1; i < 100; i++)
				{
					byte[] data = generateByteArray(i);
					byte[] padded = Util.padWithLengthInfo(data, pad);
					Assert.IsTrue(padded.Length % pad == 0, "not padded to multiple of value");

					byte[] paddingOnly = new byte[padded.Length - 4 - data.Length];
					Array.Copy(padded, data.Length + 4, paddingOnly, 0, paddingOnly.Length);

					checkRandom(paddingOnly);

					byte[] unpadded = Util.getDataFromPaddedWithLengthInfo(padded);
					Assert.AreEqual(data, unpadded, "data changed when got back");
				}
			}
		}

		/// <summary>
		/// checks (very cheap check) if the values in the array are random (in fact it only checks if not all of them are the same)
		/// </summary>
		/// <param name="_random"></param>
		private void checkRandom(byte[] _random)
		{
			// check that the bytes are not all the same
			// yes this is a very stupd/primitive random check, i know, but better than a stone in the face
			if (_random.Length > 3)
			{
				byte value = _random[0];
				bool allEqual = true;
				for (int i = 1; i < _random.Length; i++)
				{
					if (_random[i] != value)
						allEqual = false;
				}

				StringBuilder sb = new StringBuilder();
				foreach (byte b in _random)
					sb.Append((int)b + ", ");

				sb.Remove(sb.Length - 2, 2);

				Assert.IsFalse(allEqual, "all 'random' bytes were equal, not really random, is it?! (" + sb.ToString() + ")");
			}
			else
			{
				Console.WriteLine("Will not check for randomness, too short! length=" + _random.Length);
			}

		}

#endif


		[Test]
		public void paddingTests() {
			for (int pad=2; pad<33; pad++) {
				for (int i=1; i<100; i++) {
					byte[] data = generateByteArray(i);
					byte[] padded = Util.padWithLengthInfo(data, pad);
					Assert.IsTrue(padded.Length % pad == 0, "not padded to multiple of value");
					byte[] unpadded = Util.getDataFromPaddedWithLengthInfo(padded);
					Assert.AreEqual(data, unpadded, "data changed when got back");
				}
			}
		}

		


		[Test]
		public void testGetBytes()
		{
			for (int i = 1; i < 1000; i++)
			{
				String s = getRandomString(i);
				byte[] b = Util.GetBytes(s);
				String s1 = Util.FromBytes(b);
				Assert.AreEqual(s, s1, "different after GetBytes->FromBytes");
			}
		}

		[Test]
		public void basicSaltTest()
		{
			Random r = new Random();
			for (int i = 0; i < 1000; i++)
			{
				byte[] salt = null;
				byte[] key = new byte[32];
				r.NextBytes(key);
				byte[] saltedHash = AESUtil.CalculateSaltedHash(key, out salt);
				byte[] saltedHashCompare = AESUtil.CalculateSaltedHash(key, salt);
				Assert.IsTrue(Util.ArraysAreEqual(saltedHash, saltedHashCompare));

			}
		}




		private static byte[] generateByteArray(int _len)
		{
			byte[] b = new byte[_len];
			Random r = new Random();
			r.NextBytes(b);
			return b;
		}

		public static String getRandomString(int _len)
		{
			Random r = new Random();
			int c = r.Next(32, 126);
			StringBuilder sb = new StringBuilder(_len);
			for (int i = 0; i < _len; i++)
				sb.Append((char)c);
			return sb.ToString();
		}

	}
}
