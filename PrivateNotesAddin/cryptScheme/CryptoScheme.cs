using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace Tomboy.PrivateNotes.Crypto
{

	class CryptoFileFormatRev1 : CryptoFormat
	{

		public const ushort CRYPTO_VERSION = 1;

		public int Version()
		{
			return CRYPTO_VERSION;
		}

		/// <summary>
		/// writes the given data in the PrivateNotes-crypt-format to the harddrive
		/// </summary>
		/// <param name="_filename"></param>
		/// <param name="_content"></param>
		/// <param name="_key"></param>
		/// <returns></returns>
		public bool WriteCompatibleFile(String _filename, byte[] _content, byte[] _key, byte[] _salt)
		{
			Console.WriteLine("writing " + _filename);

			byte[] paddedContent = Util.padWithLengthInfo(_content, 16);


			byte[] doubleHashedKey = AESUtil.CalculateSaltedHash(_key, _salt);

			Util.AssureFileExists(_filename);
			FileStream fout = new FileStream(_filename, FileMode.Truncate, FileAccess.Write);
			
			byte[] version = System.BitConverter.GetBytes(CRYPTO_VERSION);
			
			long now = Util.ConvertToUnixTimestamp(DateTime.Now.ToUniversalTime());
			Console.WriteLine("long value: " + now);
			byte[] dateTime = System.BitConverter.GetBytes(now);

			// calculate check-hash (to verify nothing has been altered)
			MemoryStream membuf = new MemoryStream();
			byte[] fileNameBytes = Util.GetBytes(Path.GetFileName(_filename));
			membuf.Write(version, 0, version.Length);
			membuf.Write(dateTime, 0, dateTime.Length);
			membuf.Write(fileNameBytes, 0, fileNameBytes.Length);
			membuf.Write(paddedContent, 0, paddedContent.Length);
			byte[] dataHashValue = AESUtil.CalculateHash(membuf.ToArray());

			MemoryStream cryptMe = new MemoryStream();
			cryptMe.Write(dataHashValue, 0, dataHashValue.Length);
			cryptMe.Write(paddedContent, 0, paddedContent.Length);

			byte[] cryptedData = AESUtil.Encrypt(_key, cryptMe.ToArray());

			fout.Write(version, 0, version.Length);
			fout.Write(dateTime, 0, dateTime.Length);
			fout.Write(doubleHashedKey, 0, doubleHashedKey.Length);
			fout.Write(_salt, 0, _salt.Length);
			fout.Write(cryptedData, 0, cryptedData.Length);
			fout.Close();

			Console.WriteLine("wrote file");
			return true;
		}

		public byte[] DecryptFromStream(String _filename, Stream fin, byte[] _key, out bool _wasOk)
		{
			_wasOk = false;
			Console.WriteLine();
			Console.WriteLine("checking " + _filename);

			byte[] version = new byte[2];
			byte[] datetime = new byte[8];
			byte[] keyVerifyValue = new byte[32];
			byte[] keySaltValue = new byte[32];
			byte[] dataHashValue = new byte[32];
			byte[] cryptedData = null;
			MemoryStream membuf = new MemoryStream();

			if (fin.Read(version, 0, version.Length) != version.Length)
				throw new FormatException("file seems to be corrupt (version)");
			if (fin.Read(datetime, 0, datetime.Length) != datetime.Length)
				throw new FormatException("file seems to be corrupt (date)");
			if (fin.Read(keyVerifyValue, 0, keyVerifyValue.Length) != keyVerifyValue.Length)
				throw new FormatException("file seems to be corrupt (keyVerify)");
			if (fin.Read(keySaltValue, 0, keySaltValue.Length) != keySaltValue.Length)
				throw new FormatException("file seems to be corrupt (keySalt)");
				
			byte[] singleHashedKey = AESUtil.CalculateSaltedHash(_key, keySaltValue);
			byte[] doubleHashedKey = AESUtil.CalculateSaltedHash(singleHashedKey, keySaltValue);

			if (System.BitConverter.ToUInt16(version, 0) != CRYPTO_VERSION)
			{
				throw new EncryptionException("Wrong Version");
			}

			if (!Util.ArraysAreEqual(keyVerifyValue, doubleHashedKey))
			{
				throw new PasswordException("Wrong Password");
			}

			// read rest of file (encrypted data)
			int data = fin.ReadByte();
			while (data >= 0)
			{
				membuf.WriteByte((byte)data);
				data = fin.ReadByte();
			}
			cryptedData = membuf.ToArray();

			// build data that should be in hash
			membuf = new MemoryStream();
			// filename (without path) as byte[]
			byte[] fileNameBytes = Util.GetBytes(Path.GetFileName(_filename));

			byte[] realData = null;
			{
				byte[] decryptedData = AESUtil.Decrypt(singleHashedKey, cryptedData);
				// get first 32 bytes of decrypted data, this is the control-hash (dataHashValue)
				System.Array.Copy(decryptedData, dataHashValue, dataHashValue.Length);

				byte[] otherData = new byte[decryptedData.Length - dataHashValue.Length];
				System.Array.Copy(decryptedData, dataHashValue.Length, otherData, 0, otherData.Length);
				realData = Util.getDataFromPaddedWithLengthInfo(otherData);

				// write things that are used for the verification-hash into the membuf buffer
				membuf.Write(version, 0, version.Length);
				membuf.Write(datetime, 0, datetime.Length);
				membuf.Write(fileNameBytes, 0, fileNameBytes.Length);
				membuf.Write(otherData, 0, otherData.Length);
			}

			byte[] dataToHash = membuf.ToArray();
			byte[] dataHashToCompare = AESUtil.CalculateHash(dataToHash);
			if (!Util.ArraysAreEqual(dataHashValue, dataHashToCompare))
			{
				Console.WriteLine("Hashes don't match!!!! Data may have been manipulated!");
				return null;
			}

			long fileDate = System.BitConverter.ToInt64(datetime, 0);
			DateTime dateTimeObj = Util.ConvertFromUnixTimestamp(fileDate);
			dateTimeObj = dateTimeObj.ToLocalTime(); // because it's stored in utc
			Console.WriteLine("data seems ok, file is from " + dateTimeObj.ToShortDateString() + " " + dateTimeObj.ToShortTimeString());
			Console.WriteLine("note data:");
			Console.WriteLine(Util.FromBytes(realData));
			Console.WriteLine("-=END OF NOTE=-");

			_wasOk = true;
			return realData;
		}

		public byte[] DecryptFile(String _filename, byte[] _key, out bool _wasOk)
		{
			Stream s = new FileStream(_filename, FileMode.Open, FileAccess.Read);
			try
			{
				byte[] result = DecryptFromStream(_filename, s, _key, out _wasOk);
				return result;
			}
			finally
			{
				s.Close();
			}
		}

	}

	/// <summary>
	/// this is the old version which doesn't use the salted hashes and shouldn't be used any more
	/// </summary>
	class CryptoFileFormatRev0 : CryptoFormat
	{
		public const ushort CRYPTO_VERSION = 0;

		public int Version()
		{
			return CRYPTO_VERSION;
		}

		/// <summary>
		/// writes the given data in the PrivateNotes-crypt-format to the harddrive
		/// </summary>
		/// <param name="_filename"></param>
		/// <param name="_content"></param>
		/// <param name="_key"></param>
		/// <param name="_salt">ignored</param>
		/// <returns></returns>
		public bool WriteCompatibleFile(String _filename, byte[] _content, byte[] _pw, byte[] _salt)
		{
			Console.WriteLine("writing " + _filename);

			byte[] paddedContent = Util.padWithLengthInfo(_content, 16);

			byte[] singleHashedKey = AESUtil.CalculateHash(_pw);
			byte[] doubleHashedKey = AESUtil.CalculateHash(singleHashedKey);

			Util.AssureFileExists(_filename);
			FileStream fout = new FileStream(_filename, FileMode.Truncate, FileAccess.Write);
			long now = Util.ConvertToUnixTimestamp(DateTime.Now.ToUniversalTime());
			Console.WriteLine("long value: " + now);
			byte[] dateTime = System.BitConverter.GetBytes(now);

			// calculate check-hash (to verify nothing has been altered)
			MemoryStream membuf = new MemoryStream();
			byte[] fileNameBytes = Util.GetBytes(Path.GetFileName(_filename));
			membuf.Write(dateTime, 0, dateTime.Length);
			membuf.Write(fileNameBytes, 0, fileNameBytes.Length);
			membuf.Write(paddedContent, 0, paddedContent.Length);
			byte[] dataHashValue = AESUtil.CalculateHash(membuf.ToArray());

			MemoryStream cryptMe = new MemoryStream();
			cryptMe.Write(dataHashValue, 0, dataHashValue.Length);
			cryptMe.Write(paddedContent, 0, paddedContent.Length);

			byte[] cryptedData = AESUtil.Encrypt(singleHashedKey, cryptMe.ToArray());

			fout.Write(dateTime, 0, dateTime.Length);
			fout.Write(doubleHashedKey, 0, doubleHashedKey.Length);
			fout.Write(cryptedData, 0, cryptedData.Length);
			fout.Close();

			Console.WriteLine("wrote file");
			return true;
		}

		public byte[] DecryptFromStream(String _filename, Stream fin, byte[] _pw, out bool _wasOk)
		{
			_wasOk = false;
			Console.WriteLine();
			Console.WriteLine("checking " + _filename);

			byte[] singleHashedKey = AESUtil.CalculateHash(_pw);
			byte[] doubleHashedKey = AESUtil.CalculateHash(singleHashedKey);

			byte[] datetime = new byte[8];
			byte[] keyVerifyValue = new byte[32];
			byte[] dataHashValue = new byte[32];
			byte[] cryptedData = null;
			MemoryStream membuf = new MemoryStream();

			if (fin.Read(datetime, 0, datetime.Length) != datetime.Length)
				throw new FormatException("file seems to be corrupt");
			if (fin.Read(keyVerifyValue, 0, keyVerifyValue.Length) != keyVerifyValue.Length)
				throw new FormatException("file seems to be corrupt");
			if (!Util.ArraysAreEqual(keyVerifyValue, doubleHashedKey))
			{
				throw new PasswordException("Wrong Password");
			}

			// read rest of file (encrypted data)
			int data = fin.ReadByte();
			while (data >= 0)
			{
				membuf.WriteByte((byte)data);
				data = fin.ReadByte();
			}
			cryptedData = membuf.ToArray();

			// build data that should be in hash
			membuf = new MemoryStream();
			// filename (without path) as byte[]
			byte[] fileNameBytes = Util.GetBytes(Path.GetFileName(_filename));

			byte[] realData = null;
			{
				byte[] decryptedData = AESUtil.Decrypt(singleHashedKey, cryptedData);
				// get first 32 bytes of decrypted data, this is the control-hash (dataHashValue)
				System.Array.Copy(decryptedData, dataHashValue, dataHashValue.Length);

				byte[] otherData = new byte[decryptedData.Length - dataHashValue.Length];
				System.Array.Copy(decryptedData, dataHashValue.Length, otherData, 0, otherData.Length);
				realData = Util.getDataFromPaddedWithLengthInfo(otherData);

				// write things that are used for the verification-hash into the membuf buffer
				membuf.Write(datetime, 0, datetime.Length);
				membuf.Write(fileNameBytes, 0, fileNameBytes.Length);
				membuf.Write(otherData, 0, otherData.Length);
			}

			byte[] dataToHash = membuf.ToArray();
			byte[] dataHashToCompare = AESUtil.CalculateHash(dataToHash);
			if (!Util.ArraysAreEqual(dataHashValue, dataHashToCompare))
			{
				Console.WriteLine("Hashes don't match!!!! Data may have been manipulated!");
				return null;
			}

			long fileDate = System.BitConverter.ToInt64(datetime, 0);
			DateTime dateTimeObj = Util.ConvertFromUnixTimestamp(fileDate);
			dateTimeObj = dateTimeObj.ToLocalTime(); // because it's stored in utc
			Console.WriteLine("data seems ok, file is from " + dateTimeObj.ToShortDateString() + " " + dateTimeObj.ToShortTimeString());
			Console.WriteLine("note data:");
			Console.WriteLine(Util.FromBytes(realData));
			Console.WriteLine("-=END OF NOTE=-");

			_wasOk = true;
			return realData;

		}


		public byte[] DecryptFile(String _filename, byte[] _pw, out bool _wasOk)
		{
			Stream s = new FileStream(_filename, FileMode.Open, FileAccess.Read);
			try
			{
				byte[] result = DecryptFromStream(_filename, s, _pw, out _wasOk);
				return result;
			}
			finally
			{
				s.Close();
			}
		}

	}
}
